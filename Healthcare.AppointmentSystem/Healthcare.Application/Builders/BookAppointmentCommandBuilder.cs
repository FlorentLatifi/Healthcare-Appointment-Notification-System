using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Domain.Enums;

namespace Healthcare.Application.Builders;

/// <summary>
/// Builder for constructing BookAppointmentCommand objects.
/// </summary>
/// <remarks>
/// Design Pattern: Builder (Creational)
/// 
/// WHY Builder here?
///   BookAppointmentCommand has multiple fields, some required,
///   some optional. Without a builder:
///   - Easy to forget fields
///   - Hard to read at call site
///   - No validation until runtime
/// 
/// WITH Builder (Fluent Interface):
///   var command = new BookAppointmentCommandBuilder()
///       .ForPatient(patientId)
///       .WithDoctor(doctorId)
///       .At(scheduledTime)
///       .BecauseOf("Annual checkup")
///       .WithInsurance()
///       .Build();
/// 
/// WHERE (Hexagonal Architecture):
///   Lives in Application layer — builds Application-layer objects.
///   Has NO dependency on Infrastructure or Domain entities.
/// 
/// BENEFITS:
///   1. Readable — reads like English
///   2. Safe — validates before Build()
///   3. Flexible — optional fields have defaults
///   4. Testable — easy to create test commands
/// </remarks>
public sealed class BookAppointmentCommandBuilder
{
    // Required fields
    private int _patientId;
    private int _doctorId;
    private DateTime _scheduledTime;
    private string _reason = string.Empty;

    // Optional fields with defaults
    private AppointmentType _appointmentType = AppointmentType.Standard;

    // Tracking which required fields are set
    private bool _patientSet;
    private bool _doctorSet;
    private bool _timeSet;
    private bool _reasonSet;

    // ── REQUIRED SETTERS ────────────────────────────────

    /// <summary>
    /// Sets the patient for the appointment.
    /// </summary>
    public BookAppointmentCommandBuilder ForPatient(int patientId)
    {
        if (patientId <= 0)
            throw new ArgumentException(
                "Patient ID must be positive.", nameof(patientId));

        _patientId = patientId;
        _patientSet = true;
        return this;
    }

    /// <summary>
    /// Sets the doctor for the appointment.
    /// </summary>
    public BookAppointmentCommandBuilder WithDoctor(int doctorId)
    {
        if (doctorId <= 0)
            throw new ArgumentException(
                "Doctor ID must be positive.", nameof(doctorId));

        _doctorId = doctorId;
        _doctorSet = true;
        return this;
    }

    /// <summary>
    /// Sets the scheduled time for the appointment.
    /// </summary>
    public BookAppointmentCommandBuilder At(DateTime scheduledTime)
    {
        if (scheduledTime <= DateTime.UtcNow)
            throw new ArgumentException(
                "Scheduled time must be in the future.",
                nameof(scheduledTime));

        _scheduledTime = scheduledTime;
        _timeSet = true;
        return this;
    }

    /// <summary>
    /// Sets the reason for the appointment.
    /// </summary>
    public BookAppointmentCommandBuilder BecauseOf(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "Reason cannot be empty.", nameof(reason));

        if (reason.Trim().Length < 10)
            throw new ArgumentException(
                "Reason must be at least 10 characters.", nameof(reason));

        _reason = reason.Trim();
        _reasonSet = true;
        return this;
    }

    // ── OPTIONAL SETTERS (Pricing Strategy) ─────────────

    /// <summary>
    /// Marks this as a standard appointment (default).
    /// Applies standard pricing — no discounts or premiums.
    /// </summary>
    public BookAppointmentCommandBuilder AsStandard()
    {
        _appointmentType = AppointmentType.Standard;
        return this;
    }

    /// <summary>
    /// Marks this as an insurance appointment.
    /// Applies insurance pricing — 30% discount.
    /// </summary>
    public BookAppointmentCommandBuilder WithInsurance()
    {
        _appointmentType = AppointmentType.Insurance;
        return this;
    }

    /// <summary>
    /// Marks this as an emergency appointment.
    /// Applies emergency pricing — 50% premium.
    /// </summary>
    public BookAppointmentCommandBuilder AsEmergency()
    {
        _appointmentType = AppointmentType.Emergency;
        return this;
    }

    /// <summary>
    /// Marks this as a VIP appointment.
    /// Applies VIP pricing — 20% discount.
    /// </summary>
    public BookAppointmentCommandBuilder AsVip()
    {
        _appointmentType = AppointmentType.Vip;
        return this;
    }

    // ── BUILD ────────────────────────────────────────────

    /// <summary>
    /// Validates all required fields and constructs the command.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when required fields are missing.
    /// </exception>
    public BookAppointmentCommand Build()
    {
        // Validate all required fields are set
        var missing = new List<string>();

        if (!_patientSet) missing.Add("Patient (call ForPatient())");
        if (!_doctorSet) missing.Add("Doctor (call WithDoctor())");
        if (!_timeSet) missing.Add("Scheduled time (call At())");
        if (!_reasonSet) missing.Add("Reason (call BecauseOf())");

        if (missing.Any())
            throw new InvalidOperationException(
                $"Cannot build command. Missing required fields: " +
                $"{string.Join(", ", missing)}");

        return new BookAppointmentCommand
        {
            PatientId = _patientId,
            DoctorId = _doctorId,
            ScheduledTime = _scheduledTime,
            Reason = _reason,
            AppointmentType = _appointmentType
        };
    }
}
