using WalletsHub.Api;
using Xunit;

namespace WalletsHub.Tests;

public sealed class WalletMessageParserTests
{
    [Fact]
    public void New_receipts_are_confirmed_automatically()
    {
        var receipt = new WalletReceipt
        {
            Provider = "InstaPay",
            CurrencyCode = "EGP",
            Fingerprint = "test",
            ProtectedMessage = "test",
            SourcePackage = "sms"
        };

        Assert.Equal(ReceiptStatus.Confirmed, receipt.Status);
    }

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

    [Fact]
    public void Parses_vodafone_receipt_when_android_omits_sender_title_and_link()
    {
        const string message = "تم استلام مبلغ 10 جنيه من رقم 01023719913 المسجل باسم Nadia H Abdelwahab على رقم محفظتك 01023684687. رصيدك الحالي: 73173.82 جنيه تاريخ العملية: 31-08-26 18:41 رقم العملية: 023227566038";
        Assert.True(WalletMessageParser.TryParse("android.sms", message, out var parsed));
        Assert.Equal("Vodafone Cash", parsed.Provider);
        Assert.Equal(10m, parsed.Amount);
        Assert.Equal("01023719913", parsed.Sender);
        Assert.Equal("01023684687", parsed.Destination);
        Assert.Equal("023227566038", parsed.Reference);
    }

    [Fact]
    public void Parses_binance_usdt_notification()
    {
        const string message = "Binance You have received a payment You have received a payment of 1 USDT from otify on 2026-08-31 16:10:56(UTC)";
        Assert.True(WalletMessageParser.TryParse("com.binance.dev", message, out var parsed));
        Assert.Equal("Binance", parsed.Provider);
        Assert.Equal(1m, parsed.Amount);
        Assert.Equal("USDT", parsed.CurrencyCode);
        Assert.Equal("otify", parsed.Sender);
    }

    [Theory]
    [InlineData("com.samsung.android.messaging", "Vodafone Cash: You received EGP 10 from 01012345678")]
    [InlineData("com.instapay.app", "InstaPay account credited by 20 EGP from 01012345678")]
    [InlineData("android.sms", "Binance You have received a payment of 1 USDT from otify")]
    public void Rejects_provider_on_the_wrong_capture_channel(string source, string message) =>
        Assert.False(WalletMessageParser.TryParse(source, message, out _));

    [Fact]
    public void Accepts_instapay_from_sms()
    {
        Assert.True(WalletMessageParser.TryParse("android.sms", "InstaPay account credited by 950 EGP from 01098765432", out var parsed));
        Assert.Equal("InstaPay", parsed.Provider);
    }

    [Theory]
    [InlineData("تم إضافة تحويل لحظي لبطاقتكم مسبقة الدفع بمبلغ 300.00 جم من هدير ابراهيم عبدالدايم سليمان حسن عمار رقم مرجعي 639896513920 يوم 31-08 الساعة 19:43 للمزيد اتصل ب 19623", 300, "هدير ابراهيم عبدالدايم سليمان حسن عمار", "639896513920")]
    [InlineData("تم إضافة تحويل لحظي لبطاقتكم مسبقة الدفع بمبلغ 5.00 جم من NADIA HISHAM MOHAMED رقم مرجعي 510786897432 يوم 31-08 الساعة 20:28 للمزيد اتصل ب 19623", 5, "NADIA HISHAM MOHAMED", "510786897432")]
    public void Parses_instant_card_transfer_sms_as_instapay(string message, decimal amount, string sender, string reference)
    {
        Assert.True(WalletMessageParser.TryParse("android.sms", message, out var parsed));
        Assert.Equal("InstaPay", parsed.Provider);
        Assert.Equal(amount, parsed.Amount);
        Assert.Equal("EGP", parsed.CurrencyCode);
        Assert.Equal(sender, parsed.Sender);
        Assert.Equal(reference, parsed.Reference);
    }

    [Theory]
    [InlineData("Vodafone Cash: You sent 100 EGP to 01012345678")]
    [InlineData("Your verification code is 778899")]
    [InlineData("A normal personal message received today")]
    public void Rejects_outgoing_or_unrelated_messages(string message) => Assert.False(WalletMessageParser.TryParse(null, message, out _));
}
