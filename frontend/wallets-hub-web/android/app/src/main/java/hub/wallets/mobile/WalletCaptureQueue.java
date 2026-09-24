package hub.wallets.mobile;

import android.content.Context;
import androidx.work.Constraints;
import androidx.work.Data;
import androidx.work.ExistingWorkPolicy;
import androidx.work.NetworkType;
import androidx.work.OneTimeWorkRequest;
import androidx.work.WorkManager;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;

final class WalletCaptureQueue {
    private WalletCaptureQueue() {}

    static boolean enqueue(Context context, String sourcePackage, String title, String body, long receivedAt, String uniqueSourceId) {
        if (!WalletCapturePlugin.prefs(context).contains(WalletCapturePlugin.DEVICE_TOKEN)) return false;
        WalletCapturePlugin.prefs(context).edit().putLong(WalletCapturePlugin.LAST_WALLET_MATCH_AT, System.currentTimeMillis()).apply();
        String fingerprint = sha256(sourcePackage + "|" + uniqueSourceId + "|" + receivedAt + "|" + text(title) + "|" + text(body));
        Data input = new Data.Builder()
            .putString("sourcePackage", sourcePackage).putString("title", text(title)).putString("body", text(body))
            .putString("receivedAtUtc", utc(receivedAt)).putString("fingerprint", fingerprint).build();
        Constraints constraints = new Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build();
        OneTimeWorkRequest work = new OneTimeWorkRequest.Builder(WalletCaptureWorker.class)
            .setInputData(input).setConstraints(constraints).addTag("wallet-upload").build();
        WorkManager.getInstance(context).enqueueUniqueWork("wallet-" + fingerprint, ExistingWorkPolicy.KEEP, work);
        return true;
    }

    private static String text(String value) { return value == null ? "" : value; }
    private static String utc(long time) { SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.ROOT); format.setTimeZone(TimeZone.getTimeZone("UTC")); return format.format(new Date(time)); }
    private static String sha256(String value) { try { byte[] bytes = MessageDigest.getInstance("SHA-256").digest(value.getBytes(StandardCharsets.UTF_8)); StringBuilder result = new StringBuilder(); for (byte item : bytes) result.append(String.format(Locale.ROOT, "%02X", item)); return result.toString(); } catch (Exception exception) { return Integer.toHexString(value.hashCode()); } }
}
