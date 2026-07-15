using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Application.DTOs;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Domain.Entities;
using Healthcare.Presentation.API.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Healthcare.IntegrationTests;

public sealed class AppointmentAuditAccessTests : IntegrationTestBase
{
    public AppointmentAuditAccessTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAppointmentsByPatient_DoctorAccess_CreatesAuditEntry()
    {
        var (ctx, _) = await SeedAndBookAsync();

        var docToken = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(docToken);

        var response = await Client.GetAsync($"/api/v1/appointments/patient/{ctx.PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auditLogs = await GetAuditEntriesAsync(ctx.PatientId);
        auditLogs.Should().ContainSingle(e => e.EventType == "PatientRecordAccessed");
    }

    [Fact]
    public async Task GetAppointmentsByPatient_PatientAccess_DoesNotCreateAuditEntry()
    {
        var (ctx, _) = await SeedAndBookAsync();

        // First, make a Doctor call to establish a baseline audit entry count
        var docToken = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(docToken);
        await Client.GetAsync($"/api/v1/appointments/patient/{ctx.PatientId}");

        var beforeCount = (await GetAuditEntriesAsync(ctx.PatientId)).Count;

        // Now access as the patient themselves (self-access, no audit)
        var patToken = await LoginAsync(ctx.PatientUsername, "SecurePass123!");
        SetAuthToken(patToken);
        var response = await Client.GetAsync($"/api/v1/appointments/patient/{ctx.PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterLogs = await GetAuditEntriesAsync(ctx.PatientId);
        afterLogs.Count.Should().Be(beforeCount);
    }

    private async Task<List<AuditLogEntry>> GetAuditEntriesAsync(int patientId)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HealthcareDbContext>();
        return await context.AuditLogs
            .Where(e => e.EventType == "PatientRecordAccessed"
                     && e.EntityId == patientId)
            .ToListAsync();
    }

    private async Task<(SeedContext Ctx, int AppointmentId)> SeedAndBookAsync()
    {
        var ctx = await SeedContextAsync();

        var token = await LoginAsync(ctx.PatientUsername, "SecurePass123!");
        SetAuthToken(token);

        var scheduledTime = DateTime.UtcNow.AddDays(7);
        if (scheduledTime.DayOfWeek == DayOfWeek.Saturday)
            scheduledTime = scheduledTime.AddDays(2);
        else if (scheduledTime.DayOfWeek == DayOfWeek.Sunday)
            scheduledTime = scheduledTime.AddDays(1);

        var bookPayload = new
        {
            PatientId = ctx.PatientId,
            DoctorId = ctx.DoctorId,
            ScheduledTime = scheduledTime.ToString("o"),
            Reason = "Audit access test appointment",
            AppointmentType = "Standard"
        };
        var bookResponse = await Client.PostAsJsonAsync("/api/v1/appointments", bookPayload);
        bookResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var bookResult = await DeserializeResponse<AppointmentDto>(bookResponse);

        return (ctx, bookResult!.Data!.Id);
    }

    private record SeedContext(
        int PatientId,
        int DoctorId,
        string PatientUsername,
        string DoctorUsername);

    private async Task<SeedContext> SeedContextAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var patUsername = $"audit_pat_{suffix}";
        var docUsername = $"audit_doc_{suffix}";

        var adminToken = await RegisterAndLoginAsync(
            $"audit_admin_{suffix}", $"audit.admin.{suffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(adminToken);

        var docPayload = new
        {
            FirstName = "AuditTest",
            LastName = $"Doctor_{suffix}",
            Email = $"audit.doctor.{suffix}@clinic.com",
            PhoneNumber = $"+38348{suffix}00",
            LicenseNumber = $"MED-AT-{suffix}",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10
        };
        var docResponse = await Client.PostAsJsonAsync("/api/v1/doctors", docPayload);
        docResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var doctorId = await ReadCreatedProfileIdAsync(docResponse);

        await RegisterAndLoginAsync(docUsername, $"audit.doc.{suffix}@test.com", "SecurePass123!", "Doctor");
        ClearAuthToken();

        var patToken = await RegisterAndLoginAsync(patUsername, $"audit.pat.{suffix}@test.com", "SecurePass123!", "Patient");
        SetAuthToken(patToken);

        var patPayload = new
        {
            FirstName = "AuditTest",
            LastName = $"Patient_{suffix}",
            Email = $"audit.patient.{suffix}@test.com",
            PhoneNumber = $"+38349{suffix}00",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "10 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        };
        var patResponse = await Client.PostAsJsonAsync("/api/v1/patients", patPayload);
        patResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var patientId = await ReadCreatedProfileIdAsync(patResponse);

        return new SeedContext(patientId, doctorId, patUsername, docUsername);
    }
}
