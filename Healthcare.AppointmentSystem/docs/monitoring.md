# Monitoring, Observability & Alerting

## OpenTelemetry stack

| Signal | Instrumentation | Export |
|--------|-----------------|--------|
| **Traces** | ASP.NET Core, HttpClient, EF Core, MediatR (`Healthcare.Application`) | OTLP when `Otel:Endpoint` set; else console (Dev) |
| **Metrics** | ASP.NET, HttpClient, Runtime, Process, Business, Outbox, Reminders | OTLP / console |
| **Logs** | Serilog compact JSON → console + rolling file; OTLP logs when endpoint set | OTLP / file |

### Configuration (`appsettings` / env)

```json
"Otel": {
  "ServiceName": "HealthcareAPI",
  "Endpoint": "http://otel-collector:4317",
  "Protocol": "grpc"
}
```

Env overrides:

```bash
Otel__Endpoint=http://localhost:4317
Otel__ServiceName=HealthcareAPI
Otel__Protocol=grpc
```

Leave `Endpoint` empty in local dev to avoid requiring a collector (console exporters for traces/metrics in Development).

---

## Correlation ID

| Layer | Behavior |
|-------|----------|
| HTTP | `X-Correlation-Id` accepted or generated; echoed on response |
| `HttpContext.Items` | `CorrelationId` |
| `CorrelationContext` | AsyncLocal + Activity baggage `correlation.id` |
| Serilog | `CorrelationId`, `TraceId`, `SpanId` on every log |
| MediatR | Scope + activity tags |
| HttpClient | `CorrelationIdDelegatingHandler` propagates header |
| Errors | `ErrorResponse.CorrelationId` |

Clients should send `X-Correlation-Id` and log the response header for support tickets.

---

## Business metrics (Meter: `Healthcare.Business`)

| Metric | Description |
|--------|-------------|
| `healthcare.appointments.booked` | Successful bookings |
| `healthcare.appointments.cancelled` | Cancellations |
| `healthcare.appointments.confirmed` | Confirmations |
| `healthcare.appointments.completed` | Completions |
| `healthcare.appointments.no_show` | No-shows |
| `healthcare.payments.succeeded` / `.failed` / `.refunded` | Payment outcomes |
| `healthcare.auth.login.succeeded` / `.failed` | Auth |
| `healthcare.commands` + `.duration` | MediatR command/query volume & latency |

### Derived SLOs / rates (in Grafana/Prom)

```
# No-show rate (window)
rate(healthcare_appointments_no_show_total[7d])
  / (rate(healthcare_appointments_completed_total[7d]) + rate(healthcare_appointments_no_show_total[7d]))

# Payment success rate
rate(healthcare_payments_succeeded_total[1h])
  / (rate(healthcare_payments_succeeded_total[1h]) + rate(healthcare_payments_failed_total[1h]))
```

Also: `Healthcare.Outbox`, `Healthcare.Reminders` meters from background workers.

---

## Health endpoints

| Path | Auth | Purpose |
|------|------|---------|
| `/health/live` | Anonymous | Process liveness (`self` only) |
| `/health/ready` | Anonymous | Readiness (db, redis, memory, workers) — summary |
| `/health` | Anonymous | Aggregate status only |
| `/health/details` | **Admin** | Full check data, latency, errors |

DB check includes `latencyMs`; slow queries (>1s) → **Degraded**.

---

## Structured logging

- Console + file: **compact JSON** (`RenderedCompactJsonFormatter` / `CompactJsonFormatter`)
- Enrichers: environment, machine, thread, log context (correlation)
- Request logging via `UseSerilogRequestLogging` with correlation + user id
- MediatR: request name, elapsed ms, correlation, outcome

Example log fields: `@t`, `@mt`, `CorrelationId`, `TraceId`, `SpanId`, `RequestName`, `ElapsedMs`, `Application`, `Environment`.

---

## Recommended alerts

| Signal | Condition | Severity |
|--------|-----------|----------|
| Logs | `Unhandled exception` / level Error rate high | High |
| Metric | Payment failure rate > 5% for 15m | High |
| Metric | No-show rate spike day-over-day | Medium |
| Metric | `healthcare.commands` p95 duration > 2s | Medium |
| Health | `/health/ready` not Healthy for 2m | High |
| Outbox | `outbox.messages.deadlettered` or circuit open | High |
| Auth | Login failure rate spike | Medium |

### Outbox dead-letter (legacy note)

Relay logs dead-letter events; prefer metric `outbox.messages.deadlettered` + Admin query on `Status = DeadLetter`.
