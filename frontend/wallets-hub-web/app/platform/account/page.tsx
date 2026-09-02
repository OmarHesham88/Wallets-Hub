"use client";

import { FormEvent, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { KeyRound, Mail, ShieldCheck } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, User } from "@/lib/api";

type AccountUpdate = {
  email: string;
  currentPassword: string;
  newPassword: string | null;
};

export default function PlatformAccountPage() {
  const client = useQueryClient();
  const [notice, setNotice] = useState("");
  const me = useQuery({ queryKey: ["me"], queryFn: () => api<User>("/api/auth/me") });
  const update = useMutation({
    mutationFn: (body: AccountUpdate) => api<void>("/api/platform/account", { method: "PUT", body: JSON.stringify(body) }),
    onSuccess: async () => {
      setNotice("Platform administrator credentials updated successfully.");
      await client.invalidateQueries({ queryKey: ["me"] });
    },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setNotice("");
    const form = event.currentTarget;
    const values = new FormData(form);
    const newPassword = String(values.get("newPassword") ?? "");
    const confirmPassword = String(values.get("confirmPassword") ?? "");
    if (newPassword !== confirmPassword) { setNotice("The new passwords do not match."); return; }
    update.mutate({
      email: String(values.get("email") ?? "").trim(),
      currentPassword: String(values.get("currentPassword") ?? ""),
      newPassword: newPassword || null,
    }, { onSuccess: () => form.reset() });
  }

  return <Shell>
    <div className="page-head">
      <div><span className="eyebrow">Platform administration</span><h1>Account settings</h1><p>Change the platform administrator login email and password.</p></div>
    </div>
    <div className="grid">
      <form className="card" onSubmit={submit} style={{ maxWidth: 720 }} key={me.data?.email}>
        <div className="card-top"><div className="card-icon"><ShieldCheck /></div><span className="badge success">Platform admin</span></div>
        <div className="form-grid">
          <label className="full"><span><Mail size={15} /> Login email</span><input name="email" type="email" defaultValue={me.data?.email ?? ""} autoComplete="email" required /></label>
          <label className="full"><span><KeyRound size={15} /> Current password</span><input name="currentPassword" type="password" autoComplete="current-password" required /></label>
          <label>New password<input name="newPassword" type="password" autoComplete="new-password" minLength={8} /></label>
          <label>Confirm new password<input name="confirmPassword" type="password" autoComplete="new-password" minLength={8} /></label>
        </div>
        <p className="muted">Leave the new-password fields empty to change only the email. Passwords require at least eight characters, including a letter and number.</p>
        {notice && <div className={notice.includes("successfully") ? "notice" : "error"} role="status">{notice}</div>}
        {update.error && <div className="error" role="alert">{update.error.message}</div>}
        <button className="btn" disabled={update.isPending}>{update.isPending ? "Saving…" : "Update credentials"}</button>
      </form>
    </div>
  </Shell>;
}
