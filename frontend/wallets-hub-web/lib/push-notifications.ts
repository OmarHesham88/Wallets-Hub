import { PushNotifications, type ActionPerformed, type PermissionStatus, type Token } from "@capacitor/push-notifications";
import { api, appPath } from "@/lib/api";
import { isNative, WalletCapture } from "@/lib/wallet-native";

export type AppPushStatus = "unsupported" | "prompt" | "denied" | "registering" | "enabled" | "unavailable" | "error";

let listenersReady = false;
let resolveRegistration: ((status: AppPushStatus) => void) | undefined;

async function prepareListeners() {
  if (listenersReady) return;
  listenersReady = true;
  await PushNotifications.addListener("registration", async (token: Token) => {
    try {
      const capture = await WalletCapture.getStatus();
      await api("/api/push-devices", { method: "POST", body: JSON.stringify({ token: token.value, installationId: capture.installationId, platform: "android" }) });
      resolveRegistration?.("enabled");
    } catch {
      resolveRegistration?.("error");
    }
  });
  await PushNotifications.addListener("registrationError", () => resolveRegistration?.("error"));
  await PushNotifications.addListener("pushNotificationActionPerformed", (event: ActionPerformed) => {
    const link = typeof event.notification.data?.link === "string" ? event.notification.data.link : "/receipts";
    location.assign(appPath(link.startsWith("/") ? link : "/receipts"));
  });
}

export async function appPushStatus(): Promise<AppPushStatus> {
  if (!isNative()) return "unsupported";
  try {
    const permission = await PushNotifications.checkPermissions();
    if (permission.receive === "prompt" || permission.receive === "prompt-with-rationale") return "prompt";
    if (permission.receive !== "granted") return "denied";
    const capture = await WalletCapture.getStatus();
    const server = await api<{ supported: boolean; registered: boolean }>(`/api/push-devices/status?installationId=${encodeURIComponent(capture.installationId)}`);
    if (!server.supported) return "unavailable";
    return server.registered ? "enabled" : "registering";
  } catch {
    return "error";
  }
}

export async function enableAppPushNotifications(requestPermission: boolean): Promise<AppPushStatus> {
  if (!isNative()) return "unsupported";
  try {
    let permission: PermissionStatus = await PushNotifications.checkPermissions();
    if ((permission.receive === "prompt" || permission.receive === "prompt-with-rationale") && requestPermission) permission = await PushNotifications.requestPermissions();
    if (permission.receive === "prompt" || permission.receive === "prompt-with-rationale") return "prompt";
    if (permission.receive !== "granted") return "denied";
    const capture = await WalletCapture.getStatus();
    const server = await api<{ supported: boolean }>(`/api/push-devices/status?installationId=${encodeURIComponent(capture.installationId)}`);
    if (!server.supported) return "unavailable";
    await prepareListeners();
    await PushNotifications.createChannel({ id: "payments", name: "Received payments", description: "Alerts when Wallets Hub records incoming money", importance: 5, sound: "default" });
    const result = new Promise<AppPushStatus>((resolve) => {
      resolveRegistration = resolve;
      window.setTimeout(() => resolve("error"), 15_000);
    });
    await PushNotifications.register();
    return await result;
  } catch {
    return "error";
  } finally {
    resolveRegistration = undefined;
  }
}

export async function disableAppPushNotifications() {
  if (!isNative()) return;
  try {
    const capture = await WalletCapture.getStatus();
    await api(`/api/push-devices/${encodeURIComponent(capture.installationId)}`, { method: "DELETE" });
  } finally {
    await PushNotifications.unregister().catch(() => undefined);
  }
}
