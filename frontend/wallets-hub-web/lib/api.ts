export type User = {
  id: string; displayName: string; email: string; role: "PlatformAdmin" | "Owner" | "Admin" | "Manager" | "Employee";
  organizationId?: string; organizationName?: string; organizationSlug?: string; visibleReceiptDays: number;
  canConfirmReceipts: boolean; canRejectReceipts: boolean; canViewReports: boolean; canExportReports: boolean;
  canManageDevices: boolean; canManageTeam: boolean;
};

export async function api<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(path, { credentials: "include", headers: { "Content-Type": "application/json", ...options?.headers }, ...options });
  if (response.status === 401 && typeof window !== "undefined" && !location.pathname.startsWith("/login")) location.assign("/login");
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error ?? body.detail ?? body.title ?? `Request failed (${response.status})`);
  }
  return response.status === 204 ? undefined as T : response.json();
}

export const money = (amount: number, currency: string) => new Intl.NumberFormat("en-EG", { style: "currency", currency }).format(amount);
