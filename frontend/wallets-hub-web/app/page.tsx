"use client";

import Image from "next/image";
import Link from "next/link";
import { ArrowRight, BarChart3, BellRing, Check, MessageSquareText, ShieldCheck, Smartphone, UsersRound, WalletCards } from "lucide-react";
import { LanguageToggle, useI18n } from "@/components/i18n";
import { appPath } from "@/lib/api";

const planFeatures = ["Full Wallets Hub access", "Unlimited wallet monitoring", "Employee access controls", "Advanced reports and exports", "Android capture app"];

export default function LandingPage() {
  const { t, locale } = useI18n();
  return <main className="landing-page">
    <nav className="landing-nav" aria-label="Main navigation">
      <Link href="/" className="landing-brand"><Image src={appPath("/wallets-hub-logo.png")} width={46} height={46} alt="Wallets Hub"/><strong>Wallets Hub</strong></Link>
      <div className="landing-nav-actions"><LanguageToggle/><Link className="btn btn-secondary" href="/login">{t("Sign in")}</Link></div>
    </nav>

    <section className="landing-hero">
      <div className="landing-hero-copy">
        <span className="landing-kicker"><span className="status-dot"/>{t("Built for real payment operations")}</span>
        <h1>{t("Wallet operations without blind spots.")}</h1>
        <p>{t("Capture Vodafone Cash and InstaPay SMS messages and give every employee exactly the access they need.")}</p>
        <div className="landing-cta"><Link className="btn landing-primary" href="/login">{t("Start managing")}<ArrowRight size={18}/></Link><a className="btn btn-secondary" href="#pricing">{t("See pricing")}</a></div>
        <div className="landing-trust"><span><ShieldCheck size={17}/>{t("Organization-isolated")}</span><span><BellRing size={17}/>{t("Instant reporting")}</span></div>
      </div>
      <div className="product-preview" aria-label="Wallets Hub product preview">
        <div className="preview-top"><div><span/><span/><span/></div><strong>{t("Live operations")}</strong><span className="badge success">{t("Live capture")}</span></div>
        <div className="preview-stats"><article><small>{t("Received today · EGP")}</small><strong>EGP 24,850</strong><span>+18.4%</span></article><article><small>{t("Payments")}</small><strong>127</strong><span>{t("Today")}</span></article></div>
        <div className="preview-list"><div><span className="preview-icon"><MessageSquareText/></span><p><strong>EGP 1,250</strong><small>Vodafone Cash · 010••••4687</small></p><time>10:28</time></div><div><span className="preview-icon"><Smartphone/></span><p><strong>EGP 780</strong><small>InstaPay · Branch device</small></p><time>10:21</time></div><div><span className="preview-icon"><WalletCards/></span><p><strong>EGP 420</strong><small>Vodafone Cash · Main wallet</small></p><time>10:16</time></div></div>
      </div>
    </section>

    <section className="landing-section" id="features">
      <div className="landing-section-heading"><span className="eyebrow">Wallets Hub</span><h2>{t("Every receipt. Every device. One clear view.")}</h2></div>
      <div className="landing-feature-grid">
        <article><span><Smartphone/></span><h3>{t("Automatic capture")}</h3><p>{t("Vodafone Cash and InstaPay payments captured directly from SMS.")}</p></article>
        <article><span><UsersRound/></span><h3>{t("Controlled access")}</h3><p>{t("Assign all wallets or only selected wallets to each employee.")}</p></article>
        <article><span><BarChart3/></span><h3>{t("Operational reports")}</h3><p>{t("Filter, export, reconcile, and understand every received payment.")}</p></article>
      </div>
    </section>

    <section className="landing-pricing" id="pricing">
      <div className="landing-section-heading"><span className="eyebrow">{t("Simple pricing")}</span><h2>{t("Choose the plan that fits your operation.")}</h2></div>
      <div className="pricing-grid">
        <Plan name={t("Monthly")} price="250" cadence={t("per month")} action={t("Get started")} t={t}/>
        <Plan name={t("Yearly")} price="1,650" cadence={t("per year")} action={t("Get started")} t={t} featured badge={t("Save EGP 1,350 every year")}/>
      </div>
    </section>

    <section className="landing-final"><div><h2>{t("Already have an account?")}</h2><p>{t("Open your workspace")}</p></div><Link className="btn" href="/login">{t("Sign in")}<ArrowRight className={locale === "ar" ? "flip-rtl" : ""} size={18}/></Link></section>
    <footer className="landing-footer"><span>© {new Date().getFullYear()} Wallets Hub</span><span>{t("Payment operations, clearly managed.")}</span></footer>
  </main>;
}

function Plan({ name, price, cadence, action, badge, featured, t }: { name: string; price: string; cadence: string; action: string; badge?: string; featured?: boolean; t: (value: string) => string }) {
  return <article className={`pricing-card ${featured ? "featured" : ""}`}>{badge && <span className="pricing-badge">{badge}</span>}<h3>{name}</h3><div className="price"><span>{t("EGP")}</span><strong>{price}</strong><small>{cadence}</small></div><ul>{planFeatures.map(feature => <li key={feature}><Check size={17}/>{t(feature)}</li>)}</ul><Link href="/login" className={`btn btn-wide ${featured ? "" : "btn-secondary"}`}>{action}<ArrowRight size={17}/></Link></article>;
}
