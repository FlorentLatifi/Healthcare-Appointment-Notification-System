using FluentValidation;
using Healthcare.Presentation.API.Requests;

namespace Healthcare.Presentation.API.Validators;

public sealed class UpdateDoctorScheduleRequestValidator : AbstractValidator<UpdateDoctorScheduleRequest>
{
    public UpdateDoctorScheduleRequestValidator()
    {
        RuleFor(x => x.WeeklySchedule)
            .NotNull().WithMessage("Weekly schedule is required")
            .Must(s => s is { Count: > 0 }).WithMessage("Weekly schedule must include at least one day")
            .Must(s => s is null || s.Select(d => d.DayOfWeek).Distinct().Count() == s.Count)
            .WithMessage("Each day of the week may appear only once");

        RuleForEach(x => x.WeeklySchedule).ChildRules(day =>
        {
            day.RuleFor(d => d.DayOfWeek)
                .IsInEnum().WithMessage("Invalid day of week");

            day.When(d => d.IsWorkingDay, () =>
            {
                day.RuleFor(d => d.StartTime)
                    .NotEmpty().WithMessage("Start time is required for working days")
                    .Matches(@"^\d{1,2}:\d{2}$").WithMessage("Start time must be HH:mm");

                day.RuleFor(d => d.EndTime)
                    .NotEmpty().WithMessage("End time is required for working days")
                    .Matches(@"^\d{1,2}:\d{2}$").WithMessage("End time must be HH:mm");

                day.RuleFor(d => d)
                    .Must(d =>
                    {
                        if (!TimeOnly.TryParse(d.StartTime, out var start)) return true;
                        if (!TimeOnly.TryParse(d.EndTime, out var end)) return true;
                        return start < end;
                    })
                    .WithMessage("Start time must be before end time");
            });
        });
    }
}
