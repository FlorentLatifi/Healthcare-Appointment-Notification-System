using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.AnonymizePatient;

public sealed class AnonymizePatientCommand : ICommand<Result>
{
    public int PatientId { get; set; }
}
