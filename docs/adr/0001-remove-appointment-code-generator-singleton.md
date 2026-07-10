# ADR 0001: Remove Hand-Rolled Singleton from AppointmentCodeGenerator

## Status

Accepted

## Date

2026-07-11

## Context

`AppointmentCodeGenerator` was implemented as a classic double-checked-locking Singleton
with a private constructor and a static `Instance` property. Call sites (tests, DI, and
comments on the domain model) reached for that global instance directly.

That design conflicted with how the rest of the system already worked:

- The domain already depends on `IAppointmentCodeGenerator`.
- Production multi-instance deployments already use `RedisAppointmentCodeGenerator`.
- DI registered the in-memory generator by wrapping `AppointmentCodeGenerator.Instance`.

We needed a production-ready approach that is testable, swappable, and free of hidden
global state.

## Decision

Remove the hand-rolled Singleton entirely.

1. Give `AppointmentCodeGenerator` a public constructor and no static `Instance`.
2. Keep `IAppointmentCodeGenerator` as the domain port.
3. Register implementations via the DI container:
   - In-memory / single-process: `services.AddSingleton<IAppointmentCodeGenerator, AppointmentCodeGenerator>()`
   - Multi-instance production: `RedisAppointmentCodeGenerator` (unchanged)
4. Pass the generator into `Appointment.Create(...)` from the application layer (already the case).

> Note: **DI Singleton lifetime ≠ Singleton pattern**. The container may still create one
> instance per process for efficiency; the class itself is a normal, constructible service.

## Consequences

### Positive

- **Testability**: Tests construct `new AppointmentCodeGenerator()` or mock `IAppointmentCodeGenerator` without shared counter state leaking between cases.
- **Swappability**: Redis vs in-memory implementations are chosen only at composition root.
- **No hidden global state**: No static `Instance` that bypasses DI and architecture tests.
- **Clear ownership**: Code generation is an infrastructure concern injected into the domain factory, not a global ambient service.
- **Aligns with hexagonal architecture**: Domain depends on the port; adapters implement it.

### Negative / Trade-offs

- Call sites that previously used `AppointmentCodeGenerator.Instance` must receive an instance (via DI or `new` in tests). That is intentional and makes dependencies explicit.
- In-memory generator uniqueness is still **per process**. Multi-instance uniqueness remains Redis’s responsibility — the removed Singleton never solved that either.

### Why the Singleton was harmful

| Problem | Impact |
|--------|--------|
| Global mutable state | Sequence counter shared across all tests and requests; hard to isolate and reset. |
| Private constructor | Prevents legitimate construction, mocking, and alternative test doubles. |
| Bypasses DI | Encourages `Type.Instance` usage that hides dependencies and breaks layering. |
| False sense of uniqueness | Thread-safe within one process only; collisions under load balancing were still possible. |
| Violates DIP / OCP | Harder to substitute Redis, fakes, or future generators without touching domain call sites. |
| Lifecycle owned by the type | Application cannot control scope, replacement, or disposal; container cannot manage it cleanly. |

## Alternatives considered

1. **Keep Singleton, inject `Instance` only** — still global state; tests remain coupled.
2. **Stateless static utility** — fine for pure functions, but sequence generation needs state (counter) and environment (Redis); an interface + injectable service fits better.
3. **Scoped / transient DI lifetime** — possible, but a process-wide counter is more natural as container Singleton lifetime; Redis implementation is also effectively process-shared via Redis.

## Related changes

- `Healthcare.Adapters/Services/AppointmentCodeGenerator.cs` — Singleton removed
- `Healthcare.Domain/Entities/Appointment.cs` — docs updated; state machine centralized
- `AdapterServiceExtensions.cs` — DI registration uses concrete type mapping
- Unit/integration tests updated to construct the generator explicitly
