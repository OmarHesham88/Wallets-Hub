import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "hub.wallets.mobile",
  appName: "Wallets Hub",
  webDir: "out",
  backgroundColor: "#f7faf8",
  loggingBehavior: "none",
  server: { url: "https://servicehub.ink/wallets/pair-device", hostname: "servicehub.ink", androidScheme: "https" },
  android: { allowMixedContent: false, webContentsDebuggingEnabled: false },
};
export default config;
