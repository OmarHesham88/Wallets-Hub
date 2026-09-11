"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Edit3, Gauge, Plus, Trash2, WalletCards, X } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, money, User } from "@/lib/api";

type Wallet = { id: string; name: string; provider: string; accountNumber: string; currencyCode: string; deviceId?: string; isActive: boolean; openingBalance: number; balanceLimit?: number; currentBalance: number };
type Device = { id: string; name: string; isActive: boolean };
const providers = ["Vodafone Cash", "InstaPay", "Binance"];

export default function WalletsPage() {
  const client = useQueryClient(); const [editing, setEditing] = useState<Wallet | null | undefined>(undefined); const [provider, setProvider] = useState("Vodafone Cash");
  const me = useQuery({ queryKey: ["me"], queryFn: () => api<User>("/api/auth/me") });
  const canManage = ["Owner", "Admin"].includes(me.data?.role ?? "");
  const wallets = useQuery({ queryKey: ["wallets", canManage], queryFn: () => api<Wallet[]>(`/api/wallets${canManage ? "?includeInactive=true" : ""}`) });
  const devices = useQuery({ queryKey: ["devices"], queryFn: () => api<Device[]>("/api/devices"), retry: false, enabled: canManage || Boolean(me.data?.canManageDevices) });
  const save = useMutation({ mutationFn: ({ id, body }: { id?: string; body: object }) => api(id ? `/api/wallets/${id}` : "/api/wallets", { method: id ? "PUT" : "POST", body: JSON.stringify(body) }), onSuccess: () => { setEditing(undefined); client.invalidateQueries({ queryKey: ["wallets"] }); client.invalidateQueries({ queryKey: ["wallet-operations"] }); } });
  const remove = useMutation({ mutationFn: (id: string) => api(`/api/wallets/${id}`, { method: "DELETE" }), onSuccess: () => { client.invalidateQueries({ queryKey: ["wallets"] }); client.invalidateQueries({ queryKey: ["devices"] }); } });
  function open(wallet?: Wallet) { setProvider(wallet?.provider ?? "Vodafone Cash"); setEditing(wallet ?? null); save.reset(); }
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const form = new FormData(event.currentTarget); save.mutate({ id: editing?.id, body: { name: form.get("name"), provider: form.get("provider"), accountNumber: form.get("account"), currencyCode: form.get("currency"), deviceId: form.get("device") || null, isActive: editing ? form.get("active") === "on" : true, openingBalance: Number(form.get("opening")) || 0, balanceLimit: Number(form.get("limit")) || null } }); }
  function deleteWallet(wallet: Wallet) { if (window.confirm(`Archive “${wallet.name}”? Historical payments and reports will be preserved.`)) remove.mutate(wallet.id); }

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">Wallet registry</span><h1>Wallets</h1><p>Configure receiving accounts, assignment, opening balances, limits, and operating status.</p></div>{canManage && <div className="button-row"><Link className="btn btn-secondary" href="/wallet-operations"><Gauge size={17}/>Balances</Link><button className="btn" onClick={() => open()}><Plus size={18}/>Add wallet</button></div>}</div>
    {(remove.error || save.error) && <div className="error">{(remove.error ?? save.error)?.message}</div>}
    <div className="grid">{(wallets.data ?? []).map((wallet) => <article className="card" key={wallet.id}>
      <div className="card-top"><div className="card-icon"><WalletCards/></div><span className={`badge ${wallet.isActive ? "success" : "danger"}`}>{wallet.isActive ? "Active" : "Paused"}</span></div>
      <h2>{wallet.name}</h2><p><strong>{wallet.provider}</strong></p><p>{wallet.accountNumber} · {wallet.currencyCode}</p><p>{wallet.deviceId ? devices.data?.find((device) => device.id === wallet.deviceId)?.name ?? "Assigned device" : "No device assigned"}</p>
      <div className="balance-line"><span>Current balance</span><strong>{money(wallet.currentBalance, wallet.currencyCode)}</strong></div>
      {wallet.balanceLimit ? <div className="progress"><span style={{ width: `${Math.min(100, Math.max(0, wallet.currentBalance / wallet.balanceLimit * 100))}%` }}/></div> : null}
      {canManage && <div className="button-row" style={{ marginTop: 14 }}><button className="btn btn-secondary btn-small" onClick={() => open(wallet)}><Edit3 size={15}/>Edit</button><button className="btn btn-danger btn-small" disabled={remove.isPending} onClick={() => deleteWallet(wallet)}><Trash2 size={15}/>Archive</button></div>}
    </article>)}</div>
    {!wallets.isLoading && (wallets.data ?? []).length === 0 && <div className="empty"><div><WalletCards/><h2>No wallets yet</h2><p className="muted">Create your first receiving wallet to begin.</p></div></div>}
    {editing !== undefined && <div className="modal-backdrop"><form className="modal" onSubmit={submit} key={editing?.id ?? "new"}>
      <div className="modal-head"><div><span className="eyebrow">{editing ? "Edit wallet" : "New wallet"}</span><h2>{editing?.name ?? "Add receiving wallet"}</h2></div><button type="button" className="icon-button" onClick={() => setEditing(undefined)} aria-label="Close"><X/></button></div>
      <div className="form-grid"><label>Wallet name<input name="name" required defaultValue={editing?.name} placeholder="Branch 1 Vodafone"/></label><label>Provider<select name="provider" value={provider} onChange={(event) => setProvider(event.target.value)}>{providers.map((item) => <option key={item}>{item}</option>)}</select></label><label>Phone or account number<input name="account" required defaultValue={editing?.accountNumber}/></label><label>Currency<select name="currency" defaultValue={editing?.currencyCode ?? (provider === "Binance" ? "USDT" : "EGP")} key={provider}>{provider === "Binance" ? <option>USDT</option> : <><option>EGP</option><option>USD</option></>}</select></label><label>Opening balance<input name="opening" type="number" step="0.0001" defaultValue={editing?.openingBalance ?? 0}/></label><label>Optional balance limit<input name="limit" type="number" min="0" step="0.0001" defaultValue={editing?.balanceLimit ?? ""}/></label><label className="full">Capturing device<select name="device" defaultValue={editing?.deviceId ?? ""}><option value="">Assign later</option>{(devices.data ?? []).filter((device) => device.isActive !== false).map((device) => <option value={device.id} key={device.id}>{device.name}</option>)}</select></label>{editing && <label className="checkbox full"><input type="checkbox" name="active" defaultChecked={editing.isActive}/>Wallet is active</label>}</div>
      {save.error && <div className="error">{save.error.message}</div>}<div className="button-row" style={{ marginTop: 18 }}><button className="btn" disabled={save.isPending}>{save.isPending ? "Saving…" : "Save wallet"}</button><button type="button" className="btn btn-secondary" onClick={() => setEditing(undefined)}>Cancel</button></div>
    </form></div>}
  </Shell>;
}
