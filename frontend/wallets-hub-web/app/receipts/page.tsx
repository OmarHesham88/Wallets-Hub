"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { CheckCircle2, Search, ShieldCheck } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, money } from "@/lib/api";

type Receipt = { id: string; walletId: string; walletName: string; provider: string; amount: number; currencyCode: string; sender?: string; providerReference?: string; message: string; receivedAtUtc: string };
type Wallet = { id: string; name: string };

export default function ReceiptsPage() {
  const [wallet, setWallet] = useState("");
  const [search, setSearch] = useState("");
  const wallets = useQuery({ queryKey: ["wallets"], queryFn: () => api<Wallet[]>("/api/wallets") });
  const receipts = useQuery({ queryKey: ["receipts"], queryFn: () => api<Receipt[]>("/api/receipts"), refetchInterval: 15_000 });
  const rows = (receipts.data ?? []).filter((item) => (!wallet || item.walletId === wallet) && (!search || `${item.sender} ${item.providerReference} ${item.walletName} ${item.amount}`.toLowerCase().includes(search.toLowerCase())));

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">Received payments</span><h1>Received money</h1><p>Every payment shown here has reached its wallet and is included in reports automatically.</p></div></div>
    <div className="filters"><label>Search<div style={{ position: "relative" }}><Search size={16} style={{ position: "absolute", left: 11, top: 13 }}/><input style={{ paddingLeft: 35 }} value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Sender, reference, amount"/></div></label><label>Wallet<select value={wallet} onChange={(event) => setWallet(event.target.value)}><option value="">All wallets</option>{(wallets.data ?? []).map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label></div>
    {receipts.error && <div className="error">{(receipts.error as Error).message}</div>}
    <div className="grid">{rows.map((row) => <article className="card" key={row.id}><div className="card-top"><div className="card-icon"><CheckCircle2/></div><span className="badge success">Received</span></div><h2 style={{ fontSize: "1.55rem" }}>{money(row.amount, row.currencyCode)}</h2><p><strong>{row.walletName}</strong> · {row.provider}</p><p>{row.sender ? `From ${row.sender}` : "Sender unavailable"}</p>{row.providerReference && <p>Reference: <strong>{row.providerReference}</strong></p>}<p>{new Date(row.receivedAtUtc).toLocaleString()}</p><details><summary>Original message</summary><div className="message">{row.message}</div></details></article>)}</div>
    {!receipts.isLoading && rows.length === 0 && <div className="empty"><div><ShieldCheck size={38}/><h2>No matching receipts</h2><p className="muted">New detected payments will appear here automatically.</p></div></div>}
  </Shell>;
}
