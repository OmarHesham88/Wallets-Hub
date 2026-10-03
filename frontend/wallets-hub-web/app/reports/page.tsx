"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { DayPicker, type DateRange } from "react-day-picker";
import { AlertTriangle, BarChart3, CalendarDays, CheckCircle2, ChevronDown, Download, RotateCcw, ShieldCheck, TrendingDown, TrendingUp } from "lucide-react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Shell } from "@/components/shell";
import { api, appPath, money, queryString } from "@/lib/api";

type Total = { currencyCode: string; count: number; amount: number; average?: number; median?: number; maximum?: number };
type StatusTotal = { status: "Pending" | "Confirmed"; count: number; amount: number };
type Summary = { from: string; to: string; timeZone: string; totals: Total[]; previousTotals: Total[]; statuses: StatusTotal[]; wallets: { walletId: string; walletName: string; currencyCode: string; count: number; amount: number }[]; daily: { day: string; currencyCode: string; count: number; amount: number }[]; providers: { provider: string; currencyCode: string; count: number; amount: number }[]; devices: { deviceId: string; deviceName: string; currencyCode: string; count: number; amount: number }[]; hours: { hour: number; currencyCode: string; count: number; amount: number }[]; quality: { missingSender: number; unmatchedCaptures: number; duplicateCaptures: number; rejectedCaptures: number; failedUploads: number; newSenders: number; returningSenders: number } };
type Wallet = { id: string; name: string };
type Device = { id: string; name: string };

function localDateValue(date: Date) {
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
}

function parseLocalDate(value: string) {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day);
}

function dateRange(days: number) {
  const end = new Date();
  const start = new Date();
  start.setDate(end.getDate() - days + 1);
  return { from: localDateValue(start), to: localDateValue(end) };
}

function displayDate(value: string) {
  return parseLocalDate(value).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });
}

export default function ReportsPage() {
  const [range, setRange] = useState(() => dateRange(30));
  const [draftRange, setDraftRange] = useState<DateRange | undefined>({ from: parseLocalDate(range.from), to: parseLocalDate(range.to) });
  const [calendarOpen, setCalendarOpen] = useState(false);
  const [walletId, setWalletId] = useState("");
  const [deviceId, setDeviceId] = useState("");
  const [provider, setProvider] = useState("");
  const [status, setStatus] = useState("");
  const params = useMemo(() => queryString({ from: new Date(`${range.from}T00:00:00`).toISOString(), to: new Date(`${range.to}T23:59:59.999`).toISOString(), walletId, deviceId, provider, status, currency: "EGP" }), [deviceId, provider, range, status, walletId]);
  const report = useQuery({ queryKey: ["report-summary", params], queryFn: () => api<Summary>(`/api/reports/summary${params}`), placeholderData: (previous) => previous });
  const wallets = useQuery({ queryKey: ["wallets"], queryFn: () => api<Wallet[]>("/api/wallets") });
  const devices = useQuery({ queryKey: ["devices", "reports"], queryFn: () => api<Device[]>("/api/devices"), retry: false });
  const data = report.data;
  const currency = "EGP";
  const total = data?.totals.find((item) => item.currencyCode === currency);
  const previous = data?.previousTotals.find((item) => item.currencyCode === currency);
  const confirmed = data?.statuses.find((item) => item.status === "Confirmed");
  const pending = data?.statuses.find((item) => item.status === "Pending");
  const change = previous?.amount ? ((total?.amount ?? 0) - previous.amount) / previous.amount * 100 : null;

  function preset(days: number) {
    const next = dateRange(days);
    setRange(next);
    setDraftRange({ from: parseLocalDate(next.from), to: parseLocalDate(next.to) });
    setCalendarOpen(false);
  }

  function isPreset(days: number) {
    const expected = dateRange(days);
    return range.from === expected.from && range.to === expected.to;
  }

  function applyRange() {
    if (!draftRange?.from || !draftRange.to) return;
    setRange({ from: localDateValue(draftRange.from), to: localDateValue(draftRange.to) });
    setCalendarOpen(false);
  }

  function resetFilters() {
    preset(30);
    setWalletId("");
    setDeviceId("");
    setProvider("");
    setStatus("");
  }

  const hasExtraFilters = Boolean(walletId || deviceId || provider || status || !isPreset(30));

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">Operational intelligence</span><h1>Reports</h1><p>Compare all, confirmed, or pending payments across a clear date range in {data?.timeZone ?? "your workspace time zone"}.</p></div><div className="button-row"><a className="btn btn-secondary" href={appPath(`/api/reports/export.xlsx${params}`)}><Download size={17}/>Filtered Excel</a><button className="btn btn-secondary" onClick={() => window.print()}><Download size={17}/>PDF / Print</button></div></div>
    <section className="panel filter-panel filter-panel-modern report-filter-panel">
      <div className="report-filter-head"><div><CalendarDays size={18}/><span>Date range</span></div><div className="preset-row">{[[1, "Today"], [7, "7 days"], [30, "30 days"], [90, "90 days"]].map(([days, label]) => <button type="button" className={`preset-chip ${isPreset(Number(days)) ? "active" : ""}`} key={String(days)} onClick={() => preset(Number(days))}>{label}</button>)}</div>{hasExtraFilters && <button type="button" className="clear-filter-button" onClick={resetFilters}><RotateCcw size={14}/>Reset</button>}</div>
      <div className="report-filter-toolbar">
        <div className="range-picker">
          <button type="button" className="range-picker-trigger" aria-expanded={calendarOpen} onClick={() => { setDraftRange({ from: parseLocalDate(range.from), to: parseLocalDate(range.to) }); setCalendarOpen((open) => !open); }}><CalendarDays size={18}/><span><small>Selected period</small><strong>{displayDate(range.from)} – {displayDate(range.to)}</strong></span><ChevronDown size={16}/></button>
          {calendarOpen && <div className="range-picker-popover"><DayPicker mode="range" selected={draftRange} onSelect={setDraftRange} numberOfMonths={2} disabled={{ after: new Date() }} defaultMonth={draftRange?.from}/><div className="range-picker-actions"><button type="button" className="btn btn-secondary btn-small" onClick={() => setCalendarOpen(false)}>Cancel</button><button type="button" className="btn btn-small" disabled={!draftRange?.from || !draftRange.to} onClick={applyRange}>Apply range</button></div></div>}
        </div>
        <label className="select-control"><span>Payment status</span><select value={status} onChange={(event) => setStatus(event.target.value)}><option value="">All payments</option><option value="Confirmed">Confirmed only</option><option value="Pending">Not confirmed</option></select></label>
        <label className="select-control"><span>Wallet</span><select value={walletId} onChange={(event) => setWalletId(event.target.value)}><option value="">All wallets</option>{(wallets.data ?? []).map((wallet) => <option key={wallet.id} value={wallet.id}>{wallet.name}</option>)}</select></label>
        <label className="select-control"><span>Device</span><select value={deviceId} onChange={(event) => setDeviceId(event.target.value)}><option value="">All devices</option>{(devices.data ?? []).map((device) => <option key={device.id} value={device.id}>{device.name}</option>)}</select></label>
        <label className="select-control"><span>Provider</span><select value={provider} onChange={(event) => setProvider(event.target.value)}><option value="">All providers</option><option>Vodafone Cash</option><option>InstaPay</option><option>Axis</option><option>Orange Cash</option><option>e&amp; Cash</option></select></label>
      </div>
    </section>
    {report.error && <div className="error report-error"><span>{report.error.message}</span><button type="button" onClick={() => report.refetch()}>Try again</button></div>}
    <section className="confirmation-summary"><button type="button" className={status === "" ? "active" : ""} onClick={() => setStatus("")}><BarChart3/><span><small>All received</small><strong>{(confirmed?.count ?? 0) + (pending?.count ?? 0)} payments</strong></span></button><button type="button" className={status === "Confirmed" ? "active" : ""} onClick={() => setStatus("Confirmed")}><CheckCircle2/><span><small>Confirmed</small><strong>{confirmed?.count ?? 0} · {money(confirmed?.amount ?? 0)}</strong></span></button><button type="button" className={status === "Pending" ? "active pending" : "pending"} onClick={() => setStatus("Pending")}><ShieldCheck/><span><small>Awaiting confirmation</small><strong>{pending?.count ?? 0} · {money(pending?.amount ?? 0)}</strong></span></button></section>
    <div className="stats"><div className="stat"><TrendingUp/><span>{status || "All received"} {currency}</span><strong>{money(total?.amount ?? 0, currency)}</strong>{change !== null && <small className={change >= 0 ? "positive" : "negative"}>{change >= 0 ? <TrendingUp size={13}/> : <TrendingDown size={13}/>} {Math.abs(change).toFixed(1)}% vs prior period</small>}</div><div className="stat"><BarChart3/><span>Payments</span><strong>{total?.count ?? 0}</strong></div><div className="stat"><BarChart3/><span>Average / median</span><strong>{money(total?.average ?? 0, currency)}</strong><small>Median {money(total?.median ?? 0, currency)}</small></div><div className="stat"><TrendingUp/><span>Largest payment</span><strong>{money(total?.maximum ?? 0, currency)}</strong></div></div>
    <div className="grid two"><section className="panel"><span className="eyebrow">Daily received value</span><h2>{currency} movement</h2><Chart data={(data?.daily ?? []).filter((item) => item.currencyCode === currency)} dataKey="day" tick={(value) => new Date(value).toLocaleDateString(undefined, { month: "short", day: "numeric" })}/></section><section className="panel"><span className="eyebrow">Peak capture hours</span><h2>Payments by local hour</h2><Chart data={(data?.hours ?? []).filter((item) => item.currencyCode === currency)} dataKey="hour" tick={(value) => `${String(value).padStart(2, "0")}:00`}/></section></div>
    <div className="grid three-report"><Breakdown title="Wallet performance" rows={(data?.wallets ?? []).filter((item) => item.currencyCode === currency).map((item) => ({ key: item.walletId, label: item.walletName, count: item.count, amount: item.amount }))} currency={currency}/><Breakdown title="Provider performance" rows={(data?.providers ?? []).filter((item) => item.currencyCode === currency).map((item) => ({ key: item.provider, label: item.provider, count: item.count, amount: item.amount }))} currency={currency}/><Breakdown title="Device performance" rows={(data?.devices ?? []).filter((item) => item.currencyCode === currency).map((item) => ({ key: item.deviceId, label: item.deviceName, count: item.count, amount: item.amount }))} currency={currency}/></div>
    <section className="panel quality-panel"><div><span className="eyebrow">Data quality</span><h2>Capture reliability</h2></div><div><AlertTriangle/><strong>{data?.quality.unmatchedCaptures ?? 0}</strong><span>Unmatched captures</span></div><div><AlertTriangle/><strong>{data?.quality.duplicateCaptures ?? 0}</strong><span>Safe duplicates</span></div><div><AlertTriangle/><strong>{data?.quality.missingSender ?? 0}</strong><span>Missing sender</span></div><div><AlertTriangle/><strong>{data?.quality.failedUploads ?? 0}</strong><span>Failed uploads</span></div><div><TrendingUp/><strong>{data?.quality.newSenders ?? 0} / {data?.quality.returningSenders ?? 0}</strong><span>New / returning senders</span></div></section>
  </Shell>;
}

function Chart({ data, dataKey, tick }: { data: { amount: number }[]; dataKey: string; tick: (value: string | number) => string }) { return <div style={{ height: 290 }}><ResponsiveContainer width="100%" height="100%"><BarChart data={data}><CartesianGrid strokeDasharray="3 3" vertical={false}/><XAxis dataKey={dataKey} tickFormatter={tick}/><YAxis/><Tooltip/><Bar dataKey="amount" fill="#147a52" radius={[6, 6, 0, 0]}/></BarChart></ResponsiveContainer></div>; }
function Breakdown({ title, rows, currency }: { title: string; rows: { key: string; label: string; count: number; amount: number }[]; currency: string }) { const total = rows.reduce((sum, row) => sum + row.amount, 0); return <section className="panel"><span className="eyebrow">Breakdown</span><h2>{title}</h2>{rows.map((row) => <div className="breakdown-row" key={row.key}><div><strong>{row.label}</strong><small>{row.count} payments · {total ? (row.amount / total * 100).toFixed(1) : 0}% share</small></div><strong>{money(row.amount, currency)}</strong></div>)}{!rows.length && <p className="muted">No data in this period.</p>}</section>; }
