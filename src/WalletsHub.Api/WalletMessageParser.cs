using System.Globalization;
using System.Text.RegularExpressions;

namespace WalletsHub.Api;

public sealed record ParsedWalletMessage(string Provider, decimal Amount, string CurrencyCode, string? Sender, string? Destination, string? Reference);

public static partial class WalletMessageParser
{
    private sealed record ProviderRule(string Name, string[] Markers);

    private static readonly ProviderRule[] Providers =
    [
        new("Vodafone Cash", ["vodafone cash", "vf cash", "فودافون كاش", "vf.eg/vfcash"]),
        new("Orange Cash", ["orange cash", "اورنچ كاش", "اورنج كاش"]),
        new("e& Cash", ["e& cash", "etisalat cash", "اتصالات كاش", "إي آند كاش"]),
        new("WE Pay", ["we pay", "wepay", "وي باي"]),
        new("InstaPay", ["instapay", "انستاباي", "إنستاباي"]),
        new("Binance", ["binance"]),
        new("Bank transfer", ["bank transfer", "account credited", "تم اضافة مبلغ", "تم إضافة مبلغ", "تحويل بنكي"])
    ];

    [GeneratedRegex(@"(?:usdt|egp|usd|جنيه(?:اً|ا)?|دولار|l\.e)\s*[:\-]?\s*([0-9][0-9,]*(?:\.[0-9]+)?)|([0-9][0-9,]*(?:\.[0-9]+)?)\s*(?:usdt|egp|usd|جنيه(?:اً|ا)?|دولار|l\.e)", RegexOptions.IgnoreCase)]
    private static partial Regex AmountPattern();
    [GeneratedRegex(@"(?:\+?20)?01[0125][0-9]{8}")] private static partial Regex PhonePattern();
    [GeneratedRegex(@"(?:reference|ref|transaction\s*(?:id|number)|رقم\s*العملية|مرجع)\s*[:#\-]?\s*([a-z0-9\-]{4,})", RegexOptions.IgnoreCase)]
    private static partial Regex ReferencePattern();
    [GeneratedRegex(@"(?:wallet|محفظتك|الى رقم|إلى رقم|to)\D{0,20}((?:\+?20)?01[0125][0-9]{8})", RegexOptions.IgnoreCase)]
    private static partial Regex DestinationPattern();
    [GeneratedRegex(@"\bfrom\s+([a-z0-9._-]{2,80})(?:\s+on\b|[.,\r\n]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex NamedSenderPattern();

    public static bool TryParse(string? sourcePackage, string? message, out ParsedWalletMessage parsed)
    {
        parsed = null!;
        var text = NormalizeDigits(message ?? "");
        var normalized = text.ToLowerInvariant();
        var package = (sourcePackage ?? "").ToLowerInvariant();
        var provider = Providers.FirstOrDefault(rule => rule.Markers.Any(marker => normalized.Contains(marker) || package.Contains(marker.Replace(" ", ""))));
        // Some SMS applications omit the sender/title and the vf.eg link from the
        // notification payload. The receipt body still has a stable Vodafone Cash
        // structure, so recognize that structure without depending on the title.
        if (provider is null && IsVodafoneCashReceipt(normalized))
            provider = Providers[0];
        if (provider is null) return false;

        if (package.Length > 0)
        {
            var sms = package == "android.sms";
            if (sms && provider.Name is not ("Vodafone Cash" or "InstaPay")) return false;
            if (!sms && provider.Name != "Binance") return false;
        }

        var incoming = new[] { "received", "money received", "credited", "تم استلام", "استلمت", "تم تحويل مبلغ", "تم إيداع", "تم ايداع", "تم إضافة", "تم اضافة", "حوالة واردة", "تحويل وارد", "من رقم", " from " }.Any(normalized.Contains);
        var outgoing = new[] { "you sent", "debited", "تم خصم", "تم الدفع", "إلى رقم", "الى رقم" }.Any(normalized.Contains)
            && !new[] { "من رقم", " from ", "received", "credited" }.Any(normalized.Contains);
        if (!incoming || outgoing) return false;

        var amountMatch = AmountPattern().Match(text);
        if (!amountMatch.Success) return false;
        var amountText = amountMatch.Groups[1].Success ? amountMatch.Groups[1].Value : amountMatch.Groups[2].Value;
        if (!decimal.TryParse(amountText.Replace(",", ""), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount <= 0) return false;

        var currency = amountMatch.Value.Contains("usdt", StringComparison.OrdinalIgnoreCase) ? "USDT"
            : amountMatch.Value.Contains("usd", StringComparison.OrdinalIgnoreCase) || amountMatch.Value.Contains("دولار", StringComparison.OrdinalIgnoreCase) ? "USD" : "EGP";
        var phones = PhonePattern().Matches(Regex.Replace(text, @"[\s\-()]", "")).Select(x => x.Value).Distinct().ToList();
        var destinationMatch = DestinationPattern().Match(text);
        var destination = destinationMatch.Success ? destinationMatch.Groups[1].Value : phones.Skip(1).FirstOrDefault();
        var namedSender = NamedSenderPattern().Match(text);
        var sender = phones.FirstOrDefault(x => x != destination) ?? (namedSender.Success ? namedSender.Groups[1].Value : null);
        var reference = ReferencePattern().Match(text);
        parsed = new(provider.Name, amount, currency, sender, destination, reference.Success ? reference.Groups[1].Value : null);
        return true;
    }

    private static string NormalizeDigits(string value) => value
        .Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
        .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9');

    private static bool IsVodafoneCashReceipt(string value) =>
        value.Contains("تم استلام مبلغ")
        && value.Contains("من رقم")
        && value.Contains("محفظتك")
        && value.Contains("رقم العملية");
}
