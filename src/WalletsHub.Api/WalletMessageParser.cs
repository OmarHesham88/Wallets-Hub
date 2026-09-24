using System.Globalization;
using System.Text.RegularExpressions;

namespace WalletsHub.Api;

public sealed record ParsedWalletMessage(string Provider, decimal Amount, string CurrencyCode, string? Sender, string? Destination, string? Reference);

public static partial class WalletMessageParser
{
    private const string SmsSource = "android.sms";
    private const string AxisPackage = "com.axispay.consumer.wallet";
    private sealed record ProviderRule(string Name, string[] Markers);

    private static readonly ProviderRule[] Providers =
    [
        new("Axis", ["axis wallet", "axis pay", "axis -", "أكسس", "اكسس"]),
        new("Vodafone Cash", ["vodafone cash", "vf cash", "فودافون كاش", "vf.eg/vfcash"]),
        new("Orange Cash", ["orange cash", "orangecash", "orange", "اورنچ كاش", "أورنچ كاش", "اورنج كاش", "أورنج كاش", "اورنچ", "اورنج"]),
        new("e& Cash", ["e& cash", "e& money", "etisalat cash", "etisalatcash", "etisalat", "e&", "اتصالات كاش", "إي آند كاش", "اي اند كاش", "اتصالات", "إي آند", "اي اند"]),
        new("WE Pay", ["we pay", "wepay", "وي باي"]),
        new("InstaPay", ["instapay", "insta pay", "انستاباي", "إنستاباي"]),
        new("Bank transfer", ["bank transfer", "account credited", "تم اضافة مبلغ", "تم إضافة مبلغ", "تحويل بنكي"])
    ];

    [GeneratedRegex(@"(?:egp|جنيه(?:اً|ا)?(?:\s*مصري)?|ج\.?\s*م\.?|جم|l\.?\s*e\.?)\s*[:\-]?\s*([0-9][0-9,]*(?:\.[0-9]+)?)|([0-9][0-9,]*(?:\.[0-9]+)?)\s*(?:egp|جنيه(?:اً|ا)?(?:\s*مصري)?|ج\.?\s*م\.?|جم|l\.?\s*e\.?)", RegexOptions.IgnoreCase)]
    private static partial Regex AmountPattern();
    [GeneratedRegex(@"(?:\+?20)?01[0125][0-9]{8}")] private static partial Regex PhonePattern();
    [GeneratedRegex(@"(?:reference|ref|transaction\s*(?:id|number|no)|رقم\s*العملية|رقم\s*مرجع(?:ي)?|مرجع(?:ي)?)\s*[:#\-]?\s*([a-z0-9\-]{4,})", RegexOptions.IgnoreCase)]
    private static partial Regex ReferencePattern();
    [GeneratedRegex(@"(?:wallet|محفظتك|الى رقم|إلى رقم|to)\D{0,20}((?:\+?20)?01[0125][0-9]{8})", RegexOptions.IgnoreCase)]
    private static partial Regex DestinationPattern();
    [GeneratedRegex(@"\bfrom\s+([\p{L}\p{M}0-9._'\-]+(?:\s+[\p{L}\p{M}0-9._'\-]+){0,12}?)(?=\s+on\s+\d{4}-\d{1,2}-\d{1,2}\b|[,.;\r\n]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex NamedSenderPattern();
    [GeneratedRegex(@"[\u061C\u200E\u200F\u202A-\u202E\u2066-\u2069]")]
    private static partial Regex BidiControlPattern();
    [GeneratedRegex(@"\sمن\s+([\p{L}\s.]{2,120}?)\s+رقم\s+مرجع(?:ي)?", RegexOptions.IgnoreCase)]
    private static partial Regex ArabicNamedSenderPattern();

    public static bool TryParse(string? sourcePackage, string? message, out ParsedWalletMessage parsed)
    {
        parsed = null!;
        var text = BidiControlPattern().Replace(NormalizeDigits(message ?? ""), "");
        var normalized = text.ToLowerInvariant();
        var package = (sourcePackage ?? "").Trim().ToLowerInvariant();

        ProviderRule? provider = package == AxisPackage
            ? Providers.Single(rule => rule.Name == "Axis")
            : Providers.FirstOrDefault(rule => rule.Markers.Any(normalized.Contains));

        if (provider is null && IsVodafoneCashReceipt(normalized))
            provider = Providers.Single(rule => rule.Name == "Vodafone Cash");
        if (provider is null && IsInstantTransferReceipt(normalized))
            provider = Providers.Single(rule => rule.Name == "InstaPay");
        if (provider is null) return false;

        // Capture channels are deliberately isolated: Axis comes from its own app
        // notification; mobile-network wallets and InstaPay come from SMS only.
        if (package.Length > 0)
        {
            var validSmsProvider = provider.Name is "Vodafone Cash" or "InstaPay" or "Orange Cash" or "e& Cash";
            var validAxisNotification = package == AxisPackage && provider.Name == "Axis";
            if (!(package == SmsSource && validSmsProvider) && !validAxisNotification) return false;
        }

        var incoming = ContainsAny(normalized,
            "you received", "you've received", "you have received", "payment received", "money received",
            "credited", "credit of", "added to your wallet", "added to your balance", "cash in",
            "تم استلام", "استلمت", "تم تحويل مبلغ", "تم إيداع", "تم ايداع", "تم إضافة", "تم اضافه",
            "حوالة واردة", "تحويل وارد", "تم اضافة", "من رقم", " from ");
        var outgoing = ContainsAny(normalized,
            "you sent", "you paid", "payment sent", "debited", "deducted", "transferred to",
            "تم خصم", "تم الدفع", "تم تحويلك", "قمت بتحويل", "حولت مبلغ", "أرسلت", "ارسلت", "إلى رقم", "الى رقم")
            && !ContainsAny(normalized, "من رقم", " from ", "received", "credited", "تم استلام", "استلمت");
        if (!incoming || outgoing) return false;

        var amountMatch = AmountPattern().Match(text);
        if (!amountMatch.Success) return false;
        var amountText = amountMatch.Groups[1].Success ? amountMatch.Groups[1].Value : amountMatch.Groups[2].Value;
        if (!decimal.TryParse(amountText.Replace(",", ""), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount <= 0) return false;

        var phones = PhonePattern().Matches(Regex.Replace(text, @"[\s\-()]", "")).Select(x => x.Value).Distinct().ToList();
        var destinationMatch = DestinationPattern().Match(text);
        var destination = destinationMatch.Success ? destinationMatch.Groups[1].Value : phones.Skip(1).FirstOrDefault();
        var namedSender = NamedSenderPattern().Match(text);
        var arabicNamedSender = ArabicNamedSenderPattern().Match(text);
        var sender = phones.FirstOrDefault(x => x != destination)
            ?? (namedSender.Success ? namedSender.Groups[1].Value.Trim() : null)
            ?? (arabicNamedSender.Success ? Regex.Replace(arabicNamedSender.Groups[1].Value, @"\s+", " ").Trim() : null);
        var reference = ReferencePattern().Match(text);
        parsed = new(provider.Name, amount, "EGP", sender, destination, reference.Success ? reference.Groups[1].Value : null);
        return true;
    }

    private static bool ContainsAny(string value, params string[] markers) => markers.Any(value.Contains);

    private static string NormalizeDigits(string value) => value
        .Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
        .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9')
        .Replace('٫', '.').Replace('٬', ',');

    private static bool IsVodafoneCashReceipt(string value) =>
        value.Contains("تم استلام مبلغ") && value.Contains("من رقم") && value.Contains("محفظتك") && value.Contains("رقم العملية");

    private static bool IsInstantTransferReceipt(string value)
    {
        var canonical = value.Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ة', 'ه').Replace('ى', 'ي');
        return ContainsAny(canonical, "تحويل لحظي", "تحويل فوري", "instant transfer", "instant payment", " ipn ")
            && ContainsAny(canonical, "تم اضافه", "تم ايداع", "تم استلام", "credited", "received");
    }
}
