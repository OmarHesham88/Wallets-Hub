"use client";

import { useDeferredValue, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Bookmark, CheckCircle2, ChevronDown, Download, FilterX, Search, ShieldCheck, SlidersHorizontal, WalletCards } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, appPath, money, queryString } from "@/lib/api";

type Receipt = { id: string; walletId: string; walletName: string; deviceId: string; deviceName: string; provider: string; amount: number; currencyCode: string; sender?: string; providerReference?: string; message: string; receivedAtUtc: string };
type Page<T> = { items: T[]; total: number; page: number; pageSize: number; totalPages: number };
type Wallet = { id: string; name: string };
type Device = { id: string; name: string };
type Filters = { search: string; searchMode: string; walletIds: string; provider: string; currency: string; deviceId: string; from: string; to: string; minAmount: string; maxAmount: string; missingSender: boolean; missingReference: boolean; sort: string };

const empty: Filters = { search: "", searchMode: "partial", walletIds: "", provider: "", currency: "", deviceId: "", from: "", to: "", minAmount: "", maxAmount: "", missingSender: false, missingReference: false, sort: "newest" };

export default function ReceiptsPage() {
  const [filters, setFilters] = useState<Filters>(empty);
  const [page, setPage] = useState(1);
  const deferredSearch = useDeferredValue(filters.search);
  const wallets = useQuery({ queryKey: ["wallets"], queryFn: () => api<Wallet[]>("/api/wallets") });
  const devices = useQuery({ queryKey: ["devices", "receipt-filter"], queryFn: () => api<Device[]>("/api/devices"), retry: false });
  const params = useMemo(() => ({
    ...filters,
    search: deferredSearch,
    from: filters.from ? new Date(`${filters.from}T00:00:00`).toISOString() : "",
    to: filters.to ? new Date(`${filters.to}T23:59:59.999`).toISOString() : "",
    page,
    pageSize: 30,
  }), [deferredSearch, filters, page]);
  const receipts = useQuery({ queryKey: ["receipts", params], queryFn: () => api<Page<Receipt>>(`/api/receipts${queryString(params)}`), refetchInterval: 15_000, placeholderData: (previous) => previous });
  const selectedWalletIds = filters.walletIds ? filters.walletIds.split(",") : [];
  const advancedCount = [filters.from, filters.to, filters.minAmount, filters.maxAmount, filters.missingSender, filters.missingReference, filters.sort !== "newest"].filter(Boolean).length;
  const activeCount = [filters.search, filters.walletIds, filters.provider, filters.currency, filters.deviceId].filter(Boolean).length + advancedCount;

  function set<K extends keyof Filters>(key: K, value: Filters[K]) {
    setFilters((current) => ({ ...current, [key]: value }));
    setPage(1);
  }

  function toggleWallet(id: string) {
    const next = new Set(selectedWalletIds);
    if (next.has(id)) next.delete(id); else next.add(id);
    set("walletIds", [...next].join(","));
  }

  function clearFilters() { setFilters(empty); setPage(1); }
  function saveView() { localStorage.setItem("walletshub.receiptFilters.v1", JSON.stringify(filters)); window.alert("This filter view was saved on this device."); }
  function loadView() { try { const saved = JSON.parse(localStorage.getItem("walletshub.receiptFilters.v1") ?? "null") as Filters | null; if (saved) { setFilters({ ...empty, ...saved }); setPage(1); } } catch { localStorage.removeItem("walletshub.receiptFilters.v1"); } }
  const exportParams = queryString({ from: params.from, to: params.to, walletIds: filters.walletIds, provider: filters.provider, currency: filters.currency, deviceId: filters.deviceId, minAmount: filters.minAmount, maxAmount: filters.maxAmount, search: filters.search, missingSender: filters.missingSender || "", missingReference: filters.missingReference || "" });

  const walletLabel = selectedWalletIds.length === 0
    ? "All wallets"
    : selectedWalletIds.length === 1
      ? wallets.data?.find((wallet) => wallet.id === selectedWalletIds[0])?.name ?? "1 wallet"
      : `${selectedWalletIds.length} wallets`;

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">Received payments</span><h1>Received money</h1><p>Search and export confirmed receipts with fast, precise filters.</p></div><div className="button-row"><button className="btn btn-secondary" onClick={loadView}><Bookmark size={17}/>Load saved</button><button className="btn btn-secondary" onClick={saveView}><Bookmark size={17}/>Save view</button><a className="btn" href={appPath(`/api/reports/export.xlsx${exportParams}`)}><Download size={17}/>Export results</a></div></div>

    <section className="panel filter-panel filter-panel-modern">
      <div className="filter-toolbar">
        <div className="search-combo">
          <Search size={18}/>
          <input aria-label="Search receipts" value={filters.search} onChange={(event) => set("search", event.target.value)} placeholder="Search sender or reference"/>
          <select aria-label="Search matching" value={filters.searchMode} onChange={(event) => set("searchMode", event.target.value)}><option value="partial">Contains</option><option value="exact">Exact</option></select>
        </div>

        <details className="filter-popover">
          <summary><WalletCards size={17}/><span><small>Wallet</small><strong>{walletLabel}</strong></span><ChevronDown size={15}/></summary>
          <div className="filter-popover-menu">
            <button type="button" className={selectedWalletIds.length === 0 ? "selected" : ""} onClick={() => set("walletIds", "")}>All wallets <small>Default</small></button>
            {(wallets.data ?? []).map((wallet) => <label className="filter-check" key={wallet.id}><input type="checkbox" checked={selectedWalletIds.includes(wallet.id)} onChange={() => toggleWallet(wallet.id)}/><span>{wallet.name}</span></label>)}
          </div>
        </details>

        <label className="select-control"><span>Provider</span><select value={filters.provider} onChange={(event) => set("provider", event.target.value)}><option value="">All providers</option><option>Vodafone Cash</option><option>InstaPay</option><option>Binance</option></select></label>
        <label className="select-control"><span>Currency</span><select value={filters.currency} onChange={(event) => set("currency", event.target.value)}><option value="">All currencies</option><option>EGP</option><option>USD</option><option>USDT</option></select></label>
        <label className="select-control"><span>Device</span><select value={filters.deviceId} onChange={(event) => set("deviceId", event.target.value)}><option value="">All devices</option>{(devices.data ?? []).map((device) => <option key={device.id} value={device.id}>{device.name}</option>)}</select></label>
      </div>

      <details className="advanced-filters">
        <summary><SlidersHorizontal size={16}/>More filters{advancedCount > 0 && <span>{advancedCount}</span>}<ChevronDown size={15}/></summary>
        <div className="advanced-filter-grid">
          <div className="range-group"><span>Date range</span><div><input aria-label="From date" type="date" value={filters.from} onChange={(event) => set("from", event.target.value)}/><span>to</span><input aria-label="To date" type="date" value={filters.to} onChange={(event) => set("to", event.target.value)}/></div></div>
          <div className="range-group"><span>Amount range</span><div><input aria-label="Minimum amount" type="number" min="0" step="0.01" value={filters.minAmount} onChange={(event) => set("minAmount", event.target.value)} placeholder="Min"/><span>to</span><input aria-label="Maximum amount" type="number" min="0" step="0.01" value={filters.maxAmount} onChange={(event) => set("maxAmount", event.target.value)} placeholder="Max"/></div></div>
          <label className="select-control"><span>Sort by</span><select value={filters.sort} onChange={(event) => set("sort", event.target.value)}><option value="newest">Newest first</option><option value="oldest">Oldest first</option><option value="amount-high">Highest amount</option><option value="amount-low">Lowest amount</option></select></label>
          <div className="toggle-group"><span>Missing information</span><div><label className="toggle-chip"><input type="checkbox" checked={filters.missingSender} onChange={(event) => set("missingSender", event.target.checked)}/><span>Sender</span></label><label className="toggle-chip"><input type="checkbox" checked={filters.missingReference} onChange={(event) => set("missingReference", event.target.checked)}/><span>Reference</span></label></div></div>
        </div>
      </details>

      <div className="filter-footer"><span>{activeCount ? `${activeCount} active filter${activeCount === 1 ? "" : "s"}` : "Showing every confirmed receipt"}</span>{activeCount > 0 && <button type="button" className="clear-filter-button" onClick={clearFilters}><FilterX size={15}/>Clear all</button>}</div>
    </section>

    {receipts.error && <div className="error">{receipts.error.message}</div>}
    <div className="result-summary"><strong>{receipts.data?.total ?? 0} receipts</strong><span>Page {receipts.data?.page ?? page} of {receipts.data?.totalPages || 1}</span></div>
    <div className="grid">{(receipts.data?.items ?? []).map((receipt) => <article className="card" key={receipt.id}><div className="card-top"><div className="card-icon"><CheckCircle2/></div><span className="badge success">Received</span></div><h2 className="amount-heading">{money(receipt.amount, receipt.currencyCode)}</h2><p><strong>{receipt.walletName}</strong> · {receipt.provider}</p><p>{receipt.sender ? `From ${receipt.sender}` : "Sender unavailable"}</p>{receipt.providerReference && <p>Reference: <strong>{receipt.providerReference}</strong></p>}<p>{new Date(receipt.receivedAtUtc).toLocaleString()} · {receipt.deviceName}</p><details><summary>Original message</summary><div className="message">{receipt.message}</div></details></article>)}</div>
    {!receipts.isLoading && !receipts.data?.items.length && <div className="empty"><div><ShieldCheck size={38}/><h2>No matching receipts</h2><p className="muted">Change the filters or wait for a new payment.</p></div></div>}
    <div className="pagination"><button className="btn btn-secondary btn-small" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</button><span>{receipts.data?.total ?? 0} total</span><button className="btn btn-secondary btn-small" disabled={page >= (receipts.data?.totalPages ?? 1)} onClick={() => setPage((value) => value + 1)}>Next</button></div>
  </Shell>;
}
