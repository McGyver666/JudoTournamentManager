# 3. Angular SPA served same-origin from the API

Status: Accepted

## Context

The product needs a rich operator/admin/display UI that works offline/LAN without a separate web
server or cross-origin setup.

## Decision

Build the frontend as an **Angular SPA** (`frontend/`), compiled into the API's `wwwroot/` and served
**same-origin** by the API (static files + SPA fallback to `index.html`). Startup scripts build the
frontend before running the API.

## Consequences

- One origin, one process: no CORS, no second server to deploy on-site.
- Deep links fall back to `index.html`; the API owns routing at `/`.
- Frontend and API versions ship together; a UI change requires rebuilding `wwwroot`.
- UI-only development can still use `ng serve` with a proxy to the running API.
