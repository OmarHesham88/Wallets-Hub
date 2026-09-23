package hub.wallets.mobile;

import android.Manifest;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.PowerManager;
import androidx.annotation.NonNull;
import androidx.core.content.ContextCompat;
import androidx.work.Constraints;
import androidx.work.ExistingPeriodicWorkPolicy;
import androidx.work.NetworkType;
import androidx.work.OneTimeWorkRequest;
import androidx.work.PeriodicWorkRequest;
import androidx.work.ExistingWorkPolicy;
import androidx.work.WorkManager;
import androidx.work.WorkInfo;
import androidx.work.Worker;
import androidx.work.WorkerParameters;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.List;
import java.util.TimeZone;
import java.util.concurrent.TimeUnit;
import org.json.JSONObject;

public class WalletHeartbeatWorker extends Worker {
    public WalletHeartbeatWorker(@NonNull Context context, @NonNull WorkerParameters parameters) { super(context, parameters); }

    static void schedule(Context context) {
        Constraints constraints = new Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build();
        PeriodicWorkRequest work = new PeriodicWorkRequest.Builder(WalletHeartbeatWorker.class, 15, TimeUnit.MINUTES).setConstraints(constraints).build();
        WorkManager.getInstance(context).enqueueUniquePeriodicWork("wallets-hub-heartbeat", ExistingPeriodicWorkPolicy.UPDATE, work);
        OneTimeWorkRequest immediate = new OneTimeWorkRequest.Builder(WalletHeartbeatWorker.class).setConstraints(constraints).build();
        WorkManager.getInstance(context).enqueueUniqueWork("wallets-hub-heartbeat-now", ExistingWorkPolicy.REPLACE, immediate);
    }

    @NonNull @Override public Result doWork() {
        Context context = getApplicationContext(); SharedPreferences preferences = WalletCapturePlugin.prefs(context);
        String token = preferences.getString(WalletCapturePlugin.DEVICE_TOKEN, null); String api = preferences.getString(WalletCapturePlugin.API_URL, null);
        if (token == null || api == null) return Result.success(); HttpURLConnection connection = null;
        try {
            JSONObject body = new JSONObject();
            body.put("appVersion", context.getPackageManager().getPackageInfo(context.getPackageName(), 0).versionName);
            body.put("androidVersion", Build.VERSION.RELEASE);
            List<WorkInfo> uploads = WorkManager.getInstance(context).getWorkInfosByTag("wallet-upload").get();
            int pending = 0; for (WorkInfo upload : uploads) if (!upload.getState().isFinished()) pending++;
            body.put("pendingUploadCount", pending);
            body.put("failedUploadCount", preferences.getInt(WalletCapturePlugin.FAILED_UPLOADS, 0));
            body.put("smsPermissionGranted", ContextCompat.checkSelfPermission(context, Manifest.permission.RECEIVE_SMS) == PackageManager.PERMISSION_GRANTED && ContextCompat.checkSelfPermission(context, Manifest.permission.READ_SMS) == PackageManager.PERMISSION_GRANTED);
            PowerManager power = (PowerManager) context.getSystemService(Context.POWER_SERVICE);
            body.put("batteryOptimizationIgnored", power != null && power.isIgnoringBatteryOptimizations(context.getPackageName()));
            putTime(body, "lastSmsAtUtc", preferences.getLong(WalletCapturePlugin.LAST_SMS_AT, 0));
            byte[] payload = body.toString().getBytes(StandardCharsets.UTF_8);
            connection = (HttpURLConnection) new URL(api + "/api/devices/heartbeat").openConnection(); connection.setRequestMethod("POST"); connection.setConnectTimeout(15000); connection.setReadTimeout(15000); connection.setDoOutput(true); connection.setRequestProperty("Content-Type", "application/json"); connection.setRequestProperty("X-Wallet-Device-Token", token); connection.setFixedLengthStreamingMode(payload.length);
            try (OutputStream stream = connection.getOutputStream()) { stream.write(payload); }
            int status = connection.getResponseCode();
            if (status == 401) preferences.edit().remove(WalletCapturePlugin.DEVICE_TOKEN).remove(WalletCapturePlugin.DEVICE_ID).apply();
            return status >= 200 && status < 300 ? Result.success() : status >= 500 || status == 429 ? Result.retry() : Result.failure();
        } catch (Exception exception) { return getRunAttemptCount() < 6 ? Result.retry() : Result.failure(); }
        finally { if (connection != null) connection.disconnect(); }
    }

    private static void putTime(JSONObject body, String name, long value) throws Exception { if (value <= 0) { body.put(name, JSONObject.NULL); return; } SimpleDateFormat format = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.ROOT); format.setTimeZone(TimeZone.getTimeZone("UTC")); body.put(name, format.format(new Date(value))); }
}
