# Architecture Decision Records (ADRs)

This folder captures **significant, long-lived technical decisions** for the Healthcare Appointment Notification System.

## Format

Each ADR follows a lightweight standard structure:

| Section | Purpose |
|---------|---------|
| **Status** | Proposed / Accepted / Deprecated / Superseded |
| **Date** | When the decision was recorded |
| **Context** | Forces, constraints, and problem statement |
| **Decision** | What we chose and the essence of how |
| **Consequences** | Positive, negative, and follow-ups |
| **Alternatives considered** | What we rejected and why |

## Index

| ID | Title | Status |
|----|--------|--------|
| [0001](./0001-remove-appointment-code-generator-singleton.md) | Remove hand-rolled Singleton from `AppointmentCodeGenerator` | Accepted |
| [0002](./0002-adopt-mediatr-cqrs-pipeline.md) | Custom CQRS **with** MediatR pipeline (incremental) | Accepted (incremental) |
| [0003](./0003-domain-events-and-transactional-outbox.md) | Domain events + transactional outbox | Accepted |
| [0004](./0004-password-hashing-argon2id.md) | Argon2id password hashing (BCrypt upgrade path) | Accepted |
| [0005](./0005-jwt-access-tokens-and-server-sessions.md) | JWT access tokens + refresh tokens + server sessions | Accepted |
| [0006](./0006-hexagonal-clean-architecture.md) | Hexagonal / Clean Architecture layering | Accepted |
| [0007](./0007-distributed-locking-for-booking.md) | Distributed locks for double-booking prevention | Accepted |
| [0008](./0008-redis-cache-and-coordination.md) | Redis for cache, coordination, and codes | Accepted |

## When to add an ADR

Write a new ADR when you:

- Introduce or replace a major library/pattern (auth, messaging, persistence style)
- Change a security boundary (sessions, hashing, secrets)
- Choose multi-instance vs single-process assumptions
- Trade correctness for latency (or the reverse) on a core flow

Do **not** ADR every refactor or package bump.

## Numbering

Use the next free zero-padded id (`0009-…`). Prefer one decision per file.
