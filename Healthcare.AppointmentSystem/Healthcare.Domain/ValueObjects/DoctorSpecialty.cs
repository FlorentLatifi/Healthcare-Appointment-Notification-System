using Healthcare.Domain.Common;
using Healthcare.Domain.Enums;

namespace Healthcare.Domain.ValueObjects;

public sealed class DoctorSpecialty : ValueObject
{
    public Specialty Specialty { get; }

    private DoctorSpecialty(Specialty specialty)
    {
        Specialty = specialty;
    }

    public static DoctorSpecialty Create(Specialty specialty) => new(specialty);

    private DoctorSpecialty()
    {
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Specialty;
    }
}
