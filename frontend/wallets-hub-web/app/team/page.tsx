"use client";

import { FormEvent, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Edit3, KeyRound, Plus, ShieldCheck, Users, WalletCards, X } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, User } from "@/lib/api";

type Wallet = { id: string; name: string; provider: string };
type Member = { id: string; displayName: string; email: string; role: string; isActive: boolean; visibleReceiptDays: number; allWalletAccess: boolean; walletIds: string[]; canViewReports: boolean; canExportReports: boolean; canManageDevices: boolean; canManageTeam: boolean; canEdit: boolean };

export default function TeamPage() {
  const client = useQueryClient();
  const [editing, setEditing] = useState<Member | null | undefined>(undefined);
  const [allWallets, setAllWallets] = useState(true);
  const me = useQuery({ queryKey: ["me"], queryFn: () => api<User>("/api/auth/me") });
  const team = useQuery({ queryKey: ["team"], queryFn: () => api<Member[]>("/api/team") });
  const wallets = useQuery({ queryKey: ["wallets"], queryFn: () => api<Wallet[]>("/api/wallets") });
  const save = useMutation({
    mutationFn: ({ id, body }: { id?: string; body: object }) => api(id ? `/api/team/${id}` : "/api/team", { method: id ? "PUT" : "POST", body: JSON.stringify(body) }),
    onSuccess: () => { setEditing(undefined); client.invalidateQueries({ queryKey: ["team"] }); },
  });
  const resetPassword = useMutation({ mutationFn: ({ id, password }: { id: string; password: string }) => api(`/api/team/${id}/reset-password`, { method: "POST", body: JSON.stringify({ newPassword: password }) }) });
  const roles = me.data?.role === "Owner" ? ["Employee", "Manager", "Admin", "Owner"] : me.data?.role === "Admin" ? ["Employee", "Manager"] : ["Employee"];

  function openCreate() { setAllWallets(true); setEditing(null); save.reset(); }
  function openEdit(member: Member) { setAllWallets(member.allWalletAccess); setEditing(member); save.reset(); }
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = new FormData(event.currentTarget); const flag = (name: string) => form.get(name) === "on";
    save.mutate({ id: editing?.id, body: { displayName: form.get("name"), email: form.get("email"), password: form.get("password") || undefined, role: form.get("role"), isActive: editing ? flag("active") : true, visibleReceiptDays: Number(form.get("days")), canViewReports: flag("reports"), canExportReports: flag("export"), canManageDevices: flag("devices"), canManageTeam: flag("team"), allWalletAccess: allWallets, walletIds: allWallets ? [] : form.getAll("wallets") } });
  }
  function changePassword(member: Member) {
    const password = window.prompt(`Enter a new temporary password for ${member.displayName} (at least 8 characters):`);
    if (password) resetPassword.mutate({ id: member.id, password }, { onSuccess: () => window.alert("Password updated. Other sessions will be signed out shortly."), onError: (error) => window.alert(error.message) });
  }

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">People & permissions</span><h1>Team access</h1><p>Assign all wallets by default or restrict a person to selected wallets, then update access whenever responsibilities change.</p></div><button className="btn" onClick={openCreate}><Plus size={18}/>Add team member</button></div>
    {team.error && <div className="error">{team.error.message}</div>}
    <div className="grid">{(team.data ?? []).map((member) => <article className="card" key={member.id}>
      <div className="card-top"><div className="card-icon"><Users/></div><span className={`badge ${member.isActive ? "success" : "danger"}`}>{member.isActive ? "Active" : "Disabled"}</span></div>
      <h2>{member.displayName}</h2><p>{member.email}</p><p><strong>{member.role}</strong> · {member.visibleReceiptDays} days of history</p>
      <p><WalletCards size={14} className="inline-icon"/> {member.allWalletAccess ? "All wallets, including future wallets" : `${member.walletIds.length} selected wallet${member.walletIds.length === 1 ? "" : "s"}`}</p>
      <p>{member.canViewReports ? "Reports" : "No reports"}{member.canExportReports ? " + export" : ""} · {member.canManageDevices ? "Devices" : "No device management"} · {member.canManageTeam ? "Team" : "No team management"}</p>
      {member.canEdit && <div className="button-row" style={{ marginTop: 14 }}><button className="btn btn-secondary btn-small" onClick={() => openEdit(member)}><Edit3 size={15}/>Edit access</button><button className="btn btn-secondary btn-small" onClick={() => changePassword(member)}><KeyRound size={15}/>Reset password</button></div>}
    </article>)}</div>
    {!team.isLoading && (team.data ?? []).length === 0 && <div className="empty"><div><Users/><h2>No team members</h2></div></div>}
    {editing !== undefined && <div className="modal-backdrop"><form className="modal" onSubmit={submit} key={editing?.id ?? "new"}>
      <div className="modal-head"><div><span className="eyebrow">{editing ? "Update member" : "New team member"}</span><h2>{editing ? editing.displayName : "Create access"}</h2></div><button type="button" className="icon-button" onClick={() => setEditing(undefined)} aria-label="Close"><X/></button></div>
      <div className="form-grid"><label>Full name<input name="name" defaultValue={editing?.displayName} required/></label><label>Email<input name="email" type="email" defaultValue={editing?.email} required/></label>
        {!editing && <label>Temporary password<input name="password" type="password" minLength={8} required autoComplete="new-password"/></label>}
        <label>Role<select name="role" defaultValue={editing?.role ?? "Employee"}>{roles.map((role) => <option key={role}>{role}</option>)}</select></label>
        <label>Visible receipt history (days)<input name="days" type="number" min="1" max="3650" defaultValue={editing?.visibleReceiptDays ?? 2} required/></label>
        {editing && <label className="checkbox"><input type="checkbox" name="active" defaultChecked={editing.isActive}/>Account is active</label>}
      </div>
      <div className="access-box"><div className="card-top"><div><strong>Wallet access</strong><p className="muted">All wallets is the default and automatically includes future wallets.</p></div></div>
        <div className="segmented"><button type="button" className={allWallets ? "active" : ""} onClick={() => setAllWallets(true)}>All wallets</button><button type="button" className={!allWallets ? "active" : ""} onClick={() => setAllWallets(false)}>Selected wallets</button></div>
        {!allWallets && <div className="wallet-picker">{(wallets.data ?? []).map((wallet) => <label className="checkbox" key={wallet.id}><input type="checkbox" name="wallets" value={wallet.id} defaultChecked={editing?.walletIds.includes(wallet.id)}/><span><strong>{wallet.name}</strong><small>{wallet.provider}</small></span></label>)}{(wallets.data ?? []).length === 0 && <p className="muted">Create a wallet first, or use All wallets.</p>}</div>}
      </div>
      <p className="eyebrow">Additional permissions</p><div className="permission-grid">{[["reports", "View reports", editing?.canViewReports], ["export", "Export reports", editing?.canExportReports], ["devices", "Manage devices", editing?.canManageDevices], ["team", "Manage lower-role team members", editing?.canManageTeam]].map(([name, label, checked]) => <label className="checkbox" key={String(name)}><input type="checkbox" name={String(name)} defaultChecked={Boolean(checked)}/>{String(label)}</label>)}</div>
      {save.error && <div className="error" style={{ marginTop: 12 }}>{save.error.message}</div>}
      <div className="button-row" style={{ marginTop: 18 }}><button className="btn" disabled={save.isPending}><ShieldCheck size={17}/>{save.isPending ? "Saving…" : editing ? "Save changes" : "Create access"}</button><button type="button" className="btn btn-secondary" onClick={() => setEditing(undefined)}>Cancel</button></div>
    </form></div>}
  </Shell>;
}
