"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { Languages } from "lucide-react";

export type Locale = "en" | "ar";

const arabic: Record<string, string> = {
  "Overview": "نظرة عامة",
  "Received money": "الأموال المستلمة",
  "Wallets": "المحافظ",
  "Balances": "الأرصدة",
  "Devices": "الأجهزة",
  "Capture inbox": "سجل الالتقاط",
  "Team & access": "الفريق والصلاحيات",
  "Reports": "التقارير",
  "Audit trail": "سجل التدقيق",
  "Notifications": "الإشعارات",
  "Settings": "الإعدادات",
  "Client organizations": "مؤسسات العملاء",
  "Account settings": "إعدادات الحساب",
  "Security": "الأمان",
  "This phone": "هذا الهاتف",
  "Wallets Hub capture app": "تطبيق الالتقاط من Wallets Hub",
  "This secure pairing screen is available inside the Wallets Hub Android application.": "شاشة الربط الآمنة هذه متاحة داخل تطبيق Wallets Hub لأندرويد.",
  "Open web dashboard": "فتح لوحة الويب",
  "Secure capture device": "جهاز التقاط آمن",
  "Manage your Wallets Hub": "إدارة Wallets Hub",
  "Your management account": "حساب الإدارة الخاص بك",
  "Sign in to access wallets, employees, payments, and reports.": "سجّل الدخول للوصول إلى المحافظ والموظفين والمدفوعات والتقارير.",
  "Open the complete dashboard, reports, wallets, and team controls.": "افتح لوحة التحكم الكاملة والتقارير والمحافظ وإدارة الفريق.",
  "Dashboard": "لوحة التحكم",
  "Device pairing": "ربط الجهاز",
  "Connect this phone": "ربط هذا الهاتف",
  "Six-digit pairing code": "رمز الربط المكوّن من ستة أرقام",
  "Platform console": "لوحة المنصة",
  "Live capture": "التقاط مباشر",
  "Log out": "تسجيل الخروج",
  "Loading…": "جارٍ التحميل…",
  "English": "English",
  "Arabic": "العربية",
  "Sign in": "تسجيل الدخول",
  "Secure workspace": "مساحة عمل آمنة",
  "Welcome back": "مرحباً بعودتك",
  "Sign in with the account created by your Wallets Hub administrator.": "سجّل الدخول بالحساب الذي أنشأه مسؤول Wallets Hub.",
  "Email address": "البريد الإلكتروني",
  "Password": "كلمة المرور",
  "Your password": "كلمة المرور الخاصة بك",
  "Signing in…": "جارٍ تسجيل الدخول…",
  "Sessions stay securely signed in for up to one year unless you log out.": "يبقى تسجيل دخولك آمناً لمدة تصل إلى سنة ما لم تسجّل الخروج.",
  "Payment operations, clearly managed.": "إدارة واضحة لعمليات الدفع.",
  "One place for every wallet": "مكان واحد لكل محافظك",
  "Know when money arrives. See it in your reports instantly.": "اعرف لحظة وصول الأموال وشاهدها فوراً في تقاريرك.",
  "Connect wallet phones, assign employee access, capture receipts, and understand every EGP movement from a clean operational dashboard.": "اربط هواتف المحافظ، وحدد صلاحيات الموظفين، والتقط عمليات الاستلام، وتابع كل حركة بالجنيه المصري من لوحة تشغيل واضحة.",
  "Organization-isolated": "بيانات منفصلة لكل مؤسسة",
  "Instant reporting": "تقارير فورية",
  "Enter the current six-digit authenticator code, or one of your recovery codes.": "أدخل رمز المصادقة الحالي المكوّن من ستة أرقام أو أحد رموز الاسترداد.",
  "Authenticator or recovery code": "رمز المصادقة أو الاسترداد",
  "Use another account": "استخدام حساب آخر",
  "Wallet operations without blind spots.": "إدارة المحافظ بلا نقاط عمياء.",
  "Capture Vodafone Cash and InstaPay SMS messages and give every employee exactly the access they need.": "التقط رسائل Vodafone Cash وInstaPay وامنح كل موظف الصلاحيات التي يحتاجها فقط.",
  "Start managing": "ابدأ الإدارة",
  "See pricing": "عرض الأسعار",
  "Built for real payment operations": "مصمم لعمليات الدفع الحقيقية",
  "Every receipt. Every device. One clear view.": "كل عملية استلام، وكل جهاز، في شاشة واحدة واضحة.",
  "Automatic capture": "التقاط تلقائي",
  "Vodafone Cash and InstaPay payments captured directly from SMS.": "التقاط مدفوعات Vodafone Cash وInstaPay مباشرة من الرسائل النصية.",
  "Controlled access": "صلاحيات محكمة",
  "Assign all wallets or only selected wallets to each employee.": "امنح كل موظف كل المحافظ أو محافظ محددة فقط.",
  "Operational reports": "تقارير تشغيلية",
  "Filter, export, reconcile, and understand every received payment.": "صفِّ البيانات وصدّرها وطابق الأرصدة وافهم كل دفعة مستلمة.",
  "Simple pricing": "أسعار بسيطة",
  "Choose the plan that fits your operation.": "اختر الخطة المناسبة لعملك.",
  "Monthly": "شهري",
  "Yearly": "سنوي",
  "per month": "شهرياً",
  "per year": "سنوياً",
  "EGP": "جنيه",
  "Save EGP 1,350 every year": "وفّر 1,350 جنيه كل سنة",
  "Full Wallets Hub access": "وصول كامل إلى Wallets Hub",
  "Unlimited wallet monitoring": "مراقبة غير محدودة للمحافظ",
  "Employee access controls": "التحكم في صلاحيات الموظفين",
  "Advanced reports and exports": "تقارير وتصدير متقدم",
  "Android capture app": "تطبيق الالتقاط لأندرويد",
  "Get started": "ابدأ الآن",
  "Already have an account?": "لديك حساب بالفعل؟",
  "Open your workspace": "افتح مساحة عملك",
  "Live operations": "العمليات المباشرة",
  "View received money": "عرض الأموال المستلمة",
  "Received today · EGP": "المستلم اليوم · جنيه",
  "Confirmed today": "المؤكد اليوم",
  "Confirmed payments today": "المدفوعات المؤكدة اليوم",
  "Accessible wallets": "المحافظ المتاحة",
  "Live payments": "المدفوعات المباشرة",
  "Latest received money": "أحدث الأموال المستلمة",
  "Received": "تم الاستلام",
  "No payments yet": "لا توجد مدفوعات بعد",
  "New received money will appear automatically.": "ستظهر الأموال المستلمة الجديدة تلقائياً.",
  "Wallet health": "حالة المحافظ",
  "Connected coverage": "التغطية المتصلة",
  "Active": "نشط",
  "Paused": "متوقف",
  "Wallet registry": "سجل المحافظ",
  "Configure receiving accounts, assignment, opening balances, limits, and operating status.": "اضبط حسابات الاستلام والتعيين والأرصدة الافتتاحية والحدود وحالة التشغيل.",
  "Add wallet": "إضافة محفظة",
  "Current balance": "الرصيد الحالي",
  "Edit": "تعديل",
  "Archive": "أرشفة",
  "No wallets yet": "لا توجد محافظ بعد",
  "Create your first receiving wallet to begin.": "أنشئ أول محفظة استلام للبدء.",
  "Edit wallet": "تعديل المحفظة",
  "New wallet": "محفظة جديدة",
  "Add receiving wallet": "إضافة محفظة استلام",
  "Wallet name": "اسم المحفظة",
  "Provider": "مزود الخدمة",
  "Phone or account number": "رقم الهاتف أو الحساب",
  "Currency": "العملة",
  "Opening balance": "الرصيد الافتتاحي",
  "Optional balance limit": "حد الرصيد (اختياري)",
  "Capturing device": "جهاز الالتقاط",
  "Assign later": "التعيين لاحقاً",
  "Wallet is active": "المحفظة نشطة",
  "Save wallet": "حفظ المحفظة",
  "Saving…": "جارٍ الحفظ…",
  "Cancel": "إلغاء",
  "Capture network": "شبكة الالتقاط",
  "Heartbeat, permissions, app version, queue health, and capture activity for every paired phone.": "راقب الاتصال والصلاحيات وإصدار التطبيق وقائمة الرفع ونشاط الالتقاط لكل هاتف مرتبط.",
  "Pair device": "ربط جهاز",
  "Online": "متصل",
  "Offline": "غير متصل",
  "Disabled": "معطل",
  "Rename": "إعادة تسمية",
  "Disable": "تعطيل",
  "Activate": "تفعيل",
  "Re-pair": "إعادة الربط",
  "Heartbeat": "آخر اتصال",
  "Last receipt capture": "آخر عملية التقاط",
  "Last SMS": "آخر رسالة SMS",
  "Last server contact": "آخر اتصال بالخادم",
  "Heartbeat unavailable": "نبض الاتصال غير متاح",
  "Setup incomplete": "الإعداد غير مكتمل",
  "Upload queue": "قائمة الرفع",
  "Battery protection": "حماية البطارية",
  "Unrestricted": "غير مقيّد",
  "Optimization may stop capture": "تحسين البطارية قد يوقف الالتقاط",
  "Not yet": "ليس بعد",
  "Pair Android device": "ربط جهاز أندرويد",
  "Create device": "إنشاء جهاز",
  "Device name": "اسم الجهاز",
  "Generate pairing code": "إنشاء رمز الربط",
  "Enter this code in the Android app": "أدخل هذا الرمز في تطبيق أندرويد",
  "Copy code": "نسخ الرمز",
  "Capture diagnostics": "تشخيص الالتقاط",
  "Every phone upload is traceable, including accepted, duplicate, ambiguous, unsupported, and rejected events.": "يمكن تتبع كل عملية رفع من الهاتف، بما فيها المقبولة والمكررة وغير الواضحة وغير المدعومة والمرفوضة.",
  "Status": "الحالة",
  "All statuses": "كل الحالات",
  "Device": "الجهاز",
  "All devices": "كل الأجهزة",
  "Payment": "الدفعة",
  "Reason": "السبب",
  "Attempts": "المحاولات",
  "Details": "التفاصيل",
  "Action": "الإجراء",
  "Not parsed": "لم يتم تحليلها",
  "Unknown provider": "مزود غير معروف",
  "Message": "الرسالة",
  "Reprocess": "إعادة المعالجة",
  "Assign": "تعيين",
  "No matching capture events": "لا توجد عمليات التقاط مطابقة",
  "Try another status or device.": "جرّب حالة أو جهازاً آخر.",
  "Previous": "السابق",
  "Next": "التالي",
  "Search": "بحث",
  "Date": "التاريخ",
  "Amount": "المبلغ",
  "Sender": "المرسل",
  "Reference": "المرجع",
  "Wallet": "المحفظة",
  "All wallets": "كل المحافظ",
  "All providers": "كل مزودي الخدمة",
  "All currencies": "كل العملات",
  "Clear filters": "مسح عوامل التصفية",
  "Newest first": "الأحدث أولاً",
  "Oldest first": "الأقدم أولاً",
  "Minimum amount": "الحد الأدنى للمبلغ",
  "Maximum amount": "الحد الأقصى للمبلغ",
  "From": "من",
  "To": "إلى",
  "Today": "اليوم",
  "7 days": "7 أيام",
  "30 days": "30 يوماً",
  "90 days": "90 يوماً",
  "Filtered Excel": "Excel حسب التصفية",
  "PDF / Print": "PDF / طباعة",
  "Operational intelligence": "التحليل التشغيلي",
  "Date-aware performance, comparisons, wallet and device breakdowns, peak hours, and data-quality diagnostics in your workspace time zone.": "أداء ومقارنات وتفصيل حسب المحفظة والجهاز وساعات الذروة وتشخيص جودة البيانات وفق توقيت مساحة عملك.",
  "Payments": "المدفوعات",
  "Average / median": "المتوسط / الوسيط",
  "Largest payment": "أكبر دفعة",
  "Accountability": "المساءلة",
  "See who changed accounts, wallets, devices, permissions, settings, and financial records.": "اعرف من غيّر الحسابات والمحافظ والأجهزة والصلاحيات والإعدادات والسجلات المالية.",
  "Everyone and system": "الجميع والنظام",
  "Actor": "المنفذ",
  "Entity": "العنصر",
  "View changes": "عرض التغييرات",
  "No audit events found": "لم يتم العثور على أحداث تدقيق",
  "Financial control": "التحكم المالي",
  "Balances & reconciliation": "الأرصدة والمطابقة",
  "Track opening balances, received payments, withdrawals, deposits, transfers, adjustments, and physical wallet checks.": "تابع الأرصدة الافتتاحية والمدفوعات المستلمة والسحب والإيداع والتحويلات والتسويات والمطابقة الفعلية.",
  "View statement": "عرض كشف الحساب",
  "Complete statement": "كشف الحساب الكامل",
  "Type": "النوع",
  "Description": "الوصف",
  "Manual wallet entry": "حركة محفظة يدوية",
  "Choose wallet": "اختر محفظة",
  "Withdrawal": "سحب",
  "Deposit": "إيداع",
  "Adjustment": "تسوية",
  "Note": "ملاحظة",
  "Save entry": "حفظ الحركة",
  "Transfer between wallets": "تحويل بين المحافظ",
  "Choose source": "اختر المصدر",
  "Choose destination": "اختر الوجهة",
  "Record transfer": "تسجيل التحويل",
  "Reconcile actual balance": "مطابقة الرصيد الفعلي",
  "Actual provider balance": "الرصيد الفعلي لدى المزود",
  "Save reconciliation": "حفظ المطابقة",
  "Manual activity": "النشاط اليدوي",
  "Recent wallet operations": "أحدث عمليات المحافظ",
  "Operational alerts": "تنبيهات التشغيل",
  "Mark all read": "تحديد الكل كمقروء",
  "No notifications yet": "لا توجد إشعارات بعد",
  "Workspace & security": "مساحة العمل والأمان",
  "Manage your profile, account security, reporting time zone, privacy, and operational alerts.": "أدر ملفك الشخصي وأمان الحساب والمنطقة الزمنية والخصوصية والتنبيهات التشغيلية.",
  "My account": "حسابي",
  "Display name": "الاسم المعروض",
  "Email": "البريد الإلكتروني",
  "Current password": "كلمة المرور الحالية",
  "New password (optional)": "كلمة مرور جديدة (اختياري)",
  "Save account": "حفظ الحساب",
  "My notification rules": "قواعد إشعاراتي",
  "Notify me for every receipt": "أبلغني بكل عملية استلام",
  "Only at or above": "فقط عند أو أعلى من",
  "No minimum": "بلا حد أدنى",
  "Daily operational summary": "ملخص تشغيلي يومي",
  "Device offline alerts": "تنبيهات انقطاع الأجهزة",
  "Save notifications": "حفظ الإشعارات",
  "Account security": "أمان الحساب",
  "Two-factor authentication protects your account even if a password is exposed.": "تحمي المصادقة الثنائية حسابك حتى في حال كشف كلمة المرور.",
  "Enable two-factor authentication": "تفعيل المصادقة الثنائية",
  "Disable two-factor authentication": "تعطيل المصادقة الثنائية",
  "Sign out other sessions": "تسجيل خروج الجلسات الأخرى",
  "Workspace controls": "إعدادات مساحة العمل",
  "Require payment confirmation": "طلب تأكيد المدفوعات",
  "New payments stay pending until an authorized team member confirms them. Reports and balances count confirmed payments only.": "تبقى المدفوعات الجديدة معلقة حتى يؤكدها موظف مخول، ولا تحتسب التقارير والأرصدة إلا المدفوعات المؤكدة.",
  "Turning this off automatically confirms any remaining pending payments.": "عند إيقاف هذا الخيار سيتم تأكيد كل المدفوعات المعلقة تلقائياً.",
  "Advanced tools": "أدوات متقدمة",
  "Diagnostics and accountability tools for administrators.": "أدوات التشخيص والمراجعة للمسؤولين.",
  "Inspect unmatched or duplicate SMS captures": "افحص الرسائل غير المطابقة أو المكررة",
  "Review account, permission, and financial actions": "راجع إجراءات الحسابات والصلاحيات والعمليات المالية",
  "Confirm pending payments": "تأكيد المدفوعات المعلقة",
  "Can confirm payments": "يمكنه تأكيد المدفوعات",
  "No payment confirmation": "لا يمكنه تأكيد المدفوعات",
  "Pending": "معلق",
  "Confirmed": "مؤكد",
  "Confirm payment": "تأكيد الدفعة",
  "Cards": "بطاقات",
  "List": "قائمة",
  "All": "الكل",
  "Reporting time zone": "المنطقة الزمنية للتقارير",
  "Mask phone numbers in original SMS messages": "إخفاء أرقام الهواتف في رسائل SMS الأصلية",
  "Save workspace": "حفظ مساحة العمل",
  "Saved successfully.": "تم الحفظ بنجاح.",
  "Language": "اللغة",
  "Interface language": "لغة الواجهة",
  "Close": "إغلاق",
  "Owner": "المالك",
  "Admin": "مسؤول",
  "Manager": "مدير",
  "Employee": "موظف",
  "PlatformAdmin": "مسؤول المنصة"
};

const reverseArabic = Object.fromEntries(Object.entries(arabic).map(([english, translated]) => [translated, english]));

type I18nValue = { locale: Locale; setLocale: (locale: Locale) => void; t: (value: string) => string };
const I18nContext = createContext<I18nValue>({ locale: "en", setLocale: () => undefined, t: (value) => value });

function translateText(value: string, locale: Locale) {
  const trimmed = value.trim();
  if (!trimmed) return value;
  const translated = locale === "ar" ? arabic[trimmed] : reverseArabic[trimmed];
  if (!translated) return value;
  return value.replace(trimmed, translated);
}

function translateElement(root: ParentNode, locale: Locale) {
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const nodes: Text[] = [];
  while (walker.nextNode()) nodes.push(walker.currentNode as Text);
  for (const node of nodes) {
    if (node.parentElement?.closest("script,style,code,pre,[data-no-translate]")) continue;
    const next = translateText(node.data, locale);
    if (next !== node.data) node.data = next;
  }
  if (root instanceof Element && root.matches("[placeholder],[title],[aria-label]")) translateAttributes(root, locale);
  root.querySelectorAll?.("[placeholder],[title],[aria-label]").forEach((element) => translateAttributes(element, locale));
}

function translateAttributes(element: Element, locale: Locale) {
  for (const attribute of ["placeholder", "title", "aria-label"]) {
    const current = element.getAttribute(attribute);
    if (current) element.setAttribute(attribute, translateText(current, locale));
  }
}

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>("en");
  useEffect(() => {
    const saved = localStorage.getItem("walletshub-language");
    const restoreLanguage = window.setTimeout(
      () => setLocaleState(saved === "ar" ? "ar" : "en"),
      0,
    );

    return () => window.clearTimeout(restoreLanguage);
  }, []);
  const setLocale = useCallback((next: Locale) => {
    localStorage.setItem("walletshub-language", next);
    setLocaleState(next);
  }, []);
  useEffect(() => {
    document.documentElement.lang = locale;
    document.documentElement.dir = locale === "ar" ? "rtl" : "ltr";
    translateElement(document.body, locale);
    const observer = new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        if (mutation.type === "characterData" && mutation.target.parentNode) translateElement(mutation.target.parentNode, locale);
        mutation.addedNodes.forEach((node) => {
          if (node instanceof Element) translateElement(node, locale);
          else if (node.nodeType === Node.TEXT_NODE && node.parentNode) translateElement(node.parentNode, locale);
        });
      }
    });
    observer.observe(document.body, { childList: true, subtree: true, characterData: true });
    return () => observer.disconnect();
  }, [locale]);
  const t = useCallback((value: string) => locale === "ar" ? arabic[value] ?? value : value, [locale]);
  const context = useMemo(() => ({ locale, setLocale, t }), [locale, setLocale, t]);
  return <I18nContext.Provider value={context}>{children}</I18nContext.Provider>;
}

export function useI18n() { return useContext(I18nContext); }

export function LanguageToggle({ compact = false }: { compact?: boolean }) {
  const { locale, setLocale } = useI18n();
  const next = locale === "en" ? "ar" : "en";
  return <button type="button" className={`language-toggle ${compact ? "compact" : ""}`} onClick={() => setLocale(next)} aria-label={locale === "en" ? "Arabic" : "English"}>
    <Languages size={17}/><span>{locale === "en" ? "العربية" : "English"}</span>
  </button>;
}
