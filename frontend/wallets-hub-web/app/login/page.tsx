"use client";
import Image from "next/image";
import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowLeft, ArrowRight, CheckCircle2, ShieldCheck, Smartphone } from "lucide-react";
import { api, appPath, User } from "@/lib/api";
import { useIsNative } from "@/lib/wallet-native";

export default function LoginPage() {
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
            <span>Payment operations, clearly managed.</span>
          </div>
        </div>
        <div className="hero-copy">
          <span className="eyebrow">One place for every wallet</span>
          <h1>
            Know when money arrives.
            <br />
            See it in your reports instantly.
          </h1>
          <p>
            Connect wallet phones, assign employee access, capture receipts, and
            understand every EGP, USD, or USDT movement from a clean operational
            dashboard.
          </p>
          <div className="feature-row">
            <span>
              <ShieldCheck />
              Organization-isolated
            </span>
            <span>
              <CheckCircle2 />
              Instant reporting
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
              <span>Payment operations, clearly managed.</span>
            </div>
          </div>
          {native && <a className="btn btn-secondary btn-small" style={{ marginBottom: 24 }} href={appPath("/pair-device")}><ArrowLeft size={15}/><Smartphone size={16}/>This phone</a>}
          <span className="eyebrow">Secure workspace</span>
          <h2>Welcome back</h2>
          <p>
            Sign in with the account created by your Wallets Hub administrator.
          </p>
          {!requiresTwoFactor && <label>
            Email address
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="name@company.com"
            />
          </label>}
          {!requiresTwoFactor && <label>
            Password
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Your password"
            />
          </label>}
          {requiresTwoFactor && <><div className="notice">Enter the current six-digit authenticator code, or one of your recovery codes.</div><label>Authenticator or recovery code<input required value={twoFactorCode} onChange={(event) => setTwoFactorCode(event.target.value.trim().slice(0, 32))} autoFocus autoComplete="one-time-code"/></label><button type="button" className="btn btn-secondary btn-small" onClick={() => { setRequiresTwoFactor(false); setTwoFactorCode(""); }}>Use another account</button></>}
          {error && <div className="error">{error}</div>}
          <button className="btn btn-wide" disabled={busy}>
            {busy ? (
              "Signing in…"
            ) : (
              <>
                Sign in <ArrowRight size={18} />
              </>
            )}
          </button>
          <small>
            Sessions stay securely signed in for up to one year unless you log
            out.
          </small>
        </form>
      </section>
    </main>
  );
}
