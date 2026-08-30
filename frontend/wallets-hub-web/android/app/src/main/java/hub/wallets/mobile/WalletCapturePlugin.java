package hub.wallets.mobile;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import android.service.notification.NotificationListenerService;
import androidx.core.app.NotificationManagerCompat;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import java.util.Set;
import java.util.UUID;

@CapacitorPlugin(name = "WalletCapture")
public class WalletCapturePlugin extends Plugin {
    static final String PREFS = "wallets_hub_capture";
    static final String INSTALLATION_ID = "installation_id";
    static final String DEVICE_ID = "device_id";
    static final String DEVICE_TOKEN = "device_token";
    static final String API_URL = "api_url";
    static final String LISTENER_CONNECTED_AT = "listener_connected_at";
    static final String LAST_NOTIFICATION_AT = "last_notification_at";
    static final String LAST_WALLET_MATCH_AT = "last_wallet_match_at";

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
        result.put("listenerConnectedAt", preferences.getLong(LISTENER_CONNECTED_AT, 0));
        result.put("lastNotificationAt", preferences.getLong(LAST_NOTIFICATION_AT, 0));
        result.put("lastWalletMatchAt", preferences.getLong(LAST_WALLET_MATCH_AT, 0));
        if (access && preferences.contains(DEVICE_TOKEN)) NotificationListenerService.requestRebind(new ComponentName(getContext(), WalletNotificationListener.class));
        call.resolve(result);
    }

    @PluginMethod
    public void configure(PluginCall call) {
        String deviceId = call.getString("deviceId"); String token = call.getString("deviceToken");
        if (deviceId == null || token == null) { call.reject("Pairing response is incomplete."); return; }
        prefs(getContext()).edit().putString(DEVICE_ID, deviceId).putString(DEVICE_TOKEN, token).putString(API_URL, "https://wallets.servicehub.ink").apply();
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
}
