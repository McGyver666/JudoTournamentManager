# Shiai Manager API

This document lists the currently available HTTP API endpoints and the main frontend routes.
The API is served by the ASP.NET Core application under `/api`.

## Core endpoints

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
- `POST /api/tournaments/{tournamentId}/athletes/import/file` (DM4/DMF upload, auto-detect)
- `POST /api/tournaments/{tournamentId}/athletes/import/dm4` (DM4-specific compatibility route)

- `GET/POST/DELETE /api/tournaments/{tournamentId}/registrations`
- `POST /api/tournaments/{tournamentId}/registrations/auto-assign`
- `POST /api/tournaments/{tournamentId}/registrations/{registrationId}/category`
- `GET /api/tournaments/{tournamentId}/registrations/export`

- `POST /api/tournaments/{tournamentId}/categories/{categoryId}/draw`
- `GET /api/tournaments/{tournamentId}/categories/{categoryId}/fights`
- `POST /api/tournaments/{tournamentId}/categories/{categoryId}/swap`
- `GET /api/tournaments/{tournamentId}/categories/{categoryId}/rankings`
- `GET /api/tournaments/{tournamentId}/categories/{categoryId}/standings`

- `GET/PUT /api/tournaments/{tournamentId}/category-presets`
- `POST /api/tournaments/{tournamentId}/category-presets/reset-defaults`

- `GET /api/tournaments/{tournamentId}/tatamis/{tatamiId}/queue`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/assign-tatami`
- `POST /api/tournaments/{tournamentId}/fights/assign-tatami-bulk` (assign many fights to tatamis atomically)
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/queue-move`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/start`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/stop`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/resume`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/score/adjust`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/osae-komi/start`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/osae-komi/stop`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/osae-komi/pause`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/osae-komi/resume`
- `POST /api/tournaments/{tournamentId}/fights/{fightId}/result`
- `GET /api/tournaments/{tournamentId}/completed-fights` (Admin/Operator; enriched summaries of finished fights)
- `GET /api/tournaments/{tournamentId}/overview-stats` (authenticated; whole-tournament registered athletes, clubs, categories, fight progress, average duration and ippon count)
- `POST /api/tournaments/{tournamentId}/completed-fights/{fightId}/edit-result` (Admin; correct scores and winner with a confirmation flow for affected downstream fights)

- `GET /api/tournaments/{tournamentId}/medal-table`
- `GET /api/tournaments/{tournamentId}/club-scoring/age-groups`
- `GET /api/tournaments/{tournamentId}/club-scoring/global`
- `GET /api/tournaments/{tournamentId}/audit-log`
- `GET /api/tournaments/{tournamentId}/backup` (Admin; JSON download)
- `POST /api/tournaments/restore` (Admin; JSON restore)

- `GET /api/tournaments/{tournamentId}/team-matchday`
- `POST /api/tournaments/{tournamentId}/team-matchday/teams`
- `POST /api/tournaments/{tournamentId}/team-matchday/weight-class-order/draw` (Admin)
- `PUT /api/tournaments/{tournamentId}/team-matchday/weight-class-order` (Admin)
- `POST /api/tournaments/{tournamentId}/team-matchday/encounters`
- `DELETE /api/tournaments/{tournamentId}/team-matchday/encounters/{encounterId}`
- `GET /api/tournaments/{tournamentId}/team-matchday/encounters/{encounterId}/lineups/{legNumber}`
- `PUT /api/tournaments/{tournamentId}/team-matchday/encounters/{encounterId}/lineups`
- `POST /api/tournaments/{tournamentId}/team-matchday/encounters/{encounterId}/prepare`
- `POST /api/tournaments/{tournamentId}/team-matchday/encounters/{encounterId}/no-show`

- `GET /api/tournaments/{tournamentId}/public/athletes` (data-minimized; Admin/Operator/Display/Guest)
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
- `POST /api/auth/change-password` (authenticated user, own password only)
- `GET /api/time`
- `GET /api/auth/users`
- `POST /api/auth/users`
- `DELETE /api/auth/users/{userId}`
- `PATCH /api/auth/users/{userId}/active`
- `POST /api/auth/users/{userId}/reset-password`
- `GET /api/version` (anonymous; deployed application version)

## Frontend routes

- `/login`
- `/tournaments` (authenticated)
- `/tournament-overview` (tournament context)
- `/config`, `/registrations`, `/category-assignment`, `/draw`, `/draw/print-match-lists`, `/tatami-assignment`, `/team-matchday` (Operator/Admin)
- `/combat-overview`, `/results` (authenticated)
- `/match` (Admin/Operator/Competition during live operation)
- `/display`, `/display/tatami/{tatamiId}`, `/display/match-lists` (Display)
- `/users` (Admin)
- `/public/match-lists` (anonymous guest share token)

## Example request

Example `POST /api/tournaments` request body:

```json
{
  "name": "RWE Judo Cup",
  "date": "2026-09-12",
  "venue": "Essen",
  "organizer": "JC Essen"
}
```
