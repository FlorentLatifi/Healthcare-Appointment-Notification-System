using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Application.DTOs;

namespace Healthcare.IntegrationTests;

public sealed class AppointmentAuthorizationFlowTests : IntegrationTestBase
{
    public AppointmentAuthorizationFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAppointmentById_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var response = await Client.GetAsync("/api/v1/appointments/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelAppointment_PatientCancelsOwn_Returns200()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var token = await LoginAsync(ctx.PatientUsername, "SecurePass123!");
        SetAuthToken(token);

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel",
            new { CancellationReason = "Changed my mind" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelAppointment_PatientCancelsOther_Returns403()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var (otherCtx, _) = await CreateOtherPatientAsync();

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel",
            new { CancellationReason = "Not my appointment" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelAppointment_DoctorCancelsOwn_Returns200()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var token = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(token);

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel",
            new { CancellationReason = "Doctor rescheduled" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelAppointment_DoctorCancelsOther_Returns403()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var (otherCtx, _) = await CreateOtherDoctorAsync();

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel",
            new { CancellationReason = "Not my patient" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelAppointment_AdminCancelsAny_Returns200()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var token = await CreateAdminAsync();
        SetAuthToken(token);

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel",
            new { CancellationReason = "Admin override" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConfirmAppointment_DoctorConfirmsOwn_Returns200()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var token = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(token);

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/confirm",
            new { OverridePaymentRequirement = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConfirmAppointment_DoctorConfirmsOther_Returns403()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var (otherCtx, _) = await CreateOtherDoctorAsync();

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/confirm",
            new { OverridePaymentRequirement = true });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConfirmAppointment_AdminConfirmsAny_Returns200()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var token = await CreateAdminAsync();
        SetAuthToken(token);

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/confirm",
            new { OverridePaymentRequirement = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CompleteAppointment_DoctorCompletesOwn_Returns200()
    {
        var (ctx, appointmentId) = await SeedBookAndConfirmAsync();
        var token = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(token);

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/complete",
            new { DoctorNotes = "Patient recovered well" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CompleteAppointment_DoctorCompletesOther_Returns403()
    {
        var (ctx, appointmentId) = await SeedBookAndConfirmAsync();
        var (otherCtx, _) = await CreateOtherDoctorAsync();

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/complete",
            new { DoctorNotes = "Not my patient" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompleteAppointment_AdminCompletesAny_Returns200()
    {
        var (ctx, appointmentId) = await SeedBookAndConfirmAsync();
        var token = await CreateAdminAsync();
        SetAuthToken(token);

        var response = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/complete",
            new { DoctorNotes = "Admin override complete" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAppointmentById_PatientAccessingOwn_Returns200()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var token = await LoginAsync(ctx.PatientUsername, "SecurePass123!");
        SetAuthToken(token);

        var response = await Client.GetAsync($"/api/v1/appointments/{appointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAppointmentById_PatientAccessingOther_Returns403()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var (otherCtx, _) = await CreateOtherPatientAsync();

        var response = await Client.GetAsync($"/api/v1/appointments/{appointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAppointmentById_DoctorAccessingOwn_Returns200()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var token = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(token);

        var response = await Client.GetAsync($"/api/v1/appointments/{appointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAppointmentById_DoctorAccessingOther_Returns403()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();
        var (otherCtx, _) = await CreateOtherDoctorAsync();

        var response = await Client.GetAsync($"/api/v1/appointments/{appointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllAppointments_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var response = await Client.GetAsync("/api/v1/appointments");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllAppointments_WithPatientRole_Returns403()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterAndLoginAsync($"list_all_pat_{suffix}", $"list.all.pat.{suffix}@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);
        var response = await Client.GetAsync("/api/v1/appointments");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllAppointments_WithAdminRole_Returns200()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterAndLoginAsync($"list_all_admin_{suffix}", $"list.all.admin.{suffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(token);
        var response = await Client.GetAsync("/api/v1/appointments");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAppointmentsByPatient_PatientAccessesOwn_Returns200()
    {
        var (ctx, _) = await SeedAndBookAsync();
        var token = await LoginAsync(ctx.PatientUsername, "SecurePass123!");
        SetAuthToken(token);

        var response = await Client.GetAsync($"/api/v1/appointments/patient/{ctx.PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAppointmentsByPatient_PatientAccessesOther_Returns403()
    {
        var ctx = await SeedContextAsync();
        var (otherCtx, _) = await CreateOtherPatientAsync();

        var response = await Client.GetAsync($"/api/v1/appointments/patient/{ctx.PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAppointmentsByDoctor_DoctorAccessesOwn_Returns200()
    {
        var (ctx, _) = await SeedAndBookAsync();
        var token = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(token);

        var response = await Client.GetAsync($"/api/v1/appointments/doctor/{ctx.DoctorId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAppointmentsByDoctor_DoctorAccessesOther_Returns403()
    {
        var ctx = await SeedContextAsync();
        var (otherCtx, _) = await CreateOtherDoctorAsync();

        var response = await Client.GetAsync($"/api/v1/appointments/doctor/{ctx.DoctorId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

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

    private record SeedContext(
        int PatientId,
        int DoctorId,
        string PatientUsername,
        string DoctorUsername);

    private async Task<SeedContext> SeedContextAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var patUsername = $"apat_{suffix}";
        var docUsername = $"adoc_{suffix}";

        // Register Admin user, login, and create the doctor profile
        var adminToken = await RegisterAndLoginAsync(
            $"admin_{suffix}", $"admin.{suffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(adminToken);

        var docPayload = new
        {
            FirstName = "AuthTest", LastName = $"Doctor_{suffix}",
            Email = $"auth.doctor.{suffix}@clinic.com", PhoneNumber = $"+38348{suffix}00",
            LicenseNumber = $"MED-AT-{suffix}", Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m, ConsultationFeeCurrency = "USD", YearsOfExperience = 10
        };
        var docResponse = await Client.PostAsJsonAsync("/api/v1/doctors", docPayload);
        docResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var docResult = await DeserializeResponse<int>(docResponse);
        var doctorId = docResult!.Data;

        // Register Doctor user (for doctor_id claim on re-login)
        await RegisterAndLoginAsync(docUsername, $"auth.doc.{suffix}@test.com", "SecurePass123!", "Doctor");
        ClearAuthToken();

        // Register Patient user, login, create patient profile
        var patToken = await RegisterAndLoginAsync(patUsername, $"auth.pat.{suffix}@test.com", "SecurePass123!", "Patient");
        SetAuthToken(patToken);

        var patPayload = new
        {
            FirstName = "AuthTest", LastName = $"Patient_{suffix}",
            Email = $"auth.patient.{suffix}@test.com", PhoneNumber = $"+38349{suffix}00",
            DateOfBirth = "1990-01-01", Gender = "Male",
            Street = "10 St", City = "City", State = "State", PostalCode = "10000", Country = "Country"
        };
        var patResponse = await Client.PostAsJsonAsync("/api/v1/patients", patPayload);
        patResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var patResult = await DeserializeResponse<int>(patResponse);

        return new SeedContext(patResult!.Data, doctorId, patUsername, docUsername);
    }

    private async Task<(SeedContext Ctx, int AppointmentId)> SeedAndBookAsync()
    {
        var ctx = await SeedContextAsync();

        var token = await LoginAsync(ctx.PatientUsername, "SecurePass123!");
        SetAuthToken(token);

        var scheduledTime = GetNextWeekdayAt10Am();
        var bookPayload = new
        {
            PatientId = ctx.PatientId,
            DoctorId = ctx.DoctorId,
            ScheduledTime = scheduledTime.ToString("o"),
            Reason = "Integration test authorization check for appointment ownership",
            AppointmentType = "Standard"
        };
        var bookResponse = await Client.PostAsJsonAsync("/api/v1/appointments", bookPayload);
        bookResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var bookResult = await DeserializeResponse<AppointmentDto>(bookResponse);

        return (ctx, bookResult!.Data!.Id);
    }

    private async Task<(SeedContext Ctx, int AppointmentId)> SeedBookAndConfirmAsync()
    {
        var (ctx, appointmentId) = await SeedAndBookAsync();

        var docToken = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(docToken);
        var confirmResponse = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/confirm",
            new { OverridePaymentRequirement = true });
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return (ctx, appointmentId);
    }

    private async Task<(SeedContext Ctx, int PatientId)> CreateOtherPatientAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var username = $"other_pat_{suffix}";
        var token = await RegisterAndLoginAsync(username, $"other.pat.{suffix}@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);
        var createResponse = await Client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Other", LastName = "Patient",
            Email = $"other.patient.{suffix}@test.com", PhoneNumber = $"+38349{suffix}99",
            DateOfBirth = "1990-01-01", Gender = "Male",
            Street = "99 St", City = "City", State = "State", PostalCode = "10000", Country = "Country"
        });
        var result = await DeserializeResponse<int>(createResponse);
        var reloginToken = await LoginAsync(username, "SecurePass123!");
        SetAuthToken(reloginToken);
        return (new SeedContext(result!.Data, 0, username, ""), result!.Data);
    }

    private async Task<(SeedContext Ctx, int DoctorId)> CreateOtherDoctorAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var username = $"other_doc_{suffix}";
        var adminToken = await RegisterAndLoginAsync(
            $"od_admin_{suffix}", $"od.admin.{suffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(adminToken);
        var docPayload = new
        {
            FirstName = "Other", LastName = "Doctor",
            Email = $"other.doc.{suffix}@clinic.com", PhoneNumber = $"+38348{suffix}99",
            LicenseNumber = $"MED-OT-{suffix}", Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m, ConsultationFeeCurrency = "USD", YearsOfExperience = 5
        };
        var docResponse = await Client.PostAsJsonAsync("/api/v1/doctors", docPayload);
        var docResult = await DeserializeResponse<int>(docResponse);
        var token = await RegisterAndLoginAsync(username, $"other.doc.user.{suffix}@test.com", "SecurePass123!", "Doctor");
        SetAuthToken(token);
        return (new SeedContext(0, docResult!.Data, "", username), docResult!.Data);
    }

    private async Task<string> CreateAdminAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return await RegisterAndLoginAsync($"admin_{suffix}", $"admin.{suffix}@test.com", "SecurePass123!", "Admin");
    }
}
