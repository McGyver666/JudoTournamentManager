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
- Athlete file import supports DM4 and DMF uploads.

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
- Full list of assigned pending fights with manual up/down reordering (persisted, live-synced).
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

### F-05 Synchronisierte Kampfzeit und Präzisionsanzeige (P1, 5 SP) — ✅ Done
**Story:** Als Turnierleitung möchte ich, dass Kampf- und Osae-komi-Zeiten auf allen Kampfrichter- und Anzeigeansichten präzise und einheitlich laufen, damit Zeitangaben trotz unterschiedlicher Endgeräte zuverlässig sind.
**Acceptance Criteria:**
- Der Server bleibt die autoritative Zeitquelle für Start-, Pause-, Fortsetz- und Osae-komi-Zeitpunkte sowie für regelrelevante Wertungen.
- Ein gemeinsamer Frontend-Zeitdienst schätzt die Serverzeit über wenige Zeitabgleich-Messungen mit minimaler Round-Trip-Time und führt sie lokal monoton mit `performance.now()` fort.
- Zeitsynchronisation erfolgt beim Öffnen einer Ansicht, nach SignalR-Reconnect, nach Rückkehr eines Tabs in den Vordergrund und höchstens einmal je fünf Minuten während einer aktiven Ansicht.
- Der Zeitabgleich erzeugt keine periodischen Timer-API-Aufrufe; höchstens fünf Messungen beim initialen Abgleich bzw. Reconnect und anschließend eine Messung je fünf Minuten.
- Laufende Kampfzeit und laufende Osae-komi-Zeit können in Zehntelsekunden angezeigt werden; pausierte und nicht gestartete Zeiten bleiben in ganzen Sekunden lesbar.
- Die sichtbare Zehntelsekunde wird lokal aktualisiert und löst keinen Netzwerkzugriff aus.
- Bei fehlender Serververbindung läuft die Darstellung mit der zuletzt bekannten Zeitbasis bzw. lokal weiter; sie darf dadurch keine Kampf- oder Wertungsentscheidung auslösen.
- Der Kampfrichter-Dialog und die Display-Ansichten verwenden denselben Zeitdienst und dieselbe Zeitberechnung.
- Osae-komi-Wertungen, automatisches Anhalten und sonstige Regelentscheidungen werden weiterhin ausschließlich vom Server entschieden.
- Wird durch ein abgelaufenes Osae-komi Ippon vergeben, hält der Server den Kampf unmittelbar an.
- Unit-Tests decken Serverzeit-Offset, lokale monotone Fortschreibung, Pause/Fortsetzen, Zehntelsekundenformatierung und den Offline-Fallback ab.

**Implementation note:** Umgesetzt mit gemeinsamem Frontend-Zeitdienst, authentifiziertem `GET /api/time`, SignalR-Nachrichten mit Serverzeitstempel und serverseitigem `MatchClockEvaluator` fuer regelrelevante Timing-Entscheidungen.

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

### G-04 Anonyme QR-Freigabe der Wettkampflisten (P1) — ✅ Done
**Story:** Als Turnierleitung möchte ich auf der Turnieransicht einen QR-Code anzeigen, über den Personen im lokalen Netzwerk zeitlich begrenzten, anonymen Nur-Lese-Zugriff auf die Wettkampflisten erhalten, damit Zuschauer und Betreuer die Kämpfe ohne eigenes Benutzerkonto verfolgen können.

**Rahmen-Entscheidungen (Grilling-Ergebnis):**
- Anonymer Zugriff über ein **eigenständiges Guest-Share-Token** (kein Benutzerkonto); read-only Pseudo-Rolle `Guest`.
- Scope bewusst schmal: **nur** die Wettkampflisten-Ansicht.
- **Ein** aktives Token pro Turnier; manueller An/Aus-Toggle + optionale TTL (Default „bis Mitternacht heute"); „Rotieren" invalidiert das alte Token sofort.
- Verwaltung durch **Admin + Operator**; Aktionen werden auditiert; einzelne Gast-Zugriffe werden **nicht** protokolliert.
- **Datensparsamkeit:** dedizierte, reduzierte Public-DTOs (`athletes = {id, clubId, firstName, lastName}`, `clubs = {id, name}`); die gesamte Wettkampflisten-Ansicht (Display + Gast) nutzt diese.
- QR **serverseitig als SVG**; Basis-URL aus Host-Header (optionaler Override); nicht-lokaler/öffentlicher Host nur mit TLS.
- Bei Backup/Restore ist die Freigabe immer deaktiviert; kein Token im Backup.

#### G-04a Guest-Share-Token-Modell + Persistenz (P1, 3 SP) — ✅ Umgesetzt
**Story:** Als System möchte ich Guest-Share-Tokens pro Turnier speichern und verwalten, damit anonymer Zugriff zustandsbasiert freigegeben, deaktiviert und rotiert werden kann.  
**Acceptance Criteria:**
- Neue Entität/Tabelle mit genau einem aktiven Token pro Turnier (`TournamentId`, `Token` Klartext, `IsEnabled`, `ExpiresAtUtc`, `CreatedUtc`, `RotatedAtUtc`).
- Token wird mit `RandomNumberGenerator` (≥256 Bit, base64url) erzeugt.
- EF-Core-Migration ergänzt; Legacy-DB-Adoption bleibt intakt.
- Backup/Restore übernimmt den Token **nicht** (Restore ⇒ deaktiviert/kein Token).
- Unit-Tests für Erzeugen/Rotieren/Deaktivieren/Ablauf; `Category=UnitTest`.

#### G-04b Reduzierte Public-Read-Endpoints (P1, 3 SP) — ✅ Umgesetzt
**Story:** Als anonymer Gast (und als Display-Client) möchte ich die Wettkampflisten über datenminimierte Endpoints laden, damit keine personenbezogenen Zusatzdaten übertragen werden.  
**Acceptance Criteria:**
- Neue Endpoints `GET /api/tournaments/{id}/public/{athletes,clubs,categories,fights,standings}` liefern reduzierte DTOs.
- `athletes = {id, clubId, firstName, lastName}`, `clubs = {id, name}`; keine Lizenz-/Passnummer, kein Geburtsjahr, kein Gewicht, keine Kontaktdaten.
- Autorisiert für `Admin, Operator, Display, Guest`; Guest nur bei aktiver, gültiger Freigabe.
- Bestehende vollständige `getAthletes`/`getClubs` bleiben für Betreiber-Ansichten unverändert.
- Unit-/Integrationstests für DTO-Form und Autorisierung.

#### G-04c Guest-Authentifizierung im Bearer-Handler + Hub-Scope (P1, 3 SP) — ✅ Umgesetzt
**Story:** Als System möchte ich Guest-Tokens erkennen und streng auf ihr Turnier begrenzen, damit anonymer Zugriff nicht über die Wettkampflisten hinausreicht.  
**Status:** Vollständig umgesetzt. Handler + Scope-Absicherung (Guest-Principal, Gültigkeitsprüfung, DefaultPolicy zwingt echte Rolle für `[Authorize]`, Controller erzwingt Turnier-Scope). TLS-Erzwingung im `GuestShareLinkBuilder` (nicht-lokaler Host ohne HTTPS ⇒ `GuestShareInsecureHostException`; Controller liefert 400 bei `enable`/`rotate`/`qr`, Public-Link im Status entfällt). `TournamentHub` prüft Guest-Turnier-Scope und aktive Freigabe (Soft-Disconnect). Eigene `PublicPolicy` (per-IP Fixed-Window) auf dem `PublicController`. Neue Tests: Link-Builder-TLS-Theorie, Hub-Scope (4), Guest-Zugriff-Integration (3), Controller-TLS (2).  
**Acceptance Criteria:**
- ✅ `BearerTokenAuthenticationHandler` erzeugt für ein gültiges Guest-Token ein read-only `Guest`-Principal (turniergebunden).
- ✅ Gültigkeitsprüfung: `IsEnabled` und nicht abgelaufen; deaktiviert/abgelaufen/rotiert ⇒ kein Zugriff.
- ✅ Bloßes `[Authorize]` verlangt weiterhin eine Betreiberrolle (Admin/Operator/Display); Guest erreicht nur explizit freigegebene Public-Endpoints (Scope-Leak verhindert).
- ✅ Nicht-lokaler/öffentlicher Host nur über TLS akzeptiert.
- ✅ SignalR: Guest darf **nur** die Gruppe des eigenen Turniers joinen; Soft-Disconnect (keine neuen Verbindungen/Reconnects bei Deaktivierung, laufende laufen aus).
- ✅ Eigene per-IP Rate-Limit-Policy für die Public-Endpoints.
- ✅ Integrationstests für Zugriff/Verweigerung und Scope-Grenzen.

#### G-04d Serverseitige QR-Erzeugung + Verwaltung (P1, 3 SP) — ✅ Umgesetzt
**Story:** Als Admin/Operator möchte ich die Freigabe erzeugen, anzeigen, rotieren und deaktivieren, damit ich den anonymen Zugriff kontrolliert steuern kann.  
**Status:** `GuestShareController` (Admin/Operator) mit `GET`, `enable`, `disable`, `rotate`, `qr` umgesetzt; QR serverseitig als SVG via QRCoder; Basis-URL aus Host-Header oder `GuestShare:PublicBaseUrl`-Override. Backup/Restore zieht die Freigabe bewusst nicht mit (kein Token im Backup, Restore ⇒ deaktiviert; Decision 13 erfüllt). TLS-Erzwingung für nicht-lokale Hosts ergänzt (siehe G-04c).  
**Acceptance Criteria:**
- ✅ Endpoints zum Erzeugen/Aktivieren, Deaktivieren, Rotieren und Abrufen des Status (Admin/Operator).
- ✅ `GET .../guest-share/qr` liefert QR als SVG mit der Public-URL; Basis-URL aus Host-Header, optionaler konfigurierter Override.
- ✅ Audit: `GuestShareEnabled`, `GuestShareDisabled`, `GuestShareRotated` mit auslösendem Betreiber; kein Token im Audit-Detail; keine Gast-Reads.
- ✅ Unit-Tests für QR-Ausgabe (SVG), Verwaltungslogik, URL-Aufbau und tokenfreies Audit.

#### G-04e Wettkampflisten auf reduziertes Modell umstellen (P1, 2 SP) — ✅ Umgesetzt
**Story:** Als Nutzer der Wettkampflisten möchte ich, dass die Ansicht nur die tatsächlich angezeigten Daten lädt, damit weniger personenbezogene Daten übertragen werden.  
**Status:** `MatchListsComponent` lädt Athleten/Vereine/Kategorien/Kämpfe/Stände über die reduzierten Public-Endpoints (`ApiService.getPublicAthletes/Clubs/Categories/Fights/Standings`) — ein Code-Pfad für Display **und** Gast. Neue TS-Modelle `PublicAthlete`/`PublicClub`. Keine sichtbare Funktionsänderung (weiterhin „Nachname, Vorname" + Verein). Frontend-Build grün.  
**Acceptance Criteria:**
- `MatchListsComponent` nutzt die reduzierten Public-Endpoints (Display + Gast, ein Code-Pfad).
- Keine sichtbare Funktionsänderung (Name „Nachname, Vorname" + Verein wie bisher).
- Frontend-Tests bleiben grün.

#### G-04f Public-Route + QR-Anzeige im Frontend (P1, 3 SP) — ✅ Umgesetzt
**Story:** Als Gast möchte ich über den gescannten QR eine fokussierte Nur-Listen-Seite ohne App-Navigation öffnen, und als Betreiber möchte ich den QR auf der Turnieransicht sehen.  
**Status:** Route `public/match-lists?tid=…&t=<token>` (ohne Guard) rendert `MatchListsComponent` ohne App-Shell (`updateShellVisibility` blendet `/public` aus). Der Token wird via `AuthStateService.setGuestToken` als Bearer für API-Aufrufe genutzt (Gast-Modus verzichtet bewusst auf die Hub-Verbindung → statischer Snapshot). Die Turnieransicht (Admin/Operator) zeigt je Turnier ein aufklappbares Panel „Gäste-Zugriff" mit Status, Auto-Aus-Preset (Bis Mitternacht/4h/8h/kein Aus, Default Mitternacht), Freigeben/Deaktivieren/Rotieren, Ablaufanzeige, öffentlichem Link (kopierbar) und serverseitig geliefertem QR-SVG (inline). Deutsch-erste i18n-Keys `share.*` in `de.json`/`en.json`.  
**Acceptance Criteria:**
- Neue Route `public/match-lists?tournamentId=…&t=<token>` rendert die Wettkampflisten ohne App-Shell/Nav/Login; Token aus URL, als Bearer + Hub-`access_token` genutzt.
- Turnieransicht (Admin/Operator) zeigt QR + Steuerung (Freigeben/Deaktivieren/Rotieren, TTL-Preset, Ablaufanzeige).
- Deutsch-erste, lokalisierbare Labels (i18n-Keys, `en`-Platzhalter).
- Ungültige/deaktivierte Freigabe zeigt eine verständliche Hinweisseite.

#### G-04g Dokumentation (README DE/EN) (P2, 1 SP) — ✅ Umgesetzt
**Story:** Als Betreiber möchte ich die anonyme QR-Freigabe dokumentiert haben, damit Einrichtung, TLS-Anforderung und Datenschutzverhalten klar sind.  
**Status:** `README.md` und `README.de.md` enthalten einen Abschnitt „Guest access / Gastzugriff" (Freigabe-Workflow, Auto-Aus-Presets, Rotieren/Deaktivieren, Datensparsamkeit, TLS-Regel für öffentliche Hosts, Realtime/Soft-Disconnect, Audit, Backup-Verhalten, per-IP Rate-Limit) sowie die neuen Public- und Guest-Share-Endpoints in der API-Liste. Beide Sprachversionen sind strukturell konsistent.  
**Acceptance Criteria:**
- ✅ `README.md` und `README.de.md` beschreiben Freigabe-Workflow, TLS-Regel (öffentlich ⇒ TLS), Datensparsamkeit und Auto-Aus.
- ✅ Beide Versionen strukturell/inhaltlich konsistent inkl. Sprach-Querverweise.

### G-05 Vereinswertung pro Altersklasse und global (P1, 13 SP) — 🔜 Geplant
**Story:** Als Turnierleitung möchte ich zusätzlich zu Ranglisten und Medaillenspiegel eine transparente Vereinswertung sehen, damit Teamleistung je Altersklasse und turnierweit nachvollziehbar vergleichbar ist.

**Fachliche Leitregeln (Grilling-Ergebnis):**
- Platzierungspunkte: 1. Platz = 7, 2. Platz = 5, 3. Platz = 3.
- Zwei dritte Plätze zählen jeweils separat mit 3 Punkten.
- Altersklassen-Endscore: Basispunkte x Siegquote der Altersklasse.
- Globale Zusatzwertung: globale Basispunkte x globale Siegquote über das ganze Turnier.
- Siegquote = gewonnene Kämpfe / absolvierte Kämpfe; Freilose zählen nicht als Kampf.
- Sonderfall Nenner = 0: Siegquote = 0.0.
- Interne Berechnung mit voller Präzision; Anzeige auf 2 Nachkommastellen.
- Ranking erfolgt nach ungerundetem Wert; Gleichheit mit technischer Toleranz (1e-9).
- Tie-break Reihenfolge: Endscore (ungerundet), Siegquote, Anzahl 1., Anzahl 2., Anzahl 3., sonst geteilter Rang.
- Rangnummern bei Gleichstand im Wettbewerbsstil: 1, 2, 2, 4.
- Athleten ohne Verein werden unter Sammelverein Ohne Verein geführt.

#### G-05a Backend-Datenmodell und DTOs (P1, 3 SP)
**Story:** Als System möchte ich strukturierte DTOs für Altersklassen- und Globalwertung bereitstellen, damit die Frontend-Anzeige vollständig und nachvollziehbar gerendert werden kann.
**Acceptance Criteria:**
- Neue Antwortmodelle für Vereinswertung enthalten mindestens: Verein, Rang, Status (Vorläufig/Final), Basispunkte, Siege, Kämpfe, Siegquote, Endscore (raw + display), Podestzähler (1/2/3).
- Altersklassen-Antwort enthält zusätzlich Fortschritt (abgeschlossene Kämpfe vs geplante Kämpfe) und Altersklassen-Metadaten.
- Alle gemeldeten Vereine erscheinen in der Liste, auch ohne Kämpfe oder mit 0.00.
- API bleibt lokalisierungsfreundlich: keine fest verdrahteten UI-Texte in DTO-Feldern.

#### G-05b RankingService-Erweiterung (P1, 3 SP)
**Story:** Als System möchte ich Altersklassen- und Globalwertung aus den Turnierdaten berechnen, damit die Vereinsrangfolge jederzeit reproduzierbar ist.
**Acceptance Criteria:**
- Altersklassen-Siegquote nutzt nur Kämpfe der jeweiligen Altersklasse.
- Globale Siegquote nutzt Kämpfe des gesamten Turniers.
- Rechenlogik ignoriert Freilose vollständig in Zähler und Nenner.
- Rechenlogik aktualisiert nach jedem beendeten Kampf (nicht nur nach Siegen).
- Tie-break und Rangvergabe entsprechen exakt den Leitregeln inklusive geteilter Ränge.

#### G-05c API-Endpunkte im Results-Bereich (P1, 2 SP)
**Story:** Als Frontend möchte ich dedizierte Endpunkte für Vereinswertungen abrufen, damit die neue Ansicht unabhängig von Ranglisten/Medaillenspiegel geladen werden kann.
**Acceptance Criteria:**
- Neue autorisierte Endpunkte unter `api/tournaments/{id}` für:
  - Vereinswertung je Altersklasse (eine Altersklasse oder alle Altersklassen),
  - globale Vereinswertung.
- 404 bei unbekanntem Turnier; 200 mit leerer, aber strukturierter Antwort bei noch fehlenden Kampfdaten.
- Statuslabel-Logik:
  - Altersklasse ist Final, sobald alle geplanten Kämpfe der Altersklasse beendet sind.
  - Global ist Final, sobald alle geplanten Kämpfe aller Altersklassen beendet sind.

#### G-05d Frontend-Ergebnisseite: dritter Tab Vereinswertung (P1, 3 SP)
**Story:** Als Nutzer möchte ich im Ergebnisbereich einen dritten Tab Vereinswertung sehen, damit ich teambezogene Auswertungen direkt neben Ranglisten und Medaillenspiegel prüfen kann.
**Acceptance Criteria:**
- Results-Ansicht hat drei Tabs: Ranglisten, Medaillenspiegel, Vereinswertung.
- Im Tab Vereinswertung werden zwei Blöcke untereinander angezeigt:
  - Vereinswertung pro Altersklasse,
  - globale Vereinswertung.
- Je Block wird das Statuslabel Vorläufig/Final sichtbar angezeigt.
- Je Altersklasse wird ein Fortschrittshinweis angezeigt: abgeschlossene Kämpfe vs geplant.
- Darstellung der mittleren Rechentiefe pro Verein: Podestzähler 1/2/3, Basispunkte, Siege, Kämpfe, Siegquote, Endscore.
- Deutsche Standardtexte mit i18n-Keys; englische Platzhalter ergänzen.

#### G-05e Live-Aktualisierung und Konsistenz (P1, 1 SP)
**Story:** Als Turnierleitung möchte ich, dass die Vereinswertung live den aktuellen Stand zeigt, damit die Anzeige jederzeit mit den Kampfergebnissen übereinstimmt.
**Acceptance Criteria:**
- Nach Abschluss eines Kampfes wird die Vereinswertung neu geladen oder per Event aktualisiert.
- Vorläufig/Final-Status kippt automatisch bei Erreichen der Abschlussbedingungen.
- Es wird keine manuelle Abschaltlogik für die Wertung benötigt.

#### G-05f Tests (Unit + Integration + Frontend) (P1, 1 SP)
**Story:** Als Team möchten wir die Wertungslogik und Anzeige automatisiert absichern, damit Regeländerungen keine unbemerkten fachlichen Regressionen auslösen.
**Acceptance Criteria:**
- Unit-Tests für Berechnung:
  - Basispunkte inkl. zwei dritter Plätze,
  - Siegquote ohne Freilos,
  - Nenner=0 Verhalten,
  - Tie-break Reihenfolge und geteilte Ränge (1,2,2,4).
- Integrationstests für Endpunkte: Datenform, Autorisierung, Statuslabel, 404/200-Verhalten.
- Frontend-Tests für neuen Tab und Kernfelder der Tabellen.
- Neue Tests sind mit `Category=UnitTest` markiert (Backend) und im bestehenden Frontend-Testlauf enthalten.

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
