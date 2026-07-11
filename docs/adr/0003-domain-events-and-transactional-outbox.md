# ADR 0003: Domain Events + Transactional Outbox

## Status

Accepted

## Date

2026-07-11

## Context

Appointment, payment, and doctor lifecycle changes must trigger **side effects** outside the aggregate:

- Email / notification adapters  
- Audit / logging handlers  
- Cache invalidation  
- Future integrations (billing, analytics)

Naive approaches fail under production constraints:

| Approach | Failure mode |
|----------|----------------|
| Call adapters **inside** the command handler | Partial failure: DB committed, email not sent (or reverse) |
| Publish to a broker **before** commit | Message for rolled-back work |
| Publish **after** commit without durability | Process crash → lost event |
| MediatR `INotification` only (in-process) | Same process; no multi-instance replay; lost on crash after commit |

We need **at-least-once** delivery of domain events that is **atomic with business state** on SQL Server, and works with multiple API instances.

## Decision

### 1. Domain events live on aggregates

- Aggregates raise `IDomainEvent` instances (e.g. `AppointmentConfirmedEvent`).  
- Application handlers dispatch via `IDomainEventDispatcher` (port).  
- **Do not** use MediatR notifications as the domain-event bus (see ADR 0002). Domain events are a **domain** concept; MediatR is an **application** pipeline for use cases.

### 2. Transactional outbox when enabled

When `UseOutboxForDomainEvents = true` (production default):

1. On `SaveChangesAsync`, domain events are written to `OutboxMessages` **in the same EF transaction** as entity changes.  
2. `MessageId` = domain `EventId` with a **unique index** (idempotent insert).  
3. A background `OutboxRelayService` claims rows (`Pending` → `Processing`), deserializes, and dispatches with **strict** handler semantics.  
4. Success → `Processed`; failure → exponential backoff + jitter; max retries / non-retryable → `DeadLetter`.  
5. Stale `Processing` claims are reclaimed after a lease timeout.

When outbox is **off** (typical local Development): events can still be dispatched in-process after save for faster feedback (toggle via config).

### 3. Relay resilience

- Optimistic claim (`ExecuteUpdate` Pending→Processing) for multi-instance safety  
- Polly retry + circuit breaker around **batch** infrastructure failures  
- Metrics (`Healthcare.Outbox`) and health checks for ops  

Handlers that run from the outbox **must be idempotent** on `EventId` (at-least-once delivery).

## Consequences

### Positive

- **Atomicity**: business row + outbox row commit or roll back together.  
- **Durability**: crash after commit still leaves work for the relay.  
- **Decoupling**: aggregates do not know about email/Stripe/cache.  
- **Operability**: dead-letter queue, metrics, lease recovery, multi-instance claim.  
- **Clear boundary**: domain events ≠ application MediatR pipeline.

### Negative / trade-offs

- **Latency**: side effects are eventually consistent (seconds, depending on poll interval).  
- **Complexity**: status machine, backoff, DLQ ops procedures.  
- **Duplicate delivery risk**: at-least-once requires idempotent handlers.  
- **Type serialization**: events use assembly-qualified names + JSON; renames need care.

## Alternatives considered

1. **Immediate dispatcher only** — simplest; rejected for production reliability.  
2. **Broker-first (RabbitMQ/Kafka) without outbox** — dual-write problem remains.  
3. **Outbox + CDC (Debezium)** — excellent at scale; heavier ops than a small Compose/VM team needs now.  
4. **MediatR `INotification` as the only bus** — in-process only; no durable multi-instance replay; confuses use-case pipeline with domain facts.  
5. **Saga/process manager for every side effect** — overkill until multi-step distributed workflows dominate.

## Related code

- `HealthcareDbContext.SaveChangesAsync` — outbox row creation  
- `OutboxMessage` / `OutboxRelayService` / `OutboxSettings`  
- `IDomainEventDispatcher` / `DomainEventDispatcher` (`DispatchStrictAsync` for relay)  
- Domain `Healthcare.Domain/Events/*`  
- Tests: `OutboxPatternTests`, `OutboxRelayFailureAndRetryTests`
