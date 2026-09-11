"use client";

import { FormEvent, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { KeyRound, LockKeyhole, ShieldCheck } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, User } from "@/lib/api";

type Setup = { secretKey: string; authenticatorUri: string };
export default function PlatformSecurityPage() {
  const client = useQueryClient(); const [setup, setSetup] = useState<Setup>(); const [codes, setCodes] = useState<string[]>([]);
  const me = useQuery({ queryKey: ["me"], queryFn: () => api<User>("/api/auth/me") });
  const action = useMutation({ mutationFn: ({ path, body }: { path: string; body?: object }) => api<{ recoveryCodes?: string[] }>(`/api/auth/${path}`, { method: "POST", body: body ? JSON.stringify(body) : undefined }) });
  async function begin() { try { setSetup(await api<Setup>("/api/auth/mfa/setup", { method: "POST" })); } catch (error) { window.alert((error as Error).message); } }
  function confirm(event: FormEvent<HTMLFormElement>) { event.preventDefault(); action.mutate({ path: "mfa/confirm", body: { code: new FormData(event.currentTarget).get("code") } }, { onSuccess: (result) => { setCodes(result.recoveryCodes ?? []); setSetup(undefined); client.invalidateQueries({ queryKey: ["me"] }); } }); }
  function disable() { const password = window.prompt("Current password:"); if (password) action.mutate({ path: "mfa/disable", body: { password } }, { onSuccess: () => client.invalidateQueries({ queryKey: ["me"] }) }); }
  return <Shell><div className="page-head"><div><span className="eyebrow">Platform protection</span><h1>Security</h1><p>Protect the platform administrator account and revoke other authenticated sessions.</p></div></div><div className="grid two"><section className="panel"><div className="card-icon"><ShieldCheck/></div><h2>Two-factor authentication</h2><p>{me.data?.twoFactorEnabled ? "Two-factor authentication is enabled." : "Require an authenticator code after the platform password."}</p>{me.data?.twoFactorEnabled ? <button className="btn btn-danger" onClick={disable}>Disable two-factor</button> : <button className="btn" onClick={begin}>Set up authenticator</button>}{setup && <form className="mfa-box" onSubmit={confirm}><strong>Add this secret to your authenticator app:</strong><code>{setup.secretKey}</code><label>Six-digit code<input name="code" pattern="[0-9]{6}" inputMode="numeric" required/></label><button className="btn">Confirm</button></form>}{codes.length > 0 && <div className="notice"><strong>Save these recovery codes:</strong><code>{codes.join("\n")}</code></div>}{action.error && <div className="error">{action.error.message}</div>}</section><section className="panel"><div className="card-icon"><LockKeyhole/></div><h2>Authenticated sessions</h2><p>Revoke every other browser and app session while keeping this one signed in.</p><button className="btn btn-secondary" onClick={() => action.mutate({ path: "revoke-other-sessions" }, { onSuccess: () => window.alert("Other sessions revoked.") })}><KeyRound size={17}/>Sign out other sessions</button></section></div></Shell>;
}
