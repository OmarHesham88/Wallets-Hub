"use client";
import Image from "next/image";
import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowRight, CheckCircle2, ShieldCheck } from "lucide-react";
import { api, User } from "@/lib/api";

export default function LoginPage() {
  const router = useRouter(); const [email, setEmail] = useState(""); const [password, setPassword] = useState(""); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) { event.preventDefault(); setBusy(true); setError(""); try { await api("/api/auth/login", { method: "POST", body: JSON.stringify({ email, password }) }); const me = await api<User>("/api/auth/me"); router.replace(me.role === "PlatformAdmin" ? "/platform" : "/dashboard"); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  return <main className="login-page"><section className="login-intro"><div className="login-brand"><Image src="/wallets-hub-logo.png" width={62} height={62} alt="Wallets Hub"/><div><strong>Wallets Hub</strong><span>Payment operations, clearly managed.</span></div></div><div className="hero-copy"><span className="eyebrow">One place for every wallet</span><h1>Know when money arrives.<br/>Know who confirmed it.</h1><p>Connect wallet phones, assign employee access, verify receipts, and understand every EGP or USD movement from a clean operational dashboard.</p><div className="feature-row"><span><ShieldCheck/>Organization-isolated</span><span><CheckCircle2/>Audited confirmation</span></div></div></section><section className="login-panel"><form className="login-card" onSubmit={submit}><span className="eyebrow">Secure workspace</span><h2>Welcome back</h2><p>Sign in with the account created by your Wallets Hub administrator.</p><label>Email address<input type="email" required value={email} onChange={e => setEmail(e.target.value)} placeholder="name@company.com"/></label><label>Password<input type="password" required value={password} onChange={e => setPassword(e.target.value)} placeholder="Your password"/></label>{error && <div className="error">{error}</div>}<button className="btn btn-wide" disabled={busy}>{busy ? "Signing in…" : <>Sign in <ArrowRight size={18}/></>}</button><small>Sessions stay securely signed in for up to one year unless you log out.</small></form></section></main>;
}
