# Authentication & JWT security review

Date: 2026-07-11  
Scope: JWT access tokens, refresh-token rotation, logout/session revocation, password hashing.

## Summary

| Area | Status after fixes |
|------|--------------------|
| Access-token validation (iss/aud/exp/alg) | Hardened |
| Refresh rotation + reuse detection | Strong (fixed Redis TTL bug) |
| Logout / family revoke | Fixed (was incomplete) |
| Password hashing | Argon2id primary + BCrypt upgrade path |
| Common JWT attacks | Mitigated (alg allow-list, short skew, signed-only) |

---

## Findings (before → after)

### CRITICAL

#### C1. Redis refresh-token Lua used wrong EX argument
**Was:** `redis.call('SET', KEYS[2], val, 'EX', KEYS[3])` — TTL was in `ARGV`, so consumed markers may not expire correctly under Redis.  
**Fix:** `EX tonumber(ARGV[1])` with TTL seconds in `RedisValue[]`.

#### C2. Logout did not revoke refresh **family** and wiped **all** DB sessions
**Was:** `RevokeTokenAsync` only deleted one hash; logout then revoked every `UserSession` for the user (other devices’ cookies still valid in Redis).  
**Fix:** `RevokeTokenAsync` peeks token → deletes → **revokes family**; logout revokes only matching `UserSession` by `FamilyId`. Multi-device logout remains `DELETE /api/v1/sessions`.

### HIGH

#### H1. JWT validation incomplete in middleware & `ValidateTokenAsync`
**Was:** No `ValidAlgorithms`, `RequireSignedTokens`, explicit clock skew, or algorithm check on validated token.  
**Fix:** Shared `JwtTokenValidation.CreateParameters` — HS256 only, `RequireSignedTokens`, `RequireExpirationTime`, configurable skew (default 60s). Manual validation no longer throws (returns `Result.Failure`).

#### H2. Access-token lifetime default 60 minutes
**Was:** Long-lived bearer tokens for a healthcare API.  
**Fix:** Default **15 minutes** (`Jwt:ExpirationInMinutes`); refresh cookie remains longer-lived.

#### H3. In-memory refresh store key inconsistency
**Was:** Active tokens stored as raw hash without prefix; Redis used prefixes.  
**Fix:** Always use `refresh_token:{hash}` (and related prefixes).

### MEDIUM

#### M1. Password policy (register) min 8, no special char
**Fix:** Min **12**, require special character (still check HIBP on register).

#### M2. Argon2 `RequiresRehash` only checked prefix
**Fix:** Also rehash when `m/t/p` params differ from current policy.

#### M3. No access-token denylist
**Accepted residual risk** for short-lived JWTs. Prefer short TTL + refresh rotation. Optional future: jti denylist in Redis for emergency revoke.

### LOW / residual

| Item | Notes |
|------|--------|
| Username timing oracle | Dummy Argon2 verify on missing user is a future hardening step |
| Refresh not bound to IP/UA | Optional signal; care with mobile networks |
| Symmetric HS256 | Use strong ≥32-char secret from vault; consider RSA/ECDSA if multi-service validation |
| Cookie `Secure=true` | Correct for HTTPS; local HTTP needs TLS or dev-only cookie flags |

---

## Architecture (current)

```
Access JWT (HS256, ~15m)  ──► API [Authorize] via JwtBearer
Refresh (opaque, 32 bytes, SHA-256 hashed in Redis)
   │  rotation: single-use + family id
   │  reuse of old token → revoke entire family
   └── HttpOnly Secure SameSite=Strict cookie Path=/api/v1/auth

Passwords: Argon2id (OWASP interactive) + BCrypt verify/upgrade on login
```

---

## Healthcare best practices checklist

1. **Short access tokens** (5–15 min) — done (default 15).  
2. **Refresh rotation + reuse detection** — done.  
3. **Server-side session inventory** (`UserSession`) for “logout everywhere” — done.  
4. **No PHI in JWT claims** — only ids/role/username/email; avoid clinical data in tokens.  
5. **Secrets from vault / env**, never repo — JWT secret fail-fast.  
6. **Rate-limit auth endpoints** — `AuthPolicy` present.  
7. **Breach password check** — HaveIBeenPwned on register.  
8. **Audit login/logout/refresh failures** — logging present; consider immutable audit store for compliance.  
9. **TLS everywhere** in production; `RequireHttpsMetadata` when not Development.  
10. **MFA** for Admin/Doctor — recommended next step (not implemented).

---

## Config keys

| Key | Recommended |
|-----|-------------|
| `Jwt:Secret` | ≥32 cryptographically random chars |
| `Jwt:ExpirationInMinutes` | `15` (or `5`–`10` if UX allows) |
| `Jwt:ClockSkewSeconds` | `60` |
| `Jwt:RefreshTokenExpirationInDays` | `7` (or less) |
| `Jwt:Issuer` / `Audience` | Explicit, unique per environment |

---

## Tests to run

```bash
dotnet test Healthcare.UnitTests --filter FullyQualifiedName~JwtAuthenticationServiceTests
dotnet test Healthcare.UnitTests --filter FullyQualifiedName~Argon2
```
