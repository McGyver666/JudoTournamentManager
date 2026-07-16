# Project Guidelines

## Product Context
- This project builds an offline-first judo tournament management application.
- Primary usage is on-site at tournaments with unstable or no internet.
- German is the primary product language.
- The system must stay localizable from the start.

## Architecture
- Keep the architecture simple: modular monolith first.
- Backend is ASP.NET Core Web API on .NET 10.
- Target persistence is SQLite for offline durability.
- Prefer local/LAN workflows over cloud-dependent solutions.
- Do not introduce distributed services unless the backlog explicitly requires them.

## Build and Test
- Use the .NET 10 SDK available on `PATH`.
- Build with: `dotnet build .\JudoTournamentManagement.sln`
- Run tests with: `dotnet test .\JudoTournamentManagement.sln --filter Category=UnitTest`
- Start locally with: `.\start-local.ps1`

## Implementation Conventions
- Keep visible product text German by default.
- New UI and API-facing labels must be localization-ready.
- Follow the MVP backlog in `backlog.md`.
- Prefer small, end-to-end slices over speculative broad scaffolding.
- Update `backlog.md` when implementation status changes materially.
- Keep `README.md` (English) and `README.de.md` (German) current whenever setup, architecture, developer workflow, APIs, or operational behavior changes.
- Keep both README versions consistent: synchronize their structure, commands, endpoints, technical facts, and bidirectional language links while preserving the language of each document.

## .NET Conventions
- Use async APIs with `CancellationToken` for I/O-bound work.
- Keep XML doc comments on public members.
- Use `ProblemDetails` or validation responses for API errors.
- Add unit tests for new behavior and tag each new test with `Category=UnitTest`.
- Do not replace the local/offline model with cloud-only dependencies.

## Current State Notes
- Tournament CRUD is implemented.
- Current persistence is in-memory and must be migrated to SQLite next.
- Authentication, audit logging, tatami management, categories, athletes, and brackets are still pending backlog work.

# LLM Behavioral Guidelines

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.