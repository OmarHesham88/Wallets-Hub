"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, CheckCheck } from "lucide-react";
import { Shell } from "@/components/shell";
import { api } from "@/lib/api";

type Center = { unreadCount: number; items: { id: string; title: string; body: string; link?: string; createdAtUtc: string; isRead: boolean }[] };
export default function NotificationsPage() {
  const client = useQueryClient(); const center = useQuery({ queryKey: ["notifications"], queryFn: () => api<Center>("/api/notifications"), refetchInterval: 20_000 });
  const read = useMutation({ mutationFn: (id: string) => api(`/api/notifications/${id}/read`, { method: "POST" }), onSuccess: () => client.invalidateQueries({ queryKey: ["notifications"] }) });
  const all = useMutation({ mutationFn: () => api("/api/notifications/read-all", { method: "POST" }), onSuccess: () => client.invalidateQueries({ queryKey: ["notifications"] }) });
  return <Shell><div className="page-head"><div><span className="eyebrow">Operational alerts</span><h1>Notifications</h1><p>{center.data?.unreadCount ?? 0} unread notification{center.data?.unreadCount === 1 ? "" : "s"}. Receipt alerts, offline devices, and daily summaries appear here.</p></div><button className="btn btn-secondary" onClick={() => all.mutate()} disabled={!center.data?.unreadCount}><CheckCheck size={17}/>Mark all read</button></div>
    {center.error && <div className="error">{center.error.message}</div>}<div className="notification-list">{(center.data?.items ?? []).map((item) => <Link className={`notification-row ${item.isRead ? "read" : ""}`} href={item.link ?? "/notifications"} key={item.id} onClick={() => { if (!item.isRead) read.mutate(item.id); }}><div className="card-icon"><Bell/></div><div><div className="card-top"><h2>{item.title}</h2>{!item.isRead && <span className="badge success">New</span>}</div><p>{item.body}</p><small>{new Date(item.createdAtUtc).toLocaleString()}</small></div></Link>)}</div>
    {!center.isLoading && !center.data?.items.length && <div className="empty"><div><Bell/><h2>You are all caught up</h2><p className="muted">New receipt and device alerts will appear here.</p></div></div>}
  </Shell>;
}
