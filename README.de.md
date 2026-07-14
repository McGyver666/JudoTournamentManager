# Judo-Turnierverwaltung

[English](README.md)

Eine Turnierverwaltungsanwendung fuer Judo-Veranstaltungen vor Ort. Sie ist fuer einen zuverlaessigen Betrieb ohne Internetzugang ausgelegt und verwendet Deutsch als primaere Produktsprache. Sie kombiniert offline-faehige ASP.NET-Core-Backenddienste, SQLite-Persistenz und ein Angular-Frontend fuer Turnierplanung, Kampfbetrieb, Meldungen und Echtzeit-Anzeigeablaeufe.

## Projektstatus

Dieses Projekt befindet sich in aktiver MVP-Umsetzung; die meisten zentralen Turnierablaeufe sind bereits umgesetzt.

Bereits verfuegbar:
- .NET-10-Backendloesung mit SQLite-Persistenz (EF Core)
- lokales Startskript
- Health-Endpunkt
- APIs fuer Turniere, Tatamis, Kategorien, Vereine, Athleten, Meldungen, Auslosungen und Kaempfe
- Ablauf zur Kategoriezuordnung (automatisch und manuell)
- unterstuetzte Kategoriegenerierung (Vorschau und Anwenden) mit zwei Strategien:
  - Standardklassen 2026 (Quelle: `altersklassen_2026.md`)
  - athletengesteuerte Klassen nach Zielzahl von Athleten je Klasse und maximaler Gewichtsdifferenz
- Ablauf zur Tatami-Zuordnung (automatisch und manuell)
- oeffentliche Anzeigeansicht mit Echtzeitaktualisierungen (SignalR)
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
- Unit-Test-Projekt (192 erfolgreiche Tests, Category=UnitTest)
- TLS/LAN-Betriebsstabilisierung und wiederholte Feldvalidierung

## Architektur

## Zielbild
- **Offline-first**
- **Ein Host-Laptop** als Standardmodus
- **Optionale LAN-Clients** im selben lokalen Netzwerk
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
- **Betriebsmodus:** Nur lokaler Rechner oder lokales LAN

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

## Bootstrap des Administratorpassworts

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

- Offline-first vor Cloud-first
- einfache Architektur vor verteilter Architektur
- deutschsprachige UX zuerst
- lokalisierbare UI ab dem ersten Bildschirm
- explizite Validierung fuer alle schreibenden Endpunkte
- keine stille Fehlerunterdrueckung
- Arbeit am `backlog.md` ausrichten

## Sicherheit und Betriebsmodell

- Keine verpflichtende Internetabhaengigkeit fuer die Turnierdurchfuehrung
- Nur lokaler/LAN-Betrieb fuer das MVP
- Alle kuenftigen Funktionen fuer Authentifizierung, Audit-Logging und Sicherungen muessen dem Backlog folgen
- Geheimnisse duerfen bei spaeteren externen Integrationen niemals hartcodiert sein
- SignalR-Hub-Zugriff erfordert Authentifizierung; das Frontend uebergibt fuer den Echtzeitkanal ein Bearer-Token
- Hilfsskripte brechen ab, wenn `ASPNETCORE_ENVIRONMENT=Production` gesetzt ist

## Copilot-Einrichtung

Dieser Arbeitsbereich ist fuer die kuenftige Verwendung von GitHub Copilot vorbereitet mit:
- `README.md` als Projektkontext
- `.github/copilot-instructions.md` als stets geltende Arbeitsbereichsanleitung

Bei der weiteren Umsetzung mit Copilot:
1. `README.md` lesen
2. `backlog.md` lesen
3. den naechstkleineren Backlog-Schnitt umsetzen
4. `backlog.md` aktualisieren, wenn sich Umfang oder Umsetzungsstatus wesentlich aendern