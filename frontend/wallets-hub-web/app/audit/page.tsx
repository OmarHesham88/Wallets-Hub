"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ClipboardList } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, queryString } from "@/lib/api";

type Event = { id: string; action: string; entityType: string; entityId?: string; detailJson?: string; createdAtUtc: string; userId?: string; actorName: string };
type Page<T> = { items: T[]; total: number; page: number; totalPages: number };
type Member = { id: string; displayName: string };

export default function AuditPage() {
  const [actor, setActor] = useState(""); const [action, setAction] = useState(""); const [from, setFrom] = useState(""); const [to, setTo] = useState(""); const [page, setPage] = useState(1);
  const team = useQuery({ queryKey: ["team"], queryFn: () => api<Member[]>("/api/team") });
  const audit = useQuery({ queryKey: ["audit", actor, action, from, to, page], queryFn: () => api<Page<Event>>(`/api/audit${queryString({ userId: actor, action, from: from ? new Date(`${from}T00:00:00`).toISOString() : "", to: to ? new Date(`${to}T23:59:59.999`).toISOString() : "", page })}`) });
  return <Shell><div className="page-head"><div><span className="eyebrow">Accountability</span><h1>Audit trail</h1><p>See who changed accounts, wallets, devices, permissions, settings, and financial records.</p></div></div>
    <div className="filters"><label>Employee<select value={actor} onChange={(event) => { setActor(event.target.value); setPage(1); }}><option value="">Everyone and system</option>{(team.data ?? []).map((member) => <option key={member.id} value={member.id}>{member.displayName}</option>)}</select></label><label>Action<input value={action} onChange={(event) => { setAction(event.target.value); setPage(1); }} placeholder="e.g. WalletUpdated"/></label><label>From<input type="date" value={from} onChange={(event) => setFrom(event.target.value)}/></label><label>To<input type="date" value={to} onChange={(event) => setTo(event.target.value)}/></label></div>
    {audit.error && <div className="error">{audit.error.message}</div>}<div className="table-wrap"><table><thead><tr><th>Date</th><th>Actor</th><th>Action</th><th>Entity</th><th>Details</th></tr></thead><tbody>{(audit.data?.items ?? []).map((event) => <tr key={event.id}><td>{new Date(event.createdAtUtc).toLocaleString()}</td><td>{event.actorName}</td><td><span className="badge">{event.action}</span></td><td>{event.entityType}<br/><small className="muted">{event.entityId}</small></td><td>{event.detailJson ? <details><summary>View changes</summary><pre className="json-detail">{JSON.stringify(JSON.parse(event.detailJson), null, 2)}</pre></details> : "—"}</td></tr>)}</tbody></table></div>
    {!audit.isLoading && !audit.data?.items.length && <div className="empty"><div><ClipboardList/><h2>No audit events found</h2></div></div>}<div className="pagination"><button className="btn btn-secondary btn-small" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</button><span>{audit.data?.total ?? 0} events</span><button className="btn btn-secondary btn-small" disabled={page >= (audit.data?.totalPages ?? 1)} onClick={() => setPage((value) => value + 1)}>Next</button></div>
  </Shell>;
}
