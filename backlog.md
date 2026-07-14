# Judo Tournament Management - MVP Backlog (Offline, On-Site)

## 1) Scope (Reworked)

## Product Goal
Build a practical, reliable **on-site tournament app** for judo events, comparable in spirit to TUMAG, focused only on MVP functionality.

## Operating Constraints
- Must run **locally** without cloud dependency.
- Must work on **one laptop** (single-device mode).
- Should also support **multiple laptops in local LAN** (one host + clients), even with unstable/no internet.
- Primary UI language is **German**.
- Application must be **localizable** (i18n-ready for additional languages later).

## Out of Scope (for MVP)
- Federation integrations
- Mobile apps
- Advanced analytics
- Live streaming integrations
- Team competition mode
- Complex season management

---

## 2) Simple MVP Architecture (Offline-First)

## Deployment Model
- **Local Host Laptop** runs:
  - Backend API + real-time updates
  - Database
  - Frontend (served locally)
- Optional **Client Laptops** connect over local network to host.
- No mandatory internet connection.

## Suggested Simple Stack
- **Backend:** .NET 10 Web API (modular monolith)
- **Frontend:** Angular (or React) SPA
- **Database:** SQLite (default, file-based, robust for local/offline)
- **Realtime:** SignalR/WebSockets for fight table/display updates
- **Packaging:** Docker Compose (optional) + native desktop/start scripts

## Core Modules (within one backend app)
1. Turnierverwaltung (Tournament setup)
2. Teilnehmerverwaltung (Clubs, Athletes, Registration)
3. Auslosung/Kampflogik (Brackets + progression)
4. Kampfflächensteuerung (Tatami queue + fight flow)
5. Ergebniserfassung (referee/table official input)
6. Anzeige & Berichte (public screen + print/export)
7. Benutzer & Rollen (local auth + RBAC)
8. Lokalisierung (German-first i18n framework)

## Localization Strategy (German Primary)
- Default locale: `de-DE`
- All UI text comes from translation keys (no hardcoded strings in components)
- Date/time/number formatting via locale services
- Reports/exports support German labels first
- Add `en` placeholders to keep i18n pipeline ready

---

## 3) Detailed MVP Backlog

Priority legend:
- **P0** = must-have for first usable tournament
- **P1** = should-have within MVP stabilization

Story points are rough relative estimates.

## Epic A - System Setup & Offline Operation

### A-01 Local host startup (P0, 5 SP) — ✅ Done
**Story:** Als Turnierleiter möchte ich die Anwendung auf einem Laptop starten können, damit das Turnier ohne Internet durchgeführt werden kann.  
**Acceptance Criteria:**
- Start command launches backend + frontend locally.
- Health check screen shows system ready.
- No external cloud service is required for core workflow.

### A-02 Local network client access (P0, 5 SP) — ✅ Done
**Story:** Als Helfer möchte ich von einem zweiten Laptop auf das Host-System zugreifen können, damit mehrere Tische parallel arbeiten können.  
**Acceptance Criteria:**
- Host provides LAN URL.
- ✅ At least 3 concurrent clients usable in same LAN validation flow (admin/operator/display clients in script run).
- ✅ Read/write actions reflect across clients within 2 seconds.
- Repeatable validation script exists (`test-lan-validation.ps1`) and produces timestamped JSON evidence (`lan-validation-report-*.json`).
- ✅ Latest evidence run (`lan-validation-report-20260706131837.json`): max propagation 109 ms (target <= 2000 ms).

### A-03 Backup & restore tournament file (P0, 3 SP) — ✅ Done
**Story:** Als Turnierleiter möchte ich ein Turnier sichern und wiederherstellen können, damit bei Geräteproblemen keine Daten verloren gehen.  
**Acceptance Criteria:**
- ✅ Manual backup export creates restorable package/file (JSON via GET /api/tournaments/{id}/backup).
- ✅ Restore creates identical tournament state (POST /api/tournaments/restore with TournamentBackup DTO).
- ✅ Backup operation available from admin role (protected by [Authorize(Roles = "Admin")]).
- ✅ Full backup/restore integration tests covering 409 conflict, 404 not found, 403 forbidden, 201 success, 400 invalid version.

---

## Epic B - User Management & Roles

### B-01 Local authentication (P0, 3 SP) — ✅ Done
**Story:** Als Admin möchte ich Benutzerkonten lokal verwalten, damit nur berechtigte Personen Änderungen machen können.  
**Acceptance Criteria:**
- Login/logout available.
- Password policy configurable, secure hashing used.
- Failed login attempts are logged.

### B-02 Role-based permissions (P0, 5 SP) — ✅ Done
**Story:** Als System möchte ich Rollenrechte erzwingen, damit jede Rolle nur passende Funktionen nutzen kann.  
**Roles MVP:** Admin, Tischbediener/Kampfrichter-Eingabe (Operator), Anzeige (Display, read-only).  
**Acceptance Criteria:**
- ✅ Unauthorized API access is blocked (401 unauthenticated, 403 forbidden roles).
- ✅ UI hides forbidden actions based on role (template guards + method checks).
- ✅ Role checks covered by automated tests (8 new authorization integration tests + test coverage for GET endpoints, CSV export restrictions).
- ✅ All GET endpoints protected with [Authorize]; CSV export requires Admin/Operator.

---

## Epic C - Tournament Configuration

### C-01 Create/Edit tournament (P0, 3 SP) — ✅ Done
**Story:** Als Admin möchte ich Stammdaten eines Turniers verwalten, damit der Wettkampftag korrekt abgebildet ist.  
**Acceptance Criteria:**
- Fields: name, date, venue, organizer.
- Editable before tournament lock.
- Validation errors shown in German.

### C-02 Tatami setup (P0, 3 SP) — ✅ Done
**Story:** Als Admin möchte ich Kampfflächen (Tatamis) konfigurieren, damit Kämpfe zugewiesen werden können.  
**Acceptance Criteria:**
- Create/rename/activate/deactivate tatamis.
- Tatami ordering defines display and queue sequence.

### C-03 Category setup (P0, 5 SP) — ✅ Done
**Story:** Als Admin möchte ich Alters-/Gewichtsklassen definieren, damit Meldungen korrekt zugeordnet sind.  
**Acceptance Criteria:**
- Category fields: age group, gender, weight class, ruleset flags.
- Duplicate prevention for same category definition.
- Category can be locked once draw is generated.

### C-04 Assisted category generation (P1, 8 SP) — ✅ Done
**Story:** Als Admin möchte ich Kategorien per Assistent generieren, damit Standardklassen und meldungsbasierte Klassen schnell erstellt werden können.  
**Acceptance Criteria:**
- In der Kategorie-Konfiguration gibt es einen "Kategorien generieren"-Button.
- Assistent erfasst Jahrgangsbereich, Geschlecht, Kampfzeit, Golden-Score-Einstellungen.
- Zwei Strategien: Standardklassen 2026 und meldungsbasierte Zielgröße mit maximaler Gewichtsabweichung.
- Vor dem Anlegen wird eine Vorschau angezeigt und erst nach Bestätigung gespeichert.
- Beim Anwenden werden zuvor generierte, ungesperrte Kategorien ersetzt; gesperrte Kategorien bleiben erhalten.
- Gemischte Kategorien werden als eigene Gender-Ausprägung unterstützt (Mixed).

---

## Epic D - Clubs, Athletes, Registration

### D-01 Club management (P0, 3 SP) — ✅ Done
**Story:** Als Admin möchte ich Vereine anlegen und bearbeiten, damit Teilnehmer korrekt zugeordnet sind.  
**Acceptance Criteria:**
- CRUD for clubs.
- Unique club name per tournament.

### D-02 Athlete management (P0, 5 SP) — ✅ Done
**Story:** Als Admin möchte ich Athleten erfassen, damit sie in passende Klassen gemeldet werden können.  
**Acceptance Criteria:**
- Fields: first name, last name, birth year, gender, club, optional license ID.
- Input validation enforced (required fields, formats).
- Duplicate warning logic (name + birth year + club).

### D-03 Registration to categories (P0, 5 SP) — ✅ Done
**Story:** Als Admin möchte ich Athleten Klassen zuweisen, damit die Auslosung vorbereitet werden kann.  
**Acceptance Criteria:**
- Register/unregister athlete in exactly one relevant category.
- Block registration if category is locked.
- Registration list exportable (CSV/PDF optional for MVP: CSV mandatory).

### D-04 Assisted category assignment (P1, 3 SP) — ✅ Done
**Story:** Als Admin möchte ich Meldungen automatisch oder manuell Kategorien zuordnen können, damit die Auslosung schneller vorbereitet wird.  
**Acceptance Criteria:**
- Auto-assignment based on gender, birth year and weight.
- Manual per-athlete category override.
- Unassigned athletes are clearly visible.

---

## Epic E - Draw/Bracket Engine

### E-01 Generate brackets (P0, 8 SP) — ✅ Done
**Story:** Als Admin möchte ich pro Kategorie automatisch einen Turnierbaum erzeugen, damit Kämpfe gestartet werden können.  
**Acceptance Criteria:**
- Support single elimination.
- Support repechage variant required by target tournament format (configurable preset).
- Support NWJV Doppel-K.-o.-System for categories with up to 32 athletes; reject larger categories without omitting registrations.
- Byes handled automatically.
- Draw generation is deterministic for same seed input.

### E-02 Manual draw adjustments before start (P1, 5 SP) — ✅ Done
**Story:** Als Admin möchte ich vor Kampfbeginn Anpassungen vornehmen, damit organisatorische Sonderfälle behandelt werden können.  
**Acceptance Criteria:**
- Swap athletes within category before first fight starts.
- Once first fight started, structure lock enforced.

---

## Epic F - Fight Operations (Tatami Workflow)

### F-01 Fight queue per tatami (P0, 8 SP) — ✅ Done
**Story:** Als Tischbediener möchte ich die nächsten Kämpfe je Tatami sehen, damit der Ablauf flüssig bleibt.  
**Acceptance Criteria:**
- "Current", "Next", "On deck" for each tatami.
- Queue updates automatically after result confirmation.
- Manual reassignment to another tatami (admin only).

### F-02 Match control panel (P0, 8 SP) — ✅ Done
**Story:** Als Tischbediener möchte ich einen Kampf steuern und Punkte/Strafen erfassen, damit Ergebnisse korrekt dokumentiert sind.  
**Acceptance Criteria:**
- Start/pause/reset timer.
- Record core scoring events and penalties.
- Confirm winner and end match.
- Audit log entry for each result confirmation/change.

### F-03 Result correction workflow (P1, 5 SP) — ✅ Done
**Story:** Als Admin möchte ich fehlerhafte Ergebnisse kontrolliert korrigieren, damit der Turnierbaum korrekt bleibt.  
**Acceptance Criteria:**
- Corrections require elevated role.
- Previous and new values are both logged.
- Bracket progression recalculated consistently.

### F-04 Tatami assignment board (P1, 3 SP) — ✅ Done
**Story:** Als Admin möchte ich Kämpfe automatisch und manuell Tatamis zuweisen können, damit der Ablauf effizient vorbereitet wird.  
**Acceptance Criteria:**
- Auto-assignment distributes assignable fights across active tatamis.
- Manual override per fight is possible.
- Assignment is persisted and immediately reflected in queue and match views.

---

## Epic G - Public Display & Results

### G-01 Public screen view (P0, 5 SP) — ✅ Done
**Story:** Als Zuschauer möchte ich aktuelle und nächste Kämpfe sehen, damit der Turnierverlauf transparent ist.  
**Acceptance Criteria:**
- Read-only display mode.
- Shows tatami, current fight, next fights.
- Auto-refresh via realtime channel.

### G-02 Category results/rankings (P0, 5 SP) — ✅ Done
**Story:** Als Turnierleitung möchte ich Platzierungen je Kategorie sehen, damit Siegerehrungen vorbereitet werden können.  
**Acceptance Criteria:**
- Ranking generated from final bracket state.
- Clearly displays 1st/2nd/3rd placements.
- Export/print-friendly layout.

### G-03 Medal table by club (P1, 3 SP) — ✅ Done
**Story:** Als Organisator möchte ich einen Medaillenspiegel je Verein anzeigen, damit Teamleistungen sichtbar sind.  
**Acceptance Criteria:**
- Aggregates medals across categories.
- Sort by gold/silver/bronze, then club name.

---

## Epic H - Localization (German-First)

### H-01 German default UI (P0, 3 SP) — ✅ Done
**Story:** Als Nutzer möchte ich die Anwendung standardmäßig auf Deutsch sehen, damit sie im Turnierkontext direkt nutzbar ist.  
**Acceptance Criteria:**
- Default language is German.
- Core workflows fully translated in German.
- No hardcoded English labels in visible MVP screens.

### H-02 i18n infrastructure for future languages (P0, 3 SP) — ✅ Done
**Story:** Als Entwickler möchte ich Übersetzungsmechanismen vorbereitet haben, damit weitere Sprachen später einfach ergänzt werden können.  
**Acceptance Criteria:**
- Translation keys grouped by feature module.
- Fallback strategy defined.
- At least one secondary locale file scaffolded (`en`).

---

## Epic I - Reliability, Audit, Security (MVP level)

### I-01 Audit logging for critical actions (P0, 5 SP) — ✅ Done
**Story:** Als Admin möchte ich kritische Änderungen nachvollziehen können, damit Streitfälle auflösbar sind.  
**Acceptance Criteria:**
- ✅ Log: login attempts (LoginFailed/LoginSucceeded), draw generation (DrawGenerated), result confirmations (ResultConfirmed), result corrections (ResultCorrected), user/role changes (UserCreated/UserActivated/UserDeactivated/PasswordReset), tournament backup/restore (TournamentBackedUp/TournamentRestored).
- ✅ Log entries contain timestamp, user, action, entity reference (stored in AuditLogRecord).
- ✅ Sensitive data not written to logs (passwords, tokens, PII not logged).

### I-02 Input validation and error handling (P0, 5 SP) — ✅ Done
**Story:** Als System möchte ich ungültige Eingaben robust abweisen, damit Datenkonsistenz erhalten bleibt.  
**Acceptance Criteria:**
- ✅ Server-side schema validation for all write endpoints (data annotations, custom validators).
- ✅ Error messages are user-friendly and German-localized (ModelState validation messages in German).
- ✅ Validation failures do not corrupt tournament state (transactional saves, rollback on error).
- ✅ 181 unit tests validating behavior; build clean.

### I-03 Basic test suite for critical flows (P0, 8 SP) — ✅ Done
**Story:** Als Team möchte ich zentrale Abläufe abgesichert testen, damit Änderungen keine Turnierabbrüche verursachen.  
**Acceptance Criteria:**
- ✅ Automated tests for: registration (create/assign/auto-assign/delete via integration tests), draw generation (single elimination + repechage via 52 bracket tests), result progression (score/correction/confirmation via match service tests), role authorization (8 authorization integration tests).
- ✅ End-to-end smoke test for complete tournament flow (setup → clubs/athletes → registration with weight → draw generation → tatami assignment → fight start (triggers category lock) → score adjustment → result confirmation → rankings → medal table). TournamentFlowSmokeTests.FullTournamentFlow_SetupToRankings_CompletesSuccessfully.
- ✅ Smoke test script for local startup: `.\start-local.ps1` + `.\test-draw-lock-flow.ps1`.
- ✅ Total tests: 181 unit tests passing (Category=UnitTest), build clean (0 errors, 0 warnings).

---

## Epic J - Security Hardening & Code Quality (Post-MVP)

> Findings from the full security & code-quality review on 2026-07-02. Scope reflects the
> offline-first LAN deployment model (trusted local network), so items are prioritized
> proportionately rather than as a public-web-app checklist.

### J-01 Authenticate SignalR hub (P0, 2 SP) — ✅ Done
**Story:** Als System möchte ich, dass nur authentifizierte Clients Echtzeit-Kampfupdates empfangen, damit Turnierdaten nicht ungeschützt verteilt werden.  
**Acceptance Criteria:**
- ✅ `[Authorize]` auf `TournamentHub` (Hubs/TournamentHub.cs); nur angemeldete Clients dürfen verbinden.
- ✅ `JoinTournamentAsync` validiert `tournamentId` (GUID) und prüft Turnier-Existenz vor dem Group-Join.
- ✅ Test ergänzt: Hub-Negotiate ohne Token liefert 401; mit Token 200 (ApiAuthorizationIntegrationTests).
- ✅ Frontend-Hubverbindung übergibt Bearer-Token via `accessTokenFactory`; Backend akzeptiert `access_token` Query für SignalR.

### J-02 Harden .gitignore for secrets & data (P0, 1 SP) — ✅ Done
**Story:** Als Team möchte ich sensible Dateien vom Versionskontrollsystem ausschließen, damit Passwort-Hashes, Session-Tokens und Athleten-PII nicht versehentlich committet werden.  
**Acceptance Criteria:**
- ✅ `.gitignore` ergänzt um SQLite-Dateien (`*.db`, `*.db-shm`, `*.db-wal`), `frontend/node_modules/`, `appsettings*.Development.json`.
- ✅ Verifiziert im lokalen Arbeitsstand: DB-Artefakte werden nicht mehr als Änderungen erfasst.

### J-03 HTTP security response headers (P1, 2 SP) — ✅ Done
**Story:** Als System möchte ich schützende HTTP-Header senden, damit gängige Browser-Angriffe (Clickjacking, MIME-Sniffing) erschwert werden.  
**Acceptance Criteria:**
- ✅ Middleware in Program.cs setzt `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy` und CSP.
- ✅ Integrationstest prüft Header-Präsenz auf `/health` (ApiAuthorizationIntegrationTests.HealthResponse_ContainsSecurityHeaders).

### J-04 Safe defaults in seed/test scripts (P1, 2 SP) — ✅ Done
**Story:** Als Team möchte ich keine hartkodierten Standardpasswörter in Skripten, damit keine schwachen bekannten Zugangsdaten entstehen.  
**Acceptance Criteria:**
- ✅ `seed-testdata.ps1`, `test-auth.ps1`, `test-login.ps1` lesen Passwort aus `JUDO_TEST_PASSWORD`; Fallback generiert zufälliges Passwort und protokolliert die Quelle.
- ✅ Alle drei Skripte brechen bei `ASPNETCORE_ENVIRONMENT=Production` ab.

### J-05 Request size limits & auth rate limiting (P1, 3 SP) — ✅ Done
**Story:** Als System möchte ich Anfragegrößen begrenzen und Login-Versuche pro IP drosseln, damit DoS- und Brute-Force-Risiken sinken.  
**Acceptance Criteria:**
- ✅ Kestrel `MaxRequestBodySize` auf 10 MB gesetzt; Backup-Restore mit erhöhtem Endpoint-Limit (`[RequestSizeLimit]`) erlaubt.
- ✅ Rate Limiter auf `/api/auth/*` über `EnableRateLimiting("AuthPolicy")` aktiviert; 429-Handling mit ProblemDetails-ähnlicher Antwort.
- ✅ Integrationstest prüft 429 bei übermäßig vielen Auth-Requests (ApiAuthorizationIntegrationTests.AuthEndpoints_RateLimit_ExcessiveRequests_Returns429).

### J-06 Complete DTO length validation (P1, 2 SP) — ✅ Done
**Story:** Als System möchte ich Eingabelängen konsistent validieren, damit stille EF-Trunkierung durch klare 400-Fehler ersetzt wird.  
**Acceptance Criteria:**
- ✅ DTO-Audit abgeschlossen: Request-Stringfelder sind mit DB-Constraints abgeglichen (`[MaxLength]`/`[StringLength]`).
- ✅ Validierungs- und API-Tests bleiben grün; keine Hinweise auf stille Trunkierung im aktuellen Zustand.

### J-07 Masked password dialog in user management (P1, 2 SP) — ✅ Done
**Story:** Als Admin möchte ich Passwörter über ein maskiertes Eingabefeld setzen, damit Zugangsdaten nicht im Browser-Prompt sichtbar sind.  
**Acceptance Criteria:**
- ✅ `prompt()` in `user-management.component.ts` durch Dialog-Flow mit `type="password"` ersetzt.
- ✅ Clientseitige Mindestlängenprüfung (12 Zeichen) vor Passwort-Reset integriert.

### J-08 Safe localStorage deserialization (P1, 1 SP) — ✅ Done
**Story:** Als System möchte ich gespeicherte Turnierdaten robust einlesen, damit korrupte/manipulierte localStorage-Werte keinen fehlerhaften Zustand erzeugen.  
**Acceptance Criteria:**
- ✅ `tournament-context.service.ts` validiert Pflichtfelder nach `JSON.parse`, verwirft ungültige Daten und entfernt korrupten Storage-Eintrag.

### J-09 Adopt EF Core migrations (P1, 5 SP) — ✅ Done
**Story:** Als Team möchte ich sichere Schema-Upgrades, damit App-Updates keine Datenverluste durch DB-Neuanlage verursachen.  
**Acceptance Criteria:**
- ✅ Startup verwendet migration-first (`MigrateAsync`) statt `EnsureCreatedAsync`.
- ✅ Manuelle Auth-Tabellen-Bootstrap-SQL entfernt; neue EF-Migration `SyncModelWithCurrentSchema` ergänzt fehlende Schemaelemente.
- ✅ Legacy-DB-Adoption ergänzt: bestehende lokale DBs ohne `__EFMigrationsHistory` erhalten Baseline-Eintrag(e) und starten ohne Datenverlust.
- ✅ Build + UnitTests grün nach Umstellung (185/185).

### J-10 Frontend test suite (P1, 5 SP) — ✅ Done
**Story:** Als Team möchte ich automatisierte Frontend-Tests, damit Auth-, Guard- und Interceptor-Logik abgesichert ist.  
**Acceptance Criteria:**
- ✅ Unit-Tests für `auth-state.service`, Bearer-Interceptor und Route-Guards ergänzt.
- ✅ Auth-state Tests decken Token-Restore, Expiry-Clear, Login- und Logout-Verhalten ab.
- ✅ Guard-Tests prüfen Redirect-Verhalten (`/login` bzw. `/tournaments`) und Operator-Zugriff.
- ✅ Interceptor-Tests prüfen Bearer-Header nur für `api/*` Requests.
- ✅ Frontend test run erfolgreich (`ng test --watch=false --browsers=ChromeHeadless`: 10/10 SUCCESS).

### J-11 HMAC token hashing (P1, 2 SP) — ✅ Done
**Story:** Als System möchte ich Session-Tokens mit HMAC statt reinem SHA-256 hashen, damit Defense-in-Depth verbessert wird.  
**Acceptance Criteria:**
- ✅ `HashToken` in `SqliteAuthService` nutzt jetzt HMAC-SHA256 mit `Security:AuthTokenHmacSecret`.
- ✅ Secret wird in Production zwingend aus Konfiguration verlangt; Development/Testing erhalten pro Prozess einen zufälligen Fallback-Key.
- ✅ Startup-Skripte setzen bei fehlendem Secret eine zufällige Session-Variable (`Security__AuthTokenHmacSecret`).
- ✅ Auth-UnitTests angepasst und Gesamtsuite bleibt grün (185/185).

### J-12 TLS for LAN operation (P1, 3 SP) — ✅ Done
**Story:** Als Turnierleiter möchte ich verschlüsselten LAN-Zugriff, damit Zugangsdaten nicht im Klartext über (WLAN-)Netze übertragen werden.  
**Acceptance Criteria:**
- ✅ HTTPS-Binding dokumentiert/aktivierbar via `start-local.ps1 -EnableTls` und `start-local.sh --enable-tls` (selbstsigniertes Dev-Zertifikat).
- ✅ `UseHttpsRedirection` verifiziert wirksam: HTTP `/health` liefert 307 auf HTTPS `/health`.

---

## 4) MVP Release Checklist (Definition of Done)

- Full tournament possible from setup to final rankings without internet.
- German UI complete for all MVP screens.
- One-host + multi-client LAN operation validated.
- Backup/restore tested with a realistic tournament dataset.
- Security baseline active (auth, RBAC, validation, audit logs).
- Critical automated tests green.

---

## 5) Recommended Build Order (AI Implementation Sequence)

1. Epic B (local authentication + RBAC)
2. Epic A-03 (backup/restore)
3. Epic I hardening (audit completeness + validation consistency + smoke/system tests)
4. Epic A-02 validation pass (measured LAN concurrency and sync latency)
5. Final MVP release QA (offline resilience + print/export quality)

---

## 6) Implementation Status — Last updated 2026-07-06

This section tracks the verified current state.

### Verified today
- Build successful: `./.dotnet/dotnet.exe build ./JudoTournamentManagement.sln` (0 errors, 0 warnings)
- Unit tests successful: 185/185 passing (`Category=UnitTest`)
- Frontend build: Angular output previously generated into `JudoTournamentManagement.Api/wwwroot`

### Delivered capabilities
- Offline-first local deployment with SQLite persistence.
- Full setup/admin flow: tournaments, tatamis, categories, clubs, athletes.
- Registration flow: register/unregister, CSV export, category assignment (auto + manual).
- Draw and bracket flow: generation (single elimination, repechage, round-robin, round-robin-with-knockout), manual swap before lock.
- Fight operations: tatami queue, assignment board, match control, result confirmation/correction.
- Public and reporting flow: display screen, category rankings, medal table.
- Realtime updates with SignalR (fight and category updates).
- German-first UI with runtime i18n and English fallback.
- Full local auth: bootstrap admin, login/logout, PBKDF2 password hashing, RBAC (Admin/Operator/Display).
- All write endpoints require Admin or Operator role.
- **B-02 COMPLETE:** All GET (read) endpoints now require at least any authenticated role ([Authorize]). CSV export requires Admin/Operator. Authorization coverage tests extended to 8 integration scenarios across 174+ tests.
- Audit logging for all critical auth events (login, logout, user management) and operational events (draw, results).
- Golden score support in match flow.
- Round-robin draw modes with standings/tie-break.
- **A-03 COMPLETE:** Backup/restore endpoints (`GET /api/tournaments/{id}/backup`, `POST /api/tournaments/restore`). Admin-only. JSON format with version field. Full FK-order restore with conflict detection. 5 integration tests green.
- **A-03 UI COMPLETE:** Frontend admin flow for backup download and restore upload in tournaments view. Includes user feedback and i18n labels for success/error cases.

### Remaining MVP gaps
- Keine offenen P0/P1-Luecken aus Epic A und Epic J mehr.

---

## 7) Next Implementation Plan (Prioritized)

### Step 1 — Stabilization & regression checks
- Wiederholte LAN-Laeufe im echten Mehr-Laptop-Netz (QA/Turnierprobe) und Evidenz archivieren.
- Regression-Tests fuer Startup-Skripte (HTTP/TLS Modi) in CI aufnehmen.
