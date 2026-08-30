"use client";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { BarChart3, Bell, Building2, CircleDollarSign, LayoutDashboard, LogOut, Menu, Settings2, Smartphone, Users, WalletCards, X } from "lucide-react";
import { useState } from "react";
import { api, User } from "@/lib/api";

const organizationLinks = [
  ["/dashboard", "Overview", LayoutDashboard], ["/receipts", "Received money", CircleDollarSign],
  ["/wallets", "Wallets", WalletCards], ["/devices", "Devices", Smartphone],
  ["/team", "Team & access", Users], ["/reports", "Reports", BarChart3], ["/notifications", "Notifications", Bell], ["/settings", "Settings", Settings2],
] as const;

export function Shell({ children }: { children: React.ReactNode }) {
  const path = usePathname(); const router = useRouter(); const client = useQueryClient(); const [open, setOpen] = useState(false);
  const me = useQuery({ queryKey: ["me"], queryFn: () => api<User>("/api/auth/me") });
  const links = me.data?.role === "PlatformAdmin" ? [["/platform", "Client organizations", Building2] as const] : organizationLinks.filter(([href]) => {
    if (href === "/team") return me.data?.role === "Owner" || me.data?.role === "Admin" || me.data?.canManageTeam;
    if (href === "/devices") return me.data?.role === "Owner" || me.data?.role === "Admin" || me.data?.canManageDevices;
    if (href === "/reports") return me.data?.role === "Owner" || me.data?.role === "Admin" || me.data?.canViewReports;
    return true;
  });
  return <div className="app-shell">
    <aside className={`sidebar ${open ? "open" : ""}`}>
      <div className="brand"><Image src="/wallets-hub-logo.png" width={48} height={48} alt="Wallets Hub"/><div><strong>Wallets Hub</strong><span>{me.data?.organizationName ?? "Platform console"}</span></div><button className="icon-button mobile-close" onClick={() => setOpen(false)}><X/></button></div>
      <nav>{links.map(([href, label, Icon]) => <Link className={path === href ? "active" : ""} href={href} key={href} onClick={() => setOpen(false)}><Icon size={19}/>{label}</Link>)}</nav>
      <div className="sidebar-user"><span className="avatar">{me.data?.displayName?.slice(0, 1) ?? "W"}</span><div><strong>{me.data?.displayName ?? "Loading…"}</strong><span>{me.data?.role}</span></div><button className="icon-button" title="Log out" onClick={async () => { await api("/api/auth/logout", { method: "POST" }); client.clear(); router.replace("/login"); }}><LogOut size={18}/></button></div>
    </aside>
    <section className="workspace"><header className="mobile-bar"><button className="icon-button" onClick={() => setOpen(true)}><Menu/></button><strong>Wallets Hub</strong></header><main>{children}</main></section>
  </div>;
}
