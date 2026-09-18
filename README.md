# Shiai Manager

[Deutsch](README.de.md)

**Shiai Manager** (brand **SHIAI**) is a tournament management application for on-site judo events, designed to run reliably offline on a single laptop or local LAN, deployable internet-hosted behind an nginx reverse proxy, and with German as the primary user language. It combines an offline-capable ASP.NET Core backend, SQLite persistence, and an Angular frontend — styled with the SHIAI dual-theme (light/dark) dojo design system — to support tournament planning, match operation, registration, and real-time display workflows.

## Quick installation guide

On a fresh Debian/Ubuntu or RHEL-compatible (for example Oracle Linux 10)
Proxmox/LXC host, install a released build with the bootstrap command.

Install the downloader prerequisites first.

Debian/Ubuntu:

```bash
sudo apt-get update
sudo apt-get install -y curl ca-certificates unzip
```

RHEL-compatible systems:

```bash
sudo dnf install -y curl ca-certificates unzip
```

Then run the bootstrap command:

```bash
curl -fsSL https://raw.githubusercontent.com/McGyver666/Shiai-Manager/main/deploy/bootstrap_install.sh \
  | sudo bash -s -- --hostname tournament.example.com --email admin@example.com
```

It downloads the latest release (or a pinned `--version vX.Y.Z`), verifies its checksum, and runs the installer. For the `--version` flag, upgrades, and a security-conscious inspect-before-run alternative, see [`deploy/README.md`](deploy/README.md).

On a **fresh** install the script creates an initial `admin` account with a random password and prints it once at the end, in a clearly marked block — save it, as it is not shown again. Re-running the installer (an upgrade) leaves the existing account untouched and prints nothing:

```text
============================================================
Initial admin credentials (save these now):
  Username: admin
  Password: <generated>
============================================================
```

## Features

- Offline-capable tournament operation on a single laptop or local LAN, with optional internet-hosted deployment behind nginx.
- Tournament setup, clubs, athletes, registrations, category presets and assisted category generation.
- Individual tournament draws, brackets, tatami assignment, live fight control and server-authoritative timing.
- NWJV team-matchday workflows for senior and U16 profiles, including weigh-ins, lineups and encounters.
- Athlete imports from DM4/DMF files, public displays, match lists, rankings, medal tables and club scoring.
- Local authentication with role-based access, HttpOnly sessions, audit logging and guest sharing.
- SQLite persistence, backup/restore, SignalR realtime updates, German-first localization and an Angular frontend served by the API.

## Current Technical Status
- **Backend:** ASP.NET Core Web API (.NET 10)
- **Solution style:** modular monolith
- **Persistence:** SQLite via EF Core (`App_Data/judo-tournament.db`, auto-created on startup)
- **Schema compatibility:** Startup uses EF Core migrations and migration history; legacy local databases without migration history are adopted safely at startup.
- **Frontend:** Angular 19 SPA (`frontend/`), built into the API `wwwroot/` and served same-origin
- **Health endpoint:** `/health`
- **App entry point:** `/` (Angular app; deep links fall back to `index.html`)

## Target Architecture
- **Backend:** ASP.NET Core Web API
- **Frontend:** SPA served locally by the host machine
- **Database:** SQLite
- **Realtime updates:** SignalR/WebSockets
- **Operation mode:** local machine or local LAN, or internet-hosted behind an nginx reverse proxy (see `deploy/`)

## Project Structure

```text
ShiaiManager.sln
ShiaiManager.Api/
ShiaiManager.Api.Tests/
frontend/
deploy/
docs/
AGENTS.md
CONTEXT.md
start-local.ps1
start-local.sh
```

## Prerequisites

The project can run on Windows, Linux, and macOS.

Preferred .NET resolution order used by startup scripts:
1. local SDK in `.dotnet/`
2. machine-wide `dotnet` from `PATH`

Windows local SDK path:

```powershell
.\.dotnet\dotnet.exe
```

Linux/macOS local SDK path:

```bash
./.dotnet/dotnet
```

This allows fully local execution without depending on a machine-wide installation.

## Running Locally

Start the API locally (Windows / PowerShell):

```powershell
.\start-local.ps1
```

Skip frontend build and start backend only (Windows / PowerShell):

```powershell
.\start-local.ps1 -SkipFrontendBuild
```

If `ShiaiManager.Api/wwwroot/index.html` does not exist yet, the startup script performs a one-time frontend build even with `-SkipFrontendBuild` so the UI does not come up as `404`.

Start with optional HTTPS binding for LAN mode (Windows / PowerShell):

```powershell
.\start-local.ps1 -EnableTls
```

Start the API locally (Linux/macOS / bash):

```bash
chmod +x ./start-local.sh
./start-local.sh
```

Skip frontend build and start backend only (Linux/macOS / bash):

```bash
./start-local.sh --skip-frontend-build
```

If `ShiaiManager.Api/wwwroot/index.html` does not exist yet, the startup script performs a one-time frontend build even with `--skip-frontend-build` so the UI does not come up as `404`.

Start with optional HTTPS binding for LAN mode (Linux/macOS / bash):

```bash
./start-local.sh --enable-tls --https-port 7080
```

By default, both startup scripts build the Angular frontend before launching the API so `wwwroot` stays in sync with current UI sources.

This starts the API on:

```text
http://0.0.0.0:5080
```

With TLS enabled, startup scripts bind both HTTP and HTTPS, for example:

```text
http://0.0.0.0:5080
https://0.0.0.0:7080
```

Useful endpoints:
- Landing page: `http://localhost:5080/`
- Health: `http://localhost:5080/health`
- OpenAPI document (Development): `http://localhost:5080/openapi/v1.json`

If you run an older local database, startup will auto-add missing legacy columns needed by current features.
For larger local schema drifts, reset the local database by deleting `ShiaiManager.Api/App_Data/judo-tournament.db*` and restart the API.

## Production Deployment (internet-hosted)

For an internet-facing deployment, the app runs behind an nginx reverse proxy that
terminates TLS (Let's Encrypt) and forwards to the API on `127.0.0.1:5080`. The public
hostname is set at deploy time — the shipped nginx config uses a `__SERVER_NAME__`
placeholder that is substituted during installation. See `deploy/README.md` and
`deploy/shiai-manager.nginx.conf` for the systemd unit, nginx config, and Certbot setup.

The API trusts the `X-Forwarded-Proto` and `X-Forwarded-For` headers only from the
loopback proxy (`127.0.0.1`), so `HttpContext.Request.Scheme` reflects the original HTTPS
request and generated links (e.g. the guest-share public URL) use `https://`. In
offline/LAN mode without a proxy, no forwarded headers are present and the scheme stays
`http`.

Production host validation is restricted through `AllowedHosts`, and guest-share links use
the canonical `GuestShare__PublicBaseUrl` rather than the incoming `Host` header. The installer
sets both values from `--hostname`; manual deployments must configure them in the systemd
environment file.

Unlike the offline/LAN mode, this mode is public-facing and does not rely on a trusted
local network — keep TLS enforced, send the CSRF header for state-changing cookie-authenticated
requests, and inject secrets (e.g. `Security:AuthTokenHmacSecret`) via configuration rather than hardcoding them.

## Admin password bootstrap

Server installs done with `deploy/install_release.sh` (or the one-command bootstrap) create the initial admin automatically and print the credentials once — see [Quick installation guide](#quick-installation-guide). The steps below are for local/manual runs.

On first launch, the database is empty. Bootstrap an admin account using the `/api/auth/bootstrap-admin` endpoint:

**Windows (PowerShell):**

```powershell
$body = @{
    username = "admin"
    password = "MySecurePassword123!"
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:5080/api/auth/bootstrap-admin" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body
```

**Linux/macOS (curl):**

```bash
curl -X POST http://localhost:5080/api/auth/bootstrap-admin \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "MySecurePassword123!"
  }'
```

After successful bootstrap, log in at `http://localhost:5080/login` with your credentials.

**Note:** The bootstrap endpoint only works when no admin accounts exist. It will return an error if an admin user is already present.

## Build and Tests

Build the solution (Windows with local SDK):

```powershell
.\.dotnet\dotnet.exe build .\ShiaiManager.sln
```

Build the solution (Linux/macOS with local SDK):

```bash
./.dotnet/dotnet build ./ShiaiManager.sln
```

Build the solution (any OS with global SDK):

```bash
dotnet build ./ShiaiManager.sln
```

Run all unit tests (Windows with local SDK):

```powershell
.\.dotnet\dotnet.exe test .\ShiaiManager.sln --filter Category=UnitTest
```

Run all unit tests (Linux/macOS with local SDK):

```bash
./.dotnet/dotnet test ./ShiaiManager.sln --filter Category=UnitTest
```

Run all unit tests (any OS with global SDK):

```bash
dotnet test ./ShiaiManager.sln --filter Category=UnitTest
```

## Package for Another System

Create a minimal transfer bundle (published API + start scripts + README):

```powershell
.\package-transfer.ps1
```

By default the script builds the Angular frontend first so the package is directly runnable.
For API-only packaging, skip this step:

```powershell
.\package-transfer.ps1 -SkipFrontendBuild
```

Create a self-contained package for a specific runtime (larger, no dotnet runtime required on target):

```powershell
.\package-transfer.ps1 -Runtime win-x64 -SelfContained
```

Include the local SQLite data (`App_Data`) in the package:

```powershell
.\package-transfer.ps1 -IncludeDatabase
```

Output is written to `artifacts/transfer/` as a timestamped folder plus zip archive.

## Frontend (Angular)

The Angular 19 app lives in `frontend/` and is compiled into the API's `wwwroot/`,
so the running API serves the UI at `/` (no separate web server needed).

Install dependencies (once):

```powershell
cd frontend
npm install
```

Build the UI into `wwwroot/` (run before starting the API to refresh the served app):

```powershell
cd frontend
npm run build
```

Optional UI-only dev server with hot reload (proxy API calls to the running backend):

```powershell
cd frontend
npm start
```

Run frontend unit tests once (headless, exits automatically):

```powershell
cd frontend
npm run test:ci
```

This avoids Karma staying open in watch mode after tests finish.

Localization assets are plain JSON dictionaries in `frontend/public/i18n/`
(`de.json` is the complete German source; `en.json` is the English fallback) and
are served at `/i18n/{lang}.json`.

## API

See [API-doc.md](API-doc.md) for the current HTTP API endpoints and frontend routes.

## Guest access (public match lists)

Spectators on the local network can open a read-only view of the match lists by
scanning a QR code — without an account and without touching the app shell.

Workflow:
- On the tournament view, an Admin or Operator opens the **Guest access** panel and
  clicks **Enable**. This creates exactly one guest token per tournament and shows a
  QR code plus a shareable link (`/public/match-lists?tid=…&t=<token>`).
- The auto-off preset controls an optional expiry: **until midnight today** (default),
  **4h**, **8h**, or **no auto-off**.
- **Rotate** issues a fresh token and immediately invalidates the previous QR.
  **Disable** switches the share off without discarding the token.
- Guests reach only the public, read-only match lists of that one tournament. A bare
  authenticated endpoint still requires an operator role, so guest access never
  extends beyond the match lists.

Data minimization:
- The public endpoints return reduced DTOs only (athletes = id, club, first/last name;
  clubs = id, name). No license/pass number, birth year, weight, grade, or contact
  data is exposed. The whole match-list view (Display and Guest) uses this same
  reduced model.

TLS rule:
- On a local/LAN host (localhost, private IP ranges, single-label or `.local`/`.lan`
  host names) plain HTTP is accepted.
- On a non-local/public host the guest link and QR are only served over **HTTPS**;
  requesting them over plain HTTP returns `400 Bad Request`. In production nginx
  terminates TLS in front of the app. An explicit base URL can be configured via
  `GuestShare:PublicBaseUrl`.

Realtime and lifecycle:
- Guests join the existing broadcast-only SignalR hub, but only their own
  tournament group, and only while the share is active (soft-disconnect: once a
  share is disabled, no new connections or joins are accepted; running connections
  simply run out).
- Guest reads are not logged; enabling, disabling and rotating are audited
  (`GuestShareEnabled`/`GuestShareDisabled`/`GuestShareRotated`) without the token.
- Backups never contain the guest token, and a restore always leaves the share
  disabled (re-enable to share again).
- The public endpoints have their own per-IP rate-limit window.

## Localization

Localization rules for the MVP:
- primary language is German
- visible UI texts should be German by default
- new UI work must be localization-ready
- avoid hardcoded visible English strings in the product UI

Current backend culture setup:
- default: `de-DE`
- fallback-ready secondary culture: `en-US`

## Development Principles

- offline-capable before cloud-dependent
- simple architecture before distributed architecture
- German-first UX
- localizable UI from the first screen
- explicit validation on all write endpoints
- no silent error swallowing
- track work in GitHub Issues; keep decisions in `docs/adr/` and domain context in `CONTEXT.md`

## Security and Operating Model

- no mandatory internet dependency for on-site tournament execution
- supports offline/LAN operation and an internet-hosted deployment behind nginx (see `deploy/`)
- the internet-hosted mode is public-facing: enforce TLS and treat the network as untrusted (do not rely on the trusted-LAN assumption)
- all future auth, audit logging, and backup features must follow the backlog
- secrets must never be hardcoded if external integrations are added later
- SignalR hub access requires authentication; operator sessions use the HttpOnly session cookie and guest shares use an ephemeral bearer token
- fight timing remains server-authoritative; frontend clock sync is display-only and must not make rules decisions offline
- helper scripts abort when `ASPNETCORE_ENVIRONMENT=Production`

## Copilot Setup

This workspace is initialized for future GitHub Copilot use with:
- `README.md` for project context
- `AGENTS.md` for always-on workspace guidance

When continuing implementation with Copilot:
1. read `AGENTS.md` and `CONTEXT.md`
2. pick an open GitHub Issue (see `docs/agents/issue-tracker.md`)
3. implement the next smallest end-to-end slice
4. record hard-to-reverse decisions as ADRs in `docs/adr/` and keep `CONTEXT.md` current
