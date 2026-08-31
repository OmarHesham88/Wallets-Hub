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
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const native = useIsNative();
  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await api("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ email, password }),
      });
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
          <Image
            src={appPath("/wallets-hub-logo.png")}
            width={62}
            height={62}
            alt="Wallets Hub"
          />
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
          {native && <a className="btn btn-secondary btn-small" style={{ marginBottom: 24 }} href={appPath("/pair-device")}><ArrowLeft size={15}/><Smartphone size={16}/>This phone</a>}
          <span className="eyebrow">Secure workspace</span>
          <h2>Welcome back</h2>
          <p>
            Sign in with the account created by your Wallets Hub administrator.
          </p>
          <label>
            Email address
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="name@company.com"
            />
          </label>
          <label>
            Password
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Your password"
            />
          </label>
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
