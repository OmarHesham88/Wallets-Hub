package hub.wallets.mobile;

import java.util.Locale;
import java.util.regex.Pattern;

public final class WalletCaptureClassifier {
    public static final String AXIS_PACKAGE = "com.axispay.consumer.wallet";
    private static final Pattern EGP_AMOUNT = Pattern.compile(
        "(?i)(?:egp|جنيه(?:اً|ا)?|ج\\.?\\s*م\\.?|جم|l\\.?\\s*e\\.?)\\s*[:\\-]?\\s*[0-9٠-٩][0-9٠-٩,٬.٫]*|[0-9٠-٩][0-9٠-٩,٬.٫]*\\s*(?:egp|جنيه(?:اً|ا)?|ج\\.?\\s*م\\.?|جم|l\\.?\\s*e\\.?)"
    );

    private WalletCaptureClassifier() {}

    public static boolean isSmsCandidate(String sender, String body) {
        String content = normalized(sender, body);
        boolean vodafone = any(content, "vodafone cash", "vf cash", "فودافون كاش", "vf.eg/vfcash") ||
            (content.contains("تم استلام مبلغ") && content.contains("من رقم") && content.contains("محفظتك") && content.contains("رقم العملية"));
        boolean instaPay = any(content, "instapay", "insta pay", "انستاباي", "إنستاباي");
        boolean instantTransfer = any(content, "تحويل لحظي", "تحويل فورى", "تحويل فوري", "instant transfer", "instant payment", " ipn ") ||
            (content.contains("19623") && any(content, "تم إضافة", "تم اضافه", "تحويل"));
        boolean orange = any(content, "orange cash", "orangecash", "orange", "اورنچ كاش", "أورنچ كاش", "اورنج كاش", "أورنج كاش", "اورنچ", "اورنج");
        boolean eand = any(content, "e& cash", "e& money", "etisalat cash", "etisalatcash", "etisalat", "e&", "اتصالات كاش", "إي آند كاش", "اي اند كاش", "اتصالات", "إي آند", "اي اند");
        return (vodafone || instaPay || instantTransfer || orange || eand)
            && isIncoming(content) && !isOutgoing(content) && EGP_AMOUNT.matcher(content).find();
    }

    public static boolean isAxisNotification(String packageName, String title, String body) {
        if (!AXIS_PACKAGE.equals(packageName)) return false;
        String content = normalized(title, body);
        return isIncoming(content) && !isOutgoing(content) && EGP_AMOUNT.matcher(content).find();
    }

    private static boolean isIncoming(String content) {
        return any(content,
            "you received", "you've received", "you have received", "payment received", "money received",
            "credited", "credit of", "added to your wallet", "added to your balance", "cash in",
            "تم استلام", "استلمت", "تم تحويل مبلغ", "تم إيداع", "تم ايداع", "تم إضافة", "تم اضافه",
            "حوالة واردة", "تحويل وارد", "من رقم", " from ");
    }

    private static boolean isOutgoing(String content) {
        boolean outgoing = any(content,
            "you sent", "you paid", "payment sent", "debited", "deducted", "transferred to",
            "تم خصم", "تم الدفع", "تم تحويلك", "قمت بتحويل", "حولت مبلغ", "أرسلت", "ارسلت", "إلى رقم", "الى رقم");
        boolean explicitIncoming = any(content, "من رقم", " from ", "received", "credited", "تم استلام", "استلمت");
        return outgoing && !explicitIncoming;
    }

    private static String normalized(String first, String second) {
        return (text(first) + " " + text(second)).toLowerCase(Locale.ROOT)
            .replace('\u061C', ' ').replace('\u200E', ' ').replace('\u200F', ' ');
    }

    private static boolean any(String value, String... markers) { for (String marker : markers) if (value.contains(marker)) return true; return false; }
    private static String text(String value) { return value == null ? "" : value; }
}
