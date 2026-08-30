import type { NextConfig } from "next";

const mobile = process.env.CAPACITOR_BUILD === "true";
const config: NextConfig = {
  output: mobile ? "export" : "standalone",
  trailingSlash: mobile,
  images: { unoptimized: mobile },
  ...(mobile ? {} : { async rewrites() { return [{ source: "/api/:path*", destination: `${process.env.API_INTERNAL_URL ?? "http://localhost:8090"}/api/:path*` }]; } }),
};

export default config;
