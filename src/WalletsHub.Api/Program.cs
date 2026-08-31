using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WalletsHub.Api;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=walletshub;Username=postgres;Password=postgres";
builder.Services.AddDbContext<WalletsDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
    options.Lockout.MaxFailedAccessAttempts = 7;
}).AddEntityFrameworkStores<WalletsDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment() ? "WalletsHub.Development" : "__Host-WalletsHub";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(365);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddDataProtection().SetApplicationName("WalletsHub");
builder.Services.AddAuthorization(options => options.AddPolicy("PlatformAdmin", policy => policy.RequireRole(Roles.PlatformAdmin)));
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && !context.User.IsInRole(Roles.PlatformAdmin))
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var db = context.RequestServices.GetRequiredService<WalletsDbContext>();
        var allowed = userId is not null && await db.Users.AnyAsync(user => user.Id == userId && user.IsActive && user.OrganizationId != null && db.Organizations.Any(org => org.Id == user.OrganizationId && org.IsActive));
        if (!allowed) { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
    }
    await next();
});
app.UseAuthorization();
app.MapHealthChecks("/health/live");
app.MapGet("/", () => Results.Ok(new { product = "Wallets Hub", version = "0.1.0" }));

MapAuth(app);
MapPlatform(app);
MapTeam(app);
MapWallets(app);
MapDevices(app);
MapReceipts(app);
MapReports(app);
MapNotifications(app);

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<WalletsDbContext>().Database.EnsureCreatedAsync();
    return;
}
if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
{
    await SeedAsync(app.Services, builder.Configuration);
    return;
}
await app.RunAsync();

static void MapAuth(WebApplication app)
{
    var auth = app.MapGroup("/api/auth");
    auth.MapPost("/login", async (LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn, WalletsDbContext db) =>
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive) return Results.Problem(statusCode: 401, title: "Invalid credentials");
        if (user.OrganizationId.HasValue && !await db.Organizations.AnyAsync(x => x.Id == user.OrganizationId && x.IsActive))
            return Results.Problem(statusCode: 403, title: "This client workspace is suspended");
        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded) return Results.Problem(statusCode: 401, title: "Invalid credentials");
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "LoginSucceeded", "Authentication", user.Id));
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
    auth.MapPost("/logout", async (SignInManager<AppUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
    auth.MapGet("/me", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var roles = await users.GetRolesAsync(user);
        var organization = user.OrganizationId.HasValue ? await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == user.OrganizationId) : null;
        return Results.Ok(UserResponse(user, roles.SingleOrDefault() ?? Roles.Employee, organization));
    }).RequireAuthorization();
}

static void MapPlatform(WebApplication app)
{
    var platform = app.MapGroup("/api/platform").RequireAuthorization("PlatformAdmin");
    platform.MapGet("/organizations", async (WalletsDbContext db) => await db.Organizations.AsNoTracking().OrderBy(x => x.Name)
        .Select(x => new { x.Id, x.Name, x.Slug, x.IsActive, x.CreatedAtUtc, Users = db.Users.Count(u => u.OrganizationId == x.Id), Devices = db.WalletDevices.Count(d => d.OrganizationId == x.Id), Wallets = db.Wallets.Count(w => w.OrganizationId == x.Id) }).ToListAsync());
    platform.MapPost("/organizations", async (CreateOrganizationRequest request, WalletsDbContext db, UserManager<AppUser> users) =>
    {
        var slug = Slug(request.Slug ?? request.Name);
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(slug)) return Results.BadRequest(new { error = "Organization name is required." });
        if (await db.Organizations.AnyAsync(x => x.Slug == slug)) return Results.Conflict(new { error = "That organization URL is already used." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var organization = new Organization { Name = request.Name.Trim(), Slug = slug };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        var owner = new AppUser
        {
            UserName = request.OwnerEmail.Trim(), Email = request.OwnerEmail.Trim(), DisplayName = request.OwnerName.Trim(),
            OrganizationId = organization.Id, EmailConfirmed = true, CanConfirmReceipts = true, CanRejectReceipts = true,
            CanViewReports = true, CanExportReports = true, CanManageDevices = true, CanManageTeam = true, VisibleReceiptDays = 3650
        };
        var created = await users.CreateAsync(owner, request.OwnerPassword);
        if (!created.Succeeded) return Results.BadRequest(new { error = string.Join("; ", created.Errors.Select(x => x.Description)) });
        await users.AddToRoleAsync(owner, Roles.Owner);
        db.AuditEvents.Add(Audit(organization.Id, null, "OrganizationCreated", nameof(Organization), organization.Id.ToString()));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Created($"/api/platform/organizations/{organization.Id}", new { organization.Id, organization.Name, organization.Slug, OwnerId = owner.Id });
    });
    platform.MapPut("/organizations/{id:guid}/status", async (Guid id, ToggleRequest request, WalletsDbContext db) =>
    {
        var organization = await db.Organizations.SingleAsync(x => x.Id == id);
        organization.IsActive = request.Enabled;
        db.AuditEvents.Add(Audit(id, null, request.Enabled ? "OrganizationActivated" : "OrganizationSuspended", nameof(Organization), id.ToString()));
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
}

static void MapTeam(WebApplication app)
{
    var team = app.MapGroup("/api/team").RequireAuthorization();
    team.MapGet("/", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var actor = await RequireOrganizationUser(principal, users);
        if (!CanManageTeam(principal, actor)) return Results.Forbid();
        var rows = await db.Users.AsNoTracking().Where(x => x.OrganizationId == actor.OrganizationId).OrderBy(x => x.DisplayName).ToListAsync();
        var result = new List<object>();
        foreach (var user in rows)
        {
            var role = (await users.GetRolesAsync(user)).SingleOrDefault() ?? Roles.Employee;
            var wallets = await db.UserWalletAccess.Where(x => x.UserId == user.Id).Select(x => x.WalletId).ToListAsync();
            result.Add(new { user.Id, user.DisplayName, user.Email, Role = role, user.IsActive, user.VisibleReceiptDays, user.CanConfirmReceipts, user.CanRejectReceipts, user.CanViewReports, user.CanExportReports, user.CanManageDevices, user.CanManageTeam, WalletIds = wallets });
        }
        return Results.Ok(result);
    });
    team.MapPost("/", async (CreateTeamMemberRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var actor = await RequireOrganizationUser(principal, users);
        if (!CanManageTeam(principal, actor)) return Results.Forbid();
        if (!Roles.OrganizationRoles.Contains(request.Role) || request.Role == Roles.Owner && !principal.IsInRole(Roles.Owner)) return Results.Forbid();
        var user = new AppUser
        {
            UserName = request.Email.Trim(), Email = request.Email.Trim(), EmailConfirmed = true, DisplayName = request.DisplayName.Trim(), OrganizationId = actor.OrganizationId,
            VisibleReceiptDays = Math.Clamp(request.VisibleReceiptDays, 1, 3650), IsActive = true,
            CanConfirmReceipts = request.CanConfirmReceipts, CanRejectReceipts = request.CanRejectReceipts,
            CanViewReports = request.CanViewReports, CanExportReports = request.CanExportReports,
            CanManageDevices = request.CanManageDevices, CanManageTeam = request.CanManageTeam
        };
        ApplyRoleDefaults(user, request.Role);
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return Results.BadRequest(new { error = string.Join("; ", created.Errors.Select(x => x.Description)) });
        await users.AddToRoleAsync(user, request.Role);
        await SetWalletAccess(db, user, actor.OrganizationId!.Value, request.WalletIds);
        db.AuditEvents.Add(Audit(actor.OrganizationId, actor.Id, "TeamMemberCreated", nameof(AppUser), user.Id));
        await db.SaveChangesAsync();
        return Results.Created($"/api/team/{user.Id}", new { user.Id });
    });
    team.MapPut("/{id}/access", async (string id, UpdateTeamAccessRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var actor = await RequireOrganizationUser(principal, users);
        if (!CanManageTeam(principal, actor)) return Results.Forbid();
        var user = await db.Users.SingleAsync(x => x.Id == id && x.OrganizationId == actor.OrganizationId);
        user.VisibleReceiptDays = Math.Clamp(request.VisibleReceiptDays, 1, 3650);
        user.IsActive = request.IsActive;
        user.CanConfirmReceipts = request.CanConfirmReceipts;
        user.CanRejectReceipts = request.CanRejectReceipts;
        user.CanViewReports = request.CanViewReports;
        user.CanExportReports = request.CanExportReports;
        user.CanManageDevices = request.CanManageDevices;
        user.CanManageTeam = request.CanManageTeam;
        await SetWalletAccess(db, user, actor.OrganizationId!.Value, request.WalletIds);
        db.AuditEvents.Add(Audit(actor.OrganizationId, actor.Id, "TeamAccessUpdated", nameof(AppUser), user.Id));
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
}

static void MapWallets(WebApplication app)
{
    var wallets = app.MapGroup("/api/wallets").RequireAuthorization();
    wallets.MapGet("/", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var query = db.Wallets.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        if (!IsOrganizationAdmin(principal)) query = query.Where(x => db.UserWalletAccess.Any(a => a.UserId == user.Id && a.WalletId == x.Id));
        return Results.Ok(await query.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Provider, x.AccountNumber, x.CurrencyCode, x.DeviceId, x.IsActive, x.CreatedAtUtc }).ToListAsync());
    });
    wallets.MapPost("/", async (WalletRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!IsOrganizationAdmin(principal)) return Results.Forbid();
        var currency = NormalizeCurrency(request.CurrencyCode);
        if (request.DeviceId.HasValue && !await db.WalletDevices.AnyAsync(x => x.Id == request.DeviceId && x.OrganizationId == user.OrganizationId)) return Results.BadRequest(new { error = "Invalid device." });
        var wallet = new Wallet { OrganizationId = user.OrganizationId!.Value, Name = request.Name.Trim(), Provider = request.Provider.Trim(), AccountNumber = request.AccountNumber.Trim(), NormalizedAccountNumber = NormalizeAccount(request.AccountNumber), CurrencyCode = currency, DeviceId = request.DeviceId };
        db.Wallets.Add(wallet);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "WalletCreated", nameof(Wallet), wallet.Id.ToString()));
        await db.SaveChangesAsync();
        return Results.Created($"/api/wallets/{wallet.Id}", new { wallet.Id });
    });
    wallets.MapPut("/{id:guid}", async (Guid id, WalletRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!IsOrganizationAdmin(principal)) return Results.Forbid();
        var wallet = await db.Wallets.SingleAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId);
        wallet.Name = request.Name.Trim(); wallet.Provider = request.Provider.Trim(); wallet.AccountNumber = request.AccountNumber.Trim();
        wallet.NormalizedAccountNumber = NormalizeAccount(request.AccountNumber); wallet.CurrencyCode = NormalizeCurrency(request.CurrencyCode); wallet.DeviceId = request.DeviceId; wallet.IsActive = request.IsActive;
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "WalletUpdated", nameof(Wallet), wallet.Id.ToString()));
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
    wallets.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!IsOrganizationAdmin(principal)) return Results.Forbid();
        var wallet = await db.Wallets.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId);
        if (wallet is null) return Results.NotFound();
        if (await db.WalletReceipts.AnyAsync(x => x.WalletId == id))
            return Results.Conflict(new { error = "This wallet has received-money history and cannot be deleted. Pause it instead to preserve your records." });
        var preferences = await db.NotificationPreferences.Where(x => x.WalletId == id).ToListAsync();
        foreach (var preference in preferences) preference.WalletId = null;
        db.Wallets.Remove(wallet);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "WalletDeleted", nameof(Wallet), wallet.Id.ToString()));
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
}

static void MapDevices(WebApplication app)
{
    var devices = app.MapGroup("/api/devices");
    devices.MapGet("/", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        return Results.Ok(await db.WalletDevices.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId).OrderByDescending(x => x.LastSeenAtUtc)
            .Select(x => new { x.Id, x.Name, x.Platform, x.IsActive, x.PairedAtUtc, x.LastSeenAtUtc, WalletCount = db.Wallets.Count(w => w.DeviceId == x.Id) }).ToListAsync());
    }).RequireAuthorization();
    devices.MapPost("/pairing", async (DevicePairingRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var device = new WalletDevice { OrganizationId = user.OrganizationId!.Value, Name = request.Name.Trim(), Platform = "Android", PairingCodeHash = Hash(code), PairingCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(10) };
        db.WalletDevices.Add(device);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "DevicePairingCreated", nameof(WalletDevice), device.Id.ToString()));
        await db.SaveChangesAsync();
        return Results.Ok(new { device.Id, PairingCode = code, ExpiresAtUtc = device.PairingCodeExpiresAtUtc });
    }).RequireAuthorization();
    devices.MapPost("/pair", async (PairDeviceRequest request, WalletsDbContext db) =>
    {
        var codeHash = Hash(request.PairingCode.Trim());
        var device = await db.WalletDevices.SingleOrDefaultAsync(x => x.PairingCodeHash == codeHash && x.PairingCodeExpiresAtUtc > DateTime.UtcNow && x.IsActive);
        if (device is null) return Results.Problem(statusCode: 401, title: "Invalid or expired pairing code");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        device.InstallationId = request.InstallationId.Trim(); device.TokenHash = Hash(token); device.PairingCodeHash = null; device.PairingCodeExpiresAtUtc = null; device.PairedAtUtc = DateTime.UtcNow; device.LastSeenAtUtc = DateTime.UtcNow;
        db.AuditEvents.Add(Audit(device.OrganizationId, null, "DevicePaired", nameof(WalletDevice), device.Id.ToString()));
        await db.SaveChangesAsync();
        var wallets = await db.Wallets.Where(x => x.DeviceId == device.Id && x.IsActive).Select(x => new { x.Id, x.Name, x.Provider, x.AccountNumber, x.CurrencyCode }).ToListAsync();
        return Results.Ok(new { DeviceId = device.Id, DeviceToken = token, Wallets = wallets });
    });
    devices.MapPut("/{id:guid}/status", async (Guid id, ToggleRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var device = await db.WalletDevices.SingleAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId);
        device.IsActive = request.Enabled;
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, request.Enabled ? "DeviceActivated" : "DeviceDeactivated", nameof(WalletDevice), id.ToString()));
        await db.SaveChangesAsync();
        return Results.NoContent();
    }).RequireAuthorization();
    devices.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var device = await db.WalletDevices.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId);
        if (device is null) return Results.NotFound();
        if (await db.WalletReceipts.AnyAsync(x => x.DeviceId == id))
            return Results.Conflict(new { error = "This device has received-money history and cannot be deleted. Deactivate it instead to preserve your records." });
        var attachedWallets = await db.Wallets.Where(x => x.DeviceId == id).ToListAsync();
        foreach (var wallet in attachedWallets) wallet.DeviceId = null;
        db.WalletDevices.Remove(device);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "DeviceDeleted", nameof(WalletDevice), device.Id.ToString()));
        await db.SaveChangesAsync();
        return Results.NoContent();
    }).RequireAuthorization();
}

static void MapReceipts(WebApplication app)
{
    app.MapPost("/api/captures", async (CaptureRequest request, HttpContext http, WalletsDbContext db, IDataProtectionProvider protection) =>
    {
        var token = http.Request.Headers["X-Wallet-Device-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();
        var tokenHash = Hash(token);
        var device = await db.WalletDevices.SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.IsActive);
        if (device is null) return Results.Unauthorized();
        device.LastSeenAtUtc = DateTime.UtcNow;
        if (await db.WalletReceipts.AnyAsync(x => x.DeviceId == device.Id && x.Fingerprint == request.Fingerprint)) { await db.SaveChangesAsync(); return Results.Ok(new { duplicate = true }); }
        var raw = string.Join("\n", new[] { request.Title, request.Body }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!WalletMessageParser.TryParse(request.SourcePackage, raw, out var parsed)) { await db.SaveChangesAsync(); return Results.Accepted(value: new { ignored = true }); }
        if (!string.IsNullOrWhiteSpace(parsed.Reference) && await db.WalletReceipts.AnyAsync(x => x.OrganizationId == device.OrganizationId && x.Provider == parsed.Provider && x.ProviderReference == parsed.Reference))
            return Results.Ok(new { duplicate = true });
        var walletQuery = db.Wallets.Where(x => x.OrganizationId == device.OrganizationId && x.DeviceId == device.Id && x.IsActive);
        Wallet? wallet = null;
        if (request.WalletId.HasValue) wallet = await walletQuery.SingleOrDefaultAsync(x => x.Id == request.WalletId);
        if (wallet is null && !string.IsNullOrWhiteSpace(parsed.Destination))
        {
            var normalizedDestination = NormalizeAccount(parsed.Destination);
            wallet = await walletQuery.SingleOrDefaultAsync(x => x.NormalizedAccountNumber == normalizedDestination);
        }
        if (wallet is null)
        {
            var providerCandidates = await walletQuery.Where(x => x.Provider == parsed.Provider).Take(2).ToListAsync();
            if (providerCandidates.Count == 1) wallet = providerCandidates[0];
        }
        if (wallet is null)
        {
            var candidates = await walletQuery.Take(2).ToListAsync();
            if (candidates.Count == 1) wallet = candidates[0];
        }
        if (wallet is null) { await db.SaveChangesAsync(); return Results.Accepted(value: new { ignored = true, reason = "wallet-not-resolved" }); }
        var receivedAt = request.ReceivedAtUtc.Kind == DateTimeKind.Utc ? request.ReceivedAtUtc : request.ReceivedAtUtc.ToUniversalTime();
        if (receivedAt < DateTime.UtcNow.AddDays(-2) || receivedAt > DateTime.UtcNow.AddMinutes(10)) return Results.Accepted(value: new { ignored = true, reason = "outside-capture-window" });
        var receipt = new WalletReceipt
        {
            OrganizationId = device.OrganizationId, WalletId = wallet.Id, DeviceId = device.Id, Provider = parsed.Provider,
            Amount = parsed.Amount, CurrencyCode = parsed.CurrencyCode, Sender = parsed.Sender, ProviderReference = parsed.Reference,
            Fingerprint = request.Fingerprint, ProtectedMessage = protection.CreateProtector("WalletsHub.Receipt.v1").Protect(raw),
            SourcePackage = request.SourcePackage ?? "unknown", ReceivedAtUtc = receivedAt
        };
        db.WalletReceipts.Add(receipt);
        var recipientRoles = await (from candidate in db.Users
            join userRole in db.UserRoles on candidate.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where candidate.OrganizationId == device.OrganizationId && candidate.IsActive
                && (role.Name == Roles.Owner || role.Name == Roles.Admin || db.UserWalletAccess.Any(access => access.UserId == candidate.Id && access.WalletId == wallet.Id))
            select new { candidate.Id, Role = role.Name! }).ToListAsync();
        var recipientIds = recipientRoles.Select(x => x.Id).ToList();
        var preferences = await db.NotificationPreferences.Where(x => recipientIds.Contains(x.UserId) && (x.WalletId == null || x.WalletId == wallet.Id)).ToListAsync();
        foreach (var recipient in recipientRoles.DistinctBy(x => x.Id))
        {
            var preference = preferences.FirstOrDefault(x => x.UserId == recipient.Id && x.WalletId == wallet.Id) ?? preferences.FirstOrDefault(x => x.UserId == recipient.Id && x.WalletId == null);
            var enabled = preference?.EveryReceipt ?? recipient.Role is Roles.Owner or Roles.Admin;
            var meetsThreshold = preference?.MinimumAmount is null || parsed.Amount >= preference.MinimumAmount.Value;
            if (enabled && meetsThreshold)
                db.UserNotifications.Add(new UserNotification { OrganizationId = device.OrganizationId, UserId = recipient.Id, Title = $"{parsed.Amount:N2} {parsed.CurrencyCode} received", Body = $"{parsed.Provider} payment detected for {wallet.Name}.", Link = "/receipts", SourceId = receipt.Id });
        }
        db.AuditEvents.Add(Audit(device.OrganizationId, null, "ReceiptDetected", nameof(WalletReceipt), receipt.Id.ToString()));
        await db.SaveChangesAsync();
        return Results.Created($"/api/receipts/{receipt.Id}", new { receipt.Id, receipt.Amount, receipt.CurrencyCode, receipt.Provider });
    });

    var receipts = app.MapGroup("/api/receipts").RequireAuthorization();
    receipts.MapGet("/", async (DateTime? from, DateTime? to, Guid? walletId, ReceiptStatus? status, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db, IDataProtectionProvider protection) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var start = from?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-user.VisibleReceiptDays);
        var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
        var query = ScopedReceipts(principal, user, db).Where(x => x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end);
        if (walletId.HasValue) query = query.Where(x => x.WalletId == walletId);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        var rows = await query.OrderByDescending(x => x.ReceivedAtUtc).Take(1000).Join(db.Wallets, r => r.WalletId, w => w.Id, (r, w) => new { Receipt = r, WalletName = w.Name }).ToListAsync();
        var protector = protection.CreateProtector("WalletsHub.Receipt.v1");
        return Results.Ok(rows.Select(x => new { x.Receipt.Id, x.Receipt.WalletId, x.WalletName, x.Receipt.DeviceId, x.Receipt.Provider, x.Receipt.Amount, x.Receipt.CurrencyCode, x.Receipt.Sender, x.Receipt.ProviderReference, Message = Unprotect(protector, x.Receipt.ProtectedMessage), x.Receipt.Status, x.Receipt.ReceivedAtUtc, x.Receipt.ReviewedByUserId, x.Receipt.ReviewedAtUtc, x.Receipt.ReviewNote }));
    });
    receipts.MapPost("/{id:guid}/review", async (Guid id, ReviewRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var receipt = await ScopedReceipts(principal, user, db).SingleAsync(x => x.Id == id);
        if (receipt.Status != ReceiptStatus.Pending) return Results.Conflict(new { error = "Receipt has already been reviewed." });
        var action = request.Action.Trim().ToLowerInvariant();
        if (action == "confirm" && !user.CanConfirmReceipts && !IsOrganizationAdmin(principal)) return Results.Forbid();
        if (action == "reject" && !user.CanRejectReceipts && !IsOrganizationAdmin(principal)) return Results.Forbid();
        receipt.Status = action switch { "confirm" => ReceiptStatus.Confirmed, "reject" => ReceiptStatus.Rejected, _ => throw new BadHttpRequestException("Action must be Confirm or Reject.") };
        receipt.ReviewedByUserId = user.Id; receipt.ReviewedAtUtc = DateTime.UtcNow; receipt.ReviewNote = request.Note?.Trim();
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, $"Receipt{receipt.Status}", nameof(WalletReceipt), receipt.Id.ToString()));
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
}

static void MapNotifications(WebApplication app)
{
    var notifications = app.MapGroup("/api/notifications").RequireAuthorization();
    notifications.MapGet("/", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var items = await db.UserNotifications.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId && x.UserId == user.Id).OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync();
        return Results.Ok(new { UnreadCount = items.Count(x => x.ReadAtUtc == null), Items = items.Select(x => new { x.Id, x.Title, x.Body, x.Link, x.SourceId, x.CreatedAtUtc, IsRead = x.ReadAtUtc != null }) });
    });
    notifications.MapPost("/{id:guid}/read", async (Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var item = await db.UserNotifications.SingleAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId && x.UserId == user.Id);
        item.ReadAtUtc ??= DateTime.UtcNow; await db.SaveChangesAsync(); return Results.NoContent();
    });
    notifications.MapPost("/read-all", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        await db.UserNotifications.Where(x => x.OrganizationId == user.OrganizationId && x.UserId == user.Id && x.ReadAtUtc == null).ExecuteUpdateAsync(update => update.SetProperty(x => x.ReadAtUtc, DateTime.UtcNow));
        return Results.NoContent();
    });

    var settings = app.MapGroup("/api/settings").RequireAuthorization();
    settings.MapGet("/notifications", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var preference = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id && x.WalletId == null);
        return Results.Ok(preference is null ? new NotificationPreferenceResponse(true, null, true, true, true) : new NotificationPreferenceResponse(preference.EveryReceipt, preference.MinimumAmount, preference.DailySummary, preference.DeviceOffline, preference.RejectedReceipt));
    });
    settings.MapPut("/notifications", async (NotificationPreferenceRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var preference = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == user.Id && x.WalletId == null);
        if (preference is null) { preference = new NotificationPreference { OrganizationId = user.OrganizationId!.Value, UserId = user.Id }; db.NotificationPreferences.Add(preference); }
        preference.EveryReceipt = request.EveryReceipt; preference.MinimumAmount = request.MinimumAmount > 0 ? request.MinimumAmount : null;
        preference.DailySummary = request.DailySummary; preference.DeviceOffline = request.DeviceOffline; preference.RejectedReceipt = request.RejectedReceipt;
        await db.SaveChangesAsync(); return Results.NoContent();
    });
}

static void MapReports(WebApplication app)
{
    app.MapGet("/api/reports/summary", async (DateTime? from, DateTime? to, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanViewReports && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var start = from?.ToUniversalTime() ?? DateTime.UtcNow.Date.AddDays(-30);
        var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
        var query = ScopedReceipts(principal, user, db).Where(x => x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end);
        var totals = await query.GroupBy(x => new { x.CurrencyCode, x.Status }).Select(g => new { g.Key.CurrencyCode, g.Key.Status, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).ToListAsync();
        var wallets = await query.GroupBy(x => new { x.WalletId, x.CurrencyCode }).Select(g => new { g.Key.WalletId, g.Key.CurrencyCode, Count = g.Count(), Amount = g.Where(x => x.Status == ReceiptStatus.Confirmed).Sum(x => x.Amount) }).ToListAsync();
        var names = await db.Wallets.Where(x => x.OrganizationId == user.OrganizationId).ToDictionaryAsync(x => x.Id, x => x.Name);
        var daily = await query.Where(x => x.Status == ReceiptStatus.Confirmed).GroupBy(x => new { Day = x.ReceivedAtUtc.Date, x.CurrencyCode }).Select(g => new { g.Key.Day, g.Key.CurrencyCode, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).OrderBy(x => x.Day).ToListAsync();
        return Results.Ok(new { From = start, To = end, Totals = totals, Wallets = wallets.Select(x => new { x.WalletId, WalletName = names.GetValueOrDefault(x.WalletId, "Wallet"), x.CurrencyCode, x.Count, x.Amount }), Daily = daily });
    }).RequireAuthorization();
    app.MapGet("/api/reports/export.xlsx", async (DateTime? from, DateTime? to, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanExportReports && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var start = from?.ToUniversalTime() ?? DateTime.UtcNow.Date.AddDays(-30);
        var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
        var rows = await ScopedReceipts(principal, user, db).Where(x => x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end)
            .Join(db.Wallets, receipt => receipt.WalletId, wallet => wallet.Id, (receipt, wallet) => new { Receipt = receipt, WalletName = wallet.Name })
            .OrderByDescending(x => x.Receipt.ReceivedAtUtc).ToListAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Receipts");
        var headers = new[] { "Received at (UTC)", "Wallet", "Provider", "Sender", "Reference", "Amount", "Currency", "Status", "Reviewed at (UTC)", "Review note" };
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index]; var number = index + 2;
            sheet.Cell(number, 1).Value = row.Receipt.ReceivedAtUtc; sheet.Cell(number, 2).Value = row.WalletName;
            sheet.Cell(number, 3).Value = row.Receipt.Provider; sheet.Cell(number, 4).Value = row.Receipt.Sender ?? "";
            sheet.Cell(number, 5).Value = row.Receipt.ProviderReference ?? ""; sheet.Cell(number, 6).Value = row.Receipt.Amount;
            sheet.Cell(number, 7).Value = row.Receipt.CurrencyCode; sheet.Cell(number, 8).Value = row.Receipt.Status.ToString();
            if (row.Receipt.ReviewedAtUtc.HasValue) sheet.Cell(number, 9).Value = row.Receipt.ReviewedAtUtc.Value;
            sheet.Cell(number, 10).Value = row.Receipt.ReviewNote ?? "";
        }
        sheet.Row(1).Style.Font.Bold = true; sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F7EF");
        sheet.SheetView.FreezeRows(1); sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream(); workbook.SaveAs(stream);
        return Results.File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"wallets-hub-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }).RequireAuthorization();
}

static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<WalletsDbContext>();
    await db.Database.EnsureCreatedAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in Roles.All) if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
    var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var email = configuration["Seed:PlatformEmail"] ?? "admin@walletshub.local";
    var password = configuration["Seed:PlatformPassword"] ?? throw new InvalidOperationException("Seed:PlatformPassword is required.");
    if (await users.FindByEmailAsync(email) is null)
    {
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Wallets Hub Platform Admin", IsActive = true, VisibleReceiptDays = 3650 };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        await users.AddToRoleAsync(user, Roles.PlatformAdmin);
    }
}

static async Task<AppUser> RequireOrganizationUser(ClaimsPrincipal principal, UserManager<AppUser> users)
{
    var user = await users.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
    if (!user.IsActive || !user.OrganizationId.HasValue) throw new UnauthorizedAccessException();
    return user;
}

static IQueryable<WalletReceipt> ScopedReceipts(ClaimsPrincipal principal, AppUser user, WalletsDbContext db)
{
    var query = db.WalletReceipts.Where(x => x.OrganizationId == user.OrganizationId);
    if (!IsOrganizationAdmin(principal)) query = query.Where(x => db.UserWalletAccess.Any(a => a.UserId == user.Id && a.WalletId == x.WalletId));
    return query.Where(x => x.ReceivedAtUtc >= DateTime.UtcNow.AddDays(-user.VisibleReceiptDays));
}

static bool IsOrganizationAdmin(ClaimsPrincipal principal) => principal.IsInRole(Roles.Owner) || principal.IsInRole(Roles.Admin);
static bool CanManageTeam(ClaimsPrincipal principal, AppUser user) => IsOrganizationAdmin(principal) || user.CanManageTeam;
static void ApplyRoleDefaults(AppUser user, string role)
{
    if (role is Roles.Owner or Roles.Admin)
    {
        user.CanConfirmReceipts = user.CanRejectReceipts = user.CanViewReports = user.CanExportReports = user.CanManageDevices = user.CanManageTeam = true;
        user.VisibleReceiptDays = 3650;
    }
}
static async Task SetWalletAccess(WalletsDbContext db, AppUser user, Guid organizationId, IReadOnlyCollection<Guid> walletIds)
{
    var valid = await db.Wallets.Where(x => x.OrganizationId == organizationId && walletIds.Contains(x.Id)).Select(x => x.Id).ToListAsync();
    var old = await db.UserWalletAccess.Where(x => x.UserId == user.Id).ToListAsync();
    db.UserWalletAccess.RemoveRange(old);
    db.UserWalletAccess.AddRange(valid.Distinct().Select(id => new UserWalletAccess { UserId = user.Id, WalletId = id }));
}
static object UserResponse(AppUser user, string role, Organization? organization) => new { user.Id, user.DisplayName, user.Email, Role = role, user.OrganizationId, OrganizationName = organization?.Name, OrganizationSlug = organization?.Slug, user.VisibleReceiptDays, user.CanConfirmReceipts, user.CanRejectReceipts, user.CanViewReports, user.CanExportReports, user.CanManageDevices, user.CanManageTeam };
static AuditEvent Audit(Guid? organizationId, string? userId, string action, string entityType, string? entityId) => new() { OrganizationId = organizationId, UserId = userId, Action = action, EntityType = entityType, EntityId = entityId };
static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
static string NormalizeAccount(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
static string NormalizeCurrency(string value) => value.Trim().ToUpperInvariant() switch { "EGP" => "EGP", "USD" => "USD", _ => throw new BadHttpRequestException("Currency must be EGP or USD.") };
static string Slug(string value) => string.Join('-', value.Trim().ToLowerInvariant().Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries).Select(part => new string(part.Where(char.IsLetterOrDigit).ToArray())).Where(part => part.Length > 0));
static string Unprotect(IDataProtector protector, string value) { try { return protector.Unprotect(value); } catch { return "Message unavailable"; } }

public sealed record LoginRequest(string Email, string Password);
public sealed record CreateOrganizationRequest(string Name, string? Slug, string OwnerName, string OwnerEmail, string OwnerPassword);
public sealed record ToggleRequest(bool Enabled);
public sealed record CreateTeamMemberRequest(string DisplayName, string Email, string Password, string Role, int VisibleReceiptDays, bool CanConfirmReceipts, bool CanRejectReceipts, bool CanViewReports, bool CanExportReports, bool CanManageDevices, bool CanManageTeam, IReadOnlyCollection<Guid> WalletIds);
public sealed record UpdateTeamAccessRequest(bool IsActive, int VisibleReceiptDays, bool CanConfirmReceipts, bool CanRejectReceipts, bool CanViewReports, bool CanExportReports, bool CanManageDevices, bool CanManageTeam, IReadOnlyCollection<Guid> WalletIds);
public sealed record WalletRequest(string Name, string Provider, string AccountNumber, string CurrencyCode, Guid? DeviceId, bool IsActive = true);
public sealed record DevicePairingRequest(string Name);
public sealed record PairDeviceRequest(string PairingCode, string InstallationId);
public sealed record CaptureRequest(Guid? WalletId, string? SourcePackage, string? Title, string? Body, DateTime ReceivedAtUtc, string Fingerprint);
public sealed record ReviewRequest(string Action, string? Note);
public sealed record NotificationPreferenceRequest(bool EveryReceipt, decimal? MinimumAmount, bool DailySummary, bool DeviceOffline, bool RejectedReceipt);
public sealed record NotificationPreferenceResponse(bool EveryReceipt, decimal? MinimumAmount, bool DailySummary, bool DeviceOffline, bool RejectedReceipt);
