package hub.wallets.mobile;

import android.app.Notification;
import android.os.Bundle;
import android.os.Parcelable;
import android.service.notification.NotificationListenerService;
import android.service.notification.StatusBarNotification;
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

public class WalletNotificationListener extends NotificationListenerService {
    @Override public void onNotificationPosted(StatusBarNotification notification) { process(notification); }
    @Override public void onListenerConnected() {
        super.onListenerConnected();
        WalletCapturePlugin.prefs(this).edit().putLong(WalletCapturePlugin.LISTENER_CONNECTED_AT, System.currentTimeMillis()).apply();
        try { StatusBarNotification[] active = getActiveNotifications(); if (active != null) for (StatusBarNotification item : active) process(item); } catch (SecurityException ignored) {}
    }
    @Override public void onListenerDisconnected() { super.onListenerDisconnected(); requestRebind(new android.content.ComponentName(this, WalletNotificationListener.class)); }

    private void process(StatusBarNotification notification) {
        if (!WalletCapturePlugin.prefs(this).contains(WalletCapturePlugin.DEVICE_TOKEN)) return;
        WalletCapturePlugin.prefs(this).edit().putLong(WalletCapturePlugin.LAST_NOTIFICATION_AT, System.currentTimeMillis()).apply();
        Bundle extras = notification.getNotification().extras; StringBuilder body = new StringBuilder();
        append(body, extras.getCharSequence(Notification.EXTRA_BIG_TEXT)); append(body, extras.getCharSequence(Notification.EXTRA_TEXT));
        append(body, extras.getCharSequence(Notification.EXTRA_SUB_TEXT)); append(body, extras.getCharSequence(Notification.EXTRA_SUMMARY_TEXT));
        CharSequence[] lines = extras.getCharSequenceArray(Notification.EXTRA_TEXT_LINES); if (lines != null) for (CharSequence line : lines) append(body, line);
        Parcelable[] bundles = extras.getParcelableArray(Notification.EXTRA_MESSAGES);
        if (bundles != null) for (Notification.MessagingStyle.Message message : Notification.MessagingStyle.Message.getMessagesFromBundleArray(bundles)) append(body, message.getText());
        String title = text(extras.getCharSequence(Notification.EXTRA_TITLE)); String content = (title + " " + body + " " + notification.getPackageName()).toLowerCase(Locale.ROOT);
        boolean provider = any(content, "vodafone cash", "vf cash", "فودافون كاش", "vf.eg/vfcash", "orange cash", "اورنج كاش", "اورنچ كاش", "etisalat cash", "اتصالات كاش", "e& cash", "we pay", "wepay", "وي باي", "instapay", "انستاباي", "bank transfer", "account credited");
        boolean incoming = any(content, "received", "money received", "credited", "تم استلام", "استلمت", "تم إيداع", "تم ايداع", "تم إضافة", "تم اضافة", "حوالة واردة", "تحويل وارد");
        if (!provider || !incoming) return;
        WalletCapturePlugin.prefs(this).edit().putLong(WalletCapturePlugin.LAST_WALLET_MATCH_AT, System.currentTimeMillis()).apply();
        String fingerprint = sha256(notification.getPackageName() + "|" + notification.getPostTime() + "|" + notification.getKey() + "|" + title + "|" + body);
        Data input = new Data.Builder().putString("sourcePackage", notification.getPackageName()).putString("title", title).putString("body", body.toString()).putString("receivedAtUtc", utc(notification.getPostTime())).putString("fingerprint", fingerprint).build();
        Constraints constraints = new Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build();
        OneTimeWorkRequest work = new OneTimeWorkRequest.Builder(WalletCaptureWorker.class).setInputData(input).setConstraints(constraints).build();
        WorkManager.getInstance(this).enqueueUniqueWork("wallet-" + fingerprint, ExistingWorkPolicy.KEEP, work);
    }
    private static boolean any(String value, String... markers) { for (String marker : markers) if (value.contains(marker)) return true; return false; }
    private static void append(StringBuilder builder, CharSequence value) { String next = text(value).trim(); if (next.isEmpty()) return; if (builder.length() > 0) builder.append('\n'); builder.append(next); }
    private static String text(CharSequence value) { return value == null ? "" : value.toString(); }
    private static String utc(long time) { SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.ROOT); format.setTimeZone(TimeZone.getTimeZone("UTC")); return format.format(new Date(time)); }
    private static String sha256(String value) { try { byte[] bytes=MessageDigest.getInstance("SHA-256").digest(value.getBytes(StandardCharsets.UTF_8));StringBuilder result=new StringBuilder();for(byte item:bytes)result.append(String.format(Locale.ROOT,"%02X",item));return result.toString(); } catch(Exception ex){return Integer.toHexString(value.hashCode());} }
}
