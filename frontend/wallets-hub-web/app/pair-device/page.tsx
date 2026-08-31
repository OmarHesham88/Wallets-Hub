"use client";
import Image from "next/image";
import { FormEvent, useEffect, useState } from "react";
import { ArrowRight, CheckCircle2, LayoutDashboard, LogIn, RefreshCw, ShieldCheck, Smartphone } from "lucide-react";
import { api, appPath, User } from "@/lib/api";
import { CaptureStatus, isNative, WalletCapture } from "@/lib/wallet-native";
type PairResponse = {
  deviceId: string;
  deviceToken: string;
  wallets: {
    id: string;
    name: string;
    provider: string;
    accountNumber: string;
    currencyCode: string;
  }[];
};
export default function PairDevicePage() {
  const [status, setStatus] = useState<CaptureStatus>();
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [smsResult, setSmsResult] = useState("");
  const [busy, setBusy] = useState(false);
  const [accountDestination, setAccountDestination] = useState("/login");
  const refresh = () =>
    WalletCapture.getStatus()
      .then(setStatus)
      .catch(() => setStatus(undefined));
  useEffect(() => {
    if (!isNative()) return;
    void refresh();
    fetch(appPath("/api/auth/me"), { credentials: "include" })
      .then(async (response) => response.ok ? response.json() as Promise<User> : undefined)
      .then((user) => {
        if (user) setAccountDestination(user.role === "PlatformAdmin" ? "/platform" : "/dashboard");
      })
      .catch(() => undefined);
  }, []);
  async function pair(e: FormEvent) {
    e.preventDefault();
    if (!status) return;
    setBusy(true);
    setError("");
    try {
      const result = await api<PairResponse>("/api/devices/pair", {
        method: "POST",
        body: JSON.stringify({
          pairingCode: code,
          installationId: status.installationId,
        }),
      });
      await WalletCapture.configure({
        deviceId: result.deviceId,
        deviceToken: result.deviceToken,
      });
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function enableSms() {
    setBusy(true);
    setError("");
    try {
      const permission = await WalletCapture.requestPermissions({ permissions: ["sms"] });
      if (permission.sms !== "granted") throw new Error("SMS access was not allowed. Please enable it in App settings.");
      const result = await WalletCapture.scanRecentSms();
      setSmsResult(`Checked ${result.checked} recent SMS messages and found ${result.matched} Vodafone Cash/InstaPay receipt(s).`);
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function scanSms() {
    setBusy(true);
    setError("");
    setSmsResult("");
    try {
      const result = await WalletCapture.scanRecentSms();
      setSmsResult(`Checked ${result.checked} recent SMS messages and found ${result.matched} Vodafone Cash/InstaPay receipt(s).`);
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  if (!isNative())
    return (
      <main className="login-panel">
        <div className="login-card">
          <Image
            src={appPath("/wallets-hub-logo.png")}
            width={64}
            height={64}
            alt="Wallets Hub"
          />
          <h2>Wallets Hub capture app</h2>
          <p>
            This secure pairing screen is available inside the Wallets Hub
            Android application.
          </p>
          <a className="btn" href={appPath("/login")}>
            Open web dashboard
          </a>
        </div>
      </main>
    );
  return (
    <main className="login-panel">
      <div className="login-card">
        <div className="login-brand">
          <Image
            src={appPath("/wallets-hub-logo.png")}
            width={58}
            height={58}
            alt="Wallets Hub"
          />
          <div>
            <strong>Wallets Hub</strong>
            <span>Secure capture device</span>
          </div>
        </div>
        <div className="notice" style={{ marginTop: 24 }}>
          <div className="card-top" style={{ gap: 14 }}>
            <div>
              <strong>{accountDestination === "/login" ? "Manage your Wallets Hub" : "Your management account"}</strong>
              <p className="muted" style={{ margin: "4px 0 0" }}>
                {accountDestination === "/login" ? "Sign in to access wallets, employees, payments, and reports." : "Open the complete dashboard, reports, wallets, and team controls."}
              </p>
            </div>
            <a className="btn btn-small" href={appPath(accountDestination)}>
              {accountDestination === "/login" ? <LogIn size={16}/> : <LayoutDashboard size={16}/>}
              {accountDestination === "/login" ? "Sign in" : "Dashboard"}
              <ArrowRight size={15}/>
            </a>
          </div>
        </div>
        {!status?.paired ? (
          <form onSubmit={pair} style={{ marginTop: 35 }}>
            <span className="eyebrow">Device pairing</span>
            <h2>Connect this phone</h2>
            <p>
              Create a pairing code from Dashboard → Devices, then enter it
              here.
            </p>
            <label>
              Six-digit pairing code
              <input
                value={code}
                onChange={(e) =>
                  setCode(e.target.value.replace(/\D/g, "").slice(0, 6))
                }
                inputMode="numeric"
                pattern="[0-9]{6}"
                required
                style={{
                  fontSize: "1.6rem",
                  letterSpacing: ".2em",
                  textAlign: "center",
                }}
              />
            </label>
            {error && <div className="error">{error}</div>}
            <button
              className="btn btn-wide"
              disabled={busy || code.length !== 6}
            >
              <Smartphone size={18} />
              {busy ? "Pairing…" : "Pair this phone"}
            </button>
          </form>
        ) : (
          <div style={{ marginTop: 35 }}>
            <span className="eyebrow">Capture status</span>
            <h2>This phone is paired</h2>
            <div className="notice">
              <strong>Device ID</strong>
              <br />
              {status.deviceId}
            </div>
            <div className="card" style={{ marginTop: 14 }}>
              <StatusRow
                label="Notification permission (Binance)"
                ok={status.notificationAccess}
              />
              <StatusRow label="SMS permission (Vodafone + InstaPay)" ok={status.smsAccess} />
              <StatusRow
                label="Listener connected"
                ok={Boolean(status.listenerConnectedAt)}
                value={time(status.listenerConnectedAt)}
              />
              <StatusRow
                label="Last notification seen"
                ok={Boolean(status.lastNotificationAt)}
                value={time(status.lastNotificationAt)}
              />
              <StatusRow
                label="Last SMS checked"
                ok={Boolean(status.lastSmsAt)}
                value={time(status.lastSmsAt)}
              />
              <StatusRow
                label="Last wallet match"
                ok={Boolean(status.lastWalletMatchAt)}
                value={time(status.lastWalletMatchAt)}
              />
            </div>
            {error && <div className="error" style={{ marginTop: 12 }}>{error}</div>}
            {smsResult && <div className="notice" style={{ marginTop: 12 }}>{smsResult}</div>}
            {!status.smsAccess && (
              <div className="button-row" style={{ marginTop: 12 }}>
                <button className="btn" disabled={busy} onClick={enableSms}>
                  Allow SMS access
                </button>
                <button className="btn btn-secondary" onClick={() => WalletCapture.openAppSettings()}>
                  App settings
                </button>
              </div>
            )}
            {status.smsAccess && (
              <button className="btn btn-secondary btn-wide" style={{ marginTop: 12 }} disabled={busy} onClick={scanSms}>
                Scan SMS from the last 2 days
              </button>
            )}
            {!status.notificationAccess && (
              <>
                <div className="notice" style={{ marginTop: 14 }}>
                  <strong>Android 13–16:</strong> Open App settings, use the
                  top-right menu to allow restricted settings, then enable
                  Notification access.
                </div>
                <div className="button-row" style={{ marginTop: 12 }}>
                  <button
                    className="btn"
                    onClick={() => WalletCapture.openAppSettings()}
                  >
                    1. App settings
                  </button>
                  <button
                    className="btn btn-secondary"
                    onClick={() => WalletCapture.openNotificationAccess()}
                  >
                    2. Notification access
                  </button>
                </div>
              </>
            )}
            <button
              className="btn btn-secondary btn-wide"
              style={{ marginTop: 12 }}
              onClick={refresh}
            >
              <RefreshCw size={17} />
              Refresh status
            </button>
            <p className="muted" style={{ textAlign: "center" }}>
              <ShieldCheck size={15} /> Only matching incoming-payment SMS or
              notifications are uploaded.
            </p>
          </div>
        )}
      </div>
    </main>
  );
}
function StatusRow({
  label,
  ok,
  value,
}: {
  label: string;
  ok: boolean;
  value?: string;
}) {
  return (
    <div
      className="card-top"
      style={{ padding: "10px 0", borderBottom: "1px solid var(--line)" }}
    >
      <span>{label}</span>
      <span className={`badge ${ok ? "success" : "danger"}`}>
        <CheckCircle2 size={12} />
        {value ?? (ok ? "Ready" : "Required")}
      </span>
    </div>
  );
}
function time(value?: number) {
  return value ? new Date(value).toLocaleString() : "Not yet";
}
