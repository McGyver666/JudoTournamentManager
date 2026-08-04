# 2. SQLite with EF Core migrations for persistence

Status: Accepted

## Context

Persistence must survive process restarts and be robust offline on a single machine, with no external
database server to install or maintain on-site.

## Decision

Use **SQLite** (file-based, `App_Data/judo-tournament.db`) via **EF Core**, with schema managed by
**EF Core migrations** and migration history. Legacy local databases without migration history are
adopted safely at startup.

## Consequences

- Zero-install, file-based durability that fits offline/LAN operation and backup/restore.
- Schema changes are versioned and reproducible via migrations.
- Keep the local/offline model — do not replace SQLite with a cloud-only database.
- Concurrency is handled in-process (single-writer semantics); adequate for on-site scale.
