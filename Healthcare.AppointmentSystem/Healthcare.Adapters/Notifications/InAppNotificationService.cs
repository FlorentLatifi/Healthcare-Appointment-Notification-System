using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Notifications;

public sealed class InAppNotificationService : IInAppNotificationService
{
    private readonly IUserNotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InAppNotificationService> _logger;

    public InAppNotificationService(
        IUserNotificationRepository notifications,
        IUnitOfWork unitOfWork,
        ILogger<InAppNotificationService> logger)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task NotifyUserAsync(
        int userId,
        string title,
        string message,
        string? category = null,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var note = UserNotification.Create(userId, title, message, category, relatedEntityType, relatedEntityId);
            await _notifications.AddAsync(note, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create in-app notification for user {UserId}", userId);
        }
    }

    public async Task NotifyUsersLinkedToAppointmentAsync(
        int appointmentId,
        string title,
        string message,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var appointment = await _unitOfWork.Appointments.GetByIdAsync(appointmentId, cancellationToken);
            if (appointment is null) return;

            var recipientIds = new HashSet<int>();

            // Resolve linked accounts for patient and doctor (when profiles are linked).
            var patientUsers = await _unitOfWork.Users.FindByPatientIdAsync(appointment.PatientId, cancellationToken);
            foreach (var u in patientUsers) recipientIds.Add(u.Id);

            var doctorUsers = await _unitOfWork.Users.FindByDoctorIdAsync(appointment.DoctorId, cancellationToken);
            foreach (var u in doctorUsers) recipientIds.Add(u.Id);

            foreach (var userId in recipientIds)
            {
                var note = UserNotification.Create(
                    userId,
                    title,
                    message,
                    category,
                    "Appointment",
                    appointmentId);
                await _notifications.AddAsync(note, cancellationToken);
            }

            if (recipientIds.Count > 0)
                await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fan-out in-app notifications for appointment {AppointmentId}", appointmentId);
        }
    }
}
