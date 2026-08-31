"use client";

import { FormEvent, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, ShieldCheck, Users, X } from "lucide-react";
import { Shell } from "@/components/shell";
import { api } from "@/lib/api";

type Wallet = { id: string; name: string };
type Member = { id: string; displayName: string; email: string; role: string; isActive: boolean; visibleReceiptDays: number; walletIds: string[]; canViewReports: boolean; canExportReports: boolean; canManageDevices: boolean; canManageTeam: boolean };

export default function TeamPage() {
  const client = useQueryClient();
  const [open, setOpen] = useState(false);
  const team = useQuery({ queryKey: ["team"], queryFn: () => api<Member[]>("/api/team") });
  const wallets = useQuery({ queryKey: ["wallets"], queryFn: () => api<Wallet[]>("/api/wallets") });
  const create = useMutation({ mutationFn: (body: object) => api("/api/team", { method: "POST", body: JSON.stringify(body) }), onSuccess: () => { setOpen(false); client.invalidateQueries({ queryKey: ["team"] }); } });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const flag = (name: string) => form.get(name) === "on";
    create.mutate({ displayName: form.get("name"), email: form.get("email"), password: form.get("password"), role: form.get("role"), visibleReceiptDays: Number(form.get("days")), canViewReports: flag("reports"), canExportReports: flag("export"), canManageDevices: flag("devices"), canManageTeam: flag("team"), walletIds: form.getAll("wallets") });
  }

  return <Shell>
    <div className="page-head"><div><span className="eyebrow">People & permissions</span><h1>Team access</h1><p>Control which wallets, history, management tools, and reports each person can access.</p></div><button className="btn" onClick={() => setOpen(true)}><Plus size={18}/>Add team member</button></div>
    <div className="table-wrap"><table><thead><tr><th>Person</th><th>Role</th><th>History</th><th>Wallets</th><th>Reports</th><th>Status</th></tr></thead><tbody>{(team.data ?? []).map((member) => <tr key={member.id}><td><strong>{member.displayName}</strong><br/><span className="muted">{member.email}</span></td><td>{member.role}</td><td>{member.visibleReceiptDays} days</td><td>{member.walletIds.length}</td><td>{member.canViewReports ? "View" : "No access"}{member.canExportReports ? " + export" : ""}</td><td><span className={`badge ${member.isActive ? "success" : "danger"}`}>{member.isActive ? "Active" : "Disabled"}</span></td></tr>)}</tbody></table></div>
    {!team.isLoading && (team.data ?? []).length === 0 && <div className="empty"><div><Users/><h2>No team members</h2></div></div>}
    {open && <div className="modal-backdrop"><form className="modal" onSubmit={submit}><div className="modal-head"><h2>Add team member</h2><button type="button" className="icon-button" onClick={() => setOpen(false)}><X/></button></div><div className="form-grid"><label>Full name<input name="name" required/></label><label>Email<input name="email" type="email" required/></label><label>Temporary password<input name="password" type="password" minLength={8} required/></label><label>Role<select name="role"><option>Employee</option><option>Manager</option><option>Admin</option><option>Owner</option></select></label><label>Visible receipt history (days)<input name="days" type="number" min="1" max="3650" defaultValue="2" required/></label><label>Assigned wallets<select name="wallets" multiple style={{ minHeight: 110 }}>{(wallets.data ?? []).map((wallet) => <option value={wallet.id} key={wallet.id}>{wallet.name}</option>)}</select></label></div><p className="eyebrow">Additional permissions</p><div className="permission-grid">{[["reports", "View reports"], ["export", "Export reports"], ["devices", "Manage devices"], ["team", "Manage team"]].map(([name, label]) => <label className="checkbox" key={name}><input type="checkbox" name={name}/>{label}</label>)}</div>{create.error && <div className="error" style={{ marginTop: 12 }}>{create.error.message}</div>}<div className="button-row" style={{ marginTop: 18 }}><button className="btn" disabled={create.isPending}><ShieldCheck size={17}/>Create access</button><button type="button" className="btn btn-secondary" onClick={() => setOpen(false)}>Cancel</button></div></form></div>}
  </Shell>;
}
