"use client";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  BarChart3,
  Bell,
  Building2,
  CircleDollarSign,
  LayoutDashboard,
  Landmark,
  LockKeyhole,
  LogOut,
  Menu,
  Settings2,
  Smartphone,
  Users,
  WalletCards,
  X,
} from "lucide-react";
import { useState } from "react";
import { api, appPath, User } from "@/lib/api";
import { useIsNative } from "@/lib/wallet-native";
import { LanguageToggle, useI18n } from "@/components/i18n";

const organizationLinks = [
  ["/dashboard", "Overview", LayoutDashboard],
  ["/receipts", "Received money", CircleDollarSign],
  ["/wallets", "Wallets", WalletCards],
  ["/wallet-operations", "Balances", Landmark],
  ["/devices", "Devices", Smartphone],
  ["/team", "Team & access", Users],
  ["/reports", "Reports", BarChart3],
  ["/settings", "Settings", Settings2],
] as const;

export function Shell({ children }: { children: React.ReactNode }) {
  const { t } = useI18n();
  const path = usePathname();
  const router = useRouter();
  const client = useQueryClient();
  const [open, setOpen] = useState(false);
  const native = useIsNative();
  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => api<User>("/api/auth/me"),
  });
  const notifications = useQuery({
    queryKey: ["notifications", "badge"],
    queryFn: () => api<{ unreadCount: number }>("/api/notifications"),
    enabled: Boolean(me.data && me.data.role !== "PlatformAdmin"),
    refetchInterval: 20_000,
  });
  const accountLinks =
    me.data?.role === "PlatformAdmin"
      ? [
          ["/platform", "Client organizations", Building2] as const,
          ["/platform/account", "Account settings", Settings2] as const,
          ["/platform/security", "Security", LockKeyhole] as const,
        ]
      : organizationLinks.filter(([href]) => {
          if (href === "/team")
            return (
              me.data?.role === "Owner" ||
              me.data?.role === "Admin" ||
              me.data?.canManageTeam
            );
          if (href === "/devices")
            return (
              me.data?.role === "Owner" ||
              me.data?.role === "Admin" ||
              me.data?.canManageDevices
            );
          if (href === "/reports")
            return (
              me.data?.role === "Owner" ||
              me.data?.role === "Admin" ||
              me.data?.canViewReports
            );
          if (href === "/wallet-operations")
            return (
              me.data?.role === "Owner" ||
              me.data?.role === "Admin" ||
              me.data?.role === "Manager"
            );
          return true;
        });
  const links = native
    ? [...accountLinks, ["/pair-device", "This phone", Smartphone] as const]
    : accountLinks;
  return (
    <div className="app-shell">
      <aside className={`sidebar ${open ? "open" : ""}`}>
        <div className="brand">
          <Image
            src={appPath("/wallets-hub-logo.png")}
            width={48}
            height={48}
            alt="Wallets Hub"
          />
          <div>
            <strong>Wallets Hub</strong>
            <span>{me.data?.organizationName ?? t("Platform console")}</span>
          </div>
          <button
            className="icon-button mobile-close"
            onClick={() => setOpen(false)}
          >
            <X />
          </button>
        </div>
        <nav>
          {links.map(([href, label, Icon]) => (
            <Link
              className={path === href ? "active" : ""}
              href={href}
              key={href}
              onClick={() => setOpen(false)}
            >
              <Icon size={19} />
              {t(label)}
            </Link>
          ))}
        </nav>
        <div className="sidebar-user">
          <span className="avatar">
            {me.data?.displayName?.slice(0, 1) ?? "W"}
          </span>
          <div>
            <strong>{me.data?.displayName ?? t("Loading…")}</strong>
            <span>{t(me.data?.role ?? "")}</span>
          </div>
          <button
            className="icon-button"
            title={t("Log out")}
            onClick={async () => {
              await api("/api/auth/logout", { method: "POST" });
              client.clear();
              router.replace(native ? "/pair-device" : "/login");
            }}
          >
            <LogOut size={18} />
          </button>
        </div>
      </aside>
      <section className="workspace">
        <header className="desktop-toolbar"><LanguageToggle compact/>{me.data?.role !== "PlatformAdmin" && <><div><span className="status-dot"/>{t("Live capture")}</div><Link className="notification-button" href="/notifications" title={t("Notifications")}><Bell size={20}/>{Boolean(notifications.data?.unreadCount) && <span>{notifications.data!.unreadCount > 99 ? "99+" : notifications.data!.unreadCount}</span>}</Link></>}</header>
        <header className="mobile-bar">
          <button className="icon-button" onClick={() => setOpen(true)}>
            <Menu />
          </button>
          <strong>Wallets Hub</strong>
          <LanguageToggle compact/>
          {me.data?.role !== "PlatformAdmin" && <Link className="notification-button" href="/notifications" title={t("Notifications")}><Bell size={19}/>{Boolean(notifications.data?.unreadCount) && <span>{notifications.data!.unreadCount > 99 ? "99+" : notifications.data!.unreadCount}</span>}</Link>}
          {native && <Link className="icon-button native-phone-link" href="/pair-device" title={t("This phone")}><Smartphone size={19}/></Link>}
        </header>
        <main>{children}</main>
      </section>
    </div>
  );
}
