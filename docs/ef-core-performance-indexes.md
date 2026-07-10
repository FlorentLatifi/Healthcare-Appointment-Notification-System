# EF Core Performance Analysis — Indexes & Query Patterns

Date: 2026-07-11  
Migration: `20260710231122_OptimizeQueryIndexes`

## 1. Baseline (before)

### Appointment indexes already present

| Index | Columns | Notes |
|-------|---------|--------|
| `IX_Appointments_ReferenceCode` | ReferenceCode UNIQUE | Good |
| `IX_Appointments_Doctor_Time` | DoctorId, ScheduledTime | Good for day schedule |
| `IX_Appointments_Status_Time` | Status, ScheduledTime | Good for status filters |
| `IX_Appointments_DoctorId` | DoctorId | **Redundant** (left prefix of Doctor_Time) |
| `IX_Appointments_PatientId` | PatientId | **Weak** for `ORDER BY ScheduledTime` |
| `IX_Appointments_Status` | Status | **Redundant** (left prefix of Status_Time) |
| `IX_Appointments_IsDeleted` | IsDeleted | **Low value** (global filter almost always `= 0`) |

### Gaps (missing / suboptimal)

| Query shape | Problem |
|-------------|---------|
| Patient lists `WHERE PatientId ORDER BY ScheduledTime` | No composite `(PatientId, ScheduledTime)` |
| Reminder job `Status=Confirmed AND RemindedAt IS NULL AND time range` | No filtered index |
| Double-booking under concurrency | App-level lock only; no DB unique for active slots |
| Revenue `Status=Succeeded AND PaidAt range` | Only `IX_Payments_Status` (no PaidAt) |
| Outbox relay `ProcessedAt IS NULL ORDER BY OccurredOn` | Index on `ProcessedAt` alone is weak |
| Active sessions `UserId AND RevokedAt IS NULL` | No filtered index |
| Admin bootstrap `Role = Admin` | No Role index |

---

## 2. After — `OptimizeQueryIndexes` migration

### Appointments

| Index | Purpose |
|-------|---------|
| `IX_Appointments_ReferenceCode` (unique) | Lookup / collision detection |
| `IX_Appointments_Doctor_Time` | Doctor day + history (all statuses) |
| `IX_Appointments_Doctor_Time_Active` (**unique filtered**) | One active Pending/Confirmed per doctor+time |
| `IX_Appointments_Doctor_Status_Time` | Doctor dashboards by status |
| `IX_Appointments_Patient_Time` | Patient history sorted by time |
| `IX_Appointments_Status_Time` | Status / analytics by time |
| `IX_Appointments_Reminders` (**filtered**) | Confirmed, not reminded, not deleted |

Dropped: `DoctorId`, `PatientId`, `Status`, `IsDeleted` singles (covered by composites).

### Other tables

| Index | Purpose |
|-------|---------|
| `IX_Payments_Status_PaidAt` | Revenue reports |
| `IX_Payments_TransactionId` unique filtered | Gateway id lookup |
| `IX_OutboxMessages_Pending` filtered | Relay batch |
| `IX_UserSessions_User_Active` filtered | Active sessions list |
| `IX_Users_Role` | Admin existence checks |
| `IX_Users_PatientId` / `DoctorId` filtered | Link reverse lookup |
| `IX_AuditLogs_Entity_Time` | Entity timeline |

---

## 3. N+1 and lazy loading

### Lazy loading

**Not enabled.** No `UseLazyLoadingProxies()`, entities are not `virtual` navigation proxies. Risk is low for classic N+1 via proxies.

### Explicit load patterns (good)

- `EFCoreAppointmentRepository` uses `Include(Patient).Include(Doctor).ThenInclude(SpecialtyEntries)` for UI aggregates.
- `AsNoTracking()` on pure reads.

### Issues found & fixed

| Issue | Before | After |
|-------|--------|-------|
| Over-fetch on availability | `GetByDoctorAndDateAsync` loaded Patient + Doctor + specialties for every slot | Appointment rows only (no navigations) |
| Count + Include | `GetPaged*` ran `CountAsync` on queries with `Include` (wasteful JOINs) | Count on bare filter; page query with Includes |
| Payment over-Include | Almost every payment method `Include(Appointment)` | Only join when filtering by patient |
| Reminder over-Include | Loaded Doctor + specialties for email reminder | Patient only |

### Remaining recommendations

1. **Split read models** — list endpoints should project to DTO/anonymous type instead of full aggregates (smaller SQL, no VO rehydration cost).
2. **`GetAllAsync` / unbounded lists** — prefer always-paged APIs; `GetAllAsync` is a scaling hazard.
3. **`Update(appointment)`** — full graph attach can mark navigations Modified; prefer load tracked entity, mutate, SaveChanges.
4. **Specialty revenue join** — verify execution plan; consider denormalized specialty on appointment if that report is hot.
5. **Global query filter** — soft-delete filter adds `IsDeleted = 0` to every Appointment query; filtered indexes should include that predicate (done for reminders / active slots).

---

## 4. Before / after SQL shapes

### A. Patient appointment history

**Before (index seek on PatientId, then sort):**

```sql
SELECT ...
FROM Appointments a
WHERE a.IsDeleted = 0 AND a.PatientId = @patientId
ORDER BY a.ScheduledTime DESC;
-- Plan: Index Seek IX_Appointments_PatientId → Sort
```

**After:**

```sql
-- Same SQL; plan uses IX_Appointments_Patient_Time (ordered key)
-- Index Seek + ordered scan, no Sort operator
```

### B. Reminder job

**Before:**

```sql
SELECT ...
FROM Appointments a
LEFT JOIN Patients p ON ...
LEFT JOIN Doctors d ON ...
LEFT JOIN DoctorSpecialties s ON ...
WHERE a.IsDeleted = 0
  AND a.Status = 2
  AND a.ScheduledTime > @now AND a.ScheduledTime <= @now24
  AND a.RemindedAt IS NULL;
-- Often Index Seek Status_Time + residual predicate on RemindedAt
-- + 3 joins for Doctor/Specialties never used by reminder
```

**After:**

```sql
SELECT ...
FROM Appointments a
INNER JOIN Patients p ON ...
WHERE a.IsDeleted = 0
  AND a.Status = 2
  AND a.ScheduledTime > @now AND a.ScheduledTime <= @now24
  AND a.RemindedAt IS NULL
ORDER BY a.ScheduledTime;
-- Prefer IX_Appointments_Reminders (tiny filtered set)
-- Only Patient join
```

### C. Double booking (concurrency)

**Before:** Redis/in-memory lock + load day appointments in app.

**After (additional):**

```sql
-- Insert fails if another Pending/Confirmed row exists for same doctor+time
-- IX_Appointments_Doctor_Time_Active UNIQUE FILTERED
INSERT INTO Appointments (DoctorId, ScheduledTime, Status, ...)
VALUES (@doctorId, @time, 1, ...);
-- Unique key violation → map to "slot taken" in handler (optional enhancement)
```

### D. Revenue total

**Before:**

```sql
SELECT SUM(Amount)
FROM Payments
WHERE Status = @succeeded AND PaidAt >= @from AND PaidAt < @to;
-- Seek Status, residual on PaidAt (or scan)
```

**After:**

```sql
-- Uses IX_Payments_Status_PaidAt (Status, PaidAt)
```

### E. Outbox relay

**Before:**

```sql
SELECT TOP (@batch) *
FROM OutboxMessages
WHERE ProcessedAt IS NULL AND RetryCount < @max
ORDER BY OccurredOn;
-- Weak: nonclustered on ProcessedAt includes all historical NULLs + processed rows over time
```

**After:**

```sql
-- IX_OutboxMessages_Pending FILTER (ProcessedAt IS NULL) on (OccurredOn, RetryCount)
```

---

## 5. Repository pattern recommendations

| Pattern | Guidance |
|---------|----------|
| **Command vs query** | Commands: tracked load of needed aggregate only. Queries: `AsNoTracking` + project to DTO. |
| **Includes** | Only what the use case touches. Availability ≠ UI detail. |
| **Pagination** | Always count without Includes; page with Includes or projection. |
| **Existence checks** | `AnyAsync` / `CountAsync` with selective columns, never full entity graph. |
| **Conflict handling** | Catch unique violation on `IX_Appointments_Doctor_Time_Active` and `IX_Appointments_ReferenceCode` as domain failures. |
| **Compiled queries** | Consider `EF.CompileAsyncQuery` for hot paths (reminders, GetById). |
| **AsSplitQuery** | If SpecialtyEntries multiplies rows badly on large lists, use `AsSplitQuery()` for list endpoints. |

---

## 6. Apply migration

```bash
cd Healthcare.AppointmentSystem
dotnet ef database update --project Healthcare.Adapters --startup-project Healthcare.Presentation.API
# or rely on DatabaseSeeder MigrateAsync at startup
```

**Note:** Creating the unique filtered index fails if the database already has duplicate active appointments for the same doctor+time. Clean duplicates first:

```sql
-- Inspect conflicts
SELECT DoctorId, ScheduledTime, COUNT(*)
FROM Appointments
WHERE IsDeleted = 0 AND Status IN (1, 2)
GROUP BY DoctorId, ScheduledTime
HAVING COUNT(*) > 1;
```
