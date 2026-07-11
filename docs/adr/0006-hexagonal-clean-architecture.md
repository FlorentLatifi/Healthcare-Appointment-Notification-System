# ADR 0006: Hexagonal / Clean Architecture Layering

## Status

Accepted

## Date

2026-07-11

## Context

The system mixes **domain-rich appointment lifecycle**, **external adapters** (SQL, Redis, Stripe, email), and **HTTP presentation**. Without strict boundaries:

- Domain rules leak into controllers  
- Infrastructure concerns (EF, Redis) creep into use cases  
- Testing requires databases for pure business rules  
- Swapping notifications or payment gateways becomes expensive  

We need a structure that supports long-term evolution (outbox, MediatR, observability) without rewriting the domain.

## Decision

Adopt **Hexagonal Architecture** (ports & adapters) aligned with **Clean Architecture** dependency rule:

```
Healthcare.Domain          ← entities, value objects, domain events, domain services ports
        ▲
Healthcare.Application     ← use cases (commands/queries), ports (repos, auth, payments, locking)
        ▲
Healthcare.Adapters        ← EF Core, Redis, Stripe, email, JWT, outbox relay, caching
        ▲
Healthcare.Presentation.API← HTTP, DI composition root, middleware, health, OTel
```

### Rules

1. **Dependencies point inward.** Domain has no project references to Application/Adapters/API.  
2. **Ports** live in Application (or Domain when truly domain services, e.g. `IAppointmentCodeGenerator`).  
3. **Adapters** implement ports; Presentation only composes.  
4. **Architecture tests** (`Healthcare.ArchitectureTests` / NetArchTest) enforce forbidden references in CI.  
5. Controllers stay thin: auth + map DTO → command/query → map `Result` → HTTP.

### What stays outside pure domain

- EF entities configuration, migrations  
- JWT/Redis details  
- Stripe SDK  
- OpenTelemetry exporters  

## Consequences

### Positive

- Domain and handlers unit-testable with mocks/fakes.  
- Swap in-memory vs SQL Server vs Redis without changing use cases.  
- Clear place for each new feature (command vs adapter).  
- CI architecture tests catch layering regressions early.

### Negative / trade-offs

- More projects and interfaces than a modular monolith “feature folder” only.  
- Mapping DTOs ↔ domain adds ceremony.  
- New contributors must learn the dependency rule.  
- Pragmatic leaks (e.g. `DbUpdateException` handling in Application for concurrency) are documented exceptions, not free-for-alls.

## Alternatives considered

1. **Classic N-tier (UI → BLL → DAL)** — tends to anemic domain and bidirectional coupling.  
2. **Vertical slice only** — great for feature isolation; we still use folder slices *inside* Application while keeping global ports.  
3. **Microservices from day one** — rejected; modular monolith with clear boundaries is enough until scale forces split.

## Related code

- Solution projects: Domain, Application, Adapters, Presentation.API  
- `Healthcare.ArchitectureTests`  
- `docs/architecture-governance.md`
