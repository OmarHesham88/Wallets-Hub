package hub.wallets.mobile;

import android.Manifest;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.database.Cursor;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import android.provider.Telephony;
import android.service.notification.NotificationListenerService;
import androidx.core.content.ContextCompat;
import androidx.core.app.NotificationManagerCompat;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import com.getcapacitor.annotation.Permission;
import java.util.Set;
import java.util.UUID;

@CapacitorPlugin(name = "WalletCapture", permissions = {
    @Permission(strings = { Manifest.permission.RECEIVE_SMS, Manifest.permission.READ_SMS }, alias = "sms")
})
public class WalletCapturePlugin extends Plugin {
    static final String PREFS = "wallets_hub_capture";
    static final String INSTALLATION_ID = "installation_id";
    static final String DEVICE_ID = "device_id";
    static final String DEVICE_TOKEN = "device_token";
    static final String API_URL = "api_url";
    static final String LISTENER_CONNECTED_AT = "listener_connected_at";
    static final String LAST_NOTIFICATION_AT = "last_notification_at";
    static final String LAST_WALLET_MATCH_AT = "last_wallet_match_at";
    static final String LAST_SMS_AT = "last_sms_at";

    static SharedPreferences prefs(Context context) { return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE); }
    static String installationId(Context context) {
        SharedPreferences preferences = prefs(context);
        String value = preferences.getString(INSTALLATION_ID, null);
        if (value == null) { value = UUID.randomUUID().toString(); preferences.edit().putString(INSTALLATION_ID, value).apply(); }
        return value;
    }

    @PluginMethod
    public void getStatus(PluginCall call) {
        SharedPreferences preferences = prefs(getContext());
        boolean access = NotificationManagerCompat.getEnabledListenerPackages(getContext()).contains(getContext().getPackageName());
        JSObject result = new JSObject();
        result.put("installationId", installationId(getContext()));
        result.put("deviceName", (Build.MANUFACTURER + " " + Build.MODEL).trim());
        result.put("paired", preferences.contains(DEVICE_TOKEN));
        result.put("deviceId", preferences.getString(DEVICE_ID, null));
        result.put("notificationAccess", access);
        result.put("smsAccess", ContextCompat.checkSelfPermission(getContext(), Manifest.permission.RECEIVE_SMS) == PackageManager.PERMISSION_GRANTED
            && ContextCompat.checkSelfPermission(getContext(), Manifest.permission.READ_SMS) == PackageManager.PERMISSION_GRANTED);
        result.put("listenerConnectedAt", preferences.getLong(LISTENER_CONNECTED_AT, 0));
        result.put("lastNotificationAt", preferences.getLong(LAST_NOTIFICATION_AT, 0));
        result.put("lastSmsAt", preferences.getLong(LAST_SMS_AT, 0));
        result.put("lastWalletMatchAt", preferences.getLong(LAST_WALLET_MATCH_AT, 0));
        if (access && preferences.contains(DEVICE_TOKEN)) NotificationListenerService.requestRebind(new ComponentName(getContext(), WalletNotificationListener.class));
        call.resolve(result);
    }

    @PluginMethod
    public void configure(PluginCall call) {
        String deviceId = call.getString("deviceId"); String token = call.getString("deviceToken");
        if (deviceId == null || token == null) { call.reject("Pairing response is incomplete."); return; }
        prefs(getContext()).edit().putString(DEVICE_ID, deviceId).putString(DEVICE_TOKEN, token).putString(API_URL, "https://servicehub.ink/wallets").apply();
        call.resolve();
    }

    @PluginMethod
    public void clearPairing(PluginCall call) {
        prefs(getContext()).edit().remove(DEVICE_ID).remove(DEVICE_TOKEN).apply(); call.resolve();
    }

    @PluginMethod
    public void openNotificationAccess(PluginCall call) {
        ComponentName component = new ComponentName(getContext(), WalletNotificationListener.class);
        Intent intent = Build.VERSION.SDK_INT >= Build.VERSION_CODES.R ? new Intent(Settings.ACTION_NOTIFICATION_LISTENER_DETAIL_SETTINGS) : new Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) intent.putExtra(Settings.EXTRA_NOTIFICATION_LISTENER_COMPONENT_NAME, component.flattenToString());
        if (intent.resolveActivity(getContext().getPackageManager()) == null) intent = new Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS);
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK); getContext().startActivity(intent); call.resolve();
    }

    @PluginMethod
    public void openAppSettings(PluginCall call) {
        Intent intent = new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:" + getContext().getPackageName()));
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK); getContext().startActivity(intent); call.resolve();
    }

    @PluginMethod
    public void scanRecentSms(PluginCall call) {
        if (ContextCompat.checkSelfPermission(getContext(), Manifest.permission.READ_SMS) != PackageManager.PERMISSION_GRANTED) {
            call.reject("SMS access is required."); return;
        }
        int checked = 0; int matched = 0;
        long cutoff = System.currentTimeMillis() - 2L * 24 * 60 * 60 * 1000;
        String[] columns = { Telephony.Sms.ADDRESS, Telephony.Sms.BODY, Telephony.Sms.DATE };
        try (Cursor cursor = getContext().getContentResolver().query(
            Telephony.Sms.Inbox.CONTENT_URI, columns, Telephony.Sms.DATE + " >= ?",
            new String[] { Long.toString(cutoff) }, Telephony.Sms.DATE + " DESC")) {
            if (cursor != null) while (cursor.moveToNext() && checked < 200) {
                String sender = cursor.getString(0); String body = cursor.getString(1); long receivedAt = cursor.getLong(2);
                checked++;
                if (WalletSmsReceiver.capture(getContext(), sender, body, receivedAt)) matched++;
            }
            JSObject result = new JSObject(); result.put("checked", checked); result.put("matched", matched); call.resolve(result);
        } catch (Exception exception) { call.reject("Could not scan recent SMS messages.", exception); }
    }
}
