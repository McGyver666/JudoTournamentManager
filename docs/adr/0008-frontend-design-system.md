# 8. Frontend design system: dual-theme dojo tokens, self-hosted fonts, sidebar shell

Status: Accepted

## Context

The operator/admin UI needs a distinctive, cohesive visual identity (the "SHIAI" dojo design,
prototyped in `docs/design/shiai-mockup.html`) instead of the initial generic light theme. The UI is
**German-first**, runs **offline/LAN with no runtime cloud dependency** (ADR-0001), and is also
**internet-hosted** (ADR-0006). It is served same-origin from the API (ADR-0003) and is a decision-support
tool used in uncontrolled lighting (bright halls, projectors, cheap laptops), so legibility and
offline resilience outweigh pure aesthetics.

The design ships two themes (light + dark) and must let future work switch and extend them without a
large per-feature rewrite.

## Decision

- **Dual theme via CSS custom properties.** Themes are selected by a `data-theme` attribute on
  `<html>`. The dojo tokens (`--ink-*`, `--paper`, `--aka`, `--tatami`, `--ff-*`, composed
  surface/effect tokens) are the **single themed source of truth**. The legacy `--c-*` tokens are
  redefined as **aliases** onto the dojo tokens (a bridge), so existing feature CSS re-skins with no
  edits; the aliases are retired gradually, not all at once.
- **Light is the operator-app default**; dark is reserved for projected Display views. The choice is
  persisted **per-device in `localStorage`** and applied to `<html>` before first paint. No backend
  or user-profile storage.
- **Self-hosted fonts, no CDN.** The three OFL typefaces (Shippori Mincho B1, Zen Kaku Gothic New,
  Kode Mono) are vendored into the app and served locally via `@font-face`. A Google Fonts `<link>`
  is disallowed because it breaks offline operation and leaks client IPs (GDPR) in the hosted model.
- **App shell is a left sidebar** (replacing the top-nav paradigm), with the public **Display views
  remaining shell-less**.
- **Accessibility baseline: WCAG AA** for all functional text in both themes; sub-threshold contrast
  is allowed only for non-informational decoration.

## Consequences

- One attribute flips the whole UI; a small alias block gives an app-wide reskin with no feature churn.
- Fonts and themes work fully offline and add no cloud/runtime dependency; font payload is bounded by
  bundling only the weights actually used.
- The `--c-*` alias bridge is intentionally temporary — new components use dojo tokens directly, and
  legacy names are removed feature-by-feature.
- Retiring the CDN font path must not regress; any reintroduction of remote fonts is a violation of
  the offline/GDPR intent.
- Tactical concerns (motion policy, kanji-as-decoration rules, per-screen restyling, sequencing) are
  deliberately **not** fixed here — they live in the tracking issues and may evolve.
