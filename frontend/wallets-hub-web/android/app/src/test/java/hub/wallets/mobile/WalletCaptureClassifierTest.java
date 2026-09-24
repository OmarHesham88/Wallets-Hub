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

    @Test public void recognizesOrangeCashSms() {
        assertTrue(WalletCaptureClassifier.isSmsCandidate("OrangeCash", "تم استلام مبلغ ٢٥٠ جنيه من رقم 01234567890 رقم العملية 887766"));
    }

    @Test public void recognizesEandCashSms() {
        assertTrue(WalletCaptureClassifier.isSmsCandidate("e& money", "Your wallet was credited with EGP 80 from 01123456789"));
    }

    @Test public void recognizesIncomingAxisNotification() {
        assertTrue(WalletCaptureClassifier.isAxisNotification("com.axispay.consumer.wallet", "Money received", "You have received EGP 250.50 from 01012345678"));
    }

    @Test public void rejectsAxisOutgoingAndWrongPackageNotifications() {
        assertFalse(WalletCaptureClassifier.isAxisNotification("com.axispay.consumer.wallet", "Transfer complete", "You sent EGP 250 to 01012345678"));
        assertFalse(WalletCaptureClassifier.isAxisNotification("com.other.wallet", "Money received", "You have received EGP 250 from 01012345678"));
    }

}
