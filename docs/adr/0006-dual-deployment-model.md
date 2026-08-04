# 6. Dual deployment: offline/LAN and internet-hosted behind nginx

Status: Accepted

## Context

The primary use case is offline/on-site, but the app is also deployed as an internet-hosted service so
it can be reached remotely. These two modes have different trust and TLS assumptions.

## Decision

Support **both** deployment models from the same codebase:

- **Offline / LAN**: run via `start-local.ps1` / `start-local.sh`, binding the LAN, with optional
  self-signed TLS for encrypted LAN access.
- **Internet-hosted**: run on a Debian/LXC server behind an **nginx reverse proxy** that terminates
  **Let's Encrypt TLS** and forwards to the API on `127.0.0.1:5080`. The public hostname is injected at
  deploy time (nginx config ships with a `__SERVER_NAME__` placeholder). See `deploy/`.

## Consequences

- The internet-hosted mode is public-facing: the trusted-LAN assumption does not hold there, so TLS is
  enforced and the network is treated as untrusted; the security posture is evaluated against a
  public-web-app checklist for that mode.
- No hostnames or secrets are hardcoded in the deployment config; they are set during deployment.
- Keep the app offline-capable — do not add hard cloud runtime dependencies for either mode.
