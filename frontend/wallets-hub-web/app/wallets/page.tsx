"use client";
import { FormEvent, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, WalletCards, X } from "lucide-react";
import { Shell } from "@/components/shell";
import { api, User } from "@/lib/api";
type Wallet = {
  id: string;
  name: string;
  provider: string;
  accountNumber: string;
  currencyCode: string;
  deviceId?: string;
  isActive: boolean;
};
type Device = { id: string; name: string };
export default function WalletsPage() {
  const client = useQueryClient();
  const [open, setOpen] = useState(false);
  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => api<User>("/api/auth/me"),
  });
  const wallets = useQuery({
    queryKey: ["wallets"],
    queryFn: () => api<Wallet[]>("/api/wallets"),
  });
  const devices = useQuery({
    queryKey: ["devices"],
    queryFn: () => api<Device[]>("/api/devices"),
    retry: false,
  });
  const create = useMutation({
    mutationFn: (body: object) =>
      api("/api/wallets", { method: "POST", body: JSON.stringify(body) }),
    onSuccess: () => {
      setOpen(false);
      client.invalidateQueries({ queryKey: ["wallets"] });
    },
  });
  const remove = useMutation({
    mutationFn: (id: string) => api(`/api/wallets/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: ["wallets"] });
      client.invalidateQueries({ queryKey: ["devices"] });
    },
  });
  const canManage = ["Owner", "Admin"].includes(me.data?.role ?? "");
  function deleteWallet(wallet: Wallet) {
    if (
      window.confirm(
        `Delete “${wallet.name}”? It will disappear from management, while any historical payment records remain preserved in reports.`,
      )
    )
      remove.mutate(wallet.id);
  }
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    create.mutate({
      name: f.get("name"),
      provider: f.get("provider"),
      accountNumber: f.get("account"),
      currencyCode: f.get("currency"),
      deviceId: f.get("device") || null,
      isActive: true,
    });
  }
  return (
    <Shell>
      <div className="page-head">
        <div>
          <span className="eyebrow">Wallet registry</span>
          <h1>Wallets</h1>
          <p>
            Organize every receiving account and connect it to the phone that
            captures its messages.
          </p>
        </div>
        {canManage && (
          <button className="btn" onClick={() => setOpen(true)}>
            <Plus size={18} />
            Add wallet
          </button>
        )}
      </div>
      {remove.error && <div className="error">{remove.error.message}</div>}
      <div className="grid">
        {(wallets.data ?? []).map((w) => (
          <article className="card" key={w.id}>
            <div className="card-top">
              <div className="card-icon">
                <WalletCards />
              </div>
              <span className={`badge ${w.isActive ? "success" : "danger"}`}>
                {w.isActive ? "Active" : "Paused"}
              </span>
            </div>
            <h2>{w.name}</h2>
            <p>
              <strong>{w.provider}</strong>
            </p>
            <p>
              {w.accountNumber} · {w.currencyCode}
            </p>
            <p>
              {w.deviceId
                ? (devices.data?.find((d) => d.id === w.deviceId)?.name ??
                  "Assigned device")
                : "No device assigned"}
            </p>
            {canManage && (
              <button
                className="btn btn-danger btn-small"
                style={{ marginTop: 14 }}
                disabled={remove.isPending}
                onClick={() => deleteWallet(w)}
              >
                <Trash2 size={15} />
                Delete wallet
              </button>
            )}
          </article>
        ))}
      </div>
      {!wallets.isLoading && (wallets.data ?? []).length === 0 && (
        <div className="empty">
          <div>
            <WalletCards />
            <h2>No wallets yet</h2>
            <p className="muted">
              Create your first receiving wallet to begin pairing devices.
            </p>
          </div>
        </div>
      )}
      {open && (
        <div className="modal-backdrop">
          <form className="modal" onSubmit={submit}>
            <div className="modal-head">
              <h2>Add wallet</h2>
              <button
                type="button"
                className="icon-button"
                onClick={() => setOpen(false)}
              >
                <X />
              </button>
            </div>
            <div className="form-grid">
              <label>
                Wallet name
                <input name="name" required placeholder="Branch 1 Vodafone" />
              </label>
              <label>
                Provider
                <select name="provider">
                  <option>Vodafone Cash</option>
                  <option>Orange Cash</option>
                  <option>e&amp; Cash</option>
                  <option>WE Pay</option>
                  <option>InstaPay</option>
                  <option>Bank transfer</option>
                </select>
              </label>
              <label>
                Phone or account number
                <input name="account" required />
              </label>
              <label>
                Currency
                <select name="currency">
                  <option>EGP</option>
                  <option>USD</option>
                </select>
              </label>
              <label className="full">
                Capturing device
                <select name="device">
                  <option value="">Assign later</option>
                  {(devices.data ?? []).map((d) => (
                    <option value={d.id} key={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            {create.error && (
              <div className="error">{create.error.message}</div>
            )}
            <div className="button-row" style={{ marginTop: 18 }}>
              <button className="btn" disabled={create.isPending}>
                Create wallet
              </button>
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => setOpen(false)}
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}
    </Shell>
  );
}
