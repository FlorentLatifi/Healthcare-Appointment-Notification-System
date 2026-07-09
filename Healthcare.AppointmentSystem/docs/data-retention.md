# Data Retention Policy

This document describes how patient data is retained, deactivated, and
anonymized in the Healthcare Appointment System.

## Policy overview

| Category | Rule |
|----------|------|
| Active patients | Kept indefinitely while the patient remains active |
| Deactivated patients | Soft-deleted; record retained, `IsActive = false`, PII preserved for 7 years after deactivation |
| Anonymized patients | PII irreversibly replaced after the retention period |
| Hard deletion | Never performed on production patient data |

## Patient lifecycle

```
Created ──► Active ──► Deactivated ──► Anonymized
               │                           │
               └── Reactivated (within     └── Data kept for
                   7-year window)               audit/historical FK
```

### Active (`IsActive = true`, `IsAnonymized = false`)
Normal operation. All PII is real and queryable.

### Deactivated (`IsActive = false`, `IsAnonymized = false`)
Triggered by `DELETE /api/v1/patients/{id}` (Admin only).
- Sets `IsActive = false`
- PII is preserved for 7-year legal/audit retention
- The patient cannot book new appointments
- Historical appointments are kept intact (FK integrity)

### Anonymized (`IsActive = false`, `IsAnonymized = true`)
Triggered by `POST /api/v1/patients/{id}/anonymize` (Admin only),
typically after the 7-year retention window.
- All PII is irreversibly replaced with anonymized values
- `Id` (primary key) is preserved — all FK references
  (appointments, payments, etc.) remain intact
- The email is set to `anonymized-{Id}@anonymized.local`
  (unique per patient, prevents FK constraint violations)
- The anonymized account can never be reactivated

## What gets anonymized

| Field | Anonymized value |
|-------|------------------|
| `FirstName` | `Anonymized` |
| `LastName` | `{Id}` (the patient's primary key) |
| `Email` | `anonymized-{Id}@anonymized.local` |
| `PhoneNumber` | `+00000000000` |
| `DateOfBirth` | `1970-01-01` |
| `Address` | Dummy (Anonymized / Anonymized / 00000 / Anonymized) |
| `Gender` | Preserved (not PII per se) |
| `NotificationPreferences` | Reset to defaults |

## Database-level notes

- The `IsAnonymized` column is a non-nullable bit with a default of `false`.
- The unique index `IX_Patients_Email` is preserved — anonymized
  emails are unique by construction (`anonymized-{Id}@anonymized.local`).
- No cascade deletes are affected; all foreign-key relationships remain
  intact after anonymization.
- After adding the `IsAnonymized` column, run:
  ```
  dotnet ef migrations add AddIsAnonymizedToPatient
  dotnet ef database update
  ```

## Future improvements

- Scheduled job to auto-anonymize patients deactivated > 7 years ago.
- Configurable retention period per region (GDPR: different limits).
- Audit log entry when anonymization is triggered.
