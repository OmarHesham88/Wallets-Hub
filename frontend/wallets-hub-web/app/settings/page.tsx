"use client";

import { FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, Globe2, LockKeyhole, ShieldCheck } from "lucide-react";
import { Shell } from "@/components/shell";
import { api } from "@/lib/api";

type Preferences = { everyReceipt: boolean; minimumAmount?: number; dailySummary: boolean; deviceOffline: boolean };

export default function SettingsPage() {
  const client = useQueryClient();
  const preference = useQuery({ queryKey: ["notification-preferences"], queryFn: () => api<Preferences>("/api/settings/notifications") });
  const save = useMutation({ mutationFn: (body: Preferences) => api("/api/settings/notifications", { method: "PUT", body: JSON.stringify(body) }), onSuccess: () => client.invalidateQueries({ queryKey: ["notification-preferences"] }) });
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    save.mutate({ everyReceipt: form.get("every") === "on", minimumAmount: Number(form.get("minimum")) || undefined, dailySummary: form.get("daily") === "on", deviceOffline: form.get("offline") === "on" });
  }
  return <Shell><div className="page-head"><div><span className="eyebrow">Workspace preferences</span><h1>Settings</h1><p>Choose which operational events should reach your notification center.</p></div></div><div className="grid two"><form className="panel" onSubmit={submit} key={JSON.stringify(preference.data)}><div className="card-icon"><Bell/></div><h2>My notification rules</h2><div className="form-grid" style={{ gridTemplateColumns: "1fr" }}><label className="checkbox"><input type="checkbox" name="every" defaultChecked={preference.data?.everyReceipt ?? true}/>Notify me for every detected receipt</label><label>Only receipts at or above this amount<input type="number" min="0" step="0.01" name="minimum" defaultValue={preference.data?.minimumAmount ?? ""} placeholder="No minimum"/></label><label className="checkbox"><input type="checkbox" name="daily" defaultChecked={preference.data?.dailySummary ?? true}/>Daily operational summary</label><label className="checkbox"><input type="checkbox" name="offline" defaultChecked={preference.data?.deviceOffline ?? true}/>Device offline alerts</label></div><button className="btn" disabled={save.isPending}>{save.isPending ? "Saving…" : "Save preferences"}</button>{save.isSuccess && <span className="badge success" style={{ marginLeft: 10 }}>Saved</span>}</form><div className="grid" style={{ gridTemplateColumns: "1fr" }}><article className="card"><div className="card-icon"><Globe2/></div><h2>Language & currency</h2><p>Arabic and English display is prepared, with separately reported EGP, USD, and USDT totals.</p><span className="badge success">EGP + USD + USDT active</span></article><article className="card"><div className="card-icon"><LockKeyhole/></div><h2>Privacy controls</h2><p>Client data stays isolated. Platform support access is not enabled by default.</p><span className="badge success"><ShieldCheck size={12}/>Protected</span></article></div></div></Shell>;
}
