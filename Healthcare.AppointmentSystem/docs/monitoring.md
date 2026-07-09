# Monitoring & Alerting

## Outbox Message Permanent Failure

When an outbox message exhausts all retry attempts (`RetryCount >= MaxRetryAttempts`), the
`OutboxRelayService` logs an **Error**-level message with the template:

> Outbox message {Id} permanently failed after {RetryCount} attempts (max: {MaxRetries})

### Recommended Alert

| Field | Value |
|---|---|
| Log level | `Error` |
| Log message template | `Outbox message {Id} permanently failed after {RetryCount} attempts (max: {MaxRetries})` |
| Search filter | `"permanently failed"` |
| Severity | Warning / PagerDuty "warning" |
| Runbook | 1. Query the `OutboxMessages` table for rows with `ProcessedAt IS NULL` and `RetryCount = MaxRetryAttempts`.<br>2. Inspect the `Error` column for the exception details.<br>3. Identify the event type from `EventType` column.<br>4. If transient infrastructure issue, manually replay by resetting `RetryCount = 0`.<br>5. If permanent (e.g. missing event handler), deploy the fix and reset `RetryCount = 0`. |
