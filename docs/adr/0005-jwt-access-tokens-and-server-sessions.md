# ADR 0005: JWT Access Tokens + Refresh Tokens + Server Sessions

## Status

Accepted

## Date

2026-07-11

## Context

The API is a SPA-friendly HTTP backend (React frontend). Clients need:

- Stateless authorization on each API call  
- Ability to revoke sessions (“logout this device” / “logout everywhere”)  
- Protection against refresh-token theft and reuse  
- Short-lived credentials if an access token leaks  

A pure server session cookie model (sticky sessions, server RAM store only) fits less well with multi-instance APIs and a separated SPA origin. A pure long-lived JWT with no server state makes **logout** and **theft response** weak.

## Decision

### Hybrid model

```
┌─────────────┐     short-lived JWT (HS256)      ┌─────────────┐
│  SPA / client│ ───────────────────────────────► │  API        │
│              │ ◄── refresh cookie (HttpOnly) ── │  instances  │
└─────────────┘                                   └──────┬──────┘
                                                         │
                              refresh token hash ────────┤ Redis
                              family / revoke markers ───┤
                              UserSession inventory ─────┤ SQL
```

1. **Access token: JWT (HS256)**  
   - Lifetime default **15 minutes** (`Jwt:ExpirationInMinutes`).  
   - Claims: user id, role, optional `patient_id` / `doctor_id`.  
   - Validation: issuer, audience, lifetime, signed HS256 only (`JwtTokenValidation`), configurable clock skew (default 60s).  
   - Secret ≥ 32 characters; fail-fast if missing.

2. **Refresh token: opaque random, stored by hash**  
   - Value only in **HttpOnly Secure SameSite cookie** (path scoped to auth).  
   - Server stores **SHA-256 hash** in Redis (or in-memory fallback for tests).  
   - **Rotation** on each refresh; reuse of an old token **revokes the family**.  
   - Family id ties a login chain together for bulk revoke.

3. **Server-side session inventory (`UserSession`)**  
   - SQL row per login family (user agent, IP, timestamps).  
   - Powers “list my sessions”, “revoke one”, “revoke all”.  
   - Complements Redis refresh store for UX and audit.

### Explicit non-goals

- RSA/ECDSA asymmetric JWT for this deployment size (HS256 + secret management is enough; rotate secret carefully).  
- Pure opaque access tokens in Redis for every request (higher latency and Redis dependency on hot path).  
- Long-lived access JWTs (days) without refresh.

## Consequences

### Positive

- **Scalable authorize path**: any instance validates JWT without a central session lookup.  
- **Revocation path**: refresh rotation + family revoke + session table.  
- **Breach window limited**: short access TTL.  
- **SPA-friendly**: Bearer access token; cookie for refresh reduces XSS exfil of long-lived token (still need CSRF discipline on cookie endpoints).

### Negative / trade-offs

- Access JWT is valid until expiry even after logout of that device **unless** additional denylist is added (acceptable with 15m TTL).  
- Redis becomes critical for refresh; degraded mode must be explicit (tests use memory).  
- HS256 secret is a high-value secret (env/Key Vault; never in git).  
- Clock skew and claim mapping (`MapInboundClaims`) must stay consistent across Bearer middleware and manual validation.

## Alternatives considered

1. **Server sessions only (cookie + SQL/Redis session id)** — strong revoke; every request hits store; sticky affinity harder; rejected for multi-instance simplicity.  
2. **Long-lived JWT only** — no real logout; rejected for healthcare threat model.  
3. **Reference tokens (opaque access + introspection)** — excellent for revoke; extra hop every request; revisit if compliance demands immediate access kill.  
4. **OAuth2/OIDC external IdP** — preferred for enterprise SSO later; current product owns local accounts.

## Related code

- `JwtAuthenticationService`, `JwtTokenValidation`, `JwtSettings`  
- `UserSession` entity + repositories  
- `AuthController`, `SessionsController`  
- Tests: `JwtAuthenticationServiceTests`, `JwtSecurityEdgeCaseTests`  
- Reviews: `docs/security-auth-review.md`
