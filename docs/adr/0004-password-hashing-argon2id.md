# ADR 0004: Password Hashing with Argon2id

## Status

Accepted

## Date

2026-07-11

## Context

User passwords for the Healthcare API must resist offline cracking if the hash store is leaked. Healthcare and OWASP guidance strongly favor modern memory-hard algorithms over older designs.

Constraints:

- Passwords are verified on the interactive login path (latency budget ~ tens–hundreds of ms).  
- Existing or test data may still use **BCrypt** hashes.  
- Implementation lives behind `IPasswordHasher` (port) so the domain/application never depend on a concrete library.

## Decision

### Primary algorithm: **Argon2id**

Use `Argon2IdPasswordHasher` as the registered `IPasswordHasher` in production DI.

**Parameters** (OWASP 2023 interactive “second recommendation” style):

| Parameter | Value | Notes |
|-----------|--------|--------|
| Variant | Argon2id | Hybrid side-channel + GPU resistance |
| Memory | 19456 KiB (~19 MiB) | Memory-hard |
| Iterations | 2 | Time cost |
| Parallelism | 1 | Predictable CPU on API nodes |
| Salt | 16 random bytes | Per-password |
| Output | 32 bytes | Stored with PHC-like string format |

Hash format example:

```text
$argon2id$v=19$m=19456,t=2,p=1$<salt-b64>$<hash-b64>
```

### Legacy: **BCrypt verify + transparent upgrade**

- `VerifyPassword` accepts BCrypt hashes (via BCrypt.Net).  
- `RequiresRehash` returns true for non-Argon2id (or drifted parameters).  
- On successful login, rehash and store Argon2id so the fleet migrates without a bulk downtime job.

### What we do **not** use for new passwords

- Plain MD5/SHA-* alone  
- PBKDF2 as primary (acceptable legacy, inferior memory hardness vs Argon2id)  
- scrypt as primary (Argon2id preferred by current OWASP guidance)

## Consequences

### Positive

- Strong offline-attack resistance relative to BCrypt/PBKDF2 at similar interactive cost.  
- Seamless migration from BCrypt.  
- Port/adapter boundary keeps crypto out of domain entities.  
- Parameters encoded in the hash string enable future policy bumps via `RequiresRehash`.

### Negative / trade-offs

- Higher memory use per verify (size API pods accordingly; avoid unbounded login concurrency without rate limits).  
- Parameter tuning may need revisit if hardware or OWASP recommendations change.  
- Argon2id is slower than BCrypt on some low-end hardware — acceptable for security.

## Alternatives considered

1. **BCrypt only** — still common; weaker against heavily parallel GPU/ASIC offline attacks; rejected as primary.  
2. **PBKDF2-HMAC-SHA256** — FIPS-friendly; less memory-hard; keep only if compliance forces it.  
3. **scrypt** — solid; Argon2id is the newer PHC winner and current OWASP preference.  
4. **External IdP only (no local passwords)** — desirable long-term for enterprise SSO; not the current product scope.

## Related code

- `Healthcare.Adapters/Authentication/Argon2IdPasswordHasher.cs`  
- `Healthcare.Adapters/Authentication/BcryptPasswordHasher.cs` (legacy/alternate)  
- `IPasswordHasher` port  
- Registration in `AdapterServiceExtensions`  
- Auth review notes: `docs/security-auth-review.md`
