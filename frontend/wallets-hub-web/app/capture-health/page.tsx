"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Activity, AlertTriangle, CheckCircle2, RotateCcw } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, money, queryString } from "@/lib/api";

type Capture = { id: string; status: string; reason: string; deviceId: string; deviceName: string; walletId?: string; walletName?: string; receiptId?: string; provider?: string; amount?: number; currencyCode?: string; sender?: string; destination?: string; providerReference?: string; sourcePackage: string; message: string; receivedAtUtc: string; lastSeenAtUtc: string; attemptCount: number };
type Page<T> = { items: T[]; total: number; page: number; pageSize: number; totalPages: number };
type Wallet = { id: string; name: string; provider: string; currencyCode: string };
type Device = { id: string; name: string };

export default function CaptureHealthPage() {
  const client = useQueryClient(); const [status, setStatus] = useState("Unmatched"); const [deviceId, setDeviceId] = useState(""); const [page, setPage] = useState(1);
  const devices = useQuery({ queryKey: ["devices"], queryFn: () => api<Device[]>("/api/devices") });
  const wallets = useQuery({ queryKey: ["wallets"], queryFn: () => api<Wallet[]>("/api/wallets") });
  const captures = useQuery({ queryKey: ["capture-events", status, deviceId, page], queryFn: () => api<Page<Capture>>(`/api/capture-events${queryString({ status, deviceId, page, pageSize: 40 })}`), refetchInterval: 20_000 });
  const resolve = useMutation({ mutationFn: ({ id, walletId }: { id: string; walletId: string }) => api(`/api/capture-events/${id}/resolve`, { method: "POST", body: JSON.stringify({ walletId }) }), onSuccess: () => { client.invalidateQueries({ queryKey: ["capture-events"] }); client.invalidateQueries({ queryKey: ["receipts"] }); } });
  const retry = useMutation({ mutationFn: (id: string) => api(`/api/capture-events/${id}/retry`, { method: "POST" }), onSuccess: () => { client.invalidateQueries({ queryKey: ["capture-events"] }); client.invalidateQueries({ queryKey: ["receipts"] }); } });
  function resolveCapture(capture: Capture) { const choices = (wallets.data ?? []).filter((wallet) => wallet.provider === capture.provider && wallet.currencyCode === capture.currencyCode); if (!choices.length) return window.alert("Create an active wallet with the same provider and currency first."); const answer = window.prompt(`Enter the wallet number:\n${choices.map((wallet, index) => `${index + 1}. ${wallet.name}`).join("\n")}`, "1"); const selected = choices[Number(answer) - 1]; if (selected) resolve.mutate({ id: capture.id, walletId: selected.id }); }

  return <Shell><div className="page-head"><div><span className="eyebrow">Capture diagnostics</span><h1>Capture inbox</h1><p>Every phone upload is traceable, including accepted, duplicate, ambiguous, unsupported, and rejected events.</p></div></div>
    <div className="filters"><label>Status<select value={status} onChange={(event) => { setStatus(event.target.value); setPage(1); }}><option value="">All statuses</option><option>Unmatched</option><option>Rejected</option><option>Duplicate</option><option>Accepted</option></select></label><label>Device<select value={deviceId} onChange={(event) => { setDeviceId(event.target.value); setPage(1); }}><option value="">All devices</option>{(devices.data ?? []).map((device) => <option key={device.id} value={device.id}>{device.name}</option>)}</select></label></div>
    {(captures.error || retry.error || resolve.error) && <div className="error">{(captures.error ?? retry.error ?? resolve.error)?.message}</div>}
    <div className="table-wrap"><table><thead><tr><th>Status</th><th>Payment</th><th>Device</th><th>Reason</th><th>Received</th><th>Attempts</th><th>Details</th><th>Action</th></tr></thead><tbody>{(captures.data?.items ?? []).map((capture) => <tr key={capture.id}><td><span className={`badge ${capture.status === "Accepted" ? "success" : capture.status === "Unmatched" || capture.status === "Rejected" ? "danger" : ""}`}>{capture.status === "Accepted" ? <CheckCircle2 size={12}/> : <AlertTriangle size={12}/>} {capture.status}</span></td><td>{capture.amount && capture.currencyCode ? money(capture.amount, capture.currencyCode) : "Not parsed"}<br/><span className="muted">{capture.provider ?? "Unknown provider"}</span></td><td>{capture.deviceName}</td><td>{capture.reason}</td><td>{new Date(capture.receivedAtUtc).toLocaleString()}</td><td>{capture.attemptCount}</td><td><details><summary>Message</summary><div className="message">{capture.message}</div></details></td><td>{capture.status !== "Accepted" && <div className="button-row"><button className="btn btn-secondary btn-small" disabled={retry.isPending} onClick={() => retry.mutate(capture.id)}><RotateCcw size={14}/>Reprocess</button>{capture.status === "Unmatched" && capture.amount && <button className="btn btn-secondary btn-small" disabled={resolve.isPending} onClick={() => resolveCapture(capture)}>Assign</button>}</div>}</td></tr>)}</tbody></table></div>
    {!captures.isLoading && !captures.data?.items.length && <div className="empty"><div><Activity/><h2>No matching capture events</h2><p className="muted">Try another status or device.</p></div></div>}
    <div className="pagination"><button className="btn btn-secondary btn-small" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</button><span>Page {captures.data?.page ?? page} of {captures.data?.totalPages || 1} · {captures.data?.total ?? 0} events</span><button className="btn btn-secondary btn-small" disabled={page >= (captures.data?.totalPages ?? 1)} onClick={() => setPage((value) => value + 1)}>Next</button></div>
  </Shell>;
}
