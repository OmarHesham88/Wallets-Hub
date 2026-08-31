"use client";

import { useQuery } from "@tanstack/react-query";
import { BarChart3, Download, TrendingUp } from "lucide-react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Shell } from "@/components/shell";
import { api, appPath, money } from "@/lib/api";

type Summary = {
  from: string;
  to: string;
  totals: { currencyCode: string; count: number; amount: number }[];
  wallets: { walletId: string; walletName: string; currencyCode: string; count: number; amount: number }[];
  daily: { day: string; currencyCode: string; count: number; amount: number }[];
};

export default function ReportsPage() {
  const report = useQuery({ queryKey: ["report-summary"], queryFn: () => api<Summary>("/api/reports/summary") });
  const data = report.data;
  const received = (currency: string) => data?.totals.filter((item) => item.currencyCode === currency).reduce((sum, item) => sum + item.amount, 0) ?? 0;
  return <Shell>
    <div className="page-head"><div><span className="eyebrow">Operational intelligence</span><h1>Reports</h1><p>All received payments are included immediately. EGP, USD, and USDT are reported separately.</p></div><div className="button-row"><a className="btn btn-secondary" href={appPath("/api/reports/export.xlsx")}><Download size={17}/>Excel</a><button className="btn btn-secondary" onClick={() => window.print()}><Download size={17}/>PDF / Print</button></div></div>
    <div className="stats"><div className="stat"><TrendingUp/><span>Received EGP</span><strong>{money(received("EGP"), "EGP")}</strong></div><div className="stat"><TrendingUp/><span>Received USD</span><strong>{money(received("USD"), "USD")}</strong></div><div className="stat"><TrendingUp/><span>Received USDT</span><strong>{money(received("USDT"), "USDT")}</strong></div><div className="stat"><BarChart3/><span>Total payments</span><strong>{data?.totals.reduce((sum, item) => sum + item.count, 0) ?? 0}</strong></div></div>
    <div className="grid two"><section className="panel"><span className="eyebrow">Daily received value</span><h2>EGP movement</h2><div style={{ height: 300 }}><ResponsiveContainer width="100%" height="100%"><BarChart data={(data?.daily ?? []).filter((item) => item.currencyCode === "EGP")}><CartesianGrid strokeDasharray="3 3" vertical={false}/><XAxis dataKey="day" tickFormatter={(value) => new Date(value).toLocaleDateString(undefined, { month: "short", day: "numeric" })}/><YAxis/><Tooltip/><Bar dataKey="amount" fill="#147a52" radius={[6, 6, 0, 0]}/></BarChart></ResponsiveContainer></div></section><section className="panel"><span className="eyebrow">Wallet comparison</span><h2>Received totals</h2>{(data?.wallets ?? []).map((wallet) => <div className="card-top" style={{ padding: "11px 0", borderBottom: "1px solid var(--line)" }} key={`${wallet.walletId}-${wallet.currencyCode}`}><div><strong>{wallet.walletName}</strong><p className="muted">{wallet.count} payments · {wallet.currencyCode}</p></div><strong>{money(wallet.amount, wallet.currencyCode)}</strong></div>)}</section></div>
  </Shell>;
}
