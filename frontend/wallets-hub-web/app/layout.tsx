import type { Metadata } from "next";
import "./globals.css";
import { Providers } from "@/components/providers";

export const metadata: Metadata = { title: "Wallets Hub | Smart wallet payment management", description: "Capture and manage Axis, Vodafone Cash, InstaPay, Orange Cash, and e& Cash payments with team access and advanced reporting." };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en" suppressHydrationWarning><body><Providers>{children}</Providers></body></html>;
}
