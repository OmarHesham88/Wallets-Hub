import type { NextConfig } from "next";

const mobile = process.env.CAPACITOR_BUILD === "true";
const basePath = process.env.NEXT_PUBLIC_BASE_PATH ?? "";
const config: NextConfig = {
  output: mobile ? "export" : "standalone",
  basePath,
  trailingSlash: mobile,
  images: { unoptimized: mobile },
  ...(mobile ? {} : { async rewrites() { return [{ source: "/api/:path*", destination: `${process.env.API_INTERNAL_URL ?? "http://localhost:8090"}/api/:path*` }]; } }),
};

export default config;
