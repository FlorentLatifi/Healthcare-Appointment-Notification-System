using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Application.DTOs;
using Healthcare.Presentation.API.Responses;

namespace Healthcare.IntegrationTests;

public sealed class DoubleBookingTests : IntegrationTestBase
{
    public DoubleBookingTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static DateTime GetNextWeekdayAt14()
    {
        var now = DateTime.Now;
        var candidate = now.Date.AddHours(14);

        if (candidate <= now.AddHours(1))
            candidate = candidate.AddDays(1);

        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            candidate = candidate.AddDays(1);

        while (candidate <= now.AddHours(2))
            candidate = candidate.AddDays(1);

        return candidate;
    }

    [Fact]
    public async Task ConcurrentBooking_SameSlot_OnlyOneSucceeds()
    {
        // Arrange: create doctor, patient, and patient user
        var doctorPayload = new
        {
            FirstName = "Concurrent",
            LastName = "Doctor",
            Email = "concurrent.doc@clinic.com",
            PhoneNumber = "+38349111111",
            LicenseNumber = "MED-CON-01",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        };
        var doctorResponse = await Client.PostAsJsonAsync("/api/v1/doctors", doctorPayload);
        var doctorId = await ReadCreatedProfileIdAsync(doctorResponse);

        var patientPayload = new
        {
            FirstName = "Concurrent",
            LastName = "Patient",
            Email = "concurrent.patient@test.com",
            PhoneNumber = "+38349222222",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "1 Test St",
            City = "Pristina",
            State = "Kosovo",
            PostalCode = "10000",
            Country = "Kosovo"
        };
        var patientResponse = await Client.PostAsJsonAsync("/api/v1/patients", patientPayload);

        var patientToken1 = await RegisterAndLoginAsync(
            "concurrent_user1", "concurrent1@test.com", "SecurePass123!", "Patient");

        var patientToken2 = await RegisterAndLoginAsync(
            "concurrent_user2", "concurrent2@test.com", "SecurePass123!", "Patient");

        var scheduledTime = GetNextWeekdayAt14();

        var bookPayload = new
        {
            PatientId = 1,
            DoctorId = doctorId,
            ScheduledTime = scheduledTime.ToString("o"),
            Reason = "Concurrent booking test with overlapping time slot",
            AppointmentType = "Standard"
        };

        // Act: send two concurrent booking requests
        using var client1 = Factory.CreateClient();
        client1.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", patientToken1);

        using var client2 = Factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", patientToken2);

        var task1 = client1.PostAsJsonAsync("/api/v1/appointments", bookPayload);
        var task2 = client2.PostAsJsonAsync("/api/v1/appointments", bookPayload);

        var responses = await Task.WhenAll(task1, task2);

        // Assert: one should succeed (Created) and one should fail (BadRequest)
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var failCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        successCount.Should().Be(1, "only one booking should succeed for the same time slot");
        failCount.Should().Be(1, "the second concurrent booking should be rejected");
    }
}
