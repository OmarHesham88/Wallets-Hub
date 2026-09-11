using WalletsHub.Api;
using Xunit;

namespace WalletsHub.Tests;

public sealed class TimeZoneResolverTests
{
    [Theory]
    [InlineData("Africa/Cairo")]
    [InlineData("Egypt Standard Time")]
    public void ResolvesEgyptTimeZoneAcrossOperatingSystems(string id)
    {
        var zone = TimeZoneResolver.Resolve(id);

        Assert.NotNull(zone);
        Assert.NotEqual(TimeSpan.Zero, zone.GetUtcOffset(new DateTime(2026, 9, 12)));
    }

    [Fact]
    public void EmptyTimeZoneFallsBackToUtc() => Assert.Equal(TimeZoneInfo.Utc, TimeZoneResolver.Resolve(""));
}
