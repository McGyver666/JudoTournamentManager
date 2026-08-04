# JudoTournamentManager

## Product Context
- This project builds an offline-capable judo tournament management application for on-site tournament use.
- The app must keep running on a single laptop or a local LAN even with unstable or no internet — SQLite storage and a locally served UI, with no hard cloud dependency at runtime.
- It is currently also deployed as an internet-hosted single server behind an nginx reverse proxy (see Architecture and `deploy/`); both the offline/LAN and internet-hosted models must stay supported.
- German is the primary product language.
- The system must stay localizable from the start.

## Architecture
- Keep the architecture simple: modular monolith first.
- Backend is ASP.NET Core Web API on .NET 10 (`JudoTournamentManagement.Api`).
- Persistence is SQLite via EF Core with migrations (`AppDbContext`, `App_Data/`) for offline durability.
- Frontend is an Angular SPA (`frontend/`), built into the API's `wwwroot` and served by the API (static files + SPA fallback to `index.html`).
- Realtime updates (fight table/display) use SignalR at `/hubs/tournament`.
- Production is deployed on a Debian/LXC server behind an nginx reverse proxy with Let's Encrypt TLS, forwarding to the API on `127.0.0.1:5080` (see `deploy/`).
- Keep runtime dependencies local (SQLite, locally served UI): don't add cloud-only runtime dependencies, so the app also runs offline/LAN even though production is internet-hosted.
- Do not introduce distributed services unless the backlog explicitly requires them.

## Build and Test
- Use the .NET 10 SDK available on `PATH`.
- Build backend with: `dotnet build .\JudoTournamentManagement.sln`
- Run backend tests with: `dotnet test .\JudoTournamentManagement.sln --filter Category=UnitTest`
- Frontend lives in `frontend\`: build with `npm run build`, test with `npm run test:ci`.
- Start locally with: `.\start-local.ps1` (builds the Angular frontend into `wwwroot`, then runs the API).

## Implementation Conventions
- Keep visible product text German by default.
- New UI and API-facing labels must be localization-ready.
- Follow the MVP backlog in `backlog.md`.
- Prefer small, end-to-end slices over speculative broad scaffolding.
- Update `backlog.md` when implementation status changes materially.
- Keep `README.md` (English) and `README.de.md` (German) current whenever setup, architecture, developer workflow, APIs, or operational behavior changes.
- Keep both README versions consistent: synchronize their structure, commands, endpoints, technical facts, and bidirectional language links while preserving the language of each document.

## .NET Conventions
- Use async APIs with `CancellationToken` for I/O-bound work.
- Keep XML doc comments on public members.
- Use `ProblemDetails` or validation responses for API errors.
- Add unit tests for new behavior and tag each new test with `Category=UnitTest`.
- Do not replace the local/offline model with cloud-only dependencies.

## Current State Notes
- Persistence runs on SQLite via EF Core with migrations (not in-memory).
- Implemented areas include: tournaments, clubs, athletes, registrations, categories (+ presets), tatami management and queue, fights/brackets and results, authentication and roles, audit logging, guest share, and backup/restore.
- See `backlog.md` for the authoritative, up-to-date status of remaining MVP work.

## Agent skills

### Issue tracker

Issues are tracked as GitHub Issues in `McGyver666/JudoTournamentManager` (via the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default five canonical triage labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.
