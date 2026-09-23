"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, BarChart3, CalendarDays, Download, RotateCcw, TrendingDown, TrendingUp } from "lucide-react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Shell } from "@/components/shell";
import { api, appPath, money, queryString } from "@/lib/api";

type Total = { currencyCode: string; count: number; amount: number; average?: number; median?: number; maximum?: number };
type Summary = { from: string; to: string; timeZone: string; totals: Total[]; previousTotals: Total[]; wallets: { walletId: string; walletName: string; currencyCode: string; count: number; amount: number }[]; daily: { day: string; currencyCode: string; count: number; amount: number }[]; providers: { provider: string; currencyCode: string; count: number; amount: number }[]; devices: { deviceId: string; deviceName: string; currencyCode: string; count: number; amount: number }[]; hours: { hour: number; currencyCode: string; count: number; amount: number }[]; quality: { missingSender: number; unmatchedCaptures: number; duplicateCaptures: number; rejectedCaptures: number; failedUploads: number; newSenders: number; returningSenders: number } };
type Wallet = { id: string; name: string };
type Device = { id: string; name: string };

function localDateValue(date: Date) {
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
}

function dateRange(days: number) {
  const end = new Date();
  const start = new Date();
  start.setDate(end.getDate() - days + 1);
  return { from: localDateValue(start), to: localDateValue(end) };
}

export default function ReportsPage() {
  const [from, setFrom] = useState(""); const [to, setTo] = useState(""); const [walletId, setWalletId] = useState(""); const [deviceId, setDeviceId] = useState(""); const [provider, setProvider] = useState("");
  const params = useMemo(() => queryString({ from: from ? new Date(`${from}T00:00:00`).toISOString() : "", to: to ? new Date(`${to}T23:59:59.999`).toISOString() : "", walletId, deviceId, provider, currency: "EGP" }), [deviceId, from, provider, to, walletId]);
  const report = useQuery({ queryKey: ["report-summary", params], queryFn: () => api<Summary>(`/api/reports/summary${params}`), placeholderData: (previous) => previous });
  const wallets = useQuery({ queryKey: ["wallets"], queryFn: () => api<Wallet[]>("/api/wallets") }); const devices = useQuery({ queryKey: ["devices", "reports"], queryFn: () => api<Device[]>("/api/devices"), retry: false });
  const data = report.data; const currency = "EGP"; const total = data?.totals.find((item) => item.currencyCode === currency); const previous = data?.previousTotals.find((item) => item.currencyCode === currency); const change = previous?.amount ? ((total?.amount ?? 0) - previous.amount) / previous.amount * 100 : null;
  function preset(days: number) { const range = dateRange(days); setFrom(range.from); setTo(range.to); }
  function isPreset(days: number) { if (days === 30 && !from && !to) return true; const range = dateRange(days); return from === range.from && to === range.to; }
  function resetFilters() { setFrom(""); setTo(""); setWalletId(""); setDeviceId(""); setProvider(""); }
  const hasExtraFilters = Boolean(walletId || deviceId || provider || !isPreset(30));

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">Operational intelligence</span><h1>Reports</h1><p>Date-aware performance, comparisons, wallet and device breakdowns, peak hours, and data-quality diagnostics in {data?.timeZone ?? "your workspace time zone"}.</p></div><div className="button-row"><a className="btn btn-secondary" href={appPath(`/api/reports/export.xlsx${params}`)}><Download size={17}/>Filtered Excel</a><button className="btn btn-secondary" onClick={() => window.print()}><Download size={17}/>PDF / Print</button></div></div>
    <section className="panel filter-panel filter-panel-modern report-filter-panel">
      <div className="report-filter-head"><div><CalendarDays size={18}/><span>Date range</span></div><div className="preset-row">{[[1, "Today"], [7, "7 days"], [30, "30 days"], [90, "90 days"]].map(([days, label]) => <button type="button" className={`preset-chip ${isPreset(Number(days)) ? "active" : ""}`} key={String(days)} onClick={() => preset(Number(days))}>{label}</button>)}</div>{hasExtraFilters && <button type="button" className="clear-filter-button" onClick={resetFilters}><RotateCcw size={14}/>Reset</button>}</div>
      <div className="report-filter-toolbar">
        <div className="date-filter"><input aria-label="Report start date" type="date" value={from} onChange={(event) => setFrom(event.target.value)}/><span>—</span><input aria-label="Report end date" type="date" value={to} onChange={(event) => setTo(event.target.value)}/></div>
        <label className="select-control"><span>Wallet</span><select value={walletId} onChange={(event) => setWalletId(event.target.value)}><option value="">All wallets</option>{(wallets.data ?? []).map((wallet) => <option key={wallet.id} value={wallet.id}>{wallet.name}</option>)}</select></label>
        <label className="select-control"><span>Device</span><select value={deviceId} onChange={(event) => setDeviceId(event.target.value)}><option value="">All devices</option>{(devices.data ?? []).map((device) => <option key={device.id} value={device.id}>{device.name}</option>)}</select></label>
        <label className="select-control"><span>Provider</span><select value={provider} onChange={(event) => setProvider(event.target.value)}><option value="">All providers</option><option>Vodafone Cash</option><option>InstaPay</option></select></label>
      </div>
    </section>
    {report.error && <div className="error report-error"><span>{report.error.message}</span><button type="button" onClick={() => report.refetch()}>Try again</button></div>}
    <div className="stats"><div className="stat"><TrendingUp/><span>Received {currency}</span><strong>{money(total?.amount ?? 0, currency)}</strong>{change !== null && <small className={change >= 0 ? "positive" : "negative"}>{change >= 0 ? <TrendingUp size={13}/> : <TrendingDown size={13}/>} {Math.abs(change).toFixed(1)}% vs prior period</small>}</div><div className="stat"><BarChart3/><span>Payments</span><strong>{total?.count ?? 0}</strong></div><div className="stat"><BarChart3/><span>Average / median</span><strong>{money(total?.average ?? 0, currency)}</strong><small>Median {money(total?.median ?? 0, currency)}</small></div><div className="stat"><TrendingUp/><span>Largest payment</span><strong>{money(total?.maximum ?? 0, currency)}</strong></div></div>
    <div className="grid two"><section className="panel"><span className="eyebrow">Daily received value</span><h2>{currency} movement</h2><Chart data={(data?.daily ?? []).filter((item) => item.currencyCode === currency)} dataKey="day" tick={(value) => new Date(value).toLocaleDateString(undefined, { month: "short", day: "numeric" })}/></section><section className="panel"><span className="eyebrow">Peak capture hours</span><h2>Payments by local hour</h2><Chart data={(data?.hours ?? []).filter((item) => item.currencyCode === currency)} dataKey="hour" tick={(value) => `${String(value).padStart(2, "0")}:00`}/></section></div>
    <div className="grid three-report"><Breakdown title="Wallet performance" rows={(data?.wallets ?? []).filter((item) => item.currencyCode === currency).map((item) => ({ key: item.walletId, label: item.walletName, count: item.count, amount: item.amount }))} currency={currency}/><Breakdown title="Provider performance" rows={(data?.providers ?? []).filter((item) => item.currencyCode === currency).map((item) => ({ key: item.provider, label: item.provider, count: item.count, amount: item.amount }))} currency={currency}/><Breakdown title="Device performance" rows={(data?.devices ?? []).filter((item) => item.currencyCode === currency).map((item) => ({ key: item.deviceId, label: item.deviceName, count: item.count, amount: item.amount }))} currency={currency}/></div>
    <section className="panel quality-panel"><div><span className="eyebrow">Data quality</span><h2>Capture reliability</h2></div><div><AlertTriangle/><strong>{data?.quality.unmatchedCaptures ?? 0}</strong><span>Unmatched captures</span></div><div><AlertTriangle/><strong>{data?.quality.duplicateCaptures ?? 0}</strong><span>Safe duplicates</span></div><div><AlertTriangle/><strong>{data?.quality.missingSender ?? 0}</strong><span>Missing sender</span></div><div><AlertTriangle/><strong>{data?.quality.failedUploads ?? 0}</strong><span>Failed uploads</span></div><div><TrendingUp/><strong>{data?.quality.newSenders ?? 0} / {data?.quality.returningSenders ?? 0}</strong><span>New / returning senders</span></div></section>
  </Shell>;
}

function Chart({ data, dataKey, tick }: { data: { amount: number }[]; dataKey: string; tick: (value: string | number) => string }) { return <div style={{ height: 290 }}><ResponsiveContainer width="100%" height="100%"><BarChart data={data}><CartesianGrid strokeDasharray="3 3" vertical={false}/><XAxis dataKey={dataKey} tickFormatter={tick}/><YAxis/><Tooltip/><Bar dataKey="amount" fill="#147a52" radius={[6, 6, 0, 0]}/></BarChart></ResponsiveContainer></div>; }
function Breakdown({ title, rows, currency }: { title: string; rows: { key: string; label: string; count: number; amount: number }[]; currency: string }) { const total = rows.reduce((sum, row) => sum + row.amount, 0); return <section className="panel"><span className="eyebrow">Breakdown</span><h2>{title}</h2>{rows.map((row) => <div className="breakdown-row" key={row.key}><div><strong>{row.label}</strong><small>{row.count} payments · {total ? (row.amount / total * 100).toFixed(1) : 0}% share</small></div><strong>{money(row.amount, currency)}</strong></div>)}{!rows.length && <p className="muted">No data in this period.</p>}</section>; }
