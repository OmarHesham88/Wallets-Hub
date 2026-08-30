package hub.wallets.mobile;

import android.content.Context;
import android.content.SharedPreferences;
import androidx.annotation.NonNull;
import androidx.work.Worker;
import androidx.work.WorkerParameters;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import org.json.JSONObject;

public class WalletCaptureWorker extends Worker {
    public WalletCaptureWorker(@NonNull Context context, @NonNull WorkerParameters parameters) { super(context, parameters); }
    @NonNull @Override public Result doWork() {
        SharedPreferences preferences=WalletCapturePlugin.prefs(getApplicationContext());String token=preferences.getString(WalletCapturePlugin.DEVICE_TOKEN,null);String api=preferences.getString(WalletCapturePlugin.API_URL,null);if(token==null||api==null)return Result.success();HttpURLConnection connection=null;
        try { JSONObject body=new JSONObject();body.put("walletId",JSONObject.NULL);body.put("sourcePackage",getInputData().getString("sourcePackage"));body.put("title",getInputData().getString("title"));body.put("body",getInputData().getString("body"));body.put("receivedAtUtc",getInputData().getString("receivedAtUtc"));body.put("fingerprint",getInputData().getString("fingerprint"));byte[] payload=body.toString().getBytes(StandardCharsets.UTF_8);connection=(HttpURLConnection)new URL(api+"/api/captures").openConnection();connection.setRequestMethod("POST");connection.setConnectTimeout(15000);connection.setReadTimeout(15000);connection.setDoOutput(true);connection.setRequestProperty("Content-Type","application/json");connection.setRequestProperty("X-Wallet-Device-Token",token);connection.setFixedLengthStreamingMode(payload.length);try(OutputStream stream=connection.getOutputStream()){stream.write(payload);}int status=connection.getResponseCode();return status>=200&&status<500?Result.success():Result.retry(); } catch(Exception ex){return getRunAttemptCount()<6?Result.retry():Result.failure();} finally{if(connection!=null)connection.disconnect();}
    }
}
