namespace Healthcare.Domain.Enums;

/// <summary>
/// Represents medical specialties for doctors.
/// </summary>
/// <remarks>
/// This is a simplified list. In a real system, this might be a separate entity
/// with more complex hierarchies and relationships.
/// </remarks>
public enum Specialty
{
    /// <summary>
    /// General family medicine and primary care.
    /// </summary>
    GeneralPractice = 1,

    /// <summary>
    /// Heart and cardiovascular system specialist.
    /// </summary>
    Cardiology = 2,

    /// <summary>
    /// Skin, hair, and nails specialist.
    /// </summary>
    Dermatology = 3,

    /// <summary>
    /// Bone, joint, and muscle specialist.
    /// </summary>
    Orthopedics = 4,

    /// <summary>
    /// Children's health specialist.
    /// </summary>
    Pediatrics = 5,

    /// <summary>
    /// Brain and nervous system specialist.
    /// </summary>
    Neurology = 6,

    /// <summary>
    /// Mental health specialist.
    /// </summary>
    Psychiatry = 7,

    /// <summary>
    /// Women's reproductive health specialist.
    /// </summary>
    Gynecology = 8,

    /// <summary>
    /// Eye and vision specialist.
    /// </summary>
    Ophthalmology = 9,

    /// <summary>
    /// Ear, nose, and throat specialist.
    /// </summary>
    Otorhinolaryngology = 10,

    /// <summary>
    /// Cancer treatment specialist.
    /// </summary>
    Oncology = 11,

    /// <summary>
    /// Surgical procedures specialist.
    /// </summary>
    Surgery = 12
}