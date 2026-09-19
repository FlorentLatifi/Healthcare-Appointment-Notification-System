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
        // Arrange: one doctor, two patients with their own accounts and profiles
        var (_, doctorId) = await CreateDoctorAccountAsync("Cardiology");
        var patient1 = await CreatePatientAccountAsync();
        var patient2 = await CreatePatientAccountAsync();

        var scheduledTime = GetNextWeekdayAt14().ToString("o");
        object BookingFor(int patientId) => new
        {
            PatientId = patientId,
            DoctorId = doctorId,
            ScheduledTime = scheduledTime,
            Reason = "Concurrent booking test with overlapping time slot",
            AppointmentType = "Standard"
        };

        using var client1 = Factory.CreateClient();
        client1.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", patient1.Token);

        using var client2 = Factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", patient2.Token);

        // Act: both patients try to book the same doctor and slot at the same time
        var responses = await Task.WhenAll(
            client1.PostAsJsonAsync("/api/v1/appointments", BookingFor(patient1.PatientId)),
            client2.PostAsJsonAsync("/api/v1/appointments", BookingFor(patient2.PatientId)));

        // Assert: the per-slot lock lets exactly one through
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        var summary = string.Join(" | ", responses.Select((r, i) => $"{(int)r.StatusCode}: {bodies[i]}"));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created)
            .Should().Be(1, $"only one booking should succeed for the same time slot ({summary})");
        responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest)
            .Should().Be(1, $"the second concurrent booking should be rejected ({summary})");
    }
}
