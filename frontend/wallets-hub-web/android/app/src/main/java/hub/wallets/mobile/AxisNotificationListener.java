package hub.wallets.mobile;

import android.app.Notification;
import android.os.Bundle;
import android.service.notification.NotificationListenerService;
import android.service.notification.StatusBarNotification;
import java.util.ArrayList;
import java.util.List;

public class AxisNotificationListener extends NotificationListenerService {
    @Override public void onNotificationPosted(StatusBarNotification notification) {
        if (notification == null || !WalletCaptureClassifier.AXIS_PACKAGE.equals(notification.getPackageName())) return;
        Bundle extras = notification.getNotification().extras;
        String title = first(extras.getCharSequence(Notification.EXTRA_TITLE), extras.getCharSequence(Notification.EXTRA_TITLE_BIG));
        String body = notificationBody(extras);
        WalletCapturePlugin.prefs(this).edit().putLong(WalletCapturePlugin.LAST_AXIS_NOTIFICATION_AT, System.currentTimeMillis()).apply();
        if (!WalletCaptureClassifier.isAxisNotification(notification.getPackageName(), title, body)) return;
        WalletCaptureQueue.enqueue(this, notification.getPackageName(), title, body, notification.getPostTime(), notification.getKey());
    }

    private static String notificationBody(Bundle extras) {
        List<String> parts = new ArrayList<>();
        add(parts, extras.getCharSequence(Notification.EXTRA_TEXT));
        add(parts, extras.getCharSequence(Notification.EXTRA_BIG_TEXT));
        add(parts, extras.getCharSequence(Notification.EXTRA_SUB_TEXT));
        CharSequence[] lines = extras.getCharSequenceArray(Notification.EXTRA_TEXT_LINES);
        if (lines != null) for (CharSequence line : lines) add(parts, line);
        return String.join("\n", parts);
    }

    private static String first(CharSequence... values) { for (CharSequence value : values) if (value != null && value.length() > 0) return value.toString(); return ""; }
    private static void add(List<String> parts, CharSequence value) { if (value != null && value.length() > 0 && !parts.contains(value.toString())) parts.add(value.toString()); }
}
