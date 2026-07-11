# ADR 0002: Adopt MediatR for CQRS pipeline behaviors

## Status

Accepted (incremental migration)

## Date

2026-07-11

## Context

The Application layer uses a lightweight custom CQRS surface:

- `ICommand` / `ICommandHandler<TCommand, TResponse>`
- `IQuery` / `IQueryHandler<TQuery, TResponse>`
- Manual `services.AddScoped<ICommandHandler<…>, …>()` lines in `Program.cs`
- Controllers inject many typed handlers
- Cross-cutting concerns (validation, logging, performance, transactions) are either missing or duplicated

FluentValidation already exists at the **API request** layer. Application-level command validation and uniform observability are not centralized.

## Decision

**Adopt MediatR** for the Application use-case pipeline, while **keeping** the custom `ICommand` / `IQuery` marker interfaces during migration for dual-registration compatibility.

### Why MediatR (vs only improving the custom stack)

| Factor | Custom-only | MediatR |
|--------|-------------|---------|
| Pipeline behaviors | Must build/maintain ourselves | First-class `IPipelineBehavior` |
| Handler registration | Manual, error-prone | Assembly scan |
| Controller dependencies | N handler interfaces | Single `IMediator` |
| Industry familiarity | Low | High for .NET CQRS |
| Extra dependency | None | One package (Apache-2.0) |

A pure custom `IDispatcher` + decorators could work and is pedagogically pure. For this codebase size and the need for shared behaviors **now**, MediatR is the better ROI.

### What we are *not* doing

- Not removing Clean Architecture ports/adapters
- Not replacing domain events with MediatR notifications (domain events stay on `IDomainEventDispatcher` / outbox)
- Not big-bang rewriting every handler in one PR

## Consequences

### Positive

- Shared **Validation / Logging / Performance / Transaction** pipeline
- Controllers shrink to `IMediator.Send`
- New handlers auto-register when they implement `IRequestHandler`
- Clear incremental migration path

### Negative / trade-offs

- Application references MediatR (acceptable dependency)
- Two dispatch styles coexist until migration completes
- Teams must understand pipeline order
- Unit tests that construct controllers must mock `IMediator`

## Pipeline (outer → inner)

1. `LoggingBehavior` — request start/end + errors  
2. `PerformanceBehavior` — warn if &gt; 500ms  
3. `ValidationBehavior` — FluentValidation for the request type  
4. `TransactionBehavior` — only if `ITransactionalRequest`  
5. Handler  

## Migration plan (detailed)

### Phase 0 — Foundation (done in this change)

- [x] Add MediatR + FluentValidation DI extensions  
- [x] Implement four behaviors  
- [x] `AddApplication()` extension  
- [x] Pilot: `BookAppointment`, `ConfirmAppointment`, `GetAppointment`  
- [x] `AppointmentsController` uses `IMediator` for pilot requests  
- [x] Map `ValidationException` → HTTP 400  

### Phase 1 — Remaining appointment commands

- Cancel / Complete / MarkNoShow → `IRequest` + `ITransactionalRequest`  
- Switch controller methods to `_mediator.Send`  
- Remove legacy `ICommandHandler<>` registrations for those types  

### Phase 2 — Patients, doctors, payments, auth commands

- Same dual-interface pattern  
- Prefer Application validators for command invariants; keep API validators for HTTP DTO shape  

### Phase 3 — Queries / analytics

- All `IQueryHandler` → `IRequestHandler`  
- No `ITransactionalRequest` on pure reads  

### Phase 4 — Cleanup

- Delete unused `ICommandHandler` / `IQueryHandler` interfaces **or** keep as aliases  
- Remove dual `HandleAsync` if only `Handle` remains  
- Controllers depend only on `IMediator` (+ repos for pure read endpoints not yet queries)  
- Architecture test: Application may depend on MediatR; still not on Adapters  

### Phase 5 — Hardening (optional)

- OpenTelemetry activity in `LoggingBehavior` / `PerformanceBehavior`  
- `ITransactionalRequest` only when multi-save is required; simple single-`SaveChanges` handlers may skip it  
- Consider MediatR exception handler vs middleware for `ValidationException` (already middleware)  

## Handler migration recipe (per use case)

1. Command/query implements `IRequest<TResponse>` (and keep `ICommand`/`IQuery` until Phase 4).  
2. Commands that need atomic multi-step writes implement `ITransactionalRequest`.  
3. Handler implements `IRequestHandler<TRequest, TResponse>` (`Handle` delegates to existing `HandleAsync` during transition).  
4. Add `AbstractValidator<TRequest>` when input rules belong in Application.  
5. Call site: `_mediator.Send(request, ct)`.  
6. Drop manual `AddScoped<ICommandHandler<…>>` for that type.  

## Long-term maintainability benefits

1. **Single place for cross-cutting policy** — new concerns (authorization filters, idempotency keys, multi-tenancy) become one behavior, not N handler edits.  
2. **Discoverability** — `Commands/` + `Queries/` folders + auto-registration beat a growing `Program.cs`.  
3. **Testability** — handlers stay pure unit tests via `HandleAsync`/`Handle`; pipeline tested separately with a real `IMediator` in integration tests.  
4. **Controller thinness** — auth + map DTO → command + `Send` + map `Result` → HTTP.  
5. **Consistency** — every use case gets the same logging/validation shape, reducing production “why didn’t this log?” gaps.  
6. **Evolution** — if MediatR is ever replaced, the behavior interfaces map cleanly to a custom dispatcher with the same shape.  

## Alternatives considered

1. **Keep custom handlers only** — add manual decorator registration. Rejected: higher maintenance for equivalent pipeline.  
2. **Custom `IDispatcher` + pipeline** — good for zero third-party deps; more code to own. Revisit only if licensing/dependency policy forbids MediatR.  
3. **Full MediatR notifications for domain events** — rejected; outbox + domain events already solve multi-handler side effects at the right boundary.  
