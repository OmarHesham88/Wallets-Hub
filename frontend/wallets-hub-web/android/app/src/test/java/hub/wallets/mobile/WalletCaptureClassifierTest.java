package hub.wallets.mobile;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;
import org.junit.Test;

public class WalletCaptureClassifierTest {
    @Test public void recognizesVodafoneCashSmsWithoutSenderLabel() {
        assertTrue(WalletCaptureClassifier.isSmsCandidate("Vodafone", "تم استلام مبلغ 10 جنيه من رقم 01023719913 على رقم محفظتك 01023684687 رقم العملية 023227566038"));
    }

    @Test public void recognizesInstantPaymentNetworkSms() {
        assertTrue(WalletCaptureClassifier.isSmsCandidate("19623", "تم إضافة تحويل لحظي لبطاقتكم مسبقة الدفع بمبلغ 300.00 جم من هدير ابراهيم رقم مرجعي 639896513920"));
    }

    @Test public void rejectsUnrelatedSms() {
        assertFalse(WalletCaptureClassifier.isSmsCandidate("Bank", "Your verification code is 123456"));
    }

    @Test public void recognizesBinanceUsdtNotification() {
        assertTrue(WalletCaptureClassifier.isBinanceCandidate("com.binance.dev", "You have received a payment", "You have received a payment of 19 USDT from Nadia"));
    }

    @Test public void rejectsOtherNotifications() {
        assertFalse(WalletCaptureClassifier.isBinanceCandidate("com.example", "Payment", "Received 19 EGP"));
    }
}
