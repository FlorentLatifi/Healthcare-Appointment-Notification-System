using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.DeactivateDoctor;

public sealed class DeactivateDoctorCommand : ICommand<Result>
{
    public int DoctorId { get; set; }
}
