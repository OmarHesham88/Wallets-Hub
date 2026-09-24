"use client";

import { FormEvent, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BatteryWarning, CircleAlert, Copy, Edit3, Plus, RefreshCw, Smartphone, Trash2, Wifi, WifiOff, X } from "lucide-react";
import { Shell } from "@/components/shell";
import { api } from "@/lib/api";

type Device = { id: string; name: string; platform: string; isActive: boolean; pairedAtUtc?: string; lastSeenAtUtc?: string; lastHeartbeatAtUtc?: string; lastSmsAtUtc?: string; lastAxisNotificationAtUtc?: string; lastCaptureAtUtc?: string; appVersion?: string; androidVersion?: string; pendingUploadCount: number; failedUploadCount: number; smsPermissionGranted: boolean; axisNotificationAccessGranted: boolean; batteryOptimizationIgnored: boolean; walletCount: number };
type Pairing = { id: string; pairingCode: string; expiresAtUtc: string };

export default function DevicesPage() {
  const client = useQueryClient(); const [createOpen, setCreateOpen] = useState(false); const [pairing, setPairing] = useState<Pairing>();
  const devices = useQuery({ queryKey: ["devices"], queryFn: () => api<Device[]>("/api/devices"), refetchInterval: 20_000 });
  const create = useMutation({ mutationFn: (name: string) => api<Pairing>("/api/devices/pairing", { method: "POST", body: JSON.stringify({ name }) }), onSuccess: (value) => { setPairing(value); client.invalidateQueries({ queryKey: ["devices"] }); } });
  const update = useMutation({ mutationFn: ({ id, name, isActive }: { id: string; name: string; isActive: boolean }) => api(`/api/devices/${id}`, { method: "PUT", body: JSON.stringify({ name, isActive }) }), onSuccess: () => client.invalidateQueries({ queryKey: ["devices"] }) });
  const repair = useMutation({ mutationFn: (id: string) => api<Pairing>(`/api/devices/${id}/pairing`, { method: "POST" }), onSuccess: (value) => { setPairing(value); setCreateOpen(true); client.invalidateQueries({ queryKey: ["devices"] }); } });
  const remove = useMutation({ mutationFn: (id: string) => api(`/api/devices/${id}`, { method: "DELETE" }), onSuccess: () => { client.invalidateQueries({ queryKey: ["devices"] }); client.invalidateQueries({ queryKey: ["wallets"] }); } });
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); create.mutate(String(new FormData(event.currentTarget).get("name"))); }
  function rename(device: Device) { const name = window.prompt("Device name", device.name)?.trim(); if (name) update.mutate({ id: device.id, name, isActive: device.isActive }); }
  function toggle(device: Device) { update.mutate({ id: device.id, name: device.name, isActive: !device.isActive }); }
  function deleteDevice(device: Device) { if (window.confirm(`Archive “${device.name}”? Assigned wallets become unassigned; history remains.`)) remove.mutate(device.id); }

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">Capture network</span><h1>Devices</h1><p>Connection health, SMS permission, app version, queue status, and capture activity for every paired phone.</p></div><button className="btn" onClick={() => { setPairing(undefined); setCreateOpen(true); }}><Plus size={18}/>Pair device</button></div>
    {(devices.error || remove.error || update.error || repair.error) && <div className="error">{(devices.error ?? remove.error ?? update.error ?? repair.error)?.message}</div>}
    <div className="grid">{(devices.data ?? []).map((device) => { const health = deviceHealth(device, devices.dataUpdatedAt); return <article className="card" key={device.id}>
      <div className="card-top"><div className="card-icon"><Smartphone/></div><span className={`badge ${health.tone}`}>{health.icon === "online" ? <Wifi size={12}/> : health.icon === "offline" ? <WifiOff size={12}/> : <CircleAlert size={12}/>} {health.label}</span></div>
      <h2>{device.name}</h2><p>{device.platform} {device.androidVersion ? `Android ${device.androidVersion}` : ""} · App {device.appVersion ?? "unknown"} · {device.walletCount} wallet{device.walletCount === 1 ? "" : "s"}</p>
      <div className="diagnostic-list"><Diagnostic label="Last server contact" value={format(device.lastSeenAtUtc)}/><Diagnostic label="Heartbeat" value={format(device.lastHeartbeatAtUtc)} ok={Boolean(device.lastHeartbeatAtUtc)}/><Diagnostic label="Last receipt capture" value={format(device.lastCaptureAtUtc)}/><Diagnostic label="Last SMS" value={format(device.lastSmsAtUtc)} ok={device.smsPermissionGranted}/><Diagnostic label="Axis notification access" value={device.lastAxisNotificationAtUtc ? `Last checked ${format(device.lastAxisNotificationAtUtc)}` : device.axisNotificationAccessGranted ? "Enabled · no Axis notification yet" : "Permission required"} ok={device.axisNotificationAccessGranted}/><Diagnostic label="Upload queue" value={`${device.pendingUploadCount} pending · ${device.failedUploadCount} failed`} ok={device.failedUploadCount === 0}/><Diagnostic label="Battery protection" value={device.batteryOptimizationIgnored ? "Unrestricted" : "Optimization may stop capture"} ok={device.batteryOptimizationIgnored}/></div>
      <div className="button-row" style={{ marginTop: 14 }}><button className="btn btn-secondary btn-small" onClick={() => rename(device)}><Edit3 size={15}/>Rename</button><button className="btn btn-secondary btn-small" onClick={() => toggle(device)}>{device.isActive ? "Disable" : "Activate"}</button><button className="btn btn-secondary btn-small" onClick={() => repair.mutate(device.id)}><RefreshCw size={15}/>Re-pair</button><button className="btn btn-danger btn-small" onClick={() => deleteDevice(device)}><Trash2 size={15}/>Archive</button></div>
    </article>; })}</div>
    {createOpen && <div className="modal-backdrop"><div className="modal"><div className="modal-head"><h2>{pairing ? "Pair Android device" : "Create device"}</h2><button className="icon-button" onClick={() => setCreateOpen(false)} aria-label="Close"><X/></button></div>{!pairing ? <form onSubmit={submit}><label>Device name<input required name="name" placeholder="Branch 1 · Samsung A55"/></label><p className="muted">A unique six-digit code is valid for ten minutes.</p>{create.error && <div className="error">{create.error.message}</div>}<button className="btn" disabled={create.isPending}>Generate pairing code</button></form> : <div className="pair-code"><span className="eyebrow">Enter this code in the Android app</span><strong>{pairing.pairingCode}</strong><p className="muted">Expires {new Date(pairing.expiresAtUtc).toLocaleTimeString()}</p><button className="btn btn-secondary" onClick={() => navigator.clipboard.writeText(pairing.pairingCode)}><Copy size={17}/>Copy code</button></div>}</div></div>}
  </Shell>;
}

function Diagnostic({ label, value, ok = true }: { label: string; value: string; ok?: boolean }) { return <div><span>{label}</span><strong className={ok ? "" : "text-warning"}>{!ok && <BatteryWarning size={13}/>} {value}</strong></div>; }
function format(value?: string) { return value ? new Date(value).toLocaleString() : "Not yet"; }
function deviceHealth(device: Device, now: number) {
  if (!device.isActive) return { label: "Disabled", tone: "danger", icon: "offline" };
  const contactAge = device.lastSeenAtUtc ? now - new Date(device.lastSeenAtUtc).getTime() : Number.POSITIVE_INFINITY;
  if (contactAge < 30 * 60 * 1000) return { label: "Online", tone: "success", icon: "online" };
  if (!device.lastHeartbeatAtUtc && device.lastCaptureAtUtc) return { label: "Heartbeat unavailable", tone: "", icon: "warning" };
  if (!device.lastHeartbeatAtUtc) return { label: "Setup incomplete", tone: "", icon: "warning" };
  return { label: "Offline", tone: "danger", icon: "offline" };
}
