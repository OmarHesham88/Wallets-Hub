import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "hub.wallets.mobile",
  appName: "Wallets Hub",
  webDir: "out",
  backgroundColor: "#f7faf8",
  loggingBehavior: "none",
  server: { url: "https://wallets.servicehub.ink/pair-device", hostname: "app.wallets.servicehub.ink", androidScheme: "https" },
  android: { allowMixedContent: false, webContentsDebuggingEnabled: false },
};
export default config;
