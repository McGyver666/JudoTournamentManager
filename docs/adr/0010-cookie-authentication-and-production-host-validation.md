# 10. Cookie authentication and production host validation

Status: Accepted

## Context

Operator session tokens must not be readable from browser storage, because an XSS payload could
reuse a token stored in `localStorage`. The application also builds guest-share URLs from the
incoming request host when no public base URL is configured. A forged host header could therefore
produce a guest URL pointing at an attacker-controlled host.

The application has two supported deployment modes: offline/LAN operation, where arbitrary local
hostnames and IP addresses must continue to work, and internet-hosted production behind nginx.

## Decision

- Store the operator session in the existing server-side session table and expose it to the browser
  as the `shiai_auth` cookie. The cookie is `HttpOnly`, `SameSite=Strict`, scoped to `/`, expires
  with the server session, and uses `CookieSecurePolicy.SameAsRequest` so plain HTTP LAN operation
  remains supported.
- Resolve operator credentials from the bearer header first and the cookie second. Keep the
  `access_token` query path only for the guest/SignalR flow; guest-share tokens remain unchanged.
- Require `X-Requested-With: ShiaiManager` on authenticated cookie-based state-changing requests.
  Safe methods and requests authenticated with an explicit bearer header are not subject to this
  cookie CSRF check.
- Keep `AllowedHosts: "*"` in the base configuration for offline/LAN use. Production injects the
  deployed hostname through `appsettings.Production.json` and the existing environment file.
- Configure `GuestShare:PublicBaseUrl` in production and reject unmatched hosts at the nginx default
  server, so production guest links do not depend on the incoming host header.

## Consequences

- XSS cannot directly read the operator session token, and the cookie is sent automatically after
  reloads or browser restarts within the session lifetime.
- Same-origin state changes require an application-controlled header, reducing the CSRF risk of
  ambient cookie authentication.
- Plain HTTP remains possible for offline/LAN deployments; those deployments must rely on the
  existing network and host security model because the cookie is not marked `Secure` on HTTP.
- Production deployments must keep the nginx hostname, `AllowedHosts`, and
  `GuestShare:PublicBaseUrl` aligned. The host-filtering integration test covers rejection of a
  non-allowlisted host.