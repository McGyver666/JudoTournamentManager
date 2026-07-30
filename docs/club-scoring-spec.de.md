# Umsetzungsreife Spezifikation: Vereinswertung

Stand: 2026-07-30

## 1. Ziel

Ergaenzung der Ergebnisansicht um einen dritten Tab Vereinswertung mit:

1. Vereinswertung pro Altersklasse
2. Globale Vereinswertung fuer das gesamte Turnier

Die Anzeige soll waehrend des Turniers laufend aktualisieren und den Status Vorlaeufig oder Final sichtbar kennzeichnen.

## 2. Verbindliche Fachregeln

### 2.1 Basispunkte

- Platz 1: 7 Punkte
- Platz 2: 5 Punkte
- Platz 3: 3 Punkte
- Zwei dritte Plaetze werden separat gezaehlt.

### 2.2 Siegquote

Formel:

Siegquote = gewonnene Kaempfe / absolvierte Kaempfe

Regeln:

- Freilos zaehlt nicht als Kampf.
- Nur Freilos ist kampflos.
- Bei Nenner 0 gilt Siegquote = 0.0.

### 2.3 Endscore

Altersklasse:

Endscore = Basispunkte der Altersklasse x Siegquote der Altersklasse

Global:

Endscore global = globale Basispunkte x globale Siegquote

### 2.4 Praezision, Gleichstand, Rangnummern

- Interne Berechnung in voller Praezision.
- Anzeige auf 2 Nachkommastellen.
- Ranking auf ungerundetem Endscore.
- Gleichheit per Toleranz epsilon = 1e-9.
- Tie-break Reihenfolge:
	1. Hoeherer ungerundeter Endscore
	2. Hoehere Siegquote
	3. Mehr erste Plaetze
	4. Mehr zweite Plaetze
	5. Mehr dritte Plaetze
	6. Sonst geteilter Rang
- Rangschema bei geteilten Plaetzen: 1, 2, 2, 4

### 2.5 Sichtbarkeit

- Altersklassenwertung zeigt alle gemeldeten Vereine der Altersklasse.
- Globale Wertung zeigt alle gemeldeten Vereine im Turnier.
- Athleten ohne Vereinszuordnung werden dem Sammelverein Ohne Verein zugeordnet.

### 2.6 Statusumschaltung

- Altersklasse ist Final, wenn alle geplanten Kaempfe der Altersklasse beendet sind.
- Global ist Final, wenn alle geplanten Kaempfe aller Altersklassen beendet sind.
- Bis dahin Vorlaeufig.

## 3. UI-Spezifikation

## 3.1 Ergebnisseite

In der Results-Ansicht:

- Tab 1: Ranglisten
- Tab 2: Medaillenspiegel
- Tab 3: Vereinswertung

## 3.2 Inhalt Tab Vereinswertung

Zwei Bloecke untereinander:

1. Vereinswertung pro Altersklasse
2. Globale Vereinswertung

Je Altersklasse:

- Ueberschrift Altersklasse
- Statusbadge Vorlaeufig oder Final
- Fortschrittstext: x von y Kaempfen abgeschlossen
- Tabelle je Verein mit Spalten:
	- Rang
	- Verein
	- 1. Plaetze
	- 2. Plaetze
	- 3. Plaetze
	- Basispunkte
	- Siege
	- Kaempfe
	- Siegquote
	- Endscore

Global:

- Statusbadge Vorlaeufig oder Final
- Tabelle mit denselben Kernspalten

## 3.3 Lokalisierung

- Neue i18n-Keys im Namespace results.clubScoring.*
- Deutsch vollstaendig
- Englisch als Platzhalter

## 4. API-Spezifikation

Kontext: bestehender Controller ResultsController.

Neue Endpunkte unter api/tournaments/{tournamentId}:

1. GET club-scoring/age-groups
2. GET club-scoring/global

Optionale Erweiterung fuer Filter:

3. GET club-scoring/age-groups/{ageGroup}

Antwortschema Altersklassen:

- tournamentId
- generatedAtUtc
- items[]
	- ageGroup
	- status
	- completedFights
	- plannedFights
	- clubs[]
		- rank
		- isSharedRank
		- clubId
		- clubName
		- firstPlaces
		- secondPlaces
		- thirdPlaces
		- basePoints
		- wins
		- fights
		- winRateRaw
		- winRateDisplay
		- scoreRaw
		- scoreDisplay

Antwortschema Global:

- tournamentId
- status
- completedFights
- plannedFights
- clubs[] (gleiches Club-Schema)

HTTP-Verhalten:

- 200 OK bei bestehendem Turnier
- 404 NotFound bei unbekanntem Turnier
- Liste darf leer sein, Struktur bleibt stabil

## 5. Backend-Design

## 5.1 Service-Erweiterung

IRankingService erweitern um:

- GetClubScoringByAgeGroupAsync(tournamentId)
- GetClubScoringGlobalAsync(tournamentId)

Implementierung in RankingService.

## 5.2 Berechnungsablauf Altersklasse

1. Vereine der Altersklasse ermitteln (inklusive Ohne Verein bei Bedarf)
2. Podestplatzierungen aus Rankings der Kategorien der Altersklasse aggregieren
3. Basispunkte berechnen
4. Kaempfe der Altersklasse auswerten (ohne Freilos)
5. Siege und Kaempfe pro Verein zaehlen
6. Siegquote bestimmen
7. Endscore bestimmen
8. Sortieren + Tie-break + Rangnummern setzen
9. Status und Fortschritt bestimmen

## 5.3 Berechnungsablauf Global

Wie Altersklasse, aber kaempfe- und podestbezogen ueber alle Altersklassen.

## 5.4 Performance-Hinweise

- N+1 vermeiden: Athleten, Vereine, Kaempfe, Kategorien gebuendelt laden
- Pro Request in-memory aggregieren
- AsNoTracking fuer reine Leseabfragen

## 6. Live-Aktualisierung

Ausloeser:

- Nach jedem beendeten Kampf

Minimalvariante:

- Frontend aktualisiert Wertung nach bestehendem Kampfabschluss-Flow ueber REST-Reload

Optionale Optimierung:

- SignalR Event ResultsChanged fuer zielgenaues Refresh

## 7. Teststrategie

## 7.1 Backend Unit-Tests

- Basispunkte korrekt inkl. doppelter Bronze
- Freilos wird ignoriert
- Nenner-0 liefert Siegquote 0.0
- Tie-break Reihenfolge
- Geteilte Rangnummern 1,2,2,4
- Statusumschaltung Altersklasse und Global

## 7.2 Backend Integrationstests

- Endpunkte liefern erwartete Struktur
- 404 Verhalten
- Authorize Verhalten
- Alle gemeldeten Vereine erscheinen auch mit 0.00

## 7.3 Frontend Tests

- Dritter Tab sichtbar
- Untereinander-Anzeige Altersklasse und Global
- Statusbadges sichtbar
- Fortschritt je Altersklasse sichtbar
- Schluesselfelder je Vereinszeile gerendert

## 8. Implementierungsschnitt

## 8.1 Geplante Backend-Dateien

- JudoTournamentManagement.Api/Services/IRankingService.cs
- JudoTournamentManagement.Api/Services/RankingService.cs
- JudoTournamentManagement.Api/Controllers/ResultsController.cs
- JudoTournamentManagement.Api/Models (neue DTO-Dateien)
- JudoTournamentManagement.Api.Tests (neue Tests)

## 8.2 Geplante Frontend-Dateien

- frontend/src/app/features/results/results.component.ts
- frontend/src/app/features/results/results.component.html
- frontend/src/app/features/results/results.component.css
- frontend/src/app/core/api.service.ts
- frontend/src/app/core/models.ts
- i18n Dateien de/en

## 9. Abnahmecheckliste

1. Dritter Tab Vereinswertung vorhanden
2. Altersklassen- und Globalblock untereinander sichtbar
3. Regelwerk rechnerisch exakt umgesetzt
4. Live-Aktualisierung nach jedem beendeten Kampf
5. Statuslogik Vorlaeufig/Final korrekt
6. Tie-break und geteilte Rangausgabe korrekt
7. Alle gemeldeten Vereine enthalten
8. Tests gruen
