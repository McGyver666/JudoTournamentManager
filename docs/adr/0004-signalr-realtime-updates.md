# 4. SignalR for realtime fight and display updates

Status: Accepted

## Context

Operator screens, the public display, and guest match-list views must reflect fight and category
changes in near real time across multiple clients on a LAN.

## Decision

Use **SignalR** (hub at `/hubs/tournament`) for realtime broadcast of fight and category updates.
Hub access requires authentication; clients pass a bearer token. Guests join only their own
tournament group while a guest share is active.

## Consequences

- Push-based updates keep multiple clients consistent without polling.
- Timing remains **server-authoritative**; client clock sync is display-only and must not make rules
  decisions offline.
- Realtime channel is scoped and authenticated, avoiding unprotected data distribution.
