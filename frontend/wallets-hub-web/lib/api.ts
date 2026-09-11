export type User = {
  id: string; displayName: string; email: string; role: "PlatformAdmin" | "Owner" | "Admin" | "Manager" | "Employee";
  organizationId?: string; organizationName?: string; organizationSlug?: string; visibleReceiptDays: number;
  canViewReports: boolean; canExportReports: boolean;
  canManageDevices: boolean; canManageTeam: boolean; allWalletAccess: boolean; twoFactorEnabled: boolean;
};

export const appPath = (path: string) => `${process.env.NEXT_PUBLIC_BASE_PATH ?? ""}${path}`;

export async function api<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(appPath(path), { credentials: "include", headers: { "Content-Type": "application/json", ...options?.headers }, ...options });
  if (response.status === 401 && typeof window !== "undefined" && !location.pathname.startsWith(appPath("/login"))) location.assign(appPath("/login"));
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error ?? body.detail ?? body.title ?? `Request failed (${response.status})`);
  }
  return response.status === 204 ? undefined as T : response.json();
}

export const money = (amount: number, currency: string) => currency === "USDT"
  ? `${new Intl.NumberFormat("en-EG", { maximumFractionDigits: 8 }).format(amount)} USDT`
  : new Intl.NumberFormat("en-EG", { style: "currency", currency }).format(amount);

export function queryString(values: Record<string, string | number | boolean | null | undefined>) {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) if (value !== "" && value !== null && value !== undefined) search.set(key, String(value));
  const result = search.toString(); return result ? `?${result}` : "";
}
