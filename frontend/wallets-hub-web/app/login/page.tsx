"use client";
import Image from "next/image";
import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowLeft, ArrowRight, CheckCircle2, ShieldCheck, Smartphone } from "lucide-react";
import { api, appPath, User } from "@/lib/api";
import { useIsNative } from "@/lib/wallet-native";
import { LanguageToggle, useI18n } from "@/components/i18n";

export default function LoginPage() {
  const { t } = useI18n();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [twoFactorCode, setTwoFactorCode] = useState("");
  const [requiresTwoFactor, setRequiresTwoFactor] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const native = useIsNative();
  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const response = await fetch(appPath("/api/auth/login"), {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password, twoFactorCode: twoFactorCode || null }),
      });
      const body = await response.json().catch(() => ({}));
      if (response.status === 409 && body.requiresTwoFactor) { setRequiresTwoFactor(true); return; }
      if (!response.ok) throw new Error(body.error ?? body.title ?? "Sign in failed.");
      const me = await api<User>("/api/auth/me");
      router.replace(me.role === "PlatformAdmin" ? "/platform" : "/dashboard");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="login-page">
      <div className="login-language"><LanguageToggle/></div>
      <section className="login-intro">
        <div className="login-brand">
          <span className="login-mark">
            <Image
              src={appPath("/wallets-hub-logo.png")}
              width={104}
              height={104}
              alt="Wallets Hub"
            />
          </span>
          <div>
            <strong>Wallets Hub</strong>
            <span>{t("Payment operations, clearly managed.")}</span>
          </div>
        </div>
        <div className="hero-copy">
          <span className="eyebrow">{t("One place for every wallet")}</span>
          <h1>
            {t("Know when money arrives. See it in your reports instantly.")}
          </h1>
          <p>
            {t("Connect wallet phones, assign employee access, capture receipts, and understand every EGP, USD, or USDT movement from a clean operational dashboard.")}
          </p>
          <div className="feature-row">
            <span>
              <ShieldCheck />
              {t("Organization-isolated")}
            </span>
            <span>
              <CheckCircle2 />
              {t("Instant reporting")}
            </span>
          </div>
        </div>
      </section>
      <section className="login-panel">
        <form className="login-card" onSubmit={submit}>
          <div className="login-mobile-brand">
            <span className="login-mark">
              <Image
                src={appPath("/wallets-hub-logo.png")}
                width={104}
                height={104}
                alt=""
              />
            </span>
            <div>
              <strong>Wallets Hub</strong>
              <span>{t("Payment operations, clearly managed.")}</span>
            </div>
          </div>
          {native && <a className="btn btn-secondary btn-small" style={{ marginBottom: 24 }} href={appPath("/pair-device")}><ArrowLeft size={15}/><Smartphone size={16}/>This phone</a>}
          <span className="eyebrow">{t("Secure workspace")}</span>
          <h2>{t("Welcome back")}</h2>
          <p>
            {t("Sign in with the account created by your Wallets Hub administrator.")}
          </p>
          {!requiresTwoFactor && <label>
            {t("Email address")}
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="name@company.com"
            />
          </label>}
          {!requiresTwoFactor && <label>
            {t("Password")}
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder={t("Your password")}
            />
          </label>}
          {requiresTwoFactor && <><div className="notice">{t("Enter the current six-digit authenticator code, or one of your recovery codes.")}</div><label>{t("Authenticator or recovery code")}<input required value={twoFactorCode} onChange={(event) => setTwoFactorCode(event.target.value.trim().slice(0, 32))} autoFocus autoComplete="one-time-code"/></label><button type="button" className="btn btn-secondary btn-small" onClick={() => { setRequiresTwoFactor(false); setTwoFactorCode(""); }}>{t("Use another account")}</button></>}
          {error && <div className="error">{error}</div>}
          <button className="btn btn-wide" disabled={busy}>
            {busy ? (
              "Signing in…"
            ) : (
              <>
                {t("Sign in")} <ArrowRight size={18} />
              </>
            )}
          </button>
          <small>
            {t("Sessions stay securely signed in for up to one year unless you log out.")}
          </small>
        </form>
      </section>
    </main>
  );
}
