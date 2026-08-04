# Context: Judo Tournament Management

Durable product and domain context for this repo. Work items (open tasks, bugs, features) live in
**GitHub Issues** (see `docs/agents/issue-tracker.md`); hard-to-reverse decisions live as ADRs in
`docs/adr/`. This file is the stable overview + glossary — not a task tracker.

## Product goal

A practical, reliable on-site tournament app for judo events (comparable in spirit to TUMAG),
focused on MVP functionality. Primary UI language is **German**; the app must stay localizable.

## Operating model

- **Offline-capable**: runs on a single laptop or a local LAN with no hard cloud dependency at runtime
  (SQLite storage, locally served UI). See [ADR-0001](docs/adr/0001-offline-capable-modular-monolith.md).
- **Also internet-hosted**: deployed behind an nginx reverse proxy with Let's Encrypt TLS (`deploy/`).
  Both models must stay supported. See [ADR-0006](docs/adr/0006-dual-deployment-model.md).
- Optional client laptops connect over the local network to the host.

## Scope

In scope (MVP): tournament setup, clubs/athletes/registration, draw & bracket logic, tatami queue &
fight flow, result capture, public display & reports, local auth & roles, German-first i18n.

Out of scope (MVP): federation integrations, mobile apps, advanced analytics, live streaming,
team-competition mode, complex season management.

## Core modules (one backend app)

1. Turnierverwaltung — tournament setup
2. Teilnehmerverwaltung — clubs, athletes, registration
3. Auslosung/Kampflogik — brackets + progression
4. Kampfflächensteuerung — tatami queue + fight flow
5. Ergebniserfassung — referee/table-official input
6. Anzeige & Berichte — public screen + print/export
7. Benutzer & Rollen — local auth + RBAC
8. Lokalisierung — German-first i18n

## Localization strategy

Default locale `de-DE`; `en-US` fallback kept ready. All visible UI text comes from translation keys —
no hardcoded strings in components. Date/time/number formatting via locale services.

## Domain glossary (ubiquitous language)

Use these terms as-is (German primary) in issues, tests, and code names; don't drift to synonyms.

- **Turnier** (Tournament) — a single judo event; the top-level aggregate.
- **Verein** (Club) — organization an athlete competes for. Athletes without one are grouped under
  the collective club **Ohne Verein** ("no club").
- **Athlet** (Athlete) — a competitor; imported from DM4/DMF files or entered manually.
- **Altersklasse** (Category / age group) — competition class; athletes are registered into one.
- **Meldung** (Registration) — an athlete's assignment to a category.
- **Auslosung** (Draw) — bracket generation. Formats: single elimination, repechage, round-robin,
  round-robin-with-knockout. A category **locks** on the first real fight start.
- **Tatami** — a mat/fight area. Fights are queued and assigned per tatami.
- **Kampf** (Fight) — a single bout. **Freilos** = a bye (does not count as a fight).
- **Osae-komi** — hold-down; server-authoritative timing that can pause the fight clock.
- **Golden Score** — sudden-death extension.
- **Vereinswertung** (Club scoring) — team ranking per age group and globally. See
  [ADR-0007](docs/adr/0007-club-scoring-rules.md) for the exact rules.
- **Siegquote** (Win ratio) — won fights / contested fights; byes excluded; `0/0 = 0.0`.
- **Medaillenspiegel** (Medal table) — medals aggregated by club.
- **Gast-Freigabe** (Guest share) — anonymous, read-only QR access to a tournament's match lists.
- **Rollen** (Roles) — Admin, Operator, Display (RBAC). See
  [ADR-0005](docs/adr/0005-local-auth-and-rbac.md).

## Delivered capabilities (state at migration, 2026-08)

Tournaments, tatamis, categories (+ presets & assisted generation), clubs, athletes (DM4/DMF import),
registration (auto + manual assignment, CSV export), draw/bracket generation with manual swap before
lock, tatami queue & assignment board, match control with server-authoritative timing/osae-komi/golden
score, completed-fight overview with admin result correction, public display, category rankings, medal
table, club scoring (Vereinswertung), realtime updates via SignalR, German-first i18n with English
fallback, local auth (bootstrap admin, login/logout, PBKDF2, HMAC-SHA256 session tokens), RBAC on all
endpoints, audit logging, backup/restore, guest share, TLS for LAN, EF Core migrations.

Remaining open work is tracked in GitHub Issues.
