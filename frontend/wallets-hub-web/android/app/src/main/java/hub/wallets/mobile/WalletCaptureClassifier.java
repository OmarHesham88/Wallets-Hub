package hub.wallets.mobile;

import java.util.Locale;

public final class WalletCaptureClassifier {
    private WalletCaptureClassifier() {}

    public static boolean isSmsCandidate(String sender, String body) {
        String content = (text(sender) + " " + text(body)).toLowerCase(Locale.ROOT);
        boolean vodafone = any(content, "vodafone cash", "vf cash", "فودافون كاش", "vf.eg/vfcash") ||
            (content.contains("تم استلام مبلغ") && content.contains("من رقم") && content.contains("محفظتك") && content.contains("رقم العملية"));
        boolean instaPay = any(content, "instapay", "insta pay", "انستاباي", "إنستاباي");
        boolean instantTransfer = any(content, "تحويل لحظي", "تحويل فورى", "تحويل فوري", "instant transfer", "instant payment", " ipn ") ||
            (content.contains("19623") && any(content, "تم إضافة", "تم اضافه", "تحويل"));
        return vodafone || instaPay || instantTransfer;
    }

    public static boolean isBinanceCandidate(String packageName, String title, String body) {
        String content = (text(packageName) + " " + text(title) + " " + text(body)).toLowerCase(Locale.ROOT);
        return content.contains("binance") && content.contains("usdt");
    }

    private static boolean any(String value, String... markers) { for (String marker : markers) if (value.contains(marker)) return true; return false; }
    private static String text(String value) { return value == null ? "" : value; }
}
