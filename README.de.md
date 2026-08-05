# Judo-Turnierverwaltung

[English](README.md)

Eine Turnierverwaltungsanwendung fuer Judo-Veranstaltungen vor Ort. Sie ist fuer einen zuverlaessigen Betrieb offline auf einem einzelnen Laptop oder im lokalen LAN ausgelegt, kann aber auch internet-gehostet hinter einem nginx-Reverse-Proxy betrieben werden, und verwendet Deutsch als primaere Produktsprache. Sie kombiniert offline-faehige ASP.NET-Core-Backenddienste, SQLite-Persistenz und ein Angular-Frontend fuer Turnierplanung, Kampfbetrieb, Meldungen und Echtzeit-Anzeigeablaeufe.

## Schnellinstallationsanleitung

Auf einem frischen Debian/Ubuntu-Host (Proxmox/LXC) installieren Sie einen Release-Build mit einem einzigen Befehl:

```bash
curl -fsSL https://raw.githubusercontent.com/McGyver666/JudoTournamentManager/main/deploy/bootstrap_install.sh \
  | sudo bash -s -- --hostname tournament.example.com --email admin@example.com
```

Der Befehl laedt das neueste Release herunter (oder ein per `--version vX.Y.Z` festgelegtes), prueft dessen Pruefsumme und fuehrt den Installer aus. Zum `--version`-Schalter, zu Upgrades und zu einer sicherheitsbewussten Alternative ("herunterladen, pruefen, dann ausfuehren") siehe [`deploy/README.md`](deploy/README.md).

Bei einer **frischen** Installation legt das Skript ein initiales `admin`-Konto mit einem zufaelligen Passwort an und gibt es am Ende einmalig in einem klar markierten Block aus — bitte notieren, es wird nicht erneut angezeigt. Ein erneuter Lauf (Upgrade) laesst das bestehende Konto unveraendert und gibt nichts aus:

```text
============================================================
Initial admin credentials (save these now):
  Username: admin
  Password: <generated>
============================================================
```

## Projektstatus

Dieses Projekt befindet sich in aktiver MVP-Umsetzung; die meisten zentralen Turnierablaeufe sind bereits umgesetzt.

Bereits verfuegbar:
- .NET-10-Backendloesung mit SQLite-Persistenz (EF Core)
- lokales Startskript
- Health-Endpunkt
- APIs fuer Turniere, Tatamis, Gewichtsklassen, Vereine, Athleten, Meldungen, Auslosungen und Kaempfe
- Athleten-Dateiimport ueber DM4 und DMF (mit automatischer Formaterkennung)
- Ablauf zur Gewichtsklassenzuordnung (automatisch und manuell)
- unterstuetzte Gewichtsklassengenerierung (Vorschau und Anwenden) mit zwei Strategien:
  - Standardklassen 2026 (Quelle: `altersklassen_2026.md`)
  - athletengesteuerte Klassen nach Zielzahl von Athleten je Klasse und maximaler Gewichtsdifferenz
- Ablauf zur Tatami-Zuordnung (automatisch und manuell)
- Kampfuebersicht abgeschlossener Kaempfe (Operator/Admin) mit Filtern nach Gewichtsklasse/Matte und aufklappbaren Wertungsdetails
- Admin-Ergebniskorrektur in der Kampfuebersicht: Wertungen und Sieger inline bearbeiten, mit Warnung bei betroffenen Folgekämpfen und kaskadendem Reset
- oeffentliche Anzeigeansicht mit Echtzeitaktualisierungen (SignalR)
- serverautorisierte synchronisierte Kampf- und Osae-komi-Zeit in Bedien- und Anzeigeansichten
- lokale Zehntelsekundenanzeige fuer laufende Schlusssekunden des Kampfes und aktive Osae-komi-Zeiten
- Ergebnis- und Medaillenspiegelansichten
- lokale Authentifizierung (Anmelden/Abmelden, Sitzungspersistenz, Benutzerverwaltung fuer Administratoren)
- authentifizierter SignalR-Hub-Zugriff (Echtzeitaktualisierungen erfordern ein gueltiges Bearer-Token)
- Sicherheitsantwortheader (CSP sowie Frame-, MIME- und Referrer-Schutz)
- Ratenbegrenzung fuer Auth-Endpunkte und Begrenzungen der Anfragetextgroesse (der Restore-Endpunkt erlaubt ausdruecklich groessere Nutzdaten)
- migrationsbasierter Datenbankstart (`MigrateAsync`) mit EF-Migrationshistorie und Uebernahme bestehender Schemata
- HMAC-SHA256-Hashing fuer Authentifizierungs-Sitzungstoken (`Security:AuthTokenHmacSecret`)
- Grundlage fuer deutschsprachige Lokalisierung
- Angular-19-Frontend (Administration, Betrieb sowie Anzeige/Ergebnisse), das von der API bereitgestellt wird
- gehaertete lokale Skripte fuer Test- und Seed-Daten (`JUDO_TEST_PASSWORD`, Produktionsschutz)
- Sicherungs- und Wiederherstellungsablauf fuer Administratoren in der Turnieransicht (Sicherung herunterladen und Wiederherstellung hochladen)
- authentifizierter Serverzeit-Endpunkt fuer die Frontend-Zeitsynchronisation (`GET /api/time`)
- serverseitiger Kampfzeit-Auswerter fuer zeitbasierte Kampf- und Osae-komi-Entscheidungen
- Osae-komi-Ippon haelt die Kampfzeit auf dem Server sofort an
- Unit-Test-Projekt (247 erfolgreiche Tests, Category=UnitTest)
- TLS/LAN-Betriebsstabilisierung und wiederholte Feldvalidierung

## Architektur

## Zielbild
- **Offline-faehig** (keine harte Cloud-Abhaengigkeit zur Laufzeit)
- **Ein Host-Laptop** als Standardmodus vor Ort
- **Optionale LAN-Clients** im selben lokalen Netzwerk
- **Auch internet-gehostet betreibbar** hinter einem nginx-Reverse-Proxy mit TLS (siehe `deploy/`)
- **Deutschsprachige Benutzeroberflaeche**
- **Von Beginn an lokalisierbar**

## Aktueller technischer Stand
- **Backend:** ASP.NET Core Web API (.NET 10)
- **Loesungsstil:** modularer Monolith
- **Persistenz:** SQLite ueber EF Core (`App_Data/judo-tournament.db`, wird beim Start automatisch angelegt)
- **Schemakompatibilitaet:** Der Start verwendet EF-Core-Migrationen und eine Migrationshistorie; bestehende lokale Datenbanken ohne Migrationshistorie werden beim Start sicher uebernommen.
- **Frontend:** Angular-19-SPA (`frontend/`), in das API-Verzeichnis `wwwroot/` gebaut und same-origin bereitgestellt
- **Health-Endpunkt:** `/health`
- **Anwendungseinstieg:** `/` (Angular-App; Deep Links fallen auf `index.html` zurueck)

## Zielarchitektur fuer das MVP
- **Backend:** ASP.NET Core Web API
- **Frontend:** SPA, die lokal durch den Host bereitgestellt wird
- **Datenbank:** SQLite
- **Echtzeitaktualisierungen:** SignalR/WebSockets
- **Betriebsmodus:** lokaler Rechner oder lokales LAN, oder internet-gehostet hinter einem nginx-Reverse-Proxy (siehe `deploy/`)

## Projektstruktur

```text
JudoTournamentManagement.sln
JudoTournamentManagement.Api/
JudoTournamentManagement.Api.Tests/
frontend/
deploy/
docs/
AGENTS.md
CONTEXT.md
start-local.ps1
start-local.sh
```

## Voraussetzungen

Das Projekt kann unter Windows, Linux und macOS ausgefuehrt werden.

Von den Startskripten verwendete bevorzugte Reihenfolge fuer die .NET-Aufloesung:
1. lokales SDK in `.dotnet/`
2. systemweit verfuegbares `dotnet` aus `PATH`

Lokaler SDK-Pfad unter Windows:

```powershell
.\.dotnet\dotnet.exe
```

Lokaler SDK-Pfad unter Linux/macOS:

```bash
./.dotnet/dotnet
```

Damit ist eine vollstaendig lokale Ausfuehrung ohne systemweit installiertes SDK moeglich.

## Lokales Starten

Die API lokal starten (Windows / PowerShell):

```powershell
.\start-local.ps1
```

Frontend-Build ueberspringen und nur das Backend starten (Windows / PowerShell):

```powershell
.\start-local.ps1 -SkipFrontendBuild
```

Falls `JudoTournamentManagement.Api/wwwroot/index.html` noch nicht existiert, fuehrt das Startskript auch mit `-SkipFrontendBuild` einmalig einen Frontend-Build aus, damit die UI nicht mit `404` endet.

Mit optionaler HTTPS-Bindung fuer den LAN-Modus starten (Windows / PowerShell):

```powershell
.\start-local.ps1 -EnableTls
```

Die API lokal starten (Linux/macOS / bash):

```bash
chmod +x ./start-local.sh
./start-local.sh
```

Frontend-Build ueberspringen und nur das Backend starten (Linux/macOS / bash):

```bash
./start-local.sh --skip-frontend-build
```

Falls `JudoTournamentManagement.Api/wwwroot/index.html` noch nicht existiert, fuehrt das Startskript auch mit `--skip-frontend-build` einmalig einen Frontend-Build aus, damit die UI nicht mit `404` endet.

Mit optionaler HTTPS-Bindung fuer den LAN-Modus starten (Linux/macOS / bash):

```bash
./start-local.sh --enable-tls --https-port 7080
```

Standardmaessig bauen beide Startskripte vor dem Start der API das Angular-Frontend, damit `wwwroot` mit den aktuellen UI-Quellen synchron bleibt.

Die API wird gestartet auf:

```text
http://0.0.0.0:5080
```

Bei aktiviertem TLS binden die Startskripte sowohl HTTP als auch HTTPS, zum Beispiel:

```text
http://0.0.0.0:5080
https://0.0.0.0:7080
```

Nuetzliche Endpunkte:
- Startseite: `http://localhost:5080/`
- Health: `http://localhost:5080/health`
- Swagger (Development): `http://localhost:5080/swagger`

Bei einer aelteren lokalen Datenbank ergaenzt der Start fehlende Legacy-Spalten, die von aktuellen Funktionen benoetigt werden.
Bei groesseren lokalen Schemaabweichungen die lokale Datenbank durch Loeschen von `JudoTournamentManagement.Api/App_Data/judo-tournament.db*` zuruecksetzen und anschliessend neu starten.

## Produktivbetrieb (internet-gehostet)

Fuer einen internet-erreichbaren Betrieb laeuft die App hinter einem nginx-Reverse-Proxy,
der TLS (Let's Encrypt) terminiert und Anfragen an die API auf `127.0.0.1:5080` weiterleitet.
Der oeffentliche Hostname wird zur Bereitstellungszeit gesetzt — die mitgelieferte
nginx-Konfiguration verwendet einen Platzhalter `__SERVER_NAME__`, der bei der Installation
ersetzt wird. Die API vertraut den Headern `X-Forwarded-Proto` und `X-Forwarded-For` nur
vom Loopback-Proxy (`127.0.0.1`), sodass `HttpContext.Request.Scheme` das urspruengliche
HTTPS widerspiegelt und generierte Links (z. B. die oeffentliche Gast-Freigabe-URL)
`https://` verwenden. Im Offline-/LAN-Betrieb ohne Proxy sind keine Forwarded-Header
vorhanden und das Schema bleibt `http`. Siehe `deploy/README.md` und `deploy/judo-tournament.nginx.conf` fuer systemd-Unit,
nginx-Konfiguration und Certbot-Einrichtung.

Anders als im Offline-/LAN-Modus ist dieser Modus oeffentlich erreichbar und verlaesst sich
nicht auf ein vertrauenswuerdiges lokales Netzwerk — TLS erzwingen und Geheimnisse
(z. B. `Security:AuthTokenHmacSecret`) ueber die Konfiguration einspeisen statt sie zu hartcodieren.

## Bootstrap des Administratorpassworts

Server-Installationen mit `deploy/install_release.sh` (oder dem Ein-Befehl-Bootstrap) legen den initialen Admin automatisch an und geben die Zugangsdaten einmalig aus — siehe [Schnellinstallationsanleitung](#schnellinstallationsanleitung). Die folgenden Schritte gelten fuer lokale/manuelle Laeufe.

Beim ersten Start ist die Datenbank leer. Mit dem Endpunkt `/api/auth/bootstrap-admin` ein Administratorkonto initialisieren:

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

Nach erfolgreichem Bootstrap unter `http://localhost:5080/login` mit den Zugangsdaten anmelden.

**Hinweis:** Der Bootstrap-Endpunkt funktioniert nur, solange keine Administratorkonten vorhanden sind. Andernfalls wird ein Fehler zurueckgegeben.

## Build und Tests

Die Loesung bauen (Windows mit lokalem SDK):

```powershell
.\.dotnet\dotnet.exe build .\JudoTournamentManagement.sln
```

Die Loesung bauen (Linux/macOS mit lokalem SDK):

```bash
./.dotnet/dotnet build ./JudoTournamentManagement.sln
```

Die Loesung bauen (jedes Betriebssystem mit globalem SDK):

```bash
dotnet build ./JudoTournamentManagement.sln
```

Alle Unit-Tests ausfuehren (Windows mit lokalem SDK):

```powershell
.\.dotnet\dotnet.exe test .\JudoTournamentManagement.sln --filter Category=UnitTest
```

Alle Unit-Tests ausfuehren (Linux/macOS mit lokalem SDK):

```bash
./.dotnet/dotnet test ./JudoTournamentManagement.sln --filter Category=UnitTest
```

Alle Unit-Tests ausfuehren (jedes Betriebssystem mit globalem SDK):

```bash
dotnet test ./JudoTournamentManagement.sln --filter Category=UnitTest
```

Smoke-Test fuer Auslosungs-/Sperrablauf ausfuehren (Windows / PowerShell):

```powershell
./test-draw-lock-flow.ps1
```

LAN-Propagierungsvalidierung ausfuehren (Windows / PowerShell):

```powershell
./test-lan-validation.ps1
```

Optionale Zugangsdaten fuer einen vorhandenen lokalen Administrator:

```powershell
$env:JUDO_TEST_PASSWORD="<existing-admin-password>"
./test-lan-validation.ps1
```

Gegen einen selbstsignierten HTTPS-Endpunkt (lokales Zertifikat) ausfuehren und die Zertifikatspruefung in Skriptanfragen ueberspringen:

```powershell
./test-lan-validation.ps1 -BaseUrl https://localhost:7080 -SkipCertificateCheck
```

Das Skript legt Operator- und Anzeige-Testbenutzer an, fuehrt lese- und schreibende Pruefungen ueber Clients hinweg aus, misst die Propagierungslatenz und schreibt einen JSON-Nachweisbericht:
`lan-validation-report-<timestamp>.json`.

Aktuellster gemessener Nachweis:
- `lan-validation-report-20260706131837.json` -> maximale Propagierung 109 ms (Ziel <= 2000 ms)

Das Smoke-Skript validiert diese Abfolge Ende-zu-Ende gegen eine laufende lokale API:
- Die Auslosungsgenerierung laesst die Kategorie entsperrt.
- Eine Kategorieumzuordnung vor Beginn des ersten Kampfes aktualisiert die Auslosung automatisch.
- Der erste reale Kampfbeginn sperrt die Kategorie.
- Eine Umzuordnung nach der Sperre wird mit HTTP 409 abgelehnt.

## Paket fuer ein anderes System

Ein minimales Uebertragungspaket erstellen (veroeffentlichte API, Startskripte und README):

```powershell
.\package-transfer.ps1
```

Standardmaessig baut das Skript zuerst das Angular-Frontend, sodass das Paket direkt ausfuehrbar ist.
Fuer ein reines API-Paket diesen Schritt ueberspringen:

```powershell
.\package-transfer.ps1 -SkipFrontendBuild
```

Ein eigenstaendiges Paket fuer eine bestimmte Laufzeit erstellen (groesser, auf dem Zielsystem ist keine .NET-Laufzeit erforderlich):

```powershell
.\package-transfer.ps1 -Runtime win-x64 -SelfContained
```

Die lokale SQLite-Datenbank (`App_Data`) in das Paket aufnehmen:

```powershell
.\package-transfer.ps1 -IncludeDatabase
```

Die Ausgabe wird als zeitgestempelter Ordner und ZIP-Archiv unter `artifacts/transfer/` geschrieben.

## Frontend (Angular)

Die Angular-19-Anwendung liegt in `frontend/` und wird in das `wwwroot/`-Verzeichnis der API kompiliert. Die laufende API stellt die Benutzeroberflaeche daher unter `/` bereit; ein separater Webserver ist nicht erforderlich.

Abhaengigkeiten installieren (einmalig):

```powershell
cd frontend
npm install
```

Die Benutzeroberflaeche in `wwwroot/` bauen (vor dem API-Start ausfuehren, um die bereitgestellte Anwendung zu aktualisieren):

```powershell
cd frontend
npm run build
```

Optionaler reiner UI-Entwicklungsserver mit Hot Reload (API-Aufrufe werden an das laufende Backend weitergeleitet):

```powershell
cd frontend
npm start
```

Frontend-Unit-Tests einmalig ausfuehren (headless, beendet sich automatisch):

```powershell
cd frontend
npm run test:ci
```

Damit bleibt Karma nach Abschluss der Tests nicht im Watch-Modus offen.

Lokalisierungsressourcen sind einfache JSON-Woerterbuecher in `frontend/public/i18n/`.
`de.json` ist die vollstaendige deutsche Quelle, `en.json` der englische Fallback; sie werden unter `/i18n/{lang}.json` bereitgestellt.

## Aktuelle API

### Kernendpunkte

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
- `POST /api/tournaments/{tournamentId}/athletes/import/file` (DM4/DMF-Upload, automatische Erkennung)
- `POST /api/tournaments/{tournamentId}/athletes/import/dm4` (DM4-spezifische Kompatibilitaetsroute)

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
- `POST /api/tournaments/{tournamentId}/fights/assign-tatami-bulk` (mehrere Kämpfe atomar Matten zuweisen)
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/queue-move`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/start`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/stop`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/resume`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/score/adjust`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/osae-komi/start`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/osae-komi/stop`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/result`
- `GET /api/tournaments/{tournamentId}/completed-fights` (Admin/Operator; angereicherte Übersicht abgeschlossener Kämpfe)
- `POST /api/tournaments/{tournamentId}/completed-fights/{fightId}/edit-result` (Admin; Wertungen und Sieger korrigieren mit Bestätigungsflow für betroffene Folgekämpfe)

- `GET /api/tournaments/{tournamentId}/medal-table`
- `GET /api/tournaments/{tournamentId}/audit-log`

- `GET /api/tournaments/{tournamentId}/public/athletes` (datenminimiert; Admin/Operator/Display/Gast)
- `GET /api/tournaments/{tournamentId}/public/clubs`
- `GET /api/tournaments/{tournamentId}/public/categories`
- `GET /api/tournaments/{tournamentId}/public/tournament`
- `GET /api/tournaments/{tournamentId}/public/categories/{categoryId}/fights`
- `GET /api/tournaments/{tournamentId}/public/categories/{categoryId}/standings`
- `GET /api/tournaments/{tournamentId}/guest-share` (Admin/Operator)
- `POST /api/tournaments/{tournamentId}/guest-share/enable`
- `POST /api/tournaments/{tournamentId}/guest-share/disable`
- `POST /api/tournaments/{tournamentId}/guest-share/rotate`
- `GET /api/tournaments/{tournamentId}/guest-share/qr` (SVG)

- `POST /api/auth/bootstrap-admin`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/me`
- `GET /api/time`
- `GET /api/auth/users`
- `POST /api/auth/users`
- `PATCH /api/auth/users/{userId}/active`
- `POST /api/auth/users/{userId}/reset-password`

Frontend-Authentifizierungsrouten:
- `/login`
- `/users` (Administrator)

Beispielanforderung fuer `POST /api/tournaments`:

```json
{
  "name": "RWE Judo Cup",
  "date": "2026-09-12",
  "venue": "Essen",
  "organizer": "JC Essen"
}
```

## Gastzugriff (oeffentliche Wettkampflisten)

Zuschauer im lokalen Netzwerk koennen die Wettkampflisten per QR-Code als
Nur-Lese-Ansicht oeffnen — ohne Benutzerkonto und ohne die App-Navigation.

Ablauf:
- In der Turnieransicht oeffnet ein Administrator oder Operator das Panel
  **Gaeste-Zugriff** und klickt auf **Freigeben**. Damit entsteht genau ein
  Gast-Token pro Turnier; angezeigt werden ein QR-Code und ein teilbarer Link
  (`/public/match-lists?tid=…&t=<token>`).
- Das Auto-Aus-Preset steuert eine optionale Gueltigkeitsgrenze: **bis Mitternacht
  heute** (Standard), **4h**, **8h** oder **kein Auto-Aus**.
- **Rotieren** erzeugt ein neues Token und macht den vorherigen QR sofort
  ungueltig. **Deaktivieren** schaltet die Freigabe aus, ohne das Token zu
  verwerfen.
- Gaeste erreichen ausschliesslich die oeffentlichen Nur-Lese-Wettkampflisten
  dieses einen Turniers. Ein blosser authentifizierter Endpunkt verlangt weiterhin
  eine Betreiberrolle; der Gastzugriff reicht damit nie ueber die Wettkampflisten
  hinaus.

Datensparsamkeit:
- Die Public-Endpoints liefern nur reduzierte DTOs (Athleten = Id, Verein, Vor-/
  Nachname; Vereine = Id, Name). Keine Lizenz-/Passnummer, kein Geburtsjahr, kein
  Gewicht, kein Grad, keine Kontaktdaten. Die gesamte Wettkampflisten-Ansicht
  (Display und Gast) nutzt dasselbe reduzierte Modell.

TLS-Regel:
- Auf einem lokalen/LAN-Host (localhost, private IP-Bereiche, einteilige oder
  `.local`/`.lan`-Hostnamen) wird einfaches HTTP akzeptiert.
- Auf einem nicht-lokalen/oeffentlichen Host werden Gast-Link und QR nur ueber
  **HTTPS** ausgeliefert; ein Abruf ueber einfaches HTTP liefert `400 Bad Request`.
  In Produktion terminiert nginx das TLS vor der App. Eine explizite Basis-URL
  laesst sich ueber `GuestShare:PublicBaseUrl` konfigurieren.

Realtime und Lebenszyklus:
- Gaeste treten dem bestehenden reinen Broadcast-SignalR-Hub bei, aber nur der
  Gruppe des eigenen Turniers und nur solange die Freigabe aktiv ist
  (Soft-Disconnect: nach dem Deaktivieren werden keine neuen Verbindungen oder
  Beitritte mehr akzeptiert; laufende Verbindungen laufen einfach aus).
- Gast-Zugriffe werden nicht protokolliert; Freigeben, Deaktivieren und Rotieren
  werden auditiert (`GuestShareEnabled`/`GuestShareDisabled`/`GuestShareRotated`)
  — ohne das Token.
- Backups enthalten das Gast-Token nie; nach einem Restore ist die Freigabe immer
  deaktiviert (zum erneuten Teilen neu freigeben).
- Die Public-Endpoints haben ein eigenes per-IP-Rate-Limit-Fenster.

## Lokalisierung

Lokalisierungsregeln fuer das MVP:
- Die primaere Sprache ist Deutsch.
- Sichtbare UI-Texte sollen standardmaessig Deutsch sein.
- Neue UI-Arbeit muss lokalisierungsfaehig sein.
- Sichtbare hartcodierte englische Zeichenketten in der Produkt-UI vermeiden.

Aktuelle Kultureinstellung des Backends:
- Standard: `de-DE`
- Fallback-faehige zweite Kultur: `en-US`

## Entwicklungsprinzipien

- Offline-faehig vor Cloud-abhaengig
- einfache Architektur vor verteilter Architektur
- deutschsprachige UX zuerst
- lokalisierbare UI ab dem ersten Bildschirm
- explizite Validierung fuer alle schreibenden Endpunkte
- keine stille Fehlerunterdrueckung
- Arbeit in GitHub Issues verfolgen; Entscheidungen in `docs/adr/` und Domänenkontext in `CONTEXT.md` pflegen

## Sicherheit und Betriebsmodell

- Keine verpflichtende Internetabhaengigkeit fuer die Turnierdurchfuehrung vor Ort
- Unterstuetzt Offline-/LAN-Betrieb sowie eine internet-gehostete Bereitstellung hinter nginx (siehe `deploy/`)
- Der internet-gehostete Modus ist oeffentlich erreichbar: TLS erzwingen und das Netzwerk als nicht vertrauenswuerdig behandeln (nicht auf die Trusted-LAN-Annahme verlassen)
- Alle kuenftigen Funktionen fuer Authentifizierung, Audit-Logging und Sicherungen muessen dem Backlog folgen
- Geheimnisse duerfen bei spaeteren externen Integrationen niemals hartcodiert sein
- SignalR-Hub-Zugriff erfordert Authentifizierung; das Frontend uebergibt fuer den Echtzeitkanal ein Bearer-Token
- Die Kampfzeit bleibt serverautoritativ; die Frontend-Zeitsynchronisation dient nur der Anzeige und darf offline keine Regelentscheidung ausloesen
- Hilfsskripte brechen ab, wenn `ASPNETCORE_ENVIRONMENT=Production` gesetzt ist

## Copilot-Einrichtung

Dieser Arbeitsbereich ist fuer die kuenftige Verwendung von GitHub Copilot vorbereitet mit:
- `README.md` als Projektkontext
- `AGENTS.md` als stets geltende Arbeitsbereichsanleitung

Bei der weiteren Umsetzung mit Copilot:
1. `AGENTS.md` und `CONTEXT.md` lesen
2. ein offenes GitHub Issue auswaehlen (siehe `docs/agents/issue-tracker.md`)
3. den naechstkleineren Ende-zu-Ende-Schnitt umsetzen
4. schwer umkehrbare Entscheidungen als ADRs in `docs/adr/` festhalten und `CONTEXT.md` aktuell halten