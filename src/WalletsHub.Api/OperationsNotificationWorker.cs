using Microsoft.EntityFrameworkCore;

namespace WalletsHub.Api;

public sealed class OperationsNotificationWorker(IServiceScopeFactory scopeFactory, ILogger<OperationsNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Dispatch(stoppingToken); }
            catch (Exception exception) { logger.LogError(exception, "Wallets Hub operational notification dispatch failed."); }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task Dispatch(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletsDbContext>();
        var now = DateTime.UtcNow;
        var offlineCutoff = now.AddMinutes(-20);
        var offlineDevices = await db.WalletDevices
            .Where(device => device.IsActive && device.PairedAtUtc != null && (device.LastHeartbeatAtUtc ?? device.LastSeenAtUtc) < offlineCutoff && device.OfflineAlertSentAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var device in offlineDevices)
        {
            var recipients = await ActiveNotificationRecipients(db, device.OrganizationId, preference => preference.DeviceOffline, requireDeviceAccess: true, cancellationToken);
            foreach (var recipient in recipients)
                db.UserNotifications.Add(new UserNotification { OrganizationId = device.OrganizationId, UserId = recipient.Id, Title = $"{device.Name} is offline", Body = "The capture phone has not sent a heartbeat for more than 20 minutes. Check its internet connection, permissions, and battery settings.", Link = "/devices", SourceId = device.Id });
            db.AuditEvents.Add(new AuditEvent { OrganizationId = device.OrganizationId, Action = "DeviceOfflineDetected", EntityType = nameof(WalletDevice), EntityId = device.Id.ToString(), DetailJson = System.Text.Json.JsonSerializer.Serialize(new { LastActivityAtUtc = device.LastHeartbeatAtUtc ?? device.LastSeenAtUtc }) });
            device.OfflineAlertSentAtUtc = now;
        }

        var organizations = await db.Organizations.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        foreach (var organization in organizations)
        {
            var zone = TimeZoneResolver.Resolve(organization.TimeZoneId); var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
            if (localNow.TimeOfDay < TimeSpan.FromMinutes(5)) continue;
            var reportDate = localNow.Date.AddDays(-1);
            var localStart = DateTime.SpecifyKind(reportDate, DateTimeKind.Unspecified);
            var start = TimeZoneInfo.ConvertTimeToUtc(localStart, zone); var end = TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), zone);
            var recipients = await ActiveNotificationRecipients(db, organization.Id, preference => preference.DailySummary, requireDeviceAccess: false, cancellationToken);
            foreach (var recipient in recipients)
            {
                var receiptQuery = db.WalletReceipts.Where(x => x.OrganizationId == organization.Id && x.Status == ReceiptStatus.Confirmed && x.CurrencyCode == "EGP" && x.ReceivedAtUtc >= start && x.ReceivedAtUtc < end);
                if (!recipient.AllWalletAccess)
                    receiptQuery = receiptQuery.Where(x => db.UserWalletAccess.Any(access => access.UserId == recipient.Id && access.WalletId == x.WalletId));
                var totals = await receiptQuery.GroupBy(x => x.CurrencyCode).Select(group => new { Currency = group.Key, Count = group.Count(), Amount = group.Sum(x => x.Amount) }).ToListAsync(cancellationToken);
                var key = $"daily:{organization.Id}:{recipient.Id}:{reportDate:yyyy-MM-dd}";
                if (await db.NotificationDispatches.AnyAsync(x => x.DispatchKey == key, cancellationToken)) continue;
                var totalCount = totals.Sum(x => x.Count);
                var amounts = totals.Count == 0 ? "No money received" : string.Join(" · ", totals.Select(x => $"{x.Amount:N2} {x.Currency}"));
                var link = recipient.CanViewReports ? "/reports" : "/receipts";
                db.UserNotifications.Add(new UserNotification { OrganizationId = organization.Id, UserId = recipient.Id, Title = $"Daily summary · {reportDate:dd MMM}", Body = $"{totalCount:N0} received payment(s) · {amounts}.", Link = link });
                db.NotificationDispatches.Add(new NotificationDispatch { OrganizationId = organization.Id, DispatchKey = key });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await db.NotificationDispatches.Where(x => x.CreatedAtUtc < now.AddDays(-60)).ExecuteDeleteAsync(cancellationToken);
        await db.UserNotifications.Where(x => x.CreatedAtUtc < now.AddDays(-180)).ExecuteDeleteAsync(cancellationToken);
        await db.CaptureEvents.Where(x => x.LastSeenAtUtc < now.AddDays(-365)).ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<List<AppUser>> ActiveNotificationRecipients(WalletsDbContext db, Guid organizationId, Func<NotificationPreference, bool> enabled, bool requireDeviceAccess, CancellationToken cancellationToken)
    {
        var users = await db.Users.Where(x => x.OrganizationId == organizationId && x.IsActive && (!requireDeviceAccess || x.CanManageDevices)).ToListAsync(cancellationToken);
        var preferences = await db.NotificationPreferences.Where(x => x.OrganizationId == organizationId && x.WalletId == null).ToListAsync(cancellationToken);
        return users.Where(user => preferences.FirstOrDefault(x => x.UserId == user.Id) is not { } preference || enabled(preference)).ToList();
    }

}
