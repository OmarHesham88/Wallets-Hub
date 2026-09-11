using Microsoft.AspNetCore.Identity;

namespace WalletsHub.Api;

public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
    public static readonly string[] All = [PlatformAdmin, Owner, Admin, Manager, Employee];
    public static readonly string[] OrganizationRoles = [Owner, Admin, Manager, Employee];
}

public enum ReceiptStatus { Pending, Confirmed, Rejected }

public sealed class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public bool IsActive { get; set; } = true;
    public string TimeZoneId { get; set; } = "Africa/Cairo";
    public bool MaskSensitiveMessages { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AppUser : IdentityUser
{
    public Guid? OrganizationId { get; set; }
    public required string DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public int VisibleReceiptDays { get; set; } = 2;
    public bool CanConfirmReceipts { get; set; }
    public bool CanRejectReceipts { get; set; }
    public bool CanViewReports { get; set; }
    public bool CanExportReports { get; set; }
    public bool CanManageDevices { get; set; }
    public bool CanManageTeam { get; set; }
    public bool AllWalletAccess { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Wallet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? DeviceId { get; set; }
    public required string Name { get; set; }
    public required string Provider { get; set; }
    public required string AccountNumber { get; set; }
    public required string NormalizedAccountNumber { get; set; }
    public string CurrencyCode { get; set; } = "EGP";
    public decimal OpeningBalance { get; set; }
    public decimal? BalanceLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class WalletDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    public string Platform { get; set; } = "Android";
    public string? InstallationId { get; set; }
    public string? TokenHash { get; set; }
    public string? PairingCodeHash { get; set; }
    public DateTime? PairingCodeExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? PairedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public DateTime? LastSmsAtUtc { get; set; }
    public DateTime? LastNotificationAtUtc { get; set; }
    public DateTime? LastCaptureAtUtc { get; set; }
    public DateTime? OfflineAlertSentAtUtc { get; set; }
    public string? AppVersion { get; set; }
    public string? AndroidVersion { get; set; }
    public int PendingUploadCount { get; set; }
    public int FailedUploadCount { get; set; }
    public bool SmsPermissionGranted { get; set; }
    public bool NotificationPermissionGranted { get; set; }
    public bool BatteryOptimizationIgnored { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class UserWalletAccess
{
    public required string UserId { get; set; }
    public Guid WalletId { get; set; }
}

public sealed class WalletReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid WalletId { get; set; }
    public Guid DeviceId { get; set; }
    public required string Provider { get; set; }
    public decimal Amount { get; set; }
    public required string CurrencyCode { get; set; }
    public string? Sender { get; set; }
    public string? ProviderReference { get; set; }
    public required string Fingerprint { get; set; }
    public required string ProtectedMessage { get; set; }
    public required string SourcePackage { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Confirmed;
    public DateTime ReceivedAtUtc { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}

public sealed class CaptureEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? WalletId { get; set; }
    public Guid? ReceiptId { get; set; }
    public required string Fingerprint { get; set; }
    public required string Status { get; set; }
    public required string Reason { get; set; }
    public string? Provider { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Sender { get; set; }
    public string? Destination { get; set; }
    public string? ProviderReference { get; set; }
    public required string SourcePackage { get; set; }
    public required string ProtectedMessage { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
    public int AttemptCount { get; set; } = 1;
}

public sealed class WalletLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid WalletId { get; set; }
    public Guid? RelatedWalletId { get; set; }
    public Guid? CorrelationId { get; set; }
    public required string Type { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class WalletReconciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid WalletId { get; set; }
    public decimal ExpectedBalance { get; set; }
    public decimal ActualBalance { get; set; }
    public decimal Variance { get; set; }
    public string? Note { get; set; }
    public required string CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class NotificationDispatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string DispatchKey { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class NotificationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string UserId { get; set; }
    public Guid? WalletId { get; set; }
    public bool EveryReceipt { get; set; } = true;
    public decimal? MinimumAmount { get; set; }
    public bool DailySummary { get; set; } = true;
    public bool DeviceOffline { get; set; } = true;
    public bool RejectedReceipt { get; set; } = true;
}

public sealed class UserNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string UserId { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public string? Link { get; set; }
    public Guid? SourceId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public string? UserId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? DetailJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
