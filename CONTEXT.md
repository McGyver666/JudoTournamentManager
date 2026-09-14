# Context: Shiai Manager

Durable product and domain context for this repo. Work items (open tasks, bugs, features) live in
**GitHub Issues** (see `docs/agents/issue-tracker.md`); hard-to-reverse decisions live as ADRs in
`docs/adr/`. This file is the stable overview + glossary — not a task tracker.

## Product goal

A practical, reliable on-site tournament app for judo events (comparable in spirit to TUMAG),
supporting individual tournaments and configured team matchdays. Primary UI language is **German**;
the app must stay localizable.

## Operating model

- **Offline-capable**: runs on a single laptop or a local LAN with no hard cloud dependency at runtime
  (SQLite storage, locally served UI). See [ADR-0001](docs/adr/0001-offline-capable-modular-monolith.md).
- **Also internet-hosted**: deployed behind an nginx reverse proxy with Let's Encrypt TLS (`deploy/`).
  Both models must stay supported. See [ADR-0006](docs/adr/0006-dual-deployment-model.md).
- Optional client laptops connect over the local network to the host.

## Scope

In scope: tournament setup, clubs/athletes/registration, individual and team competition formats,
NWJV Landesliga matchday operations (a venue, a variable number of teams, and manually
configurable team encounters) configurable for senior women, senior men, and U16, draw & bracket
logic, tatami queue & fight flow, result capture, public display & reports, local auth & roles,
German-first i18n.

Out of scope: federation integrations, mobile apps, advanced analytics, live streaming, and complex
season management, league tables, automatic fixture generation, promotion or relegation, and U16
Kampfgemeinschaften.

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
- **Turniermodus** (Competition mode) — distinguishes the existing individual tournament workflow
  from `TeamMatchday`. In `TeamMatchday` mode, one turnier represents one local NWJV league
  matchday with its teams, matchday weigh-ins, encounters, and bouts.
- **Verein** (Club) — organization an athlete competes for. Athletes without one are grouped under
  the collective club **Ohne Verein** ("no club").
- **Mannschaft** (Team) — a team entered for a `TeamMatchday`. It has a display name and belongs
  to exactly one existing Verein; one Verein may provide multiple teams on a matchday.
- **Athlet** (Athlete) — a competitor; imported from DM4/DMF files or entered manually.
- **Altersklasse** (Category / age group) — competition class; athletes are registered into one.
- **Meldung** (Registration) — an athlete's assignment to a category.
- **NWJV Landesliga** — the first supported league competition rule profile, comprising a venue and
  configurable team encounters within one matchday; it is configurable for senior women, senior
  men, and U16. The senior rules are based on "Hinweise fur Ligamannschaften und
  Kampfrichter*innen ab 2026" (NWJV, version 01/2026) and the NWJV Wettkampfordnung (WKO,
  version 25 April 2026). The WKO is authoritative if a season-specific source conflicts with it.
  U16 rules use five weight classes: -46/-52/-58/-66/+66 kg for boys and -42/-47/-53/-60/+60 kg
  for girls; the drawn bout order, actual-weight-only participation, and U15 fight rules apply.
- **Fremdstarter** (Guest starter) — an athlete who starts for a club other than their own. The
  NWJV U16 profile permits one guest starter to weigh in and compete on one matchday.
- **Mannschaftsbegegnung** (Team encounter) — a matchup between two teams on one matchday. It
  comprises a first and return leg, which are jointly scored as one encounter; the return leg starts
  immediately after the first leg and may use substituted athletes. The team with more individual
  wins prevails; equal individual wins result in Hikiwake, regardless of any under-score difference.
- **Aufstellung** (Lineup) — each team assigns athletes to the weight classes for one encounter leg.
  It is editable before the first assigned bout starts and then locked for that leg. A return leg may
  have a separate lineup; lineups are configurable again before the team's next encounter.
- **Begegnungskampf** (Encounter bout) — an individual bout created from a locked lineup. It uses
  the existing Tatami queue and match-control lifecycle; each completed result updates the running
  team-encounter score.
- **Tageswaage** (Matchday weigh-in) — the confirmed actual body weight of an athlete on a specific
  matchday. A confirmed matchday weigh-in is required before an athlete may be assigned to a lineup;
  an existing tournament registration weight may prefill it but does not replace it. U16 athletes may
  only be assigned to their actual-weight class, while senior athletes may also compete in higher
  weight classes subject to the WKO U18 exceptions.
- **Kampftag** (Matchday) — a local event at one venue with a configurable number of teams and
  manually configured team encounters. A three-team round robin (A-B, B-C, C-A) is supported but
  is not the only permitted matchday form. The weight-class order is drawn once by an administrator,
  is recorded for all encounters, and may not change after the first encounter bout starts.
- **Nichtantritt** (No-show) — a team does not appear by the end of the scheduled weighing period.
  The league profile automatically records the WKO forfeit result; the configured loss is 0:2 team
  points and 0:10/100 individual/under-score points, or 0:14/140 in the NRW league.
- **Auslosung** (Draw) — bracket generation. Formats: single elimination, repechage, round-robin,
  round-robin-with-knockout. A category **locks** on the first real fight start.
- **Tatami** — a mat/fight area. Fights are queued and assigned per tatami.
- **Kampf** (Fight) — a single bout. **Freilos** = a bye (does not count as a fight).
- **Osae-komi** — hold-down; server-authoritative timing with Sono-mama/Yoshi pause and resume that freezes both hold and fight clocks.
- **Golden Score** — sudden-death extension.
- **Vereinswertung** (Club scoring) — team ranking per age group and globally. See
  [ADR-0007](docs/adr/0007-club-scoring-rules.md) for the exact rules.
- **Siegquote** (Win ratio) — won fights / contested fights; byes excluded; `0/0 = 0.0`.
- **Medaillenspiegel** (Medal table) — medals aggregated by club.
- **Gast-Freigabe** (Guest share) — anonymous, read-only QR access to a tournament's match lists.
- **Turnierübersicht** (Tournament overview / Leitstand) — the live control-stand dashboard: featured
  current-fight hero (cycles through mats with running fights), per-Tatami status cards, tournament
  stats strip, next-fights queue and Vereinswertung. Distinct from **Kampfübersicht**.
- **Kampfübersicht** (Combat overview) — the table of completed, non-bye fights with admin result
  correction. Not the live dashboard.
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
