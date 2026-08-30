using WalletsHub.Api;
using Xunit;

namespace WalletsHub.Tests;

public sealed class WalletMessageParserTests
{
    [Theory]
    [InlineData("Vodafone Cash: You received EGP 1,250.50 from 01012345678. Transaction ID 778899", "Vodafone Cash", 1250.50, "EGP")]
    [InlineData("Orange Cash money received: 500 EGP from 01123456789 ref: ORG-7788", "Orange Cash", 500, "EGP")]
    [InlineData("Etisalat Cash: received 80 EGP from 01212345678", "e& Cash", 80, "EGP")]
    [InlineData("WE Pay received USD 25 from 01512345678", "WE Pay", 25, "USD")]
    [InlineData("InstaPay account credited by 950 EGP from 01098765432", "InstaPay", 950, "EGP")]
    public void Parses_supported_incoming_formats(string message, string provider, decimal amount, string currency)
    {
        Assert.True(WalletMessageParser.TryParse(null, message, out var parsed));
        Assert.Equal(provider, parsed.Provider);
        Assert.Equal(amount, parsed.Amount);
        Assert.Equal(currency, parsed.CurrencyCode);
    }

    [Fact]
    public void Parses_arabic_vodafone_format_without_taking_the_balance_as_amount()
    {
        const string message = "فودافون كاش تم استلام مبلغ ٥ جنيه من رقم 01023719913 على رقم محفظتك 01023684687. رصيدك الحالي: 2977.23 جنيه رقم العملية: 022496121035";
        Assert.True(WalletMessageParser.TryParse(null, message, out var parsed));
        Assert.Equal(5m, parsed.Amount);
        Assert.Equal("01023719913", parsed.Sender);
        Assert.Equal("01023684687", parsed.Destination);
        Assert.Equal("022496121035", parsed.Reference);
    }

    [Theory]
    [InlineData("Vodafone Cash: You sent 100 EGP to 01012345678")]
    [InlineData("Your verification code is 778899")]
    [InlineData("A normal personal message received today")]
    public void Rejects_outgoing_or_unrelated_messages(string message) => Assert.False(WalletMessageParser.TryParse(null, message, out _));
}
