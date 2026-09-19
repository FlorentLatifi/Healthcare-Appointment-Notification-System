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
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

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
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

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

        // Bookings must fall on the hour and inside working hours (Mon-Fri 08:00-18:00 local).
        var scheduledTime = DateTime.Now.Date.AddDays(7).AddHours(10);
        while (scheduledTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
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
        bookResponse.StatusCode.Should().Be(HttpStatusCode.Created, await bookResponse.Content.ReadAsStringAsync());
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
        // The doctor creates its own profile so the account carries doctor_id; an admin-created
        // profile is not linked to any account and the doctor's reads would be refused.
        var (docUsername, doctorId) = await CreateDoctorAccountAsync();
        var (patUsername, patientId, _) = await CreatePatientAccountAsync();

        return new SeedContext(patientId, doctorId, patUsername, docUsername);
    }
}
