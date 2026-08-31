package hub.wallets.mobile;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.provider.Telephony;
import android.telephony.SmsMessage;
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

public class WalletSmsReceiver extends BroadcastReceiver {
    @Override public void onReceive(Context context, Intent intent) {
        if (!Telephony.Sms.Intents.SMS_RECEIVED_ACTION.equals(intent.getAction())) return;
        SmsMessage[] messages = Telephony.Sms.Intents.getMessagesFromIntent(intent);
        if (messages == null || messages.length == 0) return;
        String sender = messages[0].getDisplayOriginatingAddress();
        long receivedAt = messages[0].getTimestampMillis();
        StringBuilder body = new StringBuilder();
        for (SmsMessage message : messages) if (message.getMessageBody() != null) body.append(message.getMessageBody());
        capture(context, sender, body.toString(), receivedAt);
    }

    static boolean capture(Context context, String sender, String body, long receivedAt) {
        if (!WalletCapturePlugin.prefs(context).contains(WalletCapturePlugin.DEVICE_TOKEN)) return false;
        WalletCapturePlugin.prefs(context).edit().putLong(WalletCapturePlugin.LAST_SMS_AT, System.currentTimeMillis()).apply();
        String content = (text(sender) + " " + text(body)).toLowerCase(Locale.ROOT);
        boolean vodafone = any(content, "vodafone cash", "vf cash", "فودافون كاش", "vf.eg/vfcash") ||
            (content.contains("تم استلام مبلغ") && content.contains("من رقم") && content.contains("محفظتك") && content.contains("رقم العملية"));
        if (!vodafone || !any(content, "received", "money received", "تم استلام", "استلمت")) return false;
        WalletCapturePlugin.prefs(context).edit().putLong(WalletCapturePlugin.LAST_WALLET_MATCH_AT, System.currentTimeMillis()).apply();
        String fingerprint = sha256("android.sms|" + receivedAt + "|" + text(sender) + "|" + text(body));
        Data input = new Data.Builder().putString("sourcePackage", "android.sms").putString("title", text(sender))
            .putString("body", text(body)).putString("receivedAtUtc", utc(receivedAt)).putString("fingerprint", fingerprint).build();
        Constraints constraints = new Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build();
        OneTimeWorkRequest work = new OneTimeWorkRequest.Builder(WalletCaptureWorker.class).setInputData(input).setConstraints(constraints).build();
        WorkManager.getInstance(context).enqueueUniqueWork("wallet-" + fingerprint, ExistingWorkPolicy.KEEP, work);
        return true;
    }

    private static boolean any(String value, String... markers) { for (String marker : markers) if (value.contains(marker)) return true; return false; }
    private static String text(String value) { return value == null ? "" : value; }
    private static String utc(long time) { SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.ROOT); format.setTimeZone(TimeZone.getTimeZone("UTC")); return format.format(new Date(time)); }
    private static String sha256(String value) { try { byte[] bytes=MessageDigest.getInstance("SHA-256").digest(value.getBytes(StandardCharsets.UTF_8));StringBuilder result=new StringBuilder();for(byte item:bytes)result.append(String.format(Locale.ROOT,"%02X",item));return result.toString(); } catch(Exception ex){return Integer.toHexString(value.hashCode());} }
}
