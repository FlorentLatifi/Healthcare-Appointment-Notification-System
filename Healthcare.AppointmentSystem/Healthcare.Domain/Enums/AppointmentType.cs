namespace Healthcare.Domain.Enums;

/// <summary>
/// Defines the type of appointment — used to select pricing strategy.
/// </summary>
public enum AppointmentType
{
    /// <summary>Regular scheduled consultation.</summary>
    Standard = 0,

    /// <summary>Patient has valid insurance coverage.</summary>
    Insurance = 1,

    /// <summary>Urgent/emergency appointment.</summary>
    Emergency = 2,

    /// <summary>VIP patient with loyalty discount.</summary>
    Vip = 3
}