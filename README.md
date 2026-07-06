# Judo Tournament Management

Offline-first tournament software for on-site judo events with German as the primary language.

## Projektstatus

This project is in active MVP implementation with most core tournament workflows already delivered.

Already available:
- .NET 8 backend solution with SQLite persistence (EF Core)
- local startup script
- health endpoint
- tournament, tatami, category, club, athlete, registration, draw and fight APIs
- category assignment workflow (auto + manual)
- assisted category generation workflow (preview + apply) with two strategies:
  - standard 2026 classes (source: `altersklassen_2026.md`)
  - athlete-driven classes by target athletes per class and max weight deviation
- tatami assignment workflow (auto + manual)
- public display view with realtime updates (SignalR)
- results and medal table views
- local authentication flow (login/logout, session persistence, admin user management)
- authenticated SignalR hub access (realtime updates require valid bearer token)
- security response headers (CSP, frame/mime/referrer protections)
- auth endpoint rate limiting + request body size limits (restore endpoint explicitly allowed larger payload)
- migration-first database startup (`MigrateAsync`) with EF migration history and legacy schema adoption
- HMAC-SHA256 hashing for auth session tokens (`Security:AuthTokenHmacSecret`)
- German-first localization baseline
- Angular 19 frontend (admin + operations + display/results UIs) served from the API
- hardened local scripts for test/seed usage (`JUDO_TEST_PASSWORD`, production guard)
- admin backup/restore UI flow in tournaments view (download backup + restore upload)
- unit test project (192 passing tests, Category=UnitTest)

Current focus:
- TLS/LAN operational stabilization and repeated field validation runs

## Produktziel

Build a practical local tournament management system similar in spirit to TUMAG, optimized for tournaments with unreliable or no internet access.

## MVP Scope

The MVP focuses on:
- tournament setup
- tatami setup
- category setup
- club management
- athlete management
- athlete registration to categories
- bracket generation
- tatami fight queues
- result entry
- public display
- final rankings

See `backlog.md` for the detailed MVP backlog and current implementation state.

## Architektur

## Zielbild
- **Offline-first**
- **Single host laptop** as default mode
- **Optional LAN clients** on the same local network
- **German-first UI**
- **Localizable from the start**

## Aktueller technischer Stand
- **Backend:** ASP.NET Core Web API (.NET 8)
- **Solution style:** modular monolith
- **Persistence:** SQLite via EF Core (`App_Data/judo-tournament.db`, auto-created on startup)
- **Schema compatibility:** Startup uses EF Core migrations and migration history; legacy local databases without migration history are adopted safely at startup.
- **Frontend:** Angular 19 SPA (`frontend/`), built into the API `wwwroot/` and served same-origin
- **Health endpoint:** `/health`
- **App entry point:** `/` (Angular app; deep links fall back to `index.html`)

## Zielarchitektur fuer den MVP
- **Backend:** ASP.NET Core Web API
- **Frontend:** SPA served locally by the host machine
- **Database:** SQLite
- **Realtime updates:** SignalR/WebSockets
- **Operation mode:** local machine or local LAN only

## Projektstruktur

```text
JudoTournamentManagement.sln
JudoTournamentManagement.Api/
JudoTournamentManagement.Api.Tests/
frontend/
backlog.md
start-local.ps1
start-local.sh
.github/
  copilot-instructions.md
```

## Voraussetzungen

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

## Lokales Starten

Start the API locally (Windows / PowerShell):

```powershell
.\start-local.ps1
```

Skip frontend build and start backend only (Windows / PowerShell):

```powershell
.\start-local.ps1 -SkipFrontendBuild
```

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
- Swagger (Development): `http://localhost:5080/swagger`

If you run an older local database, startup will auto-add missing legacy columns needed by current features.
For larger local schema drifts, reset the local database by deleting `JudoTournamentManagement.Api/App_Data/judo-tournament.db*` and restart the API.

## Build und Tests

Build the solution (Windows with local SDK):

```powershell
.\.dotnet\dotnet.exe build .\JudoTournamentManagement.sln
```

Build the solution (Linux/macOS with local SDK):

```bash
./.dotnet/dotnet build ./JudoTournamentManagement.sln
```

Build the solution (any OS with global SDK):

```bash
dotnet build ./JudoTournamentManagement.sln
```

Run all unit tests (Windows with local SDK):

```powershell
.\.dotnet\dotnet.exe test .\JudoTournamentManagement.sln --filter Category=UnitTest
```

Run all unit tests (Linux/macOS with local SDK):

```bash
./.dotnet/dotnet test ./JudoTournamentManagement.sln --filter Category=UnitTest
```

Run all unit tests (any OS with global SDK):

```bash
dotnet test ./JudoTournamentManagement.sln --filter Category=UnitTest
```

Run draw/lock smoke flow (Windows / PowerShell):

```powershell
./test-draw-lock-flow.ps1
```

Run LAN propagation validation (Windows / PowerShell):

```powershell
./test-lan-validation.ps1
```

Optional credentials for existing local admin:

```powershell
$env:JUDO_TEST_PASSWORD="<existing-admin-password>"
./test-lan-validation.ps1
```

Run against self-signed HTTPS endpoint (local cert) and skip certificate validation in script requests:

```powershell
./test-lan-validation.ps1 -BaseUrl https://localhost:7080 -SkipCertificateCheck
```

The script creates operator/display test users, executes cross-client read/write checks,
measures propagation latency, and writes a JSON evidence report:
`lan-validation-report-<timestamp>.json`.

Latest measured evidence:
- `lan-validation-report-20260706131837.json` -> max propagation 109 ms (target <= 2000 ms)

The smoke script validates this sequence end-to-end against a running local API:
- draw generation keeps category unlocked
- category reassignment before first fight start triggers automatic draw refresh
- first real fight start locks the category
- reassignment after lock is rejected with HTTP 409

## Paket Fuer Anderes System

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

## Aktuelle API

### Core endpoints

- `GET /api/tournaments`
- `GET /api/tournaments/{tournamentId}`
- `POST /api/tournaments`
- `PUT /api/tournaments/{tournamentId}`
- `DELETE /api/tournaments/{tournamentId}`

- `GET/POST/PUT/DELETE /api/tournaments/{tournamentId}/tatamis`
- `GET/POST/PUT/DELETE /api/tournaments/{tournamentId}/categories`
- `POST /api/tournaments/{tournamentId}/categories/generate/preview`
- `POST /api/tournaments/{tournamentId}/categories/generate/apply`
- `GET/POST/PUT/DELETE /api/tournaments/{tournamentId}/clubs`
- `GET/POST/PUT/DELETE /api/tournaments/{tournamentId}/athletes`

- `GET/POST/DELETE /api/tournaments/{tournamentId}/registrations`
- `POST /api/tournaments/{tournamentId}/registrations/auto-assign`
- `POST /api/tournaments/{tournamentId}/registrations/{registrationId}/category`
- `GET /api/tournaments/{tournamentId}/registrations/export`

- `POST /api/tournaments/{tournamentId}/categories/{categoryId}/draw`
- `GET /api/tournaments/{tournamentId}/categories/{categoryId}/fights`
- `POST /api/tournaments/{tournamentId}/categories/{categoryId}/swap`
- `GET /api/tournaments/{tournamentId}/categories/{categoryId}/rankings`

- `GET /api/tournaments/{tournamentId}/tatamis/{tatamiId}/queue`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/assign-tatami`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/start`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/stop`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/resume`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/score/adjust`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/osae-komi/start`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/osae-komi/stop`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/result`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/correct`

- `GET /api/tournaments/{tournamentId}/medal-table`
- `GET /api/tournaments/{tournamentId}/audit-log`

- `POST /api/auth/bootstrap-admin`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/me`
- `GET /api/auth/users`
- `POST /api/auth/users`
- `PATCH /api/auth/users/{userId}/active`
- `POST /api/auth/users/{userId}/reset-password`

Frontend auth routes:
- `/login`
- `/users` (Admin)

Example `POST /api/tournaments` request body:

```json
{
  "name": "RWE Judo Cup",
  "date": "2026-09-12",
  "venue": "Essen",
  "organizer": "JC Essen"
}
```

## Lokalisierung

Localization rules for the MVP:
- primary language is German
- visible UI texts should be German by default
- new UI work must be localization-ready
- avoid hardcoded visible English strings in the product UI

Current backend culture setup:
- default: `de-DE`
- fallback-ready secondary culture: `en-US`

## Entwicklungsprinzipien

- offline-first before cloud-first
- simple architecture before distributed architecture
- German-first UX
- localizable UI from the first screen
- explicit validation on all write endpoints
- no silent error swallowing
- keep work aligned with `backlog.md`

## Sicherheit und Betriebsmodell

- no mandatory internet dependency for tournament execution
- local/LAN deployment only for MVP
- all future auth, audit logging, and backup features must follow the backlog
- secrets must never be hardcoded if external integrations are added later
- SignalR hub access requires authentication; frontend passes bearer token for realtime channel setup
- helper scripts abort when `ASPNETCORE_ENVIRONMENT=Production`

## Copilot Setup

This workspace is initialized for future GitHub Copilot use with:
- `README.md` for project context
- `.github/copilot-instructions.md` for always-on workspace guidance

When continuing implementation with Copilot:
1. read `README.md`
2. read `backlog.md`
3. implement the next smallest backlog slice
4. update `backlog.md` when scope or implementation status changes materially

## Naechste sinnvolle Umsetzungsschritte

Recommended next implementation steps:
1. run repeated LAN validation in a real multi-laptop setup and archive evidence reports
2. add startup mode checks (HTTP + TLS) to automated regression routines
