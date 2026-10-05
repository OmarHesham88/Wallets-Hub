"use client";

import Image from "next/image";
import Link from "next/link";
import { FormEvent, useState } from "react";
import { ArrowLeft, ArrowRight, CheckCircle2 } from "lucide-react";
import { appPath } from "@/lib/api";
import { LanguageToggle, useI18n } from "@/components/i18n";

export default function SignupPage() {
  const { t } = useI18n();
  const [companyName, setCompanyName] = useState("");
  const [ownerName, setOwnerName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [created, setCreated] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const response = await fetch(appPath("/api/auth/signup"), {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ companyName, ownerName, email, password }),
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(body.error ?? body.title ?? "Account creation failed.");
      setCreated(true);
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return <main className="login-page signup-page">
    <div className="login-language"><LanguageToggle/></div>
    <section className="login-intro">
      <div className="login-brand"><span className="login-mark"><Image src={appPath("/active-cash-logo.svg")} width={52} height={52} alt="Active Cash"/></span><div><strong>Active Cash</strong><span>{t("Payment operations, clearly managed.")}</span></div></div>
      <div className="hero-copy"><span className="eyebrow">{t("Create your workspace")}</span><h1>{t("Your wallet operation starts here.")}</h1><p>{t("Create your owner account now. Your workspace will be ready to use as soon as its subscription is activated.")}</p></div>
    </section>
    <section className="login-panel">
      <form className="login-card" onSubmit={submit}>
        <div className="login-mobile-brand"><span className="login-mark"><Image src={appPath("/active-cash-logo.svg")} width={52} height={52} alt=""/></span><div><strong>Active Cash</strong><span>{t("Payment operations, clearly managed.")}</span></div></div>
        {created ? <div className="signup-success"><CheckCircle2/><h2>{t("Workspace created")}</h2><p>{t("Your account is awaiting subscription activation. You can sign in as soon as your Active Cash administrator activates it.")}</p><Link className="btn btn-wide" href="/login">{t("Go to sign in")}<ArrowRight size={18}/></Link></div> : <>
          <span className="eyebrow">{t("New workspace")}</span><h2>{t("Create your account")}</h2><p>{t("Enter your business and owner details. No payment is taken during signup.")}</p>
          <label>{t("Business name")}<input required minLength={2} maxLength={120} value={companyName} onChange={event => setCompanyName(event.target.value)} autoComplete="organization"/></label>
          <label>{t("Your full name")}<input required minLength={2} maxLength={120} value={ownerName} onChange={event => setOwnerName(event.target.value)} autoComplete="name"/></label>
          <label>{t("Email address")}<input required type="email" value={email} onChange={event => setEmail(event.target.value)} autoComplete="email" placeholder="name@company.com"/></label>
          <label>{t("Password")}<input required type="password" minLength={8} value={password} onChange={event => setPassword(event.target.value)} autoComplete="new-password"/><small className="field-hint">{t("Use at least 8 characters.")}</small></label>
          {error && <div className="error">{error}</div>}
          <button className="btn btn-wide" disabled={busy}>{busy ? t("Creating account…") : <>{t("Create workspace")}<ArrowRight size={18}/></>}</button>
          <Link className="login-back-link" href="/login"><ArrowLeft size={15}/>{t("Already registered? Sign in")}</Link>
        </>}
      </form>
    </section>
  </main>;
}
