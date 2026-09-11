namespace WalletsHub.Api;

public static class TimeZoneResolver
{
    public static TimeZoneInfo Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var direct)) return direct;

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId) &&
            TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out var ianaZone))
            return ianaZone;

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId) &&
            TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out var windowsZone))
            return windowsZone;

        throw new TimeZoneNotFoundException($"The time zone '{id}' is not available on this system.");
    }
}
