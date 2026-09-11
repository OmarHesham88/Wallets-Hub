# Wallets Hub

Wallets Hub is an independent, multi-tenant wallet-payment operations product. It does not share ServiceHub's database or application runtime.

## Product hierarchy

- **Platform administrator** creates and suspends client organizations and their first owner.
- **Owner** controls the organization's administrators, managers, employees, wallets, devices, reports, and notification settings.
- **Admin** has full operational access without platform or owner lifecycle control.
- **Manager** receives explicitly assigned permissions.
- **Employee** receives all-wallet access by default, including wallets created later, or can be restricted to an explicit wallet selection and receipt-history period.

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

Capture sources are intentionally separated: Vodafone Cash and InstaPay/instant-IPN card credits are read only from SMS, while Binance USDT receipts are read only from Binance notifications. Android performs a broad provider/channel gate and the server performs detailed receipt parsing, allowing most future message-format updates without another APK install. The pairing screen requests both Android permissions and can scan SMS from the previous 30 days.

Paired phones send a heartbeat every 15 minutes with permission state, Android/app version, battery-optimization state, and upload-queue health. Revoked device tokens are removed automatically from the phone. Android must be updated to the current APK release to enable heartbeat and the 30-day recovery scan.

## Provider engine

The provider engine recognizes Arabic and English payment formats, normalizes Arabic digits, separates EGP, USD, and USDT, extracts sender/destination/reference fields, rejects outgoing messages and wrong capture channels, and preserves the encrypted original message for reference. The active production routes are Vodafone Cash and InstaPay through SMS and Binance through notifications.

Every captured payment is treated as received immediately. There is no manual confirmation or rejection queue: dashboards, reports, and exports include all captured payments directly.

Every upload also creates a capture-diagnostic record. Accepted, duplicate, rejected, unsupported, and ambiguous events are visible in **Capture inbox**. An administrator can reprocess an event after a server parser update or manually assign a parsed ambiguous event to a compatible wallet, so a failed automatic match is recoverable instead of silently discarded.

## Production operations

- Team accounts support editable roles, activation, password reset, all/selected wallet access, report/export rights, device management, and lower-role team management. Role hierarchy rules prevent privilege escalation and preserve at least one active owner.
- Receipt search is server-paginated and supports date, multiple wallets, provider, currency, device, amount range, sender/reference quality, exact/partial search, sorting, saved local views, and filter-aware Excel export.
- Reports include configurable date ranges, prior-period comparisons, average/median/maximum, wallet/provider/device shares, daily values, peak hours, new/returning senders, and capture-quality metrics.
- Wallet operations include opening balance, operating limit, withdrawals, deposits, manual adjustments, wallet-to-wallet transfers, reconciliation, and combined receipt/ledger statements.
- Device heartbeat drives offline alerts. Daily operational summaries and retention cleanup run in the API background worker.
- Platform admins and client accounts can enable authenticator-based two-factor authentication and revoke other login sessions.
- Optional original-message masking hides most phone-number digits in the UI while encrypted source messages remain stored for diagnostic use.
- The audit trail records the actor, entity, timestamp, and structured details for administrative and financial changes.

The Android app opens on its device capture and pairing screen, with account sign-in available from the same view. After sign-in, the complete web management experience is available inside the APK, and a native-only **This phone** entry keeps pairing, SMS permission, notification permission, and capture status accessible from the dashboard.

New provider variations must be added with regression samples in `WalletMessageParserTests.cs` before release.

Wallet account numbers are unique per provider inside each organization. The same phone number can therefore be registered separately for Vodafone Cash and InstaPay, while duplicate wallets for the same provider and number are rejected.

## Production

Copy `.env.production.example` to the deployment directory as `.env.production` and replace every secret. The production application is served at `https://servicehub.ink/wallets/`. The compose file connects only the web container to the existing `servicehub_default` reverse-proxy network while keeping its database and API isolated.

Deployment creates a verified PostgreSQL backup and matching ASP.NET data-protection-key archive before changing containers. `.github/workflows/backup.yml` also runs daily and retains 30 days on the deployment host. Keeping the data-protection keys with the database is required to decrypt preserved source messages after recovery.

Restore is intentionally interactive and accepts only backup files inside `$HOME/walletshub-backups`:

```bash
bash "$HOME/walletshub/deploy/restore-backup.sh" "$HOME/walletshub-backups/daily-YYYYMMDDTHHMMSSZ.sql.gz"
```

For full disaster recovery, replicate `$HOME/walletshub-backups` to a separately secured off-server location managed by the operator.

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

Subscription billing is intentionally deferred. Organization and owner lifecycle management are already separated so limits and billing can be added without redesigning tenant data.
