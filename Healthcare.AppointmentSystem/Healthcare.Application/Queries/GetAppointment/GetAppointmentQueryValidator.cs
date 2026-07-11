using FluentValidation;

namespace Healthcare.Application.Queries.GetAppointment;

public sealed class GetAppointmentQueryValidator : AbstractValidator<GetAppointmentQuery>
{
    public GetAppointmentQueryValidator()
    {
        RuleFor(x => x.AppointmentId)
            .GreaterThan(0).WithMessage("AppointmentId must be a positive identifier.");
    }
}
