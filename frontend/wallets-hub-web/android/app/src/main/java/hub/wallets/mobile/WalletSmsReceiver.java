package hub.wallets.mobile;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.provider.Telephony;
import android.telephony.SmsMessage;

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
        if (!WalletCaptureClassifier.isSmsCandidate(sender, body)) return false;
        return WalletCaptureQueue.enqueue(context, "android.sms", sender, body, receivedAt, sender);
    }

    private static String text(String value) { return value == null ? "" : value; }
}
