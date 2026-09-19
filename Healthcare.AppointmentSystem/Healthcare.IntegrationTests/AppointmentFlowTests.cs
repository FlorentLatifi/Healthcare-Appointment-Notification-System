using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Application.DTOs;
using Healthcare.Presentation.API.Responses;

namespace Healthcare.IntegrationTests;

public sealed class AppointmentFlowTests : IntegrationTestBase
{
    public AppointmentFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static DateTime GetNextWeekdayAt10Am()
    {
        var now = DateTime.Now;
        var candidate = now.Date.AddHours(10);

        if (candidate <= now.AddHours(1))
            candidate = candidate.AddDays(1);

        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            candidate = candidate.AddDays(1);

        while (candidate <= now.AddHours(2))
            candidate = candidate.AddDays(1);

        return candidate;
    }

    [Fact]
    public async Task BookAndConfirmAppointment_FullFlow_Succeeds()
    {
        // 1. A doctor and a patient, each with their own account and linked profile
        var (_, doctorId) = await CreateDoctorAccountAsync("Cardiology");
        var (_, patientId, patientToken) = await CreatePatientAccountAsync();
        SetAuthToken(patientToken);

        // 2. Book appointment
        var scheduledTime = GetNextWeekdayAt10Am();
        var bookPayload = new
        {
            PatientId = patientId,
            DoctorId = doctorId,
            ScheduledTime = scheduledTime.ToString("o"),
            Reason = "Annual checkup and blood pressure monitoring",
            AppointmentType = "Standard"
        };
        var bookResponse = await Client.PostAsJsonAsync("/api/v1/appointments", bookPayload);
        bookResponse.StatusCode.Should().Be(HttpStatusCode.Created, await bookResponse.Content.ReadAsStringAsync());
        var bookResult = await DeserializeResponse<AppointmentDto>(bookResponse);
        bookResult.Should().NotBeNull();
        bookResult!.Success.Should().BeTrue();
        bookResult.Data.Should().NotBeNull();
        var appointmentId = bookResult.Data!.Id;

        // Verify appointment via GET
        var getResponse = await Client.GetAsync($"/api/v1/appointments/{appointmentId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, await getResponse.Content.ReadAsStringAsync());
        var getResult = await DeserializeResponse<AppointmentDto>(getResponse);
        getResult!.Data!.Status.Should().Be("Pending");

        // 3. Login as Admin and confirm
        ClearAuthToken();
        var adminToken = await LoginAsPreSeededAdminAsync();
        SetAuthToken(adminToken);

        var confirmPayload = new
        {
            AppointmentId = appointmentId,
            OverridePaymentRequirement = true,
            OverrideReason = "Admin override for integration test verification."
        };
        var confirmResponse = await Client
            .PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/confirm", confirmPayload);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK, await confirmResponse.Content.ReadAsStringAsync());

        // Verify status changed to Confirmed
        ClearAuthToken();
        SetAuthToken(patientToken);
        var getConfirmedResponse = await Client.GetAsync($"/api/v1/appointments/{appointmentId}");
        getConfirmedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await getConfirmedResponse.Content.ReadAsStringAsync());
        var confirmedResult = await DeserializeResponse<AppointmentDto>(getConfirmedResponse);
        confirmedResult!.Data!.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task BookAppointment_InvalidTime_Returns400()
    {
        var patientToken = await RegisterAndLoginAsync(
            "invalid_time_user", "invalid_time@test.com", "SecurePass123!", "Patient");
        SetAuthToken(patientToken);

        var bookPayload = new
        {
            PatientId = 1,
            DoctorId = 1,
            ScheduledTime = DateTime.UtcNow.AddDays(-1).ToString("o"),
            Reason = "This is a test reason for invalid time",
            AppointmentType = "Standard"
        };
        var response = await Client.PostAsJsonAsync("/api/v1/appointments", bookPayload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }
}
