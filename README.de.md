# Shiai Manager

[English](README.md)

**Shiai Manager** (Marke **SHIAI**) ist eine Turnierverwaltungsanwendung fuer Judo-Veranstaltungen vor Ort. Sie ist fuer einen zuverlaessigen Betrieb offline auf einem einzelnen Laptop oder im lokalen LAN ausgelegt, kann aber auch internet-gehostet hinter einem nginx-Reverse-Proxy betrieben werden, und verwendet Deutsch als primaere Produktsprache. Sie kombiniert ein offline-faehiges ASP.NET-Core-Backend, SQLite-Persistenz und ein Angular-Frontend — gestaltet mit dem SHIAI-Dual-Theme-Dojo-Design-System (hell/dunkel) — fuer Turnierplanung, Kampfbetrieb, Meldungen und Echtzeit-Anzeigeablaeufe.

## Schnellinstallationsanleitung

Auf einem frischen Debian/Ubuntu- oder RHEL-kompatiblen (z. B. Oracle Linux 10)
Proxmox/LXC-Host installieren Sie einen Release-Build mit dem Bootstrap-Befehl.

Installieren Sie zuerst die Downloader-Voraussetzungen.

Debian/Ubuntu:

```bash
sudo apt-get update
sudo apt-get install -y curl ca-certificates unzip
```

RHEL-kompatible Systeme:

```bash
sudo dnf install -y curl ca-certificates unzip
```

Danach den Bootstrap-Befehl ausfuehren:

```bash
curl -fsSL https://raw.githubusercontent.com/McGyver666/Shiai-Manager/main/deploy/bootstrap_install.sh \
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

## Funktionen

- Offline-faehiger Turnierbetrieb auf einem Laptop oder im lokalen LAN, optional internet-gehostet hinter nginx.
- Turnierverwaltung, Vereine, Athleten, Meldungen, Kategorien, Voreinstellungen und assistierte Klassengenerierung.
- Einzelturnier-Auslosungen, Brackets, Tatami-Zuordnung, Kampfbetrieb und serverautorisierte Zeitmessung.
- NWJV-Mannschafts-Kampftage fuer Senioren- und U16-Profile mit Tageswaage, Aufstellungen und Begegnungen.
- DM4/DMF-Athletenimport, oeffentliche Anzeigen, Wettkampflisten, Ranglisten, Medaillenspiegel und Vereinswertung.
- Lokale Authentifizierung mit Rollen, HttpOnly-Sitzungen, Audit-Logging und Gastfreigaben.
- SQLite-Persistenz, Sicherung/Wiederherstellung, SignalR-Echtzeitaktualisierungen, deutschsprachige Lokalisierung und Angular-Frontend aus der API.

## Aktueller technischer Stand
- **Backend:** ASP.NET Core Web API (.NET 10)
- **Loesungsstil:** modularer Monolith
- **Persistenz:** SQLite ueber EF Core (`App_Data/judo-tournament.db`, wird beim Start automatisch angelegt)
- **Schemakompatibilitaet:** Der Start verwendet EF-Core-Migrationen und eine Migrationshistorie; bestehende lokale Datenbanken ohne Migrationshistorie werden beim Start sicher uebernommen.
- **Frontend:** Angular-19-SPA (`frontend/`), in das API-Verzeichnis `wwwroot/` gebaut und same-origin bereitgestellt
- **Health-Endpunkt:** `/health`
- **Anwendungseinstieg:** `/` (Angular-App; Deep Links fallen auf `index.html` zurueck)

## Zielarchitektur
- **Backend:** ASP.NET Core Web API
- **Frontend:** SPA, die lokal durch den Host bereitgestellt wird
- **Datenbank:** SQLite
- **Echtzeitaktualisierungen:** SignalR/WebSockets
- **Betriebsmodus:** lokaler Rechner oder lokales LAN, oder internet-gehostet hinter einem nginx-Reverse-Proxy (siehe `deploy/`)

## Projektstruktur

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

Falls `ShiaiManager.Api/wwwroot/index.html` noch nicht existiert, fuehrt das Startskript auch mit `-SkipFrontendBuild` einmalig einen Frontend-Build aus, damit die UI nicht mit `404` endet.

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

Falls `ShiaiManager.Api/wwwroot/index.html` noch nicht existiert, fuehrt das Startskript auch mit `--skip-frontend-build` einmalig einen Frontend-Build aus, damit die UI nicht mit `404` endet.

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
- OpenAPI-Dokument (Development): `http://localhost:5080/openapi/v1.json`

Bei einer aelteren lokalen Datenbank ergaenzt der Start fehlende Legacy-Spalten, die von aktuellen Funktionen benoetigt werden.
Bei groesseren lokalen Schemaabweichungen die lokale Datenbank durch Loeschen von `ShiaiManager.Api/App_Data/judo-tournament.db*` zuruecksetzen und anschliessend neu starten.

## Produktivbetrieb (internet-gehostet)

Fuer einen internet-erreichbaren Betrieb laeuft die App hinter einem nginx-Reverse-Proxy,
der TLS (Let's Encrypt) terminiert und Anfragen an die API auf `127.0.0.1:5080` weiterleitet.
Der oeffentliche Hostname wird zur Bereitstellungszeit gesetzt — die mitgelieferte
nginx-Konfiguration verwendet einen Platzhalter `__SERVER_NAME__`, der bei der Installation
ersetzt wird. Die API vertraut den Headern `X-Forwarded-Proto` und `X-Forwarded-For` nur
vom Loopback-Proxy (`127.0.0.1`), sodass `HttpContext.Request.Scheme` das urspruengliche
HTTPS widerspiegelt und generierte Links (z. B. die oeffentliche Gast-Freigabe-URL)
`https://` verwenden. Im Offline-/LAN-Betrieb ohne Proxy sind keine Forwarded-Header
vorhanden und das Schema bleibt `http`. Siehe `deploy/README.md` und `deploy/shiai-manager.nginx.conf` fuer systemd-Unit,
nginx-Konfiguration und Certbot-Einrichtung.

Anders als im Offline-/LAN-Modus ist dieser Modus oeffentlich erreichbar und verlaesst sich
nicht auf ein vertrauenswuerdiges lokales Netzwerk — TLS erzwingen und Geheimnisse
(z. B. `Security:AuthTokenHmacSecret`) ueber die Konfiguration einspeisen statt sie zu hartcodieren.

Die Produktionspruefung beschraenkt zulaessige Hosts ueber `AllowedHosts`; Gastfreigabe-Links
verwenden die kanonische `GuestShare__PublicBaseUrl` statt des eingehenden `Host`-Headers.
Das Installationsskript setzt beide Werte aus `--hostname`; bei manuellen Bereitstellungen
muessen sie in der systemd-Umgebungsdatei konfiguriert werden.

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
.\.dotnet\dotnet.exe build .\ShiaiManager.sln
```

Die Loesung bauen (Linux/macOS mit lokalem SDK):

```bash
./.dotnet/dotnet build ./ShiaiManager.sln
```

Die Loesung bauen (jedes Betriebssystem mit globalem SDK):

```bash
dotnet build ./ShiaiManager.sln
```

Alle Unit-Tests ausfuehren (Windows mit lokalem SDK):

```powershell
.\.dotnet\dotnet.exe test .\ShiaiManager.sln --filter Category=UnitTest
```

Alle Unit-Tests ausfuehren (Linux/macOS mit lokalem SDK):

```bash
./.dotnet/dotnet test ./ShiaiManager.sln --filter Category=UnitTest
```

Alle Unit-Tests ausfuehren (jedes Betriebssystem mit globalem SDK):

```bash
dotnet test ./ShiaiManager.sln --filter Category=UnitTest
```

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

## API

Siehe [API-doc.md](API-doc.md) fuer die aktuellen HTTP-Endpunkte und Frontend-Routen.

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
- SignalR-Hub-Zugriff erfordert Authentifizierung; Operator-Sitzungen verwenden das HttpOnly-Sitzungscookie, Gastfreigaben ein fluechtiges Bearer-Token
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