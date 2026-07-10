-- ============================================================================
-- FIND ORPHANED PATIENT / DOCTOR ROWS
-- ============================================================================
-- Background:
--   Before the fix in CreatePatientHandler and CreateDoctorHandler, the
--   handlers persisted the new entity to the database *before* checking
--   whether the requesting user was already linked to a patient/doctor
--   profile.  When the check failed, the failure was returned to the caller
--   but the new row was already committed — orphaned and unlinked.
--
--   This script finds those orphaned rows for manual review.
--   It does NOT delete anything.
--
-- Run against:  HealthcareAppointmentDb (or your configured database name)
-- Safety:       READ-ONLY — no INSERT / UPDATE / DELETE.
-- ============================================================================

-- Orphaned Patients: rows with no User pointing to them via PatientId
SELECT
    p.Id        AS PatientId,
    p.FirstName,
    p.LastName,
    p.Email,
    p.CreatedAt AS PatientCreatedAt,
    'PATIENT'   AS OrphanType
FROM dbo.Patients p
LEFT JOIN dbo.Users u ON u.PatientId = p.Id
WHERE u.Id IS NULL
ORDER BY p.CreatedAt DESC;

-- Orphaned Doctors: rows with no User pointing to them via DoctorId
SELECT
    d.Id        AS DoctorId,
    d.FirstName,
    d.LastName,
    d.Email,
    d.CreatedAt AS DoctorCreatedAt,
    'DOCTOR'    AS OrphanType
FROM dbo.Doctors d
LEFT JOIN dbo.Users u ON u.DoctorId = d.Id
WHERE u.Id IS NULL
ORDER BY d.CreatedAt DESC;

-- Combined summary
SELECT
    COUNT(*) AS TotalOrphanedRows,
    'PATIENT' AS RowType
FROM dbo.Patients p
LEFT JOIN dbo.Users u ON u.PatientId = p.Id
WHERE u.Id IS NULL
UNION ALL
SELECT
    COUNT(*) AS TotalOrphanedRows,
    'DOCTOR' AS RowType
FROM dbo.Doctors d
LEFT JOIN dbo.Users u ON u.DoctorId = d.Id
WHERE u.Id IS NULL;
