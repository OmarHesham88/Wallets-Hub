using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(2));
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
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientIp(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    options.AddPolicy("pairing", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientIp(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});
builder.Services.AddHostedService<OperationsNotificationWorker>();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers["Cache-Control"] = "no-store";
    await next();
});
app.UseAuthentication();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole(Roles.PlatformAdmin)
        && context.Request.Path.StartsWithSegments("/api")
        && !context.Request.Path.StartsWithSegments("/api/auth")
        && !context.Request.Path.StartsWithSegments("/api/platform"))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden; return;
    }
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
MapAudit(app);
MapOperations(app);

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<WalletsDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Wallets" ALTER COLUMN "CurrencyCode" TYPE character varying(4);
        ALTER TABLE "WalletReceipts" ALTER COLUMN "CurrencyCode" TYPE character varying(4);
        DROP INDEX IF EXISTS "IX_Wallets_OrganizationId_NormalizedAccountNumber";
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Wallets_OrganizationId_Provider_NormalizedAccountNumber"
            ON "Wallets" ("OrganizationId", "Provider", "NormalizedAccountNumber");
        ALTER TABLE "Organizations" ADD COLUMN IF NOT EXISTS "TimeZoneId" character varying(80) NOT NULL DEFAULT 'Africa/Cairo';
        ALTER TABLE "Organizations" ADD COLUMN IF NOT EXISTS "MaskSensitiveMessages" boolean NOT NULL DEFAULT false;
        ALTER TABLE "Organizations" ADD COLUMN IF NOT EXISTS "RequireReceiptConfirmation" boolean NOT NULL DEFAULT false;
        ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "AllWalletAccess" boolean NOT NULL DEFAULT false;
        ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "CanConfirmReceipts" boolean NOT NULL DEFAULT false;
        -- Rejection was removed from Wallets Hub. Older production databases can
        -- still contain this required column, which blocks creation of new users
        -- because the current identity model no longer writes a value for it.
        ALTER TABLE "AspNetUsers" DROP COLUMN IF EXISTS "CanRejectReceipts";
        UPDATE "AspNetUsers" u SET "AllWalletAccess" = true
          WHERE u."OrganizationId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "UserWalletAccess" a WHERE a."UserId" = u."Id");
        ALTER TABLE "Wallets" ADD COLUMN IF NOT EXISTS "OpeningBalance" numeric(18,4) NOT NULL DEFAULT 0;
        ALTER TABLE "Wallets" ADD COLUMN IF NOT EXISTS "BalanceLimit" numeric(18,4) NULL;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "LastHeartbeatAtUtc" timestamp with time zone NULL;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "LastSmsAtUtc" timestamp with time zone NULL;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "LastAxisNotificationAtUtc" timestamp with time zone NULL;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "LastCaptureAtUtc" timestamp with time zone NULL;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "OfflineAlertSentAtUtc" timestamp with time zone NULL;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "AppVersion" character varying(40) NULL;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "AndroidVersion" character varying(40) NULL;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "PendingUploadCount" integer NOT NULL DEFAULT 0;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "FailedUploadCount" integer NOT NULL DEFAULT 0;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "SmsPermissionGranted" boolean NOT NULL DEFAULT false;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "AxisNotificationAccessGranted" boolean NOT NULL DEFAULT false;
        ALTER TABLE "WalletDevices" ADD COLUMN IF NOT EXISTS "BatteryOptimizationIgnored" boolean NOT NULL DEFAULT false;
        CREATE TABLE IF NOT EXISTS "CaptureEvents" (
            "Id" uuid NOT NULL PRIMARY KEY, "OrganizationId" uuid NOT NULL REFERENCES "Organizations" ("Id") ON DELETE RESTRICT,
            "DeviceId" uuid NOT NULL REFERENCES "WalletDevices" ("Id") ON DELETE RESTRICT,
            "WalletId" uuid NULL REFERENCES "Wallets" ("Id") ON DELETE SET NULL,
            "ReceiptId" uuid NULL REFERENCES "WalletReceipts" ("Id") ON DELETE SET NULL,
            "Fingerprint" character varying(128) NOT NULL, "Status" character varying(30) NOT NULL, "Reason" character varying(100) NOT NULL,
            "Provider" character varying(80) NULL, "Amount" numeric(18,4) NULL, "CurrencyCode" character varying(4) NULL,
            "Sender" text NULL, "Destination" text NULL, "ProviderReference" character varying(160) NULL,
            "SourcePackage" character varying(200) NOT NULL, "ProtectedMessage" text NOT NULL, "ReceivedAtUtc" timestamp with time zone NOT NULL,
            "FirstSeenAtUtc" timestamp with time zone NOT NULL, "LastSeenAtUtc" timestamp with time zone NOT NULL, "AttemptCount" integer NOT NULL DEFAULT 1
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_CaptureEvents_DeviceId_Fingerprint" ON "CaptureEvents" ("DeviceId", "Fingerprint");
        CREATE INDEX IF NOT EXISTS "IX_CaptureEvents_OrganizationId_Status_LastSeenAtUtc" ON "CaptureEvents" ("OrganizationId", "Status", "LastSeenAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_CaptureEvents_OrganizationId_Reason_LastSeenAtUtc" ON "CaptureEvents" ("OrganizationId", "Reason", "LastSeenAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_WalletReceipts_OrganizationId_WalletId_ReceivedAtUtc" ON "WalletReceipts" ("OrganizationId", "WalletId", "ReceivedAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_WalletReceipts_OrganizationId_DeviceId_ReceivedAtUtc" ON "WalletReceipts" ("OrganizationId", "DeviceId", "ReceivedAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_WalletReceipts_OrganizationId_CurrencyCode_ReceivedAtUtc" ON "WalletReceipts" ("OrganizationId", "CurrencyCode", "ReceivedAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_WalletReceipts_OrganizationId_Status_ReceivedAtUtc" ON "WalletReceipts" ("OrganizationId", "Status", "ReceivedAtUtc");
        CREATE TABLE IF NOT EXISTS "WalletLedgerEntries" (
            "Id" uuid NOT NULL PRIMARY KEY, "OrganizationId" uuid NOT NULL REFERENCES "Organizations" ("Id") ON DELETE RESTRICT,
            "WalletId" uuid NOT NULL REFERENCES "Wallets" ("Id") ON DELETE RESTRICT,
            "RelatedWalletId" uuid NULL REFERENCES "Wallets" ("Id") ON DELETE RESTRICT, "CorrelationId" uuid NULL,
            "Type" character varying(30) NOT NULL, "Amount" numeric(18,4) NOT NULL, "Note" character varying(500) NULL,
            "CreatedByUserId" text NULL, "OccurredAtUtc" timestamp with time zone NOT NULL, "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_WalletLedgerEntries_OrganizationId_WalletId_OccurredAtUtc" ON "WalletLedgerEntries" ("OrganizationId", "WalletId", "OccurredAtUtc");
        CREATE TABLE IF NOT EXISTS "WalletReconciliations" (
            "Id" uuid NOT NULL PRIMARY KEY, "OrganizationId" uuid NOT NULL REFERENCES "Organizations" ("Id") ON DELETE RESTRICT,
            "WalletId" uuid NOT NULL, "ExpectedBalance" numeric(18,4) NOT NULL, "ActualBalance" numeric(18,4) NOT NULL,
            "Variance" numeric(18,4) NOT NULL, "Note" character varying(500) NULL, "CreatedByUserId" text NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_WalletReconciliations_OrganizationId_WalletId_CreatedAtUtc" ON "WalletReconciliations" ("OrganizationId", "WalletId", "CreatedAtUtc");
        CREATE TABLE IF NOT EXISTS "NotificationDispatches" (
            "Id" uuid NOT NULL PRIMARY KEY, "OrganizationId" uuid NOT NULL, "DispatchKey" character varying(180) NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationDispatches_DispatchKey" ON "NotificationDispatches" ("DispatchKey");

        -- Wallets Hub is EGP-only. Remove legacy Binance, USD, and USDT data in
        -- dependency order while keeping unrelated EGP history intact.
        DELETE FROM "UserNotifications"
          WHERE "SourceId" IN (SELECT "Id" FROM "WalletReceipts" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance')
             OR lower("Title") LIKE '%usdt%' OR lower("Body") LIKE '%usdt%'
             OR lower("Title") LIKE '%usd%' OR lower("Body") LIKE '%usd%';
        DELETE FROM "AuditEvents"
          WHERE ("EntityType" = 'WalletReceipt' AND "EntityId" IN (SELECT "Id"::text FROM "WalletReceipts" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance'))
             OR ("EntityType" = 'Wallet' AND "EntityId" IN (SELECT "Id"::text FROM "Wallets" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance'))
             OR lower(COALESCE("DetailJson", '')) LIKE '%binance%'
             OR lower(COALESCE("DetailJson", '')) LIKE '%usdt%'
             OR lower(COALESCE("DetailJson", '')) LIKE '%usd%';
        DELETE FROM "CaptureEvents"
          WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance' OR lower("SourcePackage") LIKE '%binance%'
             OR "ReceiptId" IN (SELECT "Id" FROM "WalletReceipts" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance')
             OR "WalletId" IN (SELECT "Id" FROM "Wallets" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance');
        DELETE FROM "WalletReceipts" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance';
        DELETE FROM "NotificationPreferences" WHERE "WalletId" IN (SELECT "Id" FROM "Wallets" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance');
        DELETE FROM "UserWalletAccess" WHERE "WalletId" IN (SELECT "Id" FROM "Wallets" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance');
        DELETE FROM "WalletLedgerEntries" WHERE "WalletId" IN (SELECT "Id" FROM "Wallets" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance') OR "RelatedWalletId" IN (SELECT "Id" FROM "Wallets" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance');
        DELETE FROM "WalletReconciliations" WHERE "WalletId" IN (SELECT "Id" FROM "Wallets" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance');
        DELETE FROM "Wallets" WHERE "CurrencyCode" <> 'EGP' OR "Provider" = 'Binance';
        """);
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
        if (result.RequiresTwoFactor)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                return Results.Json(new { requiresTwoFactor = true }, statusCode: StatusCodes.Status409Conflict);
            var code = request.TwoFactorCode.Replace(" ", "").Replace("-", "");
            result = code.Length == 6
                ? await signIn.TwoFactorAuthenticatorSignInAsync(code, isPersistent: true, rememberClient: false)
                : await signIn.TwoFactorRecoveryCodeSignInAsync(request.TwoFactorCode);
        }
        if (!result.Succeeded) return Results.Problem(statusCode: 401, title: "Invalid credentials");
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "LoginSucceeded", "Authentication", user.Id));
        await db.SaveChangesAsync();
        return Results.NoContent();
    }).RequireRateLimiting("login");
    auth.MapPost("/logout", async (SignInManager<AppUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
    auth.MapGet("/me", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var roles = await users.GetRolesAsync(user);
        var organization = user.OrganizationId.HasValue ? await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == user.OrganizationId) : null;
        return Results.Ok(UserResponse(user, roles.SingleOrDefault() ?? Roles.Employee, organization));
    }).RequireAuthorization();
    auth.MapPut("/account", async (AccountUpdateRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, SignInManager<AppUser> signIn, WalletsDbContext db) =>
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (!await users.CheckPasswordAsync(user, request.CurrentPassword)) return Results.BadRequest(new { error = "The current password is incorrect." });
        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        if (email.Length == 0 || displayName.Length == 0) return Results.BadRequest(new { error = "Name and email are required." });
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null && existing.Id != user.Id) return Results.Conflict(new { error = "That email address is already in use." });
        user.Email = email; user.UserName = email; user.DisplayName = displayName; user.EmailConfirmed = true;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded) return Results.BadRequest(new { error = string.Join("; ", updated.Errors.Select(x => x.Description)) });
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var changed = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!changed.Succeeded) return Results.BadRequest(new { error = string.Join("; ", changed.Errors.Select(x => x.Description)) });
        }
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "AccountUpdated", nameof(AppUser), user.Id, new { Email = email, DisplayName = displayName, PasswordChanged = !string.IsNullOrWhiteSpace(request.NewPassword) }));
        await db.SaveChangesAsync(); await signIn.RefreshSignInAsync(user); return Results.NoContent();
    }).RequireAuthorization();
    auth.MapPost("/mfa/setup", async (ClaimsPrincipal principal, UserManager<AppUser> users) =>
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        await users.ResetAuthenticatorKeyAsync(user);
        var key = await users.GetAuthenticatorKeyAsync(user);
        var issuer = Uri.EscapeDataString("Wallets Hub");
        var account = Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id);
        return Results.Ok(new { secretKey = key, authenticatorUri = $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6" });
    }).RequireAuthorization();
    auth.MapPost("/mfa/confirm", async (MfaCodeRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, request.Code.Replace(" ", "").Replace("-", ""));
        if (!valid) return Results.BadRequest(new { error = "The authenticator code is invalid." });
        await users.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "MfaEnabled", nameof(AppUser), user.Id)); await db.SaveChangesAsync();
        return Results.Ok(new { recoveryCodes });
    }).RequireAuthorization();
    auth.MapPost("/mfa/disable", async (PasswordRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (!await users.CheckPasswordAsync(user, request.Password)) return Results.BadRequest(new { error = "The current password is incorrect." });
        await users.SetTwoFactorEnabledAsync(user, false); await users.ResetAuthenticatorKeyAsync(user);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "MfaDisabled", nameof(AppUser), user.Id)); await db.SaveChangesAsync();
        return Results.NoContent();
    }).RequireAuthorization();
    auth.MapPost("/revoke-other-sessions", async (ClaimsPrincipal principal, UserManager<AppUser> users, SignInManager<AppUser> signIn, WalletsDbContext db) =>
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        await users.UpdateSecurityStampAsync(user); await signIn.RefreshSignInAsync(user);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "OtherSessionsRevoked", nameof(AppUser), user.Id)); await db.SaveChangesAsync();
        return Results.NoContent();
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
            OrganizationId = organization.Id, EmailConfirmed = true,
            CanViewReports = true, CanExportReports = true, CanManageDevices = true, CanManageTeam = true, CanConfirmReceipts = true, VisibleReceiptDays = 3650
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
    platform.MapPost("/organizations/{id:guid}/reset-owner-password", async (Guid id, PlatformOwnerPasswordResetRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var platformUser = await users.GetUserAsync(principal); if (platformUser is null) return Results.Unauthorized();
        var owner = await users.FindByEmailAsync(request.OwnerEmail.Trim());
        if (owner is null || owner.OrganizationId != id || !await users.IsInRoleAsync(owner, Roles.Owner)) return Results.NotFound(new { error = "That owner account was not found in this organization." });
        var token = await users.GeneratePasswordResetTokenAsync(owner); var result = await users.ResetPasswordAsync(owner, token, request.NewPassword);
        if (!result.Succeeded) return Results.BadRequest(new { error = string.Join("; ", result.Errors.Select(x => x.Description)) });
        await users.UpdateSecurityStampAsync(owner); db.AuditEvents.Add(Audit(id, platformUser.Id, "OwnerPasswordResetByPlatform", nameof(AppUser), owner.Id)); await db.SaveChangesAsync();
        return Results.NoContent();
    });
    platform.MapPut("/account", async (PlatformAccountUpdateRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, SignInManager<AppUser> signIn, WalletsDbContext db) =>
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email)) return Results.BadRequest(new { error = "Email is required." });
        if (!await users.CheckPasswordAsync(user, request.CurrentPassword)) return Results.BadRequest(new { error = "The current password is incorrect." });
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null && existing.Id != user.Id) return Results.Conflict(new { error = "That email address is already in use." });

        await using var transaction = await db.Database.BeginTransactionAsync();
        user.Email = email;
        user.UserName = email;
        user.EmailConfirmed = true;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded) return Results.BadRequest(new { error = string.Join("; ", updated.Errors.Select(x => x.Description)) });
        var passwordChanged = !string.IsNullOrWhiteSpace(request.NewPassword);
        if (passwordChanged)
        {
            var password = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword!);
            if (!password.Succeeded) return Results.BadRequest(new { error = string.Join("; ", password.Errors.Select(x => x.Description)) });
        }
        db.AuditEvents.Add(Audit(null, user.Id, passwordChanged ? "PlatformCredentialsUpdated" : "PlatformEmailUpdated", nameof(AppUser), user.Id));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await signIn.RefreshSignInAsync(user);
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
        var actorRole = await GetRole(users, actor);
        var rows = await db.Users.AsNoTracking().Where(x => x.OrganizationId == actor.OrganizationId).OrderBy(x => x.DisplayName).ToListAsync();
        var result = new List<object>();
        foreach (var user in rows)
        {
            var role = (await users.GetRolesAsync(user)).SingleOrDefault() ?? Roles.Employee;
            var wallets = await db.UserWalletAccess.Where(x => x.UserId == user.Id).Select(x => x.WalletId).ToListAsync();
            result.Add(new { user.Id, user.DisplayName, user.Email, Role = role, user.IsActive, user.VisibleReceiptDays, user.CanViewReports, user.CanExportReports, user.CanManageDevices, user.CanManageTeam, user.CanConfirmReceipts, user.AllWalletAccess, WalletIds = wallets, CanEdit = user.Id != actor.Id && AccessControl.CanManageRole(actorRole, role) });
        }
        return Results.Ok(result);
    });
    team.MapPost("/", async (CreateTeamMemberRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var actor = await RequireOrganizationUser(principal, users);
        if (!CanManageTeam(principal, actor)) return Results.Forbid();
        var actorRole = await GetRole(users, actor);
        if (!Roles.OrganizationRoles.Contains(request.Role) || !AccessControl.CanManageRole(actorRole, request.Role)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest(new { error = "Name and email are required." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new AppUser
        {
            UserName = request.Email.Trim(), Email = request.Email.Trim(), EmailConfirmed = true, DisplayName = request.DisplayName.Trim(), OrganizationId = actor.OrganizationId,
            VisibleReceiptDays = Math.Clamp(request.VisibleReceiptDays, 1, 3650), IsActive = true,
            CanViewReports = request.CanViewReports, CanExportReports = request.CanExportReports,
            CanManageDevices = request.CanManageDevices, CanManageTeam = request.CanManageTeam, CanConfirmReceipts = request.CanConfirmReceipts, AllWalletAccess = request.AllWalletAccess
        };
        ApplyRoleDefaults(user, request.Role);
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return Results.BadRequest(new { error = string.Join("; ", created.Errors.Select(x => x.Description)) });
        await users.AddToRoleAsync(user, request.Role);
        var accessError = await SetWalletAccess(db, user, actor.OrganizationId!.Value, request.AllWalletAccess, request.WalletIds);
        if (accessError is not null) { await users.DeleteAsync(user); return Results.BadRequest(new { error = accessError }); }
        db.AuditEvents.Add(Audit(actor.OrganizationId, actor.Id, "TeamMemberCreated", nameof(AppUser), user.Id, new { request.Role, request.AllWalletAccess, request.WalletIds }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Created($"/api/team/{user.Id}", new { user.Id });
    });
    team.MapPut("/{id}", async (string id, UpdateTeamMemberRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var actor = await RequireOrganizationUser(principal, users);
        if (!CanManageTeam(principal, actor)) return Results.Forbid();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == actor.OrganizationId);
        if (user is null) return Results.NotFound();
        if (user.Id == actor.Id) return Results.BadRequest(new { error = "Use Account settings to update your own account." });
        var actorRole = await GetRole(users, actor);
        var currentRole = await GetRole(users, user);
        if (!Roles.OrganizationRoles.Contains(request.Role) || !AccessControl.CanManageRole(actorRole, currentRole) || !AccessControl.CanManageRole(actorRole, request.Role)) return Results.Forbid();
        if (currentRole == Roles.Owner && (!request.IsActive || request.Role != Roles.Owner))
        {
            var activeOwnerCount = await ActiveOwnerCount(db, actor.OrganizationId!.Value);
            if (activeOwnerCount <= 1) return Results.BadRequest(new { error = "The organization must keep at least one active owner." });
        }
        var email = request.Email.Trim(); var displayName = request.DisplayName.Trim();
        if (email.Length == 0 || displayName.Length == 0) return Results.BadRequest(new { error = "Name and email are required." });
        var duplicate = await users.FindByEmailAsync(email);
        if (duplicate is not null && duplicate.Id != user.Id) return Results.Conflict(new { error = "That email address is already in use." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        user.Email = email; user.UserName = email; user.EmailConfirmed = true; user.DisplayName = displayName;
        user.VisibleReceiptDays = Math.Clamp(request.VisibleReceiptDays, 1, 3650);
        user.IsActive = request.IsActive;
        user.CanViewReports = request.CanViewReports;
        user.CanExportReports = request.CanExportReports;
        user.CanManageDevices = request.CanManageDevices;
        user.CanManageTeam = request.CanManageTeam;
        user.CanConfirmReceipts = request.CanConfirmReceipts;
        user.AllWalletAccess = request.AllWalletAccess;
        ApplyRoleDefaults(user, request.Role);
        var accessError = await SetWalletAccess(db, user, actor.OrganizationId!.Value, request.AllWalletAccess, request.WalletIds);
        if (accessError is not null) return Results.BadRequest(new { error = accessError });
        if (currentRole != request.Role)
        {
            await users.RemoveFromRoleAsync(user, currentRole);
            await users.AddToRoleAsync(user, request.Role);
        }
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded) return Results.BadRequest(new { error = string.Join("; ", updated.Errors.Select(x => x.Description)) });
        await users.UpdateSecurityStampAsync(user);
        db.AuditEvents.Add(Audit(actor.OrganizationId, actor.Id, "TeamMemberUpdated", nameof(AppUser), user.Id, new { PreviousRole = currentRole, request.Role, request.IsActive, request.AllWalletAccess, request.WalletIds }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.NoContent();
    });
    team.MapPost("/{id}/reset-password", async (string id, ResetPasswordRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var actor = await RequireOrganizationUser(principal, users);
        if (!CanManageTeam(principal, actor)) return Results.Forbid();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == actor.OrganizationId);
        if (user is null) return Results.NotFound();
        if (user.Id == actor.Id) return Results.BadRequest(new { error = "Use Account settings to change your own password." });
        if (!AccessControl.CanManageRole(await GetRole(users, actor), await GetRole(users, user))) return Results.Forbid();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded) return Results.BadRequest(new { error = string.Join("; ", result.Errors.Select(x => x.Description)) });
        await users.UpdateSecurityStampAsync(user);
        db.AuditEvents.Add(Audit(actor.OrganizationId, actor.Id, "TeamPasswordReset", nameof(AppUser), user.Id)); await db.SaveChangesAsync();
        return Results.NoContent();
    });
}

static void MapWallets(WebApplication app)
{
    var wallets = app.MapGroup("/api/wallets").RequireAuthorization();
    wallets.MapGet("/", async (bool? includeInactive, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var query = db.Wallets.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        if (includeInactive != true || !IsOrganizationAdmin(principal)) query = query.Where(x => x.IsActive);
        if (!IsOrganizationAdmin(principal) && !user.AllWalletAccess) query = query.Where(x => db.UserWalletAccess.Any(a => a.UserId == user.Id && a.WalletId == x.Id));
        if (!CanViewBalances(principal))
        {
            return Results.Ok(await query.OrderBy(x => x.Name).Select(x => new
            {
                x.Id, x.Name, x.Provider, x.AccountNumber, x.CurrencyCode, x.DeviceId, x.IsActive, x.CreatedAtUtc
            }).ToListAsync());
        }
        return Results.Ok(await query.OrderBy(x => x.Name).Select(x => new
        {
            x.Id, x.Name, x.Provider, x.AccountNumber, x.CurrencyCode, x.DeviceId, x.IsActive, x.OpeningBalance, x.BalanceLimit, x.CreatedAtUtc,
            CurrentBalance = x.OpeningBalance + db.WalletReceipts.Where(r => r.WalletId == x.Id && r.Status == ReceiptStatus.Confirmed).Sum(r => (decimal?)r.Amount)!.GetValueOrDefault()
                + db.WalletLedgerEntries.Where(entry => entry.WalletId == x.Id).Sum(entry => (decimal?)entry.Amount)!.GetValueOrDefault()
        }).ToListAsync());
    });
    wallets.MapPost("/", async (WalletRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!IsOrganizationAdmin(principal)) return Results.Forbid();
        var currency = NormalizeCurrency(request.CurrencyCode);
        var provider = request.Provider.Trim();
        ValidateProviderCurrency(provider, currency);
        var normalizedAccount = NormalizeAccount(request.AccountNumber);
        if (await db.Wallets.AnyAsync(x => x.OrganizationId == user.OrganizationId && x.Provider == provider && x.NormalizedAccountNumber == normalizedAccount))
            return Results.BadRequest(new { error = "This account number already exists for the selected provider." });
        if (request.DeviceId.HasValue && !await db.WalletDevices.AnyAsync(x => x.Id == request.DeviceId && x.OrganizationId == user.OrganizationId && x.IsActive)) return Results.BadRequest(new { error = "The selected device is not active in this organization." });
        var wallet = new Wallet { OrganizationId = user.OrganizationId!.Value, Name = request.Name.Trim(), Provider = provider, AccountNumber = request.AccountNumber.Trim(), NormalizedAccountNumber = normalizedAccount, CurrencyCode = currency, DeviceId = request.DeviceId, OpeningBalance = request.OpeningBalance, BalanceLimit = request.BalanceLimit > 0 ? request.BalanceLimit : null };
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
        var currency = NormalizeCurrency(request.CurrencyCode); var provider = request.Provider.Trim(); ValidateProviderCurrency(provider, currency);
        var normalizedAccount = NormalizeAccount(request.AccountNumber);
        if (await db.Wallets.AnyAsync(x => x.Id != id && x.OrganizationId == user.OrganizationId && x.Provider == provider && x.NormalizedAccountNumber == normalizedAccount))
            return Results.BadRequest(new { error = "This account number already exists for the selected provider." });
        if (request.DeviceId.HasValue && !await db.WalletDevices.AnyAsync(x => x.Id == request.DeviceId && x.OrganizationId == user.OrganizationId && x.IsActive)) return Results.BadRequest(new { error = "The selected device is not active in this organization." });
        wallet.Name = request.Name.Trim(); wallet.Provider = provider; wallet.AccountNumber = request.AccountNumber.Trim();
        wallet.NormalizedAccountNumber = normalizedAccount; wallet.CurrencyCode = currency; wallet.DeviceId = request.DeviceId; wallet.IsActive = request.IsActive;
        wallet.OpeningBalance = request.OpeningBalance; wallet.BalanceLimit = request.BalanceLimit > 0 ? request.BalanceLimit : null;
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
        var preferences = await db.NotificationPreferences.Where(x => x.WalletId == id).ToListAsync();
        foreach (var preference in preferences) preference.WalletId = null;
        var hasHistory = await db.WalletReceipts.AnyAsync(x => x.WalletId == id);
        if (hasHistory)
        {
            wallet.IsActive = false;
            wallet.DeviceId = null;
            var access = await db.UserWalletAccess.Where(x => x.WalletId == id).ToListAsync();
            db.UserWalletAccess.RemoveRange(access);
        }
        else db.Wallets.Remove(wallet);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, hasHistory ? "WalletArchived" : "WalletDeleted", nameof(Wallet), wallet.Id.ToString()));
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
        return Results.Ok(await db.WalletDevices.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId).OrderByDescending(x => x.IsActive).ThenByDescending(x => x.LastSeenAtUtc)
            .Select(x => new { x.Id, x.Name, x.Platform, x.IsActive, x.PairedAtUtc, x.LastSeenAtUtc, x.LastHeartbeatAtUtc, x.LastSmsAtUtc, x.LastAxisNotificationAtUtc, x.LastCaptureAtUtc, x.AppVersion, x.AndroidVersion, x.PendingUploadCount, x.FailedUploadCount, x.SmsPermissionGranted, x.AxisNotificationAccessGranted, x.BatteryOptimizationIgnored, WalletCount = db.Wallets.Count(w => w.DeviceId == x.Id && w.IsActive) }).ToListAsync());
    }).RequireAuthorization();
    devices.MapPost("/pairing", async (DevicePairingRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var code = await UniquePairingCode(db);
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
    }).RequireRateLimiting("pairing");
    devices.MapPost("/heartbeat", async (DeviceHeartbeatRequest request, HttpContext http, WalletsDbContext db) =>
    {
        var device = await DeviceFromToken(http, db);
        if (device is null) return Results.Unauthorized();
        var now = DateTime.UtcNow;
        device.LastSeenAtUtc = now; device.LastHeartbeatAtUtc = now;
        device.AppVersion = Clean(request.AppVersion, 40); device.AndroidVersion = Clean(request.AndroidVersion, 40);
        device.PendingUploadCount = Math.Clamp(request.PendingUploadCount, 0, 100000);
        device.FailedUploadCount = Math.Clamp(request.FailedUploadCount, 0, 100000);
        device.SmsPermissionGranted = request.SmsPermissionGranted;
        device.AxisNotificationAccessGranted = request.AxisNotificationAccessGranted;
        device.BatteryOptimizationIgnored = request.BatteryOptimizationIgnored;
        if (request.LastSmsAtUtc.HasValue) device.LastSmsAtUtc = request.LastSmsAtUtc.Value.ToUniversalTime();
        if (request.LastAxisNotificationAtUtc.HasValue) device.LastAxisNotificationAtUtc = request.LastAxisNotificationAtUtc.Value.ToUniversalTime();
        if (device.OfflineAlertSentAtUtc.HasValue) device.OfflineAlertSentAtUtc = null;
        await db.SaveChangesAsync(); return Results.NoContent();
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
    devices.MapPut("/{id:guid}", async (Guid id, UpdateDeviceRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var device = await db.WalletDevices.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId);
        if (device is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "Device name is required." });
        device.Name = request.Name.Trim(); device.IsActive = request.IsActive;
        if (!request.IsActive) { device.TokenHash = null; device.InstallationId = null; }
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "DeviceUpdated", nameof(WalletDevice), id.ToString(), new { device.Name, device.IsActive }));
        await db.SaveChangesAsync(); return Results.NoContent();
    }).RequireAuthorization();
    devices.MapPost("/{id:guid}/pairing", async (Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var device = await db.WalletDevices.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId);
        if (device is null) return Results.NotFound();
        var code = await UniquePairingCode(db);
        device.IsActive = true; device.TokenHash = null; device.InstallationId = null; device.PairingCodeHash = Hash(code); device.PairingCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "DeviceRepairingCreated", nameof(WalletDevice), id.ToString()));
        await db.SaveChangesAsync(); return Results.Ok(new { device.Id, PairingCode = code, ExpiresAtUtc = device.PairingCodeExpiresAtUtc });
    }).RequireAuthorization();
    devices.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var device = await db.WalletDevices.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId);
        if (device is null) return Results.NotFound();
        var attachedWallets = await db.Wallets.Where(x => x.DeviceId == id).ToListAsync();
        foreach (var wallet in attachedWallets) wallet.DeviceId = null;
        var hasHistory = await db.WalletReceipts.AnyAsync(x => x.DeviceId == id);
        if (hasHistory)
        {
            device.IsActive = false;
            device.InstallationId = null;
            device.TokenHash = null;
            device.PairingCodeHash = null;
            device.PairingCodeExpiresAtUtc = null;
        }
        else db.WalletDevices.Remove(device);
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, hasHistory ? "DeviceArchived" : "DeviceDeleted", nameof(WalletDevice), device.Id.ToString()));
        await db.SaveChangesAsync();
        return Results.NoContent();
    }).RequireAuthorization();
}

static void MapReceipts(WebApplication app)
{
    app.MapPost("/api/captures", async (CaptureRequest request, HttpContext http, WalletsDbContext db, IDataProtectionProvider protection) =>
    {
        var device = await DeviceFromToken(http, db);
        if (device is null) return Results.Unauthorized();
        var now = DateTime.UtcNow;
        device.LastSeenAtUtc = now; device.LastCaptureAtUtc = now;
        var existingEvent = await db.CaptureEvents.SingleOrDefaultAsync(x => x.DeviceId == device.Id && x.Fingerprint == request.Fingerprint);
        if (existingEvent is not null)
        {
            existingEvent.AttemptCount++; existingEvent.LastSeenAtUtc = now;
            await db.SaveChangesAsync(); return Results.Ok(new { duplicate = true, captureEventId = existingEvent.Id, status = existingEvent.Status, reason = existingEvent.Reason });
        }
        var raw = string.Join("\n", new[] { request.Title, request.Body }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        var protector = protection.CreateProtector("WalletsHub.Receipt.v1");
        var receivedAt = request.ReceivedAtUtc.Kind == DateTimeKind.Utc ? request.ReceivedAtUtc : request.ReceivedAtUtc.ToUniversalTime();
        var capture = new CaptureEvent
        {
            OrganizationId = device.OrganizationId, DeviceId = device.Id, Fingerprint = request.Fingerprint,
            Status = "Processing", Reason = "received", SourcePackage = request.SourcePackage ?? "unknown",
            ProtectedMessage = protector.Protect(raw), ReceivedAtUtc = receivedAt, FirstSeenAtUtc = now, LastSeenAtUtc = now
        };
        db.CaptureEvents.Add(capture);
        var fingerprintReceipt = await db.WalletReceipts.FirstOrDefaultAsync(x => x.DeviceId == device.Id && x.Fingerprint == request.Fingerprint);
        if (fingerprintReceipt is not null)
        {
            capture.Status = "Duplicate"; capture.Reason = "device-fingerprint-exists"; capture.ReceiptId = fingerprintReceipt.Id; capture.WalletId = fingerprintReceipt.WalletId;
            await db.SaveChangesAsync(); return Results.Ok(new { duplicate = true, captureEventId = capture.Id });
        }
        if (!WalletMessageParser.TryParse(request.SourcePackage, raw, out var parsed))
        {
            capture.Status = "Unmatched"; capture.Reason = "unsupported-or-not-incoming";
            await QueueCaptureIssueNotifications(db, device, capture);
            await db.SaveChangesAsync(); return Results.Accepted(value: new { ignored = true, reason = capture.Reason, captureEventId = capture.Id });
        }
        capture.Provider = parsed.Provider; capture.Amount = parsed.Amount; capture.CurrencyCode = parsed.CurrencyCode;
        capture.Sender = parsed.Sender; capture.Destination = parsed.Destination; capture.ProviderReference = parsed.Reference;
        if (!string.IsNullOrWhiteSpace(parsed.Reference))
        {
            var duplicateReceipt = await db.WalletReceipts.FirstOrDefaultAsync(x => x.OrganizationId == device.OrganizationId && x.Provider == parsed.Provider && x.ProviderReference == parsed.Reference);
            if (duplicateReceipt is not null)
            {
                capture.Status = "Duplicate"; capture.Reason = "provider-reference-exists"; capture.ReceiptId = duplicateReceipt.Id; capture.WalletId = duplicateReceipt.WalletId;
                await db.SaveChangesAsync(); return Results.Ok(new { duplicate = true, captureEventId = capture.Id });
            }
        }
        var walletQuery = db.Wallets.Where(x => x.OrganizationId == device.OrganizationId && x.DeviceId == device.Id && x.IsActive);
        Wallet? wallet = null;
        if (request.WalletId.HasValue) wallet = await walletQuery.SingleOrDefaultAsync(x => x.Id == request.WalletId);
        if (wallet is null && !string.IsNullOrWhiteSpace(parsed.Destination))
        {
            var normalizedDestination = NormalizeAccount(parsed.Destination);
            var exactCandidates = await walletQuery.Where(x => x.Provider == parsed.Provider && x.CurrencyCode == parsed.CurrencyCode && x.NormalizedAccountNumber == normalizedDestination).Take(2).ToListAsync();
            if (exactCandidates.Count == 1) wallet = exactCandidates[0];
            else if (exactCandidates.Count > 1) capture.Reason = "ambiguous-destination";
        }
        if (wallet is null)
        {
            var providerCandidates = await walletQuery.Where(x => x.Provider == parsed.Provider && x.CurrencyCode == parsed.CurrencyCode).Take(2).ToListAsync();
            if (providerCandidates.Count == 1) wallet = providerCandidates[0];
            else if (providerCandidates.Count > 1) capture.Reason = "ambiguous-provider-wallet";
        }
        if (wallet is null)
        {
            var candidates = await walletQuery.Take(2).ToListAsync();
            if (candidates.Count == 1) wallet = candidates[0];
            else if (candidates.Count > 1 && capture.Reason == "received") capture.Reason = "ambiguous-wallet";
        }
        if (wallet is null)
        {
            capture.Status = "Unmatched"; if (capture.Reason == "received") capture.Reason = "wallet-not-resolved";
            await QueueCaptureIssueNotifications(db, device, capture);
            await db.SaveChangesAsync(); return Results.Accepted(value: new { ignored = true, reason = capture.Reason, captureEventId = capture.Id });
        }
        capture.WalletId = wallet.Id;
        if (receivedAt < DateTime.UtcNow.AddDays(-30) || receivedAt > DateTime.UtcNow.AddMinutes(10))
        {
            capture.Status = "Rejected"; capture.Reason = "outside-30-day-capture-window";
            await QueueCaptureIssueNotifications(db, device, capture);
            await db.SaveChangesAsync(); return Results.Accepted(value: new { ignored = true, reason = capture.Reason, captureEventId = capture.Id });
        }
        var requireConfirmation = await db.Organizations.Where(x => x.Id == device.OrganizationId).Select(x => x.RequireReceiptConfirmation).SingleAsync();
        var receipt = new WalletReceipt
        {
            OrganizationId = device.OrganizationId, WalletId = wallet.Id, DeviceId = device.Id, Provider = parsed.Provider,
            Amount = parsed.Amount, CurrencyCode = parsed.CurrencyCode, Sender = parsed.Sender, ProviderReference = parsed.Reference,
            Fingerprint = request.Fingerprint, ProtectedMessage = capture.ProtectedMessage,
            SourcePackage = request.SourcePackage ?? "unknown", Status = requireConfirmation ? ReceiptStatus.Pending : ReceiptStatus.Confirmed, ReceivedAtUtc = receivedAt
        };
        db.WalletReceipts.Add(receipt);
        capture.Status = "Accepted"; capture.Reason = "receipt-created"; capture.ReceiptId = receipt.Id;
        var currentWalletBalance = await WalletBalance(db, wallet);
        var recentAmounts = await db.WalletReceipts.Where(x => x.WalletId == wallet.Id && x.Status == ReceiptStatus.Confirmed && x.ReceivedAtUtc >= DateTime.UtcNow.AddDays(-30)).Select(x => x.Amount).ToListAsync();
        var unusual = recentAmounts.Count >= 10 && parsed.Amount > recentAmounts.Average() * 5;
        var exceedsLimit = wallet.BalanceLimit.HasValue && currentWalletBalance + parsed.Amount > wallet.BalanceLimit.Value;
        if (unusual || exceedsLimit)
        {
            var managers = await db.Users.Where(x => x.OrganizationId == device.OrganizationId && x.IsActive && (x.CanManageDevices || x.CanViewReports)).Select(x => x.Id).ToListAsync();
            foreach (var managerId in managers)
                db.UserNotifications.Add(new UserNotification { OrganizationId = device.OrganizationId, UserId = managerId, Title = exceedsLimit ? $"{wallet.Name} exceeded its limit" : "Unusually large payment", Body = $"{parsed.Amount:N2} {parsed.CurrencyCode} was received in {wallet.Name}. Review the wallet balance and receipt.", Link = "/receipts", SourceId = receipt.Id });
        }
        var recipientRoles = await (from candidate in db.Users
            join userRole in db.UserRoles on candidate.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where candidate.OrganizationId == device.OrganizationId && candidate.IsActive
                && (role.Name == Roles.Owner || role.Name == Roles.Admin || candidate.AllWalletAccess || db.UserWalletAccess.Any(access => access.UserId == candidate.Id && access.WalletId == wallet.Id))
            select new { candidate.Id, Role = role.Name! }).ToListAsync();
        var recipientIds = recipientRoles.Select(x => x.Id).ToList();
        var preferences = await db.NotificationPreferences.Where(x => recipientIds.Contains(x.UserId) && (x.WalletId == null || x.WalletId == wallet.Id)).ToListAsync();
        foreach (var recipient in recipientRoles.DistinctBy(x => x.Id))
        {
            var preference = preferences.FirstOrDefault(x => x.UserId == recipient.Id && x.WalletId == wallet.Id) ?? preferences.FirstOrDefault(x => x.UserId == recipient.Id && x.WalletId == null);
            var enabled = preference?.EveryReceipt ?? recipient.Role is Roles.Owner or Roles.Admin;
            var meetsThreshold = preference?.MinimumAmount is null || parsed.Amount >= preference.MinimumAmount.Value;
            if (enabled && meetsThreshold)
                db.UserNotifications.Add(new UserNotification { OrganizationId = device.OrganizationId, UserId = recipient.Id, Title = requireConfirmation ? $"{parsed.Amount:N2} EGP awaiting confirmation" : $"{parsed.Amount:N2} EGP received", Body = $"{parsed.Provider} payment detected for {wallet.Name}.", Link = "/receipts", SourceId = receipt.Id });
        }
        db.AuditEvents.Add(Audit(device.OrganizationId, null, "ReceiptDetected", nameof(WalletReceipt), receipt.Id.ToString(), new { wallet.Id, parsed.Provider, parsed.Amount, parsed.CurrencyCode, CaptureEventId = capture.Id }));
        await db.SaveChangesAsync();
        return Results.Created($"/api/receipts/{receipt.Id}", new { receipt.Id, receipt.Amount, receipt.CurrencyCode, receipt.Provider, captureEventId = capture.Id });
    });

    var receipts = app.MapGroup("/api/receipts").RequireAuthorization();
    receipts.MapGet("/", async ([AsParameters] ReceiptSearchRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db, IDataProtectionProvider protection) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var start = request.From?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-user.VisibleReceiptDays);
        var end = request.To?.ToUniversalTime() ?? DateTime.UtcNow;
        var query = ScopedReceipts(principal, user, db).Where(x => x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end);
        var walletIds = ParseGuids(request.WalletIds);
        if (request.WalletId.HasValue) walletIds.Add(request.WalletId.Value);
        if (walletIds.Count > 0) query = query.Where(x => walletIds.Contains(x.WalletId));
        if (!string.IsNullOrWhiteSpace(request.Provider)) query = query.Where(x => x.Provider == request.Provider);
        if (!string.IsNullOrWhiteSpace(request.Currency)) query = query.Where(x => x.CurrencyCode == request.Currency.ToUpper());
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ReceiptStatus>(request.Status, true, out var receiptStatus)) query = query.Where(x => x.Status == receiptStatus);
        if (request.DeviceId.HasValue) query = query.Where(x => x.DeviceId == request.DeviceId);
        if (request.MinAmount.HasValue) query = query.Where(x => x.Amount >= request.MinAmount);
        if (request.MaxAmount.HasValue) query = query.Where(x => x.Amount <= request.MaxAmount);
        if (request.MissingSender == true) query = query.Where(x => x.Sender == null || x.Sender == "");
        if (request.MissingReference == true) query = query.Where(x => x.ProviderReference == null || x.ProviderReference == "");
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            if (string.Equals(request.SearchMode, "exact", StringComparison.OrdinalIgnoreCase))
            {
                var isAmount = decimal.TryParse(search, NumberStyles.Number, CultureInfo.InvariantCulture, out var exactAmount);
                query = query.Where(x => x.Sender == search || x.ProviderReference == search || isAmount && x.Amount == exactAmount);
            }
            else
            {
                var pattern = $"%{EscapeLike(search)}%";
                query = query.Where(x => EF.Functions.ILike(x.Sender ?? "", pattern) || EF.Functions.ILike(x.ProviderReference ?? "", pattern) || EF.Functions.ILike(x.Provider, pattern));
            }
        }
        var total = await query.CountAsync();
        query = request.Sort?.ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(x => x.ReceivedAtUtc),
            "amount-high" => query.OrderByDescending(x => x.Amount).ThenByDescending(x => x.ReceivedAtUtc),
            "amount-low" => query.OrderBy(x => x.Amount).ThenByDescending(x => x.ReceivedAtUtc),
            _ => query.OrderByDescending(x => x.ReceivedAtUtc)
        };
        var page = Math.Max(request.Page ?? 1, 1); var pageSize = Math.Clamp(request.PageSize ?? 30, 10, 200);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Join(db.Wallets, r => r.WalletId, w => w.Id, (r, w) => new { Receipt = r, WalletName = w.Name })
            .Join(db.WalletDevices, row => row.Receipt.DeviceId, d => d.Id, (row, d) => new { row.Receipt, row.WalletName, DeviceName = d.Name }).ToListAsync();
        var protector = protection.CreateProtector("WalletsHub.Receipt.v1");
        var maskMessages = await db.Organizations.Where(x => x.Id == user.OrganizationId).Select(x => x.MaskSensitiveMessages).SingleAsync();
        var items = rows.Select(x => new { x.Receipt.Id, x.Receipt.WalletId, x.WalletName, x.Receipt.DeviceId, x.DeviceName, x.Receipt.Provider, x.Receipt.Amount, x.Receipt.CurrencyCode, x.Receipt.Sender, x.Receipt.ProviderReference, Status = x.Receipt.Status.ToString(), x.Receipt.ReviewedByUserId, x.Receipt.ReviewedAtUtc, Message = maskMessages ? MaskSensitive(Unprotect(protector, x.Receipt.ProtectedMessage)) : Unprotect(protector, x.Receipt.ProtectedMessage), x.Receipt.ReceivedAtUtc });
        return Results.Ok(new { Items = items, Total = total, Page = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(total / (double)pageSize) });
    });

    receipts.MapPost("/{id:guid}/confirm", async (Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!IsOrganizationAdmin(principal) && !user.CanConfirmReceipts) return Results.Forbid();
        var receipt = await ScopedReceipts(principal, user, db).SingleOrDefaultAsync(x => x.Id == id);
        if (receipt is null) return Results.NotFound();
        if (receipt.Status == ReceiptStatus.Confirmed) return Results.NoContent();
        receipt.Status = ReceiptStatus.Confirmed;
        receipt.ReviewedByUserId = user.Id;
        receipt.ReviewedAtUtc = DateTime.UtcNow;
        receipt.ReviewNote = "Confirmed by user.";
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "ReceiptConfirmed", nameof(WalletReceipt), receipt.Id.ToString(), new { receipt.WalletId, receipt.Amount, receipt.CurrencyCode }));
        await db.SaveChangesAsync();
        return Results.NoContent();
    });

    app.MapGet("/api/capture-events", async ([AsParameters] CaptureEventSearchRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db, IDataProtectionProvider protection) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var query = db.CaptureEvents.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(x => x.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Reason)) query = query.Where(x => x.Reason == request.Reason);
        if (request.DeviceId.HasValue) query = query.Where(x => x.DeviceId == request.DeviceId);
        if (request.From.HasValue) query = query.Where(x => x.ReceivedAtUtc >= request.From.Value.ToUniversalTime());
        if (request.To.HasValue) query = query.Where(x => x.ReceivedAtUtc <= request.To.Value.ToUniversalTime());
        var total = await query.CountAsync(); var page = Math.Max(request.Page ?? 1, 1); var pageSize = Math.Clamp(request.PageSize ?? 40, 10, 200);
        var rows = await query.OrderByDescending(x => x.LastSeenAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Join(db.WalletDevices, x => x.DeviceId, d => d.Id, (x, d) => new { Event = x, DeviceName = d.Name })
            .GroupJoin(db.Wallets, x => x.Event.WalletId, w => w.Id, (x, wallets) => new { x.Event, x.DeviceName, Wallets = wallets })
            .SelectMany(x => x.Wallets.DefaultIfEmpty(), (x, wallet) => new { x.Event, x.DeviceName, WalletName = wallet == null ? null : wallet.Name }).ToListAsync();
        var protector = protection.CreateProtector("WalletsHub.Receipt.v1");
        var maskMessages = await db.Organizations.Where(x => x.Id == user.OrganizationId).Select(x => x.MaskSensitiveMessages).SingleAsync();
        var items = rows.Select(x => new { x.Event.Id, x.Event.Status, x.Event.Reason, x.Event.DeviceId, x.DeviceName, x.Event.WalletId, x.WalletName, x.Event.ReceiptId, x.Event.Provider, x.Event.Amount, x.Event.CurrencyCode, x.Event.Sender, x.Event.Destination, x.Event.ProviderReference, x.Event.SourcePackage, Message = maskMessages ? MaskSensitive(Unprotect(protector, x.Event.ProtectedMessage)) : Unprotect(protector, x.Event.ProtectedMessage), x.Event.ReceivedAtUtc, x.Event.LastSeenAtUtc, x.Event.AttemptCount });
        return Results.Ok(new { Items = items, Total = total, Page = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }).RequireAuthorization();
    app.MapPost("/api/capture-events/{id:guid}/resolve", async (Guid id, ResolveCaptureRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var capture = await db.CaptureEvents.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId);
        if (capture is null) return Results.NotFound();
        if (capture.ReceiptId.HasValue) return Results.Conflict(new { error = "This capture already has a receipt." });
        if (!capture.Amount.HasValue || string.IsNullOrWhiteSpace(capture.Provider) || string.IsNullOrWhiteSpace(capture.CurrencyCode)) return Results.BadRequest(new { error = "This message did not contain enough payment data to resolve manually." });
        var wallet = await db.Wallets.SingleOrDefaultAsync(x => x.Id == request.WalletId && x.OrganizationId == user.OrganizationId && x.IsActive);
        if (wallet is null || wallet.Provider != capture.Provider || wallet.CurrencyCode != capture.CurrencyCode) return Results.BadRequest(new { error = "Choose an active wallet with the same provider and currency." });
        var requireConfirmation = await db.Organizations.Where(x => x.Id == user.OrganizationId).Select(x => x.RequireReceiptConfirmation).SingleAsync();
        var receipt = new WalletReceipt { OrganizationId = user.OrganizationId!.Value, WalletId = wallet.Id, DeviceId = capture.DeviceId, Provider = capture.Provider, Amount = capture.Amount.Value, CurrencyCode = capture.CurrencyCode, Sender = capture.Sender, ProviderReference = capture.ProviderReference, Fingerprint = capture.Fingerprint, ProtectedMessage = capture.ProtectedMessage, SourcePackage = capture.SourcePackage, Status = requireConfirmation ? ReceiptStatus.Pending : ReceiptStatus.Confirmed, ReceivedAtUtc = capture.ReceivedAtUtc };
        db.WalletReceipts.Add(receipt); capture.WalletId = wallet.Id; capture.ReceiptId = receipt.Id; capture.Status = "Accepted"; capture.Reason = "manually-resolved";
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "CaptureManuallyResolved", nameof(CaptureEvent), capture.Id.ToString(), new { wallet.Id, ReceiptId = receipt.Id })); await db.SaveChangesAsync();
        return Results.Created($"/api/receipts/{receipt.Id}", new { receipt.Id });
    }).RequireAuthorization();
    app.MapPost("/api/capture-events/{id:guid}/retry", async (Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db, IDataProtectionProvider protection) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanManageDevices && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var capture = await db.CaptureEvents.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId);
        if (capture is null) return Results.NotFound();
        if (capture.ReceiptId.HasValue) return Results.Conflict(new { error = "This capture already has a receipt." });
        var raw = Unprotect(protection.CreateProtector("WalletsHub.Receipt.v1"), capture.ProtectedMessage);
        if (!WalletMessageParser.TryParse(capture.SourcePackage, raw, out var parsed))
        {
            capture.Status = "Unmatched"; capture.Reason = "still-unsupported"; capture.LastSeenAtUtc = DateTime.UtcNow; capture.AttemptCount++;
            await db.SaveChangesAsync(); return Results.Accepted(value: new { matched = false, reason = capture.Reason });
        }
        capture.Provider = parsed.Provider; capture.Amount = parsed.Amount; capture.CurrencyCode = parsed.CurrencyCode; capture.Sender = parsed.Sender; capture.Destination = parsed.Destination; capture.ProviderReference = parsed.Reference; capture.AttemptCount++; capture.LastSeenAtUtc = DateTime.UtcNow;
        var walletQuery = db.Wallets.Where(x => x.OrganizationId == capture.OrganizationId && x.DeviceId == capture.DeviceId && x.IsActive && x.Provider == parsed.Provider && x.CurrencyCode == parsed.CurrencyCode);
        if (!string.IsNullOrWhiteSpace(parsed.Destination)) { var destination = NormalizeAccount(parsed.Destination); walletQuery = walletQuery.Where(x => x.NormalizedAccountNumber == destination); }
        var candidates = await walletQuery.Take(2).ToListAsync();
        if (candidates.Count != 1)
        {
            capture.Status = "Unmatched"; capture.Reason = candidates.Count == 0 ? "wallet-not-resolved" : "ambiguous-wallet";
            await db.SaveChangesAsync(); return Results.Accepted(value: new { matched = false, reason = capture.Reason });
        }
        var requireConfirmation = await db.Organizations.Where(x => x.Id == capture.OrganizationId).Select(x => x.RequireReceiptConfirmation).SingleAsync();
        var wallet = candidates[0]; var receipt = new WalletReceipt { OrganizationId = capture.OrganizationId, WalletId = wallet.Id, DeviceId = capture.DeviceId, Provider = parsed.Provider, Amount = parsed.Amount, CurrencyCode = parsed.CurrencyCode, Sender = parsed.Sender, ProviderReference = parsed.Reference, Fingerprint = capture.Fingerprint, ProtectedMessage = capture.ProtectedMessage, SourcePackage = capture.SourcePackage, Status = requireConfirmation ? ReceiptStatus.Pending : ReceiptStatus.Confirmed, ReceivedAtUtc = capture.ReceivedAtUtc };
        db.WalletReceipts.Add(receipt); capture.WalletId = wallet.Id; capture.ReceiptId = receipt.Id; capture.Status = "Accepted"; capture.Reason = "reprocessed";
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "CaptureReprocessed", nameof(CaptureEvent), capture.Id.ToString(), new { wallet.Id, ReceiptId = receipt.Id })); await db.SaveChangesAsync();
        return Results.Created($"/api/receipts/{receipt.Id}", new { receipt.Id });
    }).RequireAuthorization();
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
        return Results.Ok(preference is null ? new NotificationPreferenceResponse(true, null, true, true) : new NotificationPreferenceResponse(preference.EveryReceipt, preference.MinimumAmount, preference.DailySummary, preference.DeviceOffline));
    });
    settings.MapPut("/notifications", async (NotificationPreferenceRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var preference = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == user.Id && x.WalletId == null);
        if (preference is null) { preference = new NotificationPreference { OrganizationId = user.OrganizationId!.Value, UserId = user.Id }; db.NotificationPreferences.Add(preference); }
        preference.EveryReceipt = request.EveryReceipt; preference.MinimumAmount = request.MinimumAmount > 0 ? request.MinimumAmount : null;
        preference.DailySummary = request.DailySummary; preference.DeviceOffline = request.DeviceOffline; preference.RejectedReceipt = false;
        await db.SaveChangesAsync(); return Results.NoContent();
    });
    settings.MapGet("/workspace", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var organization = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == user.OrganizationId);
        return Results.Ok(new { organization.TimeZoneId, organization.MaskSensitiveMessages, organization.RequireReceiptConfirmation });
    });
    settings.MapPut("/workspace", async (WorkspaceSettingsRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!IsOrganizationAdmin(principal)) return Results.Forbid();
        try { _ = ResolveTimeZone(request.TimeZoneId); } catch { return Results.BadRequest(new { error = "That time zone is not supported." }); }
        var organization = await db.Organizations.SingleAsync(x => x.Id == user.OrganizationId);
        organization.TimeZoneId = request.TimeZoneId; organization.MaskSensitiveMessages = request.MaskSensitiveMessages;
        var wasRequired = organization.RequireReceiptConfirmation;
        organization.RequireReceiptConfirmation = request.RequireReceiptConfirmation;
        if (wasRequired && !request.RequireReceiptConfirmation)
        {
            var pending = await db.WalletReceipts.Where(x => x.OrganizationId == user.OrganizationId && x.Status == ReceiptStatus.Pending).ToListAsync();
            foreach (var receipt in pending)
            {
                receipt.Status = ReceiptStatus.Confirmed;
                receipt.ReviewedByUserId = user.Id;
                receipt.ReviewedAtUtc = DateTime.UtcNow;
                receipt.ReviewNote = "Automatically confirmed when confirmation mode was disabled.";
            }
        }
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "WorkspaceSettingsUpdated", nameof(Organization), organization.Id.ToString(), request));
        await db.SaveChangesAsync(); return Results.NoContent();
    });
}

static void MapAudit(WebApplication app)
{
    app.MapGet("/api/audit", async ([AsParameters] AuditSearchRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!IsOrganizationAdmin(principal)) return Results.Forbid();
        var query = db.AuditEvents.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(x => x.Action == request.Action);
        if (!string.IsNullOrWhiteSpace(request.UserId)) query = query.Where(x => x.UserId == request.UserId);
        if (request.From.HasValue) query = query.Where(x => x.CreatedAtUtc >= request.From.Value.ToUniversalTime());
        if (request.To.HasValue) query = query.Where(x => x.CreatedAtUtc <= request.To.Value.ToUniversalTime());
        var total = await query.CountAsync(); var page = Math.Max(request.Page ?? 1, 1); var pageSize = Math.Clamp(request.PageSize ?? 50, 10, 200);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .GroupJoin(db.Users, audit => audit.UserId, actor => actor.Id, (audit, actors) => new { Audit = audit, Actors = actors })
            .SelectMany(x => x.Actors.DefaultIfEmpty(), (x, actor) => new { x.Audit.Id, x.Audit.Action, x.Audit.EntityType, x.Audit.EntityId, x.Audit.DetailJson, x.Audit.CreatedAtUtc, UserId = x.Audit.UserId, ActorName = actor == null ? "System / device" : actor.DisplayName }).ToListAsync();
        return Results.Ok(new { Items = items, Total = total, Page = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }).RequireAuthorization();
}

static void MapOperations(WebApplication app)
{
    var operations = app.MapGroup("/api/wallet-operations").RequireAuthorization();
    operations.MapGet("/", async (Guid? walletId, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!CanViewBalances(principal)) return Results.Forbid();
        var wallets = db.Wallets.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        if (!IsOrganizationAdmin(principal) && !user.AllWalletAccess) wallets = wallets.Where(x => db.UserWalletAccess.Any(a => a.UserId == user.Id && a.WalletId == x.Id));
        if (walletId.HasValue) wallets = wallets.Where(x => x.Id == walletId);
        var balances = await wallets.OrderBy(x => x.Name).Select(x => new
        {
            x.Id, x.Name, x.Provider, x.CurrencyCode, x.OpeningBalance, x.BalanceLimit, x.IsActive,
            Received = db.WalletReceipts.Where(r => r.WalletId == x.Id && r.Status == ReceiptStatus.Confirmed).Sum(r => (decimal?)r.Amount) ?? 0,
            Adjustments = db.WalletLedgerEntries.Where(e => e.WalletId == x.Id).Sum(e => (decimal?)e.Amount) ?? 0,
            LastReconciledAtUtc = db.WalletReconciliations.Where(r => r.WalletId == x.Id).Max(r => (DateTime?)r.CreatedAtUtc)
        }).ToListAsync();
        var walletIds = balances.Select(x => x.Id).ToList();
        var recent = await db.WalletLedgerEntries.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId && walletIds.Contains(x.WalletId)).OrderByDescending(x => x.OccurredAtUtc).Take(100)
            .Join(db.Wallets, x => x.WalletId, w => w.Id, (x, w) => new { x.Id, x.WalletId, WalletName = w.Name, x.RelatedWalletId, x.CorrelationId, x.Type, x.Amount, x.Note, x.CreatedByUserId, x.OccurredAtUtc }).ToListAsync();
        return Results.Ok(new { Balances = balances.Select(x => new { x.Id, x.Name, x.Provider, x.CurrencyCode, x.OpeningBalance, x.BalanceLimit, x.IsActive, x.Received, x.Adjustments, CurrentBalance = x.OpeningBalance + x.Received + x.Adjustments, x.LastReconciledAtUtc }), Recent = recent });
    });
    operations.MapGet("/statement", async ([AsParameters] StatementRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!CanViewBalances(principal)) return Results.Forbid();
        var walletQuery = db.Wallets.AsNoTracking().Where(x => x.Id == request.WalletId && x.OrganizationId == user.OrganizationId);
        if (!IsOrganizationAdmin(principal) && !user.AllWalletAccess) walletQuery = walletQuery.Where(x => db.UserWalletAccess.Any(a => a.UserId == user.Id && a.WalletId == x.Id));
        var wallet = await walletQuery.SingleOrDefaultAsync(); if (wallet is null) return Results.NotFound();
        var start = request.From?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-30); var end = request.To?.ToUniversalTime() ?? DateTime.UtcNow;
        if (end <= start || end - start > TimeSpan.FromDays(366)) return Results.BadRequest(new { error = "Choose a statement period up to 366 days." });
        var receiptRows = await db.WalletReceipts.AsNoTracking().Where(x => x.WalletId == wallet.Id && x.Status == ReceiptStatus.Confirmed && x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end)
            .Select(x => new { x.Id, Type = "Receipt", x.Amount, Note = x.Sender ?? x.ProviderReference, OccurredAtUtc = x.ReceivedAtUtc }).ToListAsync();
        var ledgerRows = await db.WalletLedgerEntries.AsNoTracking().Where(x => x.WalletId == wallet.Id && x.OccurredAtUtc >= start && x.OccurredAtUtc <= end)
            .Select(x => new { x.Id, x.Type, x.Amount, x.Note, x.OccurredAtUtc }).ToListAsync();
        var rows = receiptRows.Concat(ledgerRows).OrderByDescending(x => x.OccurredAtUtc).ToList(); var total = rows.Count; var page = Math.Max(request.Page ?? 1, 1); var pageSize = Math.Clamp(request.PageSize ?? 50, 10, 200);
        var received = await db.WalletReceipts.Where(x => x.WalletId == wallet.Id && x.Status == ReceiptStatus.Confirmed).SumAsync(x => (decimal?)x.Amount) ?? 0; var adjustments = await db.WalletLedgerEntries.Where(x => x.WalletId == wallet.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
        return Results.Ok(new { Wallet = new { wallet.Id, wallet.Name, wallet.Provider, wallet.CurrencyCode }, CurrentBalance = wallet.OpeningBalance + received + adjustments, Items = rows.Skip((page - 1) * pageSize).Take(pageSize), Total = total, Page = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(total / (double)pageSize) });
    });
    operations.MapPost("/entry", async (LedgerEntryRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users); if (!IsOrganizationAdmin(principal)) return Results.Forbid();
        var wallet = await db.Wallets.SingleOrDefaultAsync(x => x.Id == request.WalletId && x.OrganizationId == user.OrganizationId && x.IsActive);
        if (wallet is null) return Results.BadRequest(new { error = "Choose an active wallet." });
        if (request.Amount == 0) return Results.BadRequest(new { error = "Amount cannot be zero." });
        var type = request.Type.Trim();
        var amount = type switch { "Withdrawal" => -Math.Abs(request.Amount), "Deposit" => Math.Abs(request.Amount), "Adjustment" => request.Amount, _ => 0 };
        if (amount == 0) return Results.BadRequest(new { error = "Type must be Withdrawal, Deposit, or Adjustment." });
        var currentBalance = await WalletBalance(db, wallet);
        if (type == "Withdrawal" && currentBalance < Math.Abs(amount)) return Results.BadRequest(new { error = "This withdrawal exceeds the calculated wallet balance." });
        if (type == "Deposit" && wallet.BalanceLimit.HasValue && currentBalance + amount > wallet.BalanceLimit) return Results.BadRequest(new { error = "This deposit would exceed the configured wallet limit." });
        var entry = new WalletLedgerEntry { OrganizationId = user.OrganizationId!.Value, WalletId = wallet.Id, Type = type, Amount = amount, Note = Clean(request.Note, 500), CreatedByUserId = user.Id, OccurredAtUtc = request.OccurredAtUtc?.ToUniversalTime() ?? DateTime.UtcNow };
        db.WalletLedgerEntries.Add(entry); db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "WalletLedgerEntryCreated", nameof(WalletLedgerEntry), entry.Id.ToString(), new { wallet.Id, entry.Type, entry.Amount, entry.Note })); await db.SaveChangesAsync();
        return Results.Created($"/api/wallet-operations/{entry.Id}", new { entry.Id });
    });
    operations.MapPost("/transfer", async (WalletTransferRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users); if (!IsOrganizationAdmin(principal)) return Results.Forbid();
        if (request.FromWalletId == request.ToWalletId || request.Amount <= 0) return Results.BadRequest(new { error = "Choose two different wallets and a positive amount." });
        var wallets = await db.Wallets.Where(x => x.OrganizationId == user.OrganizationId && x.IsActive && (x.Id == request.FromWalletId || x.Id == request.ToWalletId)).ToListAsync();
        if (wallets.Count != 2 || wallets[0].CurrencyCode != wallets[1].CurrencyCode) return Results.BadRequest(new { error = "Both active wallets must exist and use the same currency." });
        var source = wallets.Single(x => x.Id == request.FromWalletId); var destination = wallets.Single(x => x.Id == request.ToWalletId);
        var sourceBalance = await WalletBalance(db, source); var destinationBalance = await WalletBalance(db, destination);
        if (sourceBalance < request.Amount) return Results.BadRequest(new { error = "The transfer exceeds the calculated source-wallet balance." });
        if (destination.BalanceLimit.HasValue && destinationBalance + request.Amount > destination.BalanceLimit) return Results.BadRequest(new { error = "The transfer would exceed the destination wallet limit." });
        var correlation = Guid.NewGuid(); var occurred = request.OccurredAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        db.WalletLedgerEntries.AddRange(
            new WalletLedgerEntry { OrganizationId = user.OrganizationId!.Value, WalletId = request.FromWalletId, RelatedWalletId = request.ToWalletId, CorrelationId = correlation, Type = "TransferOut", Amount = -request.Amount, Note = Clean(request.Note, 500), CreatedByUserId = user.Id, OccurredAtUtc = occurred },
            new WalletLedgerEntry { OrganizationId = user.OrganizationId!.Value, WalletId = request.ToWalletId, RelatedWalletId = request.FromWalletId, CorrelationId = correlation, Type = "TransferIn", Amount = request.Amount, Note = Clean(request.Note, 500), CreatedByUserId = user.Id, OccurredAtUtc = occurred });
        db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "WalletTransferCreated", nameof(WalletLedgerEntry), correlation.ToString(), new { request.FromWalletId, request.ToWalletId, request.Amount, request.Note })); await db.SaveChangesAsync();
        return Results.Created($"/api/wallet-operations/transfers/{correlation}", new { correlationId = correlation });
    });
    operations.MapPost("/reconcile", async (ReconciliationRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users); if (!IsOrganizationAdmin(principal)) return Results.Forbid();
        var wallet = await db.Wallets.SingleOrDefaultAsync(x => x.Id == request.WalletId && x.OrganizationId == user.OrganizationId);
        if (wallet is null) return Results.NotFound();
        var received = await db.WalletReceipts.Where(x => x.WalletId == wallet.Id && x.Status == ReceiptStatus.Confirmed).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var adjustments = await db.WalletLedgerEntries.Where(x => x.WalletId == wallet.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var expected = wallet.OpeningBalance + received + adjustments;
        var reconciliation = new WalletReconciliation { OrganizationId = user.OrganizationId!.Value, WalletId = wallet.Id, ExpectedBalance = expected, ActualBalance = request.ActualBalance, Variance = request.ActualBalance - expected, Note = Clean(request.Note, 500), CreatedByUserId = user.Id };
        db.WalletReconciliations.Add(reconciliation); db.AuditEvents.Add(Audit(user.OrganizationId, user.Id, "WalletReconciled", nameof(WalletReconciliation), reconciliation.Id.ToString(), new { wallet.Id, expected, request.ActualBalance, reconciliation.Variance })); await db.SaveChangesAsync();
        return Results.Created($"/api/wallet-operations/reconciliations/{reconciliation.Id}", new { reconciliation.Id, reconciliation.ExpectedBalance, reconciliation.ActualBalance, reconciliation.Variance });
    });
}

static void MapReports(WebApplication app)
{
    app.MapGet("/api/dashboard", async (ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        var organization = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == user.OrganizationId);
        var zone = ResolveTimeZone(organization.TimeZoneId); var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var start = TimeZoneInfo.ConvertTimeToUtc(localNow.Date, zone); var end = TimeZoneInfo.ConvertTimeToUtc(localNow.Date.AddDays(1), zone);
        var query = ConfirmedReceipts(principal, user, db);
        var today = await query.Where(x => x.ReceivedAtUtc >= start && x.ReceivedAtUtc < end).GroupBy(x => x.CurrencyCode).Select(g => new { CurrencyCode = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).ToListAsync();
        var latest = await query.OrderByDescending(x => x.ReceivedAtUtc).Take(8).Join(db.Wallets, r => r.WalletId, w => w.Id, (r, w) => new { r.Id, r.WalletId, WalletName = w.Name, r.Provider, r.Amount, r.CurrencyCode, r.Sender, r.ReceivedAtUtc }).ToListAsync();
        return Results.Ok(new { LocalDate = localNow.Date, TimeZone = organization.TimeZoneId, Today = today, Latest = latest });
    }).RequireAuthorization();
    app.MapGet("/api/reports/summary", async ([AsParameters] ReportRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanViewReports && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var organization = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == user.OrganizationId);
        var zone = ResolveTimeZone(organization.TimeZoneId);
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
        var start = request.From?.ToUniversalTime() ?? TimeZoneInfo.ConvertTimeToUtc(localToday.AddDays(-29), zone);
        var end = request.To?.ToUniversalTime() ?? DateTime.UtcNow;
        if (end <= start || end - start > TimeSpan.FromDays(366)) return Results.BadRequest(new { error = "Choose a date range between one minute and 366 days." });
        var query = ApplyReportFilters(ConfirmedReceipts(principal, user, db).Where(x => x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end), request);
        var rows = await query.Select(x => new { x.WalletId, x.DeviceId, x.Provider, x.CurrencyCode, x.Amount, x.Sender, x.ReceivedAtUtc }).ToListAsync();
        var totals = rows.GroupBy(x => x.CurrencyCode).Select(g => new { CurrencyCode = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount), Average = g.Average(x => x.Amount), Median = Median(g.Select(x => x.Amount)), Maximum = g.Max(x => x.Amount) }).ToList();
        var wallets = rows.GroupBy(x => new { x.WalletId, x.CurrencyCode }).Select(g => new { g.Key.WalletId, g.Key.CurrencyCode, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).ToList();
        var names = await db.Wallets.Where(x => x.OrganizationId == user.OrganizationId).ToDictionaryAsync(x => x.Id, x => x.Name);
        var deviceNames = await db.WalletDevices.Where(x => x.OrganizationId == user.OrganizationId).ToDictionaryAsync(x => x.Id, x => x.Name);
        var localRows = rows.Select(x => new { Row = x, Local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.ReceivedAtUtc, DateTimeKind.Utc), zone) }).ToList();
        var daily = localRows.GroupBy(x => new { Day = x.Local.Date, x.Row.CurrencyCode }).Select(g => new { g.Key.Day, g.Key.CurrencyCode, Count = g.Count(), Amount = g.Sum(x => x.Row.Amount) }).OrderBy(x => x.Day).ToList();
        var providers = rows.GroupBy(x => new { x.Provider, x.CurrencyCode }).Select(g => new { g.Key.Provider, g.Key.CurrencyCode, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).OrderByDescending(x => x.Amount).ToList();
        var devices = rows.GroupBy(x => new { x.DeviceId, x.CurrencyCode }).Select(g => new { g.Key.DeviceId, DeviceName = deviceNames.GetValueOrDefault(g.Key.DeviceId, "Device"), g.Key.CurrencyCode, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).OrderByDescending(x => x.Amount).ToList();
        var hours = localRows.GroupBy(x => new { Hour = x.Local.Hour, x.Row.CurrencyCode }).Select(g => new { g.Key.Hour, g.Key.CurrencyCode, Count = g.Count(), Amount = g.Sum(x => x.Row.Amount) }).OrderBy(x => x.Hour).ToList();
        var duration = end - start; var previousStart = start - duration;
        var previous = await ApplyReportFilters(ConfirmedReceipts(principal, user, db).Where(x => x.ReceivedAtUtc >= previousStart && x.ReceivedAtUtc < start), request)
            .GroupBy(x => x.CurrencyCode).Select(g => new { CurrencyCode = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).ToListAsync();
        var senders = rows.Where(x => !string.IsNullOrWhiteSpace(x.Sender)).Select(x => x.Sender!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var knownBefore = await ConfirmedReceipts(principal, user, db).Where(x => x.ReceivedAtUtc < start && x.Sender != null && senders.Contains(x.Sender)).Select(x => x.Sender!).Distinct().ToListAsync();
        var newSenders = senders.Count(sender => !knownBefore.Contains(sender, StringComparer.OrdinalIgnoreCase));
        var captureQuality = await db.CaptureEvents.Where(x => x.OrganizationId == user.OrganizationId && x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end).GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        var quality = new { MissingSender = rows.Count(x => string.IsNullOrWhiteSpace(x.Sender)), UnmatchedCaptures = captureQuality.Where(x => x.Status == "Unmatched").Sum(x => x.Count), DuplicateCaptures = captureQuality.Where(x => x.Status == "Duplicate").Sum(x => x.Count), RejectedCaptures = captureQuality.Where(x => x.Status == "Rejected").Sum(x => x.Count), FailedUploads = await db.WalletDevices.Where(x => x.OrganizationId == user.OrganizationId).SumAsync(x => x.FailedUploadCount), NewSenders = newSenders, ReturningSenders = senders.Count - newSenders };
        return Results.Ok(new { From = start, To = end, TimeZone = organization.TimeZoneId, Totals = totals, PreviousTotals = previous, Wallets = wallets.Select(x => new { x.WalletId, WalletName = names.GetValueOrDefault(x.WalletId, "Wallet"), x.CurrencyCode, x.Count, x.Amount }), Daily = daily, Providers = providers, Devices = devices, Hours = hours, Quality = quality });
    }).RequireAuthorization();
    app.MapGet("/api/reports/export.xlsx", async ([AsParameters] ReportRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, WalletsDbContext db) =>
    {
        var user = await RequireOrganizationUser(principal, users);
        if (!user.CanExportReports && !IsOrganizationAdmin(principal)) return Results.Forbid();
        var start = request.From?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-30);
        var end = request.To?.ToUniversalTime() ?? DateTime.UtcNow;
        var rows = await ApplyReportFilters(ConfirmedReceipts(principal, user, db).Where(x => x.ReceivedAtUtc >= start && x.ReceivedAtUtc <= end), request)
            .Join(db.Wallets, receipt => receipt.WalletId, wallet => wallet.Id, (receipt, wallet) => new { Receipt = receipt, WalletName = wallet.Name })
            .OrderByDescending(x => x.Receipt.ReceivedAtUtc).ToListAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Receipts");
        var headers = new[] { "Received at (UTC)", "Wallet", "Provider", "Sender", "Reference", "Amount", "Currency" };
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index]; var number = index + 2;
            sheet.Cell(number, 1).Value = row.Receipt.ReceivedAtUtc; sheet.Cell(number, 2).Value = row.WalletName;
            sheet.Cell(number, 3).Value = row.Receipt.Provider; sheet.Cell(number, 4).Value = row.Receipt.Sender ?? "";
            sheet.Cell(number, 5).Value = row.Receipt.ProviderReference ?? ""; sheet.Cell(number, 6).Value = row.Receipt.Amount;
            sheet.Cell(number, 7).Value = row.Receipt.CurrencyCode;
        }
        sheet.Row(1).Style.Font.Bold = true; sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F7EF");
        sheet.SheetView.FreezeRows(1); sheet.Columns().AdjustToContents();
        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = "Wallets Hub report"; summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(2, 1).Value = "From (UTC)"; summary.Cell(2, 2).Value = start;
        summary.Cell(3, 1).Value = "To (UTC)"; summary.Cell(3, 2).Value = end;
        var grouped = rows.GroupBy(x => x.Receipt.CurrencyCode).ToList(); var summaryRow = 5;
        foreach (var group in grouped) { summary.Cell(summaryRow, 1).Value = group.Key; summary.Cell(summaryRow, 2).Value = group.Count(); summary.Cell(summaryRow, 3).Value = group.Sum(x => x.Receipt.Amount); summaryRow++; }
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
    var platformRole = await roles.FindByNameAsync(Roles.PlatformAdmin);
    var existingPlatformAdmin = platformRole is null ? null : await (from user in db.Users
        join userRole in db.UserRoles on user.Id equals userRole.UserId
        where userRole.RoleId == platformRole.Id
        select user).FirstOrDefaultAsync();
    if (existingPlatformAdmin is null)
    {
        var email = configuration["Seed:PlatformEmail"] ?? "admin@walletshub.local";
        var password = configuration["Seed:PlatformPassword"] ?? throw new InvalidOperationException("Seed:PlatformPassword is required.");
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
    if (!IsOrganizationAdmin(principal) && !user.AllWalletAccess) query = query.Where(x => db.UserWalletAccess.Any(a => a.UserId == user.Id && a.WalletId == x.WalletId));
    return query.Where(x => x.ReceivedAtUtc >= DateTime.UtcNow.AddDays(-user.VisibleReceiptDays));
}
static IQueryable<WalletReceipt> ConfirmedReceipts(ClaimsPrincipal principal, AppUser user, WalletsDbContext db) =>
    ScopedReceipts(principal, user, db).Where(x => x.Status == ReceiptStatus.Confirmed && x.CurrencyCode == "EGP");

static bool IsOrganizationAdmin(ClaimsPrincipal principal) => principal.IsInRole(Roles.Owner) || principal.IsInRole(Roles.Admin);
static bool CanViewBalances(ClaimsPrincipal principal) => IsOrganizationAdmin(principal) || principal.IsInRole(Roles.Manager);
static bool CanManageTeam(ClaimsPrincipal principal, AppUser user) => IsOrganizationAdmin(principal) || user.CanManageTeam;
static void ApplyRoleDefaults(AppUser user, string role)
{
    if (role is Roles.Owner or Roles.Admin)
    {
        user.CanViewReports = user.CanExportReports = user.CanManageDevices = user.CanManageTeam = user.CanConfirmReceipts = true;
        user.VisibleReceiptDays = 3650; user.AllWalletAccess = true;
    }
}
static async Task<string?> SetWalletAccess(WalletsDbContext db, AppUser user, Guid organizationId, bool allWalletAccess, IReadOnlyCollection<Guid> walletIds)
{
    var old = await db.UserWalletAccess.Where(x => x.UserId == user.Id).ToListAsync();
    db.UserWalletAccess.RemoveRange(old);
    user.AllWalletAccess = allWalletAccess;
    if (allWalletAccess) return null;
    var requested = walletIds.Distinct().ToList();
    var valid = await db.Wallets.Where(x => x.OrganizationId == organizationId && x.IsActive && requested.Contains(x.Id)).Select(x => x.Id).ToListAsync();
    if (valid.Count != requested.Count) return "One or more selected wallets are invalid or inactive.";
    db.UserWalletAccess.AddRange(valid.Select(walletId => new UserWalletAccess { UserId = user.Id, WalletId = walletId }));
    return null;
}
static object UserResponse(AppUser user, string role, Organization? organization) => new { user.Id, user.DisplayName, user.Email, Role = role, user.OrganizationId, OrganizationName = organization?.Name, OrganizationSlug = organization?.Slug, user.VisibleReceiptDays, user.CanViewReports, user.CanExportReports, user.CanManageDevices, user.CanManageTeam, user.CanConfirmReceipts, user.AllWalletAccess, user.TwoFactorEnabled };
static AuditEvent Audit(Guid? organizationId, string? userId, string action, string entityType, string? entityId, object? detail = null) => new() { OrganizationId = organizationId, UserId = userId, Action = action, EntityType = entityType, EntityId = entityId, DetailJson = detail is null ? null : JsonSerializer.Serialize(detail) };
static async Task<string> GetRole(UserManager<AppUser> users, AppUser user) => (await users.GetRolesAsync(user)).SingleOrDefault() ?? Roles.Employee;
static async Task<int> ActiveOwnerCount(WalletsDbContext db, Guid organizationId)
{
    var ownerRoleId = await db.Roles.Where(x => x.Name == Roles.Owner).Select(x => x.Id).SingleAsync();
    return await db.Users.CountAsync(x => x.OrganizationId == organizationId && x.IsActive && db.UserRoles.Any(r => r.UserId == x.Id && r.RoleId == ownerRoleId));
}
static async Task<string> UniquePairingCode(WalletsDbContext db)
{
    for (var attempt = 0; attempt < 20; attempt++)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(); var hash = Hash(code);
        if (!await db.WalletDevices.AnyAsync(x => x.PairingCodeHash == hash && x.PairingCodeExpiresAtUtc > DateTime.UtcNow)) return code;
    }
    throw new InvalidOperationException("Could not create a unique pairing code.");
}
static async Task<WalletDevice?> DeviceFromToken(HttpContext http, WalletsDbContext db)
{
    var token = http.Request.Headers["X-Wallet-Device-Token"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(token)) return null; var tokenHash = Hash(token);
    return await db.WalletDevices.SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.IsActive);
}
static async Task QueueCaptureIssueNotifications(WalletsDbContext db, WalletDevice device, CaptureEvent capture)
{
    var recipients = await db.Users.Where(x => x.OrganizationId == device.OrganizationId && x.IsActive && x.CanManageDevices).Select(x => x.Id).ToListAsync();
    foreach (var userId in recipients)
        db.UserNotifications.Add(new UserNotification { OrganizationId = device.OrganizationId, UserId = userId, Title = "Capture needs attention", Body = $"{device.Name}: {capture.Reason.Replace('-', ' ')}.", Link = "/capture-health", SourceId = capture.Id });
}
static IQueryable<WalletReceipt> ApplyReportFilters(IQueryable<WalletReceipt> query, ReportRequest request)
{
    var walletIds = ParseGuids(request.WalletIds); if (request.WalletId.HasValue) walletIds.Add(request.WalletId.Value);
    if (walletIds.Count > 0) query = query.Where(x => walletIds.Contains(x.WalletId));
    if (request.DeviceId.HasValue) query = query.Where(x => x.DeviceId == request.DeviceId);
    if (!string.IsNullOrWhiteSpace(request.Provider)) query = query.Where(x => x.Provider == request.Provider);
    if (!string.IsNullOrWhiteSpace(request.Currency)) query = query.Where(x => x.CurrencyCode == request.Currency.ToUpper());
    if (request.MinAmount.HasValue) query = query.Where(x => x.Amount >= request.MinAmount);
    if (request.MaxAmount.HasValue) query = query.Where(x => x.Amount <= request.MaxAmount);
    if (request.MissingSender == true) query = query.Where(x => x.Sender == null || x.Sender == "");
    if (request.MissingReference == true) query = query.Where(x => x.ProviderReference == null || x.ProviderReference == "");
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
        var pattern = $"%{EscapeLike(request.Search.Trim())}%";
        query = query.Where(x => EF.Functions.ILike(x.Sender ?? "", pattern) || EF.Functions.ILike(x.ProviderReference ?? "", pattern) || EF.Functions.ILike(x.Provider, pattern));
    }
    return query;
}
static decimal Median(IEnumerable<decimal> values)
{
    var sorted = values.Order().ToArray(); if (sorted.Length == 0) return 0;
    var middle = sorted.Length / 2; return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
}
static async Task<decimal> WalletBalance(WalletsDbContext db, Wallet wallet)
{
    var received = await db.WalletReceipts.Where(x => x.WalletId == wallet.Id && x.Status == ReceiptStatus.Confirmed).SumAsync(x => (decimal?)x.Amount) ?? 0;
    var ledger = await db.WalletLedgerEntries.Where(x => x.WalletId == wallet.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
    return wallet.OpeningBalance + received + ledger;
}
static TimeZoneInfo ResolveTimeZone(string id) => TimeZoneResolver.Resolve(id);
static HashSet<Guid> ParseGuids(string? value) => string.IsNullOrWhiteSpace(value) ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToHashSet();
static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
static string MaskSensitive(string value) => System.Text.RegularExpressions.Regex.Replace(value, @"(?<!\d)(\+?20)?(01\d{2})\d{4}(\d{3})(?!\d)", "$2****$3");
static string? Clean(string? value, int maxLength) { var clean = value?.Trim(); return string.IsNullOrWhiteSpace(clean) ? null : clean[..Math.Min(clean.Length, maxLength)]; }
static string ClientIp(HttpContext context) => context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
static string NormalizeAccount(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
static string NormalizeCurrency(string value) => value.Trim().ToUpperInvariant() switch { "EGP" => "EGP", _ => throw new BadHttpRequestException("Wallets Hub supports EGP only.") };
static void ValidateProviderCurrency(string provider, string currency)
{
    if (provider.Equals("Binance", StringComparison.OrdinalIgnoreCase)) throw new BadHttpRequestException("Binance wallets are not supported.");
    if (currency != "EGP") throw new BadHttpRequestException("Wallets Hub supports EGP only.");
}
static string Slug(string value) => string.Join('-', value.Trim().ToLowerInvariant().Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries).Select(part => new string(part.Where(char.IsLetterOrDigit).ToArray())).Where(part => part.Length > 0));
static string Unprotect(IDataProtector protector, string value) { try { return protector.Unprotect(value); } catch { return "Message unavailable"; } }

public sealed record LoginRequest(string Email, string Password, string? TwoFactorCode = null);
public sealed record AccountUpdateRequest(string DisplayName, string Email, string CurrentPassword, string? NewPassword);
public sealed record MfaCodeRequest(string Code);
public sealed record PasswordRequest(string Password);
public sealed record PlatformAccountUpdateRequest(string Email, string CurrentPassword, string? NewPassword);
public sealed record CreateOrganizationRequest(string Name, string? Slug, string OwnerName, string OwnerEmail, string OwnerPassword);
public sealed record PlatformOwnerPasswordResetRequest(string OwnerEmail, string NewPassword);
public sealed record ToggleRequest(bool Enabled);
public sealed record CreateTeamMemberRequest(string DisplayName, string Email, string Password, string Role, int VisibleReceiptDays, bool CanViewReports, bool CanExportReports, bool CanManageDevices, bool CanManageTeam, bool CanConfirmReceipts, bool AllWalletAccess, IReadOnlyCollection<Guid> WalletIds);
public sealed record UpdateTeamMemberRequest(string DisplayName, string Email, string Role, bool IsActive, int VisibleReceiptDays, bool CanViewReports, bool CanExportReports, bool CanManageDevices, bool CanManageTeam, bool CanConfirmReceipts, bool AllWalletAccess, IReadOnlyCollection<Guid> WalletIds);
public sealed record ResetPasswordRequest(string NewPassword);
public sealed record WalletRequest(string Name, string Provider, string AccountNumber, string CurrencyCode, Guid? DeviceId, bool IsActive = true, decimal OpeningBalance = 0, decimal? BalanceLimit = null);
public sealed record DevicePairingRequest(string Name);
public sealed record PairDeviceRequest(string PairingCode, string InstallationId);
public sealed record UpdateDeviceRequest(string Name, bool IsActive);
public sealed record DeviceHeartbeatRequest(string? AppVersion, string? AndroidVersion, int PendingUploadCount, int FailedUploadCount, bool SmsPermissionGranted, bool AxisNotificationAccessGranted, bool BatteryOptimizationIgnored, DateTime? LastSmsAtUtc, DateTime? LastAxisNotificationAtUtc);
public sealed record CaptureRequest(Guid? WalletId, string? SourcePackage, string? Title, string? Body, DateTime ReceivedAtUtc, string Fingerprint);
public sealed record NotificationPreferenceRequest(bool EveryReceipt, decimal? MinimumAmount, bool DailySummary, bool DeviceOffline);
public sealed record NotificationPreferenceResponse(bool EveryReceipt, decimal? MinimumAmount, bool DailySummary, bool DeviceOffline);
public sealed record WorkspaceSettingsRequest(string TimeZoneId, bool MaskSensitiveMessages, bool RequireReceiptConfirmation);
public sealed record ResolveCaptureRequest(Guid WalletId);
public sealed record LedgerEntryRequest(Guid WalletId, string Type, decimal Amount, string? Note, DateTime? OccurredAtUtc);
public sealed record WalletTransferRequest(Guid FromWalletId, Guid ToWalletId, decimal Amount, string? Note, DateTime? OccurredAtUtc);
public sealed record ReconciliationRequest(Guid WalletId, decimal ActualBalance, string? Note);
public sealed class StatementRequest { public Guid WalletId { get; set; } public DateTime? From { get; set; } public DateTime? To { get; set; } public int? Page { get; set; } public int? PageSize { get; set; } }
public sealed class ReceiptSearchRequest
{
    public DateTime? From { get; set; } public DateTime? To { get; set; } public Guid? WalletId { get; set; } public string? WalletIds { get; set; }
    public string? Provider { get; set; } public string? Currency { get; set; } public Guid? DeviceId { get; set; } public decimal? MinAmount { get; set; } public decimal? MaxAmount { get; set; }
    public string? Search { get; set; } public string? SearchMode { get; set; } public string? Status { get; set; } public bool? MissingSender { get; set; } public bool? MissingReference { get; set; } public string? Sort { get; set; }
    public int? Page { get; set; } public int? PageSize { get; set; }
}
public sealed class CaptureEventSearchRequest { public string? Status { get; set; } public string? Reason { get; set; } public Guid? DeviceId { get; set; } public DateTime? From { get; set; } public DateTime? To { get; set; } public int? Page { get; set; } public int? PageSize { get; set; } }
public sealed class AuditSearchRequest { public string? Action { get; set; } public string? UserId { get; set; } public DateTime? From { get; set; } public DateTime? To { get; set; } public int? Page { get; set; } public int? PageSize { get; set; } }
public sealed class ReportRequest { public DateTime? From { get; set; } public DateTime? To { get; set; } public Guid? WalletId { get; set; } public string? WalletIds { get; set; } public Guid? DeviceId { get; set; } public string? Provider { get; set; } public string? Currency { get; set; } public decimal? MinAmount { get; set; } public decimal? MaxAmount { get; set; } public string? Search { get; set; } public bool? MissingSender { get; set; } public bool? MissingReference { get; set; } }
