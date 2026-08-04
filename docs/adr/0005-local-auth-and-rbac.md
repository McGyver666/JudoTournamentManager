# 5. Local authentication and role-based access control

Status: Accepted

## Context

The app runs on-site without a mandatory internet connection, so it cannot depend on an external
identity provider for core operation. Access to tournament data still needs authentication and
role separation.

## Decision

Use **local authentication**: a bootstrap admin, username/password login/logout with session
persistence, **PBKDF2** password hashing, and **HMAC-SHA256** hashing of session tokens
(`Security:AuthTokenHmacSecret`). Enforce **RBAC** with roles **Admin / Operator / Display**: all
write endpoints require Admin or Operator; read endpoints require at least an authenticated role;
public/guest endpoints are explicitly scoped and data-minimized.

## Consequences

- Works fully offline; no dependency on an external IdP for tournament execution.
- The HMAC secret must be injected via configuration (never hardcoded); startup generates a random
  per-process fallback only in Development/Testing.
- If SSO is ever required, it would be an additive change, not a replacement of local auth.
