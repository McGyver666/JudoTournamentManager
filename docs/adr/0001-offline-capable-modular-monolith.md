# 1. Offline-capable modular monolith on .NET 10

Status: Accepted

## Context

The app is used on-site at judo tournaments, often with unstable or no internet. It must run reliably
on a single laptop or a small LAN, and stay simple to operate and maintain by a small team.

## Decision

Build a single **modular monolith** as an ASP.NET Core Web API on **.NET 10** (`JudoTournamentManagement.Api`),
with clearly separated modules inside one deployable app. No distributed services unless a concrete
requirement forces it.

## Consequences

- Simple to run, package, and reason about; one process to start on-site.
- Modules stay in-process — cross-module calls are method calls, not network hops.
- Scaling is vertical/LAN-oriented rather than horizontal; acceptable for the target scale.
- Introducing distributed services later would be a deliberate, backlog-driven change.
