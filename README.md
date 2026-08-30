# Wallets Hub

Wallets Hub is an independent, multi-tenant wallet-payment operations product. It does not share ServiceHub's database or application runtime.

## Product hierarchy

- **Platform administrator** creates and suspends client organizations and their first owner.
- **Owner** controls the organization's administrators, managers, employees, wallets, devices, reports, and notification settings.
- **Admin** has full operational access without platform or owner lifecycle control.
- **Manager** receives explicitly assigned permissions.
- **Employee** sees only assigned wallets and the receipt-history period configured for that employee.

Platform administrators cannot read client receipt data through the API. Every operational query requires an organization user and is scoped by `OrganizationId`.

## Applications

- `src/WalletsHub.Api`: ASP.NET Core API, Identity authentication, PostgreSQL persistence, pairing, parser, receipt review, reporting, and auditing.
- `frontend/wallets-hub-web`: responsive Next.js web dashboard and Capacitor Android application.
- `tests/WalletsHub.Tests`: provider parser regression suite.
- `docker-compose.production.yml`: independent PostgreSQL, API, and web deployment.

## Local web development

```powershell
Set-Location WalletsHub/frontend/wallets-hub-web
npm ci
npm run dev
```

The frontend proxies `/api/*` to `http://localhost:8090` by default.

## Local API development

```powershell
dotnet run --project src/WalletsHub.Api
```

Set `ConnectionStrings__Postgres` to a dedicated PostgreSQL database. Initialize and seed with:

```powershell
dotnet run --project src/WalletsHub.Api -- --migrate
$env:Seed__PlatformPassword = "a-strong-password"
dotnet run --project src/WalletsHub.Api -- --seed
```

## Android pairing

1. An owner or authorized user creates a device from **Devices**.
2. Wallets Hub returns a six-digit code valid for ten minutes.
3. The Android app opens its pairing screen and exchanges the code for a device-only token.
4. The phone never stores an employee password or web session.
5. The notification listener filters wallet messages locally and uploads matching receipts through `/api/captures`.

Direct SMS permissions are deliberately not required because current Android versions hard-restrict them for ordinary applications. Notification access is the supported capture path.

## Provider engine

The initial provider adapters recognize Arabic and English variants for Vodafone Cash, Orange Cash, e& Cash, WE Pay, InstaPay, and common bank-credit notifications. The parser normalizes Arabic digits, separates EGP and USD, extracts sender/destination/reference fields, rejects outgoing messages, and preserves the encrypted original message for review.

New provider variations must be added with regression samples in `WalletMessageParserTests.cs` before release.

## Production

Copy `.env.production.example` to `/opt/walletshub/.env.production` and replace every secret. The recommended hostname is `wallets.servicehub.ink`. The compose file connects the web container to the existing `servicehub_default` reverse-proxy network while keeping its database and API isolated. The repository's manual deployment workflow installs the included Caddy route when needed.

## Android preview download

Run the **Publish Android preview** workflow after pushing this repository. It creates the stable release asset `wallets-hub-preview.apk`. Because preview builds use GitHub's temporary debug signing identity, users may need to uninstall an older preview before installing a newly generated one. Configure a permanent protected signing key before customer production distribution.

Subscription billing is intentionally deferred. Organization and owner lifecycle management are already separated so limits and billing can be added without redesigning tenant data.
