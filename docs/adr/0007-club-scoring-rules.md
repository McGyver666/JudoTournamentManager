# 7. Club scoring (Vereinswertung) rules

Status: Accepted

## Context

Beyond individual rankings and the medal table, organizers need a transparent **team** ranking per age
group and across the whole tournament. The exact scoring is a business rule that must be reproducible
and hard to change silently (result of a domain grilling).

## Decision

Compute **Vereinswertung** with these rules:

- Placement points: 1st = 7, 2nd = 5, 3rd = 3. **Two third places** each count separately with 3 points.
- Age-group end score = base points × age-group **win ratio** (Siegquote).
- Global end score = global base points × global win ratio over the whole tournament.
- Win ratio = won fights / contested fights. A **bye (Freilos) is the only contest-less outcome** and is excluded from the denominator; every other decided fight counts. Denominator 0 ⇒ ratio `0.0`.
- Internal calculation at full precision; display rounded to 2 decimals.
- Ranking uses the **unrounded** value; equality within tolerance `1e-9`.
- Tie-break order: end score (unrounded), win ratio, count of 1st, 2nd, 3rd, else shared rank.
- Competition-style ranks on ties: `1, 2, 2, 4`.
- Athletes without a club are grouped under **Ohne Verein**.
- Visibility is exhaustive: the age-group ranking lists **all clubs registered in that age group**, the
  global ranking lists **all clubs registered in the tournament**. Clubs with no points still appear
  (score `0.00`); the row set never depends on who scored.
- Status label: `Final` once all planned fights (of the age group, or all age groups for global) are
  finished; otherwise `Vorläufig`.

## Consequences

- Scoring is deterministic and testable; changes to constants/tie-breaks must update tests and this ADR.
- Implemented in `RankingService` with `AgeGroupClubScoringResponse` / `GlobalClubScoringResponse` /
  `ClubScoringEntry` DTOs (raw + display values, podium counts).
