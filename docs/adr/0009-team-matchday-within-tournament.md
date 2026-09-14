# 9. Team matchdays are a tournament mode

Status: Accepted

## Context

The app is adding NWJV league support incrementally. The first deliverable is a single local
matchday with configurable team encounters, not a league season, table, or automated fixture list.
The existing tournament aggregate already owns local persistence, backup and restore, RBAC, audit
logging, tatamis, and the individual-fight lifecycle.

## Decision

Add `TeamMatchday` as an explicit competition mode on the existing tournament aggregate. A tournament
in this mode represents exactly one local team matchday, including its teams, matchday weigh-ins,
encounters, encounter bouts, and configured NWJV rule profile.

Individual tournaments retain their current behavior. League-season management, league tables,
automated fixture generation, promotion, and relegation remain separate future capabilities.

## Consequences

- Team encounter bouts can reuse the established tatami queue and match-control lifecycle.
- Existing local/offline deployment, backup/restore, RBAC, and audit behavior remains shared.
- The model remains bounded to one event, avoiding premature season-management aggregates.
- A later league-season feature will compose or reference completed matchdays instead of redefining
  individual fight execution.