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

- `src/WalletsHub.Api`: ASP.NET Core API, Identity authentication, PostgreSQL persistence, pairing, payment capture, reporting, and auditing.
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
5. The phone filters payment events locally and uploads only matching receipts through `/api/captures`.

Capture sources are intentionally separated: Vodafone Cash and InstaPay/instant-IPN card credits are read only from SMS, while Binance USDT receipts are read only from Binance notifications. Android performs a broad provider/channel gate and the server performs detailed receipt parsing, allowing most future message-format updates without another APK install. The pairing screen requests both Android permissions and can scan SMS from the previous two days.

## Provider engine

The provider engine recognizes Arabic and English payment formats, normalizes Arabic digits, separates EGP, USD, and USDT, extracts sender/destination/reference fields, rejects outgoing messages and wrong capture channels, and preserves the encrypted original message for reference. The active production routes are Vodafone Cash and InstaPay through SMS and Binance through notifications.

Every captured payment is treated as received immediately. There is no manual confirmation or rejection queue: dashboards, reports, and exports include all captured payments directly.

The Android app opens on its device capture and pairing screen, with account sign-in available from the same view. After sign-in, the complete web management experience is available inside the APK, and a native-only **This phone** entry keeps pairing, SMS permission, notification permission, and capture status accessible from the dashboard.

New provider variations must be added with regression samples in `WalletMessageParserTests.cs` before release.

Wallet account numbers are unique per provider inside each organization. The same phone number can therefore be registered separately for Vodafone Cash and InstaPay, while duplicate wallets for the same provider and number are rejected.

## Production

Copy `.env.production.example` to the deployment directory as `.env.production` and replace every secret. The production application is served at `https://servicehub.ink/wallets/`. The compose file connects only the web container to the existing `servicehub_default` reverse-proxy network while keeping its database and API isolated.

## Android download

Every push to `main` independently runs **Publish Android release** and creates the permanently signed release asset `wallets-hub.apk`. Users of the old preview must uninstall it once; later signed releases can update the installed application normally.

`https://github.com/OmarHesham88/Wallets-Hub/releases/download/android-latest/wallets-hub.apk`

Mobile-friendly direct download with resume support:

`https://servicehub.ink/downloads/wallets-hub.apk`

## Publishing without a connected GitHub plugin

Codex maintains and commits the standalone repository locally. From a normal Windows PowerShell session, publish the prepared commit with one command:

```powershell
& "D:\ServiceHub\ServiceHub_Store\ServiceHub System\Code\WalletsHub\Publish-WalletsHub.cmd"
```

The launcher uses a process-only PowerShell execution-policy bypass; it does not weaken the system or user policy. The script verifies the repository, configures the exact Git safe-directory entry when necessary, pushes `main`, prints the permanent APK link, and opens the Actions page. It refuses to publish uncommitted files so partially prepared changes cannot be uploaded accidentally.

Subscription billing is intentionally deferred. Organization and owner lifecycle management are already

separated so limits and billing can be added without redesigning tenant data.
