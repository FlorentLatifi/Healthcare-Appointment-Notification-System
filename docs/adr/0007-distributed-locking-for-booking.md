# ADR 0007: Distributed Locking for Appointment Booking

## Status

Accepted

## Date

2026-07-11

## Context

Booking the same doctor/time from concurrent requests (or multiple API instances) can create **double bookings** if we only check availability then insert:

```
T1: read free → T2: read free → T1: insert → T2: insert  ✗
```

We already have:

- Domain `Doctor.IsAvailable` (±30 minute conflict window)  
- Unique index on active doctor/time (DB last line of defense)  

We still need a **short critical section** so most races fail with a clean “try again / not available” instead of only raw constraint errors, and so multi-instance deploys coordinate.

## Decision

Use a **distributed lock** on the booking critical path via `IDistributedLockService`:

1. Key shape: `lock:appointment:doctor:{id}:time:{yyyyMMddHHmm}` (see `CacheKeys.AppointmentBookingLock`).  
2. Acquire with TTL (e.g. 30s) before availability re-check + insert.  
3. If lock not acquired → fail closed: *“Another booking is in progress.”*  
4. Release on dispose (Lua compare-and-del on Redis so only the owner deletes).  
5. Production: `RedisDistributedLockService`; tests/dev: `InMemoryLockService`.  
6. Keys prefixed with Redis `InstanceName` for environment isolation.

DB unique index remains **mandatory** as the final safety net if a lock expires mid-write or Redis is misconfigured.

## Consequences

### Positive

- Dramatically reduces concurrent double-book success under load.  
- Clear user-facing failure when contention exists.  
- Multi-instance safe with Redis.  
- Port allows test doubles without Redis.

### Negative / trade-offs

- Redis becomes part of the booking availability story (health + capacity).  
- Lock TTL too short → rare double attempts hit unique index; too long → slow recovery after crash (mitigated by TTL).  
- Not a substitute for correct domain availability rules.  
- In-memory lock is **not** multi-process.

## Alternatives considered

1. **DB serializable transaction only** — works; harder to get right with EF; poorer UX messages; still used as unique index backup.  
2. **Pessimistic row lock on doctor** — coarse; serializes all bookings for that doctor.  
3. **No lock, unique index only** — correct eventual reject; noisier logs and uglier errors under contention.  
4. **Single-threaded booking service** — does not scale horizontally.

## Related code

- `BookAppointmentHandler`  
- `IDistributedLockService` / `RedisDistributedLockService`  
- `CacheKeys.AppointmentBookingLock`  
- Tests: `BookAppointmentConcurrencyTests`, integration `DoubleBookingTests`
