import type { Metadata } from "next";
import "./globals.css";
import "react-day-picker/style.css";
import { Providers } from "@/components/providers";

export const metadata: Metadata = { title: "Active Cash | Smart wallet payment management", description: "Capture and manage Vodafone Cash, InstaPay, Orange Cash, e& Cash, and other wallet payments with team access and advanced reporting.", icons: { icon: "/wallets/active-cash-logo.svg" } };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en" suppressHydrationWarning><body><Providers>{children}</Providers></body></html>;
}
