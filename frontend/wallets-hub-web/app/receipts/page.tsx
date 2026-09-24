"use client";

import { useDeferredValue, useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bookmark, Check, CheckCircle2, ChevronDown, Download, FilterX, LayoutGrid, List, Search, ShieldCheck, SlidersHorizontal, WalletCards, X } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, appPath, money, queryString, User } from "@/lib/api";

type Receipt = { id: string; walletId: string; walletName: string; deviceId: string; deviceName: string; provider: string; amount: number; currencyCode: string; sender?: string; providerReference?: string; status: "Pending" | "Confirmed"; message: string; receivedAtUtc: string };
type Page<T> = { items: T[]; total: number; page: number; pageSize: number; totalPages: number };
type Wallet = { id: string; name: string };
type Device = { id: string; name: string };
type Filters = { search: string; searchMode: string; walletIds: string; provider: string; deviceId: string; status: string; from: string; to: string; minAmount: string; maxAmount: string; missingSender: boolean; missingReference: boolean; sort: string };
type Workspace = { requireReceiptConfirmation: boolean };

const empty: Filters = { search: "", searchMode: "partial", walletIds: "", provider: "", deviceId: "", status: "", from: "", to: "", minAmount: "", maxAmount: "", missingSender: false, missingReference: false, sort: "newest" };

export default function ReceiptsPage() {
  const client = useQueryClient();
  const [filters, setFilters] = useState<Filters>(empty);
  const [page, setPage] = useState(1);
  const [view, setViewState] = useState<"cards" | "list">("cards");
  useEffect(() => { const timer = window.setTimeout(() => setViewState(localStorage.getItem("walletshub.receiptView") === "list" ? "list" : "cards"), 0); return () => window.clearTimeout(timer); }, []);
  function setView(next: "cards" | "list") { setViewState(next); localStorage.setItem("walletshub.receiptView", next); }
  const deferredSearch = useDeferredValue(filters.search);
  const me = useQuery({ queryKey: ["me"], queryFn: () => api<User>("/api/auth/me") });
  const workspace = useQuery({ queryKey: ["workspace-settings"], queryFn: () => api<Workspace>("/api/settings/workspace") });
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
  const confirm = useMutation({ mutationFn: (id: string) => api(`/api/receipts/${id}/confirm`, { method: "POST" }), onSuccess: () => { client.invalidateQueries({ queryKey: ["receipts"] }); client.invalidateQueries({ queryKey: ["dashboard"] }); client.invalidateQueries({ queryKey: ["report-summary"] }); client.invalidateQueries({ queryKey: ["wallet-operations"] }); client.invalidateQueries({ queryKey: ["wallets"] }); } });
  const canConfirm = ["Owner", "Admin"].includes(me.data?.role ?? "") || Boolean(me.data?.canConfirmReceipts);
  const selectedWalletIds = filters.walletIds ? filters.walletIds.split(",") : [];
  const advancedCount = [filters.from, filters.to, filters.minAmount, filters.maxAmount, filters.missingSender, filters.missingReference, filters.sort !== "newest"].filter(Boolean).length;
  const activeCount = [filters.search, filters.walletIds, filters.provider, filters.deviceId, filters.status].filter(Boolean).length + advancedCount;

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
  function saveView() { localStorage.setItem("walletshub.receiptFilters.v2", JSON.stringify(filters)); window.alert("This filter view was saved on this device."); }
  function loadView() { try { const saved = JSON.parse(localStorage.getItem("walletshub.receiptFilters.v2") ?? "null") as Filters | null; if (saved) { setFilters({ ...empty, ...saved }); setPage(1); } } catch { localStorage.removeItem("walletshub.receiptFilters.v2"); } }
  const exportParams = queryString({ from: params.from, to: params.to, walletIds: filters.walletIds, provider: filters.provider, currency: "EGP", deviceId: filters.deviceId, minAmount: filters.minAmount, maxAmount: filters.maxAmount, search: filters.search, missingSender: filters.missingSender || "", missingReference: filters.missingReference || "" });

  const walletLabel = selectedWalletIds.length === 0
    ? "All wallets"
    : selectedWalletIds.length === 1
      ? wallets.data?.find((wallet) => wallet.id === selectedWalletIds[0])?.name ?? "1 wallet"
      : `${selectedWalletIds.length} wallets`;

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">Received payments</span><h1>Received money</h1><p>{workspace.data?.requireReceiptConfirmation ? "Review pending payments and search every received EGP payment." : "Search every received EGP payment with fast, precise filters."}</p></div><div className="button-row"><button className="btn btn-secondary" onClick={loadView}><Bookmark size={17}/>Load saved</button><button className="btn btn-secondary" onClick={saveView}><Bookmark size={17}/>Save view</button><a className="btn" href={appPath(`/api/reports/export.xlsx${exportParams}`)}><Download size={17}/>Export confirmed</a></div></div>

    <div className="receipt-viewbar">
      {workspace.data?.requireReceiptConfirmation && <div className="status-tabs" aria-label="Payment status">{[["", "All"], ["Pending", "Pending"], ["Confirmed", "Confirmed"]].map(([value, label]) => <button type="button" key={label} className={filters.status === value ? "active" : ""} onClick={() => set("status", value)}>{label}</button>)}</div>}
      <div className="view-switch" aria-label="Display style"><button type="button" className={view === "cards" ? "active" : ""} onClick={() => setView("cards")} title="Card view"><LayoutGrid size={16}/><span>Cards</span></button><button type="button" className={view === "list" ? "active" : ""} onClick={() => setView("list")} title="List view"><List size={17}/><span>List</span></button></div>
    </div>

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

        <label className="select-control"><span>Provider</span><select value={filters.provider} onChange={(event) => set("provider", event.target.value)}><option value="">All providers</option><option>Vodafone Cash</option><option>InstaPay</option><option>Axis</option><option>Orange Cash</option><option>e&amp; Cash</option></select></label>
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

      <div className="filter-footer"><span>{activeCount ? `${activeCount} active filter${activeCount === 1 ? "" : "s"}` : workspace.data?.requireReceiptConfirmation ? "Showing every payment" : "Showing every confirmed payment"}</span>{activeCount > 0 && <button type="button" className="clear-filter-button" onClick={clearFilters}><FilterX size={15}/>Clear all</button>}</div>
      {activeCount > 0 && <div className="active-filter-chips">{filters.search && <FilterChip label={`Search: ${filters.search}`} onClear={() => set("search", "")}/>} {selectedWalletIds.length > 0 && <FilterChip label={walletLabel} onClear={() => set("walletIds", "")}/>} {filters.provider && <FilterChip label={filters.provider} onClear={() => set("provider", "")}/>} {filters.deviceId && <FilterChip label={devices.data?.find((item) => item.id === filters.deviceId)?.name ?? "Device"} onClear={() => set("deviceId", "")}/>} {filters.status && <FilterChip label={filters.status} onClear={() => set("status", "")}/>} {(advancedCount > 0) && <FilterChip label={`${advancedCount} more`} onClear={() => setFilters((current) => ({ ...current, from: "", to: "", minAmount: "", maxAmount: "", missingSender: false, missingReference: false, sort: "newest" }))}/>}</div>}
    </section>

    {receipts.error && <div className="error">{receipts.error.message}</div>}
    {confirm.error && <div className="error">{confirm.error.message}</div>}
    <div className="result-summary"><strong>{receipts.data?.total ?? 0} receipts</strong><span>Page {receipts.data?.page ?? page} of {receipts.data?.totalPages || 1}</span></div>
    {view === "cards" ? <div className="grid receipt-card-grid">{(receipts.data?.items ?? []).map((receipt) => <article className="card receipt-card" key={receipt.id}><div className="card-top"><div className="card-icon">{receipt.status === "Pending" ? <ShieldCheck/> : <CheckCircle2/>}</div><StatusBadge status={receipt.status}/></div><h2 className="amount-heading">{money(receipt.amount)}</h2><p><strong>{receipt.walletName}</strong> · {receipt.provider}</p><p>{receipt.sender ? `From ${receipt.sender}` : "Sender unavailable"}</p>{receipt.providerReference && <p>Reference: <strong>{receipt.providerReference}</strong></p>}<p>{new Date(receipt.receivedAtUtc).toLocaleString()} · {receipt.deviceName}</p>{receipt.status === "Pending" && canConfirm && <button className="btn btn-wide confirm-payment" disabled={confirm.isPending} onClick={() => confirm.mutate(receipt.id)}><Check size={17}/>Confirm payment</button>}<details><summary>Original message</summary><div className="message">{receipt.message}</div></details></article>)}</div> : <div className="receipt-compact-list">{(receipts.data?.items ?? []).map((receipt) => <article className="receipt-compact-row" key={receipt.id}><div className="receipt-primary"><strong>{receipt.walletName}</strong><span>{receipt.provider} · {receipt.deviceName}</span></div><div className="receipt-sender"><span>Sender</span><strong>{receipt.sender ?? "Unavailable"}</strong></div><div className="receipt-reference"><span>Reference</span><strong>{receipt.providerReference ?? "—"}</strong></div><div className="receipt-state"><StatusBadge status={receipt.status}/></div><div className="receipt-amount"><strong>{money(receipt.amount)}</strong><time>{new Date(receipt.receivedAtUtc).toLocaleDateString()} · {new Date(receipt.receivedAtUtc).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</time></div><div className="receipt-row-actions"><details className="message-popover"><summary>Details</summary><div className="message">{receipt.message}</div></details>{receipt.status === "Pending" && canConfirm && <button className="btn btn-small" disabled={confirm.isPending} onClick={() => confirm.mutate(receipt.id)}><Check size={15}/>Confirm</button>}</div></article>)}</div>}
    {!receipts.isLoading && !receipts.data?.items.length && <div className="empty"><div><ShieldCheck size={38}/><h2>No matching receipts</h2><p className="muted">Change the filters or wait for a new payment.</p></div></div>}
    <div className="pagination"><button className="btn btn-secondary btn-small" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</button><span>{receipts.data?.total ?? 0} total</span><button className="btn btn-secondary btn-small" disabled={page >= (receipts.data?.totalPages ?? 1)} onClick={() => setPage((value) => value + 1)}>Next</button></div>
  </Shell>;
}

function StatusBadge({ status }: { status: Receipt["status"] }) { return <span className={`badge ${status === "Confirmed" ? "success" : ""}`}>{status === "Confirmed" ? <CheckCircle2 size={12}/> : <ShieldCheck size={12}/>} {status}</span>; }
function FilterChip({ label, onClear }: { label: string; onClear: () => void }) { return <button type="button" onClick={onClear}>{label}<X size={13}/></button>; }
