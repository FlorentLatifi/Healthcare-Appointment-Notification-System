# ADR 0008: Redis for Cache, Coordination, and Ephemeral Auth State

## Status

Accepted

## Date

2026-07-11

## Context

Multiple runtime concerns need a shared, low-latency store across API instances:

| Concern | Requirement |
|---------|-------------|
| Doctor catalog / schedule / day availability | Cache-aside reads, invalidation on writes |
| Booking locks | Distributed mutual exclusion |
| Appointment reference codes | Atomic multi-instance counters |
| Refresh tokens / families | Ephemeral, TTL, atomic consume |

SQL Server is the **system of record** for appointments, payments, users, sessions inventory, and outbox. Putting everything in SQL would increase latency and lock contention for hot read paths and short-lived tokens.

## Decision

### Use Redis as a **coordination and cache** plane, not the system of record

1. **Cache-aside** (`ICacheService` / `IDoctorCacheService` / `IAvailabilityCacheService`)  
   - Keys versioned (`cache:v1:…`) with generation counters for list invalidation.  
   - Stampede protection via short locks.  
   - Short TTLs for availability; longer for schedule.  
   - Invalidate on domain events (doctor changes, appointment lifecycle).

2. **Distributed locks** — see ADR 0007.

3. **Appointment codes** — `RedisAppointmentCodeGenerator` (`INCR` per day) in multi-instance production; in-memory generator only for single-process/tests (ADR 0001).

4. **Auth refresh material** — hashed refresh tokens and family revoke markers in Redis with TTL; SQL `UserSession` remains inventory (ADR 0005).

5. **Configuration** — connection string + `InstanceName` prefix for env isolation (`HealthcareApp:Prod:` vs staging).

### Explicit non-uses

- Do not store primary clinical entities only in Redis.  
- Do not treat cache as authoritative for booking conflicts (always re-check DB under lock).  
- Do not put PHI in cache keys or values beyond what the API already exposes in DTOs under auth.

## Consequences

### Positive

- Lower read latency for doctor browse / availability UIs.  
- Correct multi-instance behavior for locks and codes.  
- Natural TTL for refresh tokens.  
- Clear failure modes: cache miss → DB; Redis down → degrade per component (auth may fail closed on refresh store).

### Negative / trade-offs

- Operational dependency: Redis health checks, memory, persistence settings.  
- Cache invalidation bugs → stale UI (mitigated by TTL + event invalidation).  
- Two stores to reason about (SQL + Redis).  
- Local dev needs Redis or in-memory fallbacks.

## Alternatives considered

1. **SQL only** — simpler ops; worse hot-path latency and lock noise.  
2. **NCache / Memcached** — Redis wins on ecosystem (locks, INCR, TTL, StackExchange.Redis).  
3. **IDistributedCache abstraction only** — fine for simple cache; we need locks and Lua scripts, so direct Redis adapter is justified.  
4. **Sticky in-process memory cache only** — incorrect with multiple instances.

## Related code

- `Healthcare.Adapters/Caching/*`  
- `Healthcare.Adapters/Locking/*`  
- `RedisAppointmentCodeGenerator`  
- `JwtAuthenticationService` Redis paths  
- `docs/secrets-management.md` (Redis network not public in prod compose)  
- Tests: `DoctorCacheTests`, Redis generator tests, JWT Redis exception tests
