using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WalletsHub.Api;

public sealed class WalletsDbContext(DbContextOptions<WalletsDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletDevice> WalletDevices => Set<WalletDevice>();
    public DbSet<UserWalletAccess> UserWalletAccess => Set<UserWalletAccess>();
    public DbSet<WalletReceipt> WalletReceipts => Set<WalletReceipt>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Organization>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.Slug).HasMaxLength(80);
        });
        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(160);
            e.HasIndex(x => new { x.OrganizationId, x.IsActive });
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Wallet>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Provider).HasMaxLength(80);
            e.Property(x => x.AccountNumber).HasMaxLength(120);
            e.Property(x => x.NormalizedAccountNumber).HasMaxLength(120);
            e.Property(x => x.CurrencyCode).HasMaxLength(4);
            e.HasIndex(x => new { x.OrganizationId, x.Provider, x.NormalizedAccountNumber }).IsUnique();
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<WalletDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<WalletDevice>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.InstallationId).HasMaxLength(120);
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.Property(x => x.PairingCodeHash).HasMaxLength(64);
            e.HasIndex(x => x.InstallationId).IsUnique();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<UserWalletAccess>(e =>
        {
            e.HasKey(x => new { x.UserId, x.WalletId });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Wallet>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<WalletReceipt>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.Property(x => x.CurrencyCode).HasMaxLength(4);
            e.Property(x => x.Provider).HasMaxLength(80);
            e.Property(x => x.ProviderReference).HasMaxLength(160);
            e.Property(x => x.Fingerprint).HasMaxLength(128);
            e.HasIndex(x => new { x.DeviceId, x.Fingerprint }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.ReceivedAtUtc });
            e.HasIndex(x => new { x.OrganizationId, x.Provider, x.ProviderReference });
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Wallet>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<WalletDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<NotificationPreference>(e =>
        {
            e.Property(x => x.MinimumAmount).HasPrecision(18, 4);
            e.HasIndex(x => new { x.UserId, x.WalletId }).IsUnique();
        });
        b.Entity<UserNotification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            e.Property(x => x.Title).HasMaxLength(180);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AuditEvent>().HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
    }
}
