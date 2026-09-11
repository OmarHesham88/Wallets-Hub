"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { CircleDollarSign, Smartphone, WalletCards } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, money, User } from "@/lib/api";

type Receipt = { id: string; walletName: string; provider: string; amount: number; currencyCode: string; sender?: string; receivedAtUtc: string };
type Wallet = { id: string; name: string; provider: string; currencyCode: string; isActive: boolean };
type Dashboard = { localDate: string; timeZone: string; today: { currencyCode: string; count: number; amount: number }[]; latest: Receipt[] };

export default function DashboardPage() {
  const me = useQuery({ queryKey: ["me"], queryFn: () => api<User>("/api/auth/me") });
  const dashboard = useQuery({ queryKey: ["dashboard"], queryFn: () => api<Dashboard>("/api/dashboard"), refetchInterval: 15_000 });
  const wallets = useQuery({ queryKey: ["wallets"], queryFn: () => api<Wallet[]>("/api/wallets") });
  const rows = dashboard.data?.latest ?? []; const total = (currency: string) => dashboard.data?.today.find((item) => item.currencyCode === currency)?.amount ?? 0;

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">Live operations</span><h1>Good day, {me.data?.displayName?.split(" ")[0] ?? "there"}</h1><p>Monitor every captured payment in {dashboard.data?.timeZone ?? "your workspace time zone"}.</p></div><Link className="btn" href="/receipts"><CircleDollarSign size={18}/>View received money</Link></div>
    <div className="stats"><div className="stat"><CircleDollarSign/><span>Received today · EGP</span><strong>{money(total("EGP"), "EGP")}</strong></div><div className="stat"><CircleDollarSign/><span>Received today · USD</span><strong>{money(total("USD"), "USD")}</strong></div><div className="stat"><CircleDollarSign/><span>Received today · USDT</span><strong>{money(total("USDT"), "USDT")}</strong></div><div className="stat"><WalletCards/><span>Accessible wallets</span><strong>{(wallets.data ?? []).filter((item) => item.isActive).length}</strong></div></div>
    <div className="grid two"><section className="panel"><div className="card-top"><div><span className="eyebrow">Live payments</span><h2>Latest received money</h2></div><CircleDollarSign className="muted"/></div>{rows.slice(0, 6).map((row) => <Link href="/receipts" className="card-top dashboard-row" key={row.id}><div><strong>{money(row.amount, row.currencyCode)}</strong><p>{row.walletName} · {row.provider}</p></div><span className="badge success">Received</span></Link>)}{rows.length === 0 && <div className="empty compact"><div><CircleDollarSign/><h2>No payments yet</h2><p className="muted">New received money will appear automatically.</p></div></div>}</section>
    <section className="panel"><div className="card-top"><div><span className="eyebrow">Wallet health</span><h2>Connected coverage</h2></div><Smartphone className="muted"/></div>{(wallets.data ?? []).slice(0, 6).map((wallet) => <div className="card-top dashboard-row" key={wallet.id}><div><strong>{wallet.name}</strong><p className="muted">{wallet.provider} · {wallet.currencyCode}</p></div><span className={`badge ${wallet.isActive ? "success" : "danger"}`}>{wallet.isActive ? "Active" : "Paused"}</span></div>)}</section></div>
  </Shell>;
}
