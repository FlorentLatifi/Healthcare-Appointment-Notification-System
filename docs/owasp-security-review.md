# OWASP Top 10 & healthcare data protection review

Date: 2026-07-11  
System: Healthcare Appointment Notification System

## Executive summary

The stack already has solid foundations (JWT hardening, Argon2id, refresh rotation, admin-only audit API, seeded-credential removal, architecture tests). This review maps residual risks to **OWASP Top 10 (2021)** and healthcare expectations (HIPAA-aligned technical safeguards), then lists prioritized fixes—**highest items implemented in code**.

---

## OWASP Top 10 mapping

| OWASP | Risk in this system | Severity | Status |
|-------|---------------------|----------|--------|
| **A01 Broken Access Control** | Role checks on controllers; patient/doctor ownership on appointments | Medium residual (IDOR tests exist) | Monitor |
| **A02 Cryptographic Failures** | JWT HS256 + Argon2id; secrets via env | Low after prior work | Secrets empty in appsettings ✅ |
| **A03 Injection** | EF Core parameterized; FluentValidation | Low–Med | Validators depth improved |
| **A04 Insecure Design** | CQRS + ports; session inventory | Low | — |
| **A05 Security Misconfiguration** | CORS, headers, Swagger, proxies | **High → fixed** | Headers/CORS/HSTS/proxy |
| **A06 Vulnerable Components** | CI vulnerability scan | Process | Keep Dependabot |
| **A07 Auth Failures** | Rate limit, refresh family, password policy | **High → improved** | Auth audit + rate limit |
| **A08 Software/Data Integrity** | Outbox; Stripe webhook signature | Low | Webhook secret required |
| **A09 Logging/Monitoring Failures** | Domain audit for appointments; weak auth audit | **High → fixed** | Login/logout audit |
| **A10 SSRF** | Limited outbound (HIBP, Stripe, email) | Low | — |

---

## Focus-area findings

### 1. Rate limiting (behind proxies)

| Finding | Priority |
|---------|----------|
| Partition was raw `RemoteIpAddress` only — behind reverse proxy all clients share one bucket unless `UseForwardedHeaders` + trusted networks | **P0** |
| Authenticated users behind NAT still shared IP buckets | **P1** |
| No `Retry-After` on 429 | **P1** |
| Auth endpoints only IP-based (correct) but weak if proxy spoofing allowed | **P0** |

**Implemented:** `ClientIpResolver` (user id when authenticated, else IP); `ForwardLimit = 1`; AuthPolicy IP partition; `Retry-After` header.

### 2. Input validation & sanitization

| Finding | Priority |
|---------|----------|
| FluentValidation + `ValidationFilter` on controllers | Good |
| Application command validators only for MediatR pilots | **P2** |
| Reset password min length lagged register (8 vs 12) | **P1 fixed** |
| Free-text `Reason` / doctor notes — length limited; no HTML (JSON API) | OK |
| Max request body unbounded (DoS) | **P1 fixed** (1 MiB) |

### 3. Audit logging quality

| Finding | Priority |
|---------|----------|
| Appointment/payment domain events → `AuditLogEntry` | Good |
| Login/logout not in durable audit store | **P0 fixed** |
| `UserId` often null on domain event audits | **P2** |
| Audit log not append-only at DB level (no delete endpoint for non-admin; repo has no Delete) | OK-ish |
| No immutable WORM store / SIEM export | **P2** |

**Implemented:** `SecurityAuditWriter` + login success/failure + logout events (no passwords/tokens).

### 4. Secrets handling

| Finding | Priority |
|---------|----------|
| Empty secrets in committed appsettings | Good |
| JWT fail-fast if secret missing/short | Good |
| Bootstrap admin password denylist | Good |
| Stripe webhook secret empty → webhook verify fails open/closed? | Verify fail closed |
| Connection strings in compose env | OK with secrets |

### 5. CORS, security headers, HTTPS

| Finding | Priority |
|---------|----------|
| CORS required in Production | Good |
| `AllowAnyMethod` / `AllowAnyHeader` too broad with credentials | **P1 fixed** |
| CSP/`X-Frame-Options` present; headers set before body may be fragile | **P1 fixed** (`OnStarting`) |
| HSTS only if request already HTTPS in middleware | **P1 fixed** (`UseHsts`) |
| Swagger disabled outside Development | Good |
| `AllowedHosts: *` | **P2** tighten per env |

### 6. Sensitive health data (PHI)

| Finding | Priority |
|---------|----------|
| Role + ownership checks on patient/appointment reads | Good |
| Patient list/search for doctors | Need continuous authz tests |
| Audit of patient record access | Partial (explicit events) |
| API `Cache-Control: no-store` | **P1 fixed** |
| Encryption at rest | **Infra** (SQL TDE / disk) |
| Field-level encryption for notes/DOB | **P2** product decision |
| Logs may contain patient/doctor IDs | Acceptable; avoid free-text clinical notes in logs |

---

## Prioritized improvements

### P0 — Do now (implemented)

1. **Proxy-aware, identity-aware rate limiting**  
2. **Auth security audit trail** (login fail/success, logout)  
3. **Request body size caps**  
4. **Security headers + HSTS + no-store cache**  
5. **Restrict CORS methods/headers**  

### P1 — Next sprint

6. Extend MediatR validators to all commands  
7. Populate `AuditLogEntry.UserId` from ambient `ICurrentUser` in domain event handlers  
8. Fail Production startup if `TrustedNetworks`/`TrustedProxies` empty **and** `RateLimiting:RequireTrustedProxyConfig=true`  
9. MFA for Admin/Doctor  
10. Stripe webhook secret required when webhooks enabled  

### P2 — Backlog

11. Field encryption for clinical notes  
12. SIEM export / immutable audit  
13. Dummy password hash on unknown username (timing)  
14. `AllowedHosts` per environment  
15. CSP reporting endpoint  

---

## Code examples (highest priority — in repo)

### Rate-limit partition key

```csharp
// ClientIpResolver.GetRateLimitPartitionKey
var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
if (!string.IsNullOrWhiteSpace(userId))
    return $"user:{userId}";
return $"ip:{GetClientIp(httpContext)}"; // after UseForwardedHeaders
```

### Auth audit (never log secrets)

```csharp
await _securityAudit.WriteAsync(
    "LoginFailed", "User", null, null,
    new { username, reason = "invalid_credentials_or_inactive", ip, userAgent },
    ct);
```

### Security headers (API)

```csharp
Cache-Control: no-store
X-Frame-Options: DENY
Content-Security-Policy: default-src 'none'; frame-ancestors 'none'
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload  // via UseHsts
```

### Request size

```csharp
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1_048_576);
```

---

## Healthcare technical safeguards (alignment)

| Control | Practice |
|---------|----------|
| Access control | Role + ownership; least privilege Admin |
| Audit controls | Domain + security audit events |
| Integrity | Outbox, parameterized queries |
| Transmission | HTTPS redirect + HSTS (prod) |
| Authentication | JWT 15m + refresh rotation + Argon2id |
| Session | Server-side session list + family revoke |

**Not software-only:** BAA with cloud/DB vendors, encryption at rest, backup encryption, workforce training, incident response.

---

## Verify

```bash
dotnet build Healthcare.AppointmentSystem.sln -c Release
dotnet test Healthcare.UnitTests --filter FullyQualifiedName~RateLimit
dotnet test Healthcare.ArchitectureTests
```
