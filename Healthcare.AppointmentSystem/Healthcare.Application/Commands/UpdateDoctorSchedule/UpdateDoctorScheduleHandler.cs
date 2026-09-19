using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Events;

namespace Healthcare.Application.Commands.UpdateDoctorSchedule;

public sealed class UpdateDoctorScheduleHandler : ICommandHandler<UpdateDoctorScheduleCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateDoctorScheduleHandler(IUnitOfWork unitOfWork, IDomainEventDispatcher eventDispatcher)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Result> HandleAsync(
        UpdateDoctorScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _unitOfWork.Doctors.GetByIdAsync(command.DoctorId, cancellationToken);
        if (doctor is null)
            return Result.Failure($"Doctor with ID '{command.DoctorId}' not found.");

        if (!doctor.IsActive)
            return Result.Failure("Cannot update schedule for an inactive doctor profile.");

        if (command.WeeklySchedule is null || command.WeeklySchedule.Count == 0)
            return Result.Failure("Weekly schedule is required.");

        // Normalize / validate day uniqueness
        var byDay = new Dictionary<DayOfWeek, WorkingHoursDto>();
        foreach (var row in command.WeeklySchedule)
        {
            if (byDay.ContainsKey(row.DayOfWeek))
                return Result.Failure($"Duplicate schedule entry for {row.DayOfWeek}.");
            byDay[row.DayOfWeek] = row;
        }

        // Apply every day of the week (missing days become day off)
        try
        {
            foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
            {
                if (!byDay.TryGetValue(day, out var row) || !row.IsWorkingDay)
                {
                    doctor.MarkDayOff(day);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.StartTime) || string.IsNullOrWhiteSpace(row.EndTime))
                    return Result.Failure($"{day}: start and end times are required for working days.");

                if (!TimeOnly.TryParse(row.StartTime, out var start))
                    return Result.Failure($"{day}: invalid start time '{row.StartTime}' (use HH:mm).");

                if (!TimeOnly.TryParse(row.EndTime, out var end))
                    return Result.Failure($"{day}: invalid end time '{row.EndTime}' (use HH:mm).");

                doctor.SetWorkingHours(day, start, end);
            }
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }

        await _unitOfWork.Doctors.UpdateAsync(doctor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Bust doctor + schedule + availability caches
        await _eventDispatcher.DispatchAsync(
            new DoctorCacheInvalidationNeededEvent(doctor.Id), cancellationToken);

        return Result.Success();
    }
}
