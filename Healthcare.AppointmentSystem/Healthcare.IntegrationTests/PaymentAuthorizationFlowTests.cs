using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Application.DTOs;

namespace Healthcare.IntegrationTests;

public sealed class PaymentAuthorizationFlowTests : IntegrationTestBase
{
    public PaymentAuthorizationFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetPaymentById_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var response = await Client.GetAsync("/api/v1/payments/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPaymentByAppointment_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var response = await Client.GetAsync("/api/v1/payments/appointment/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPaymentById_PatientAccessesOwn_Returns200()
    {
        var ctx = await SeedPaymentScenarioAsync();

        var token = await LoginAsync(ctx.PatientUsername, "SecurePass123!");
        SetAuthToken(token);
        var response = await Client.GetAsync($"/api/v1/payments/{ctx.PaymentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPaymentById_PatientAccessesOther_Returns403()
    {
        var ctx = await SeedPaymentScenarioAsync();

        var other = await CreateOtherPatientAsync();
        var response = await Client.GetAsync($"/api/v1/payments/{ctx.PaymentId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPaymentById_DoctorAccessesOwn_Returns200()
    {
        var ctx = await SeedPaymentScenarioAsync();

        var token = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(token);
        var response = await Client.GetAsync($"/api/v1/payments/{ctx.PaymentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPaymentById_DoctorAccessesOther_Returns403()
    {
        var ctx = await SeedPaymentScenarioAsync();

        var other = await CreateOtherDoctorAsync();
        var response = await Client.GetAsync($"/api/v1/payments/{ctx.PaymentId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPaymentById_AdminAccessesAny_Returns200()
    {
        var ctx = await SeedPaymentScenarioAsync();
        var adminSuffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterAndLoginAsync($"pay_admin_{adminSuffix}", $"pay.admin.{adminSuffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(token);

        var response = await Client.GetAsync($"/api/v1/payments/{ctx.PaymentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPaymentByAppointment_PatientAccessesOwn_Returns200()
    {
        var ctx = await SeedPaymentScenarioAsync();

        var token = await LoginAsync(ctx.PatientUsername, "SecurePass123!");
        SetAuthToken(token);
        var response = await Client.GetAsync($"/api/v1/payments/appointment/{ctx.AppointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPaymentByAppointment_PatientAccessesOther_Returns403()
    {
        var ctx = await SeedPaymentScenarioAsync();

        var other = await CreateOtherPatientAsync();
        var response = await Client.GetAsync($"/api/v1/payments/appointment/{ctx.AppointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPaymentByAppointment_DoctorAccessesOwn_Returns200()
    {
        var ctx = await SeedPaymentScenarioAsync();

        var token = await LoginAsync(ctx.DoctorUsername, "SecurePass123!");
        SetAuthToken(token);
        var response = await Client.GetAsync($"/api/v1/payments/appointment/{ctx.AppointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPaymentByAppointment_DoctorAccessesOther_Returns403()
    {
        var ctx = await SeedPaymentScenarioAsync();

        var other = await CreateOtherDoctorAsync();
        var response = await Client.GetAsync($"/api/v1/payments/appointment/{ctx.AppointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPaymentByAppointment_AdminAccessesAny_Returns200()
    {
        var ctx = await SeedPaymentScenarioAsync();
        var adminSuffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterAndLoginAsync($"pay_app_admin_{adminSuffix}", $"pay.app.admin.{adminSuffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(token);

        var response = await Client.GetAsync($"/api/v1/payments/appointment/{ctx.AppointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record PaymentScenarioContext(
        int PaymentId,
        int AppointmentId,
        string PatientUsername,
        string DoctorUsername,
        int PatientId,
        int DoctorId);

    private async Task<PaymentScenarioContext> SeedPaymentScenarioAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var patUsername = $"apat_{suffix}";
        var docUsername = $"adoc_{suffix}";

        // 1. Register Admin, login, create doctor profile
        var adminToken = await RegisterAndLoginAsync(
            $"pay_admin_{suffix}", $"pay.admin.{suffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(adminToken);

        var docPayload = new
        {
            FirstName = "Pay",
            LastName = $"Doctor_{suffix}",
            Email = $"pay.doctor.{suffix}@clinic.com",
            PhoneNumber = $"+38348{suffix}10",
            LicenseNumber = $"MED-PAY-{suffix}",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10
        };
        var docResponse = await Client.PostAsJsonAsync("/api/v1/doctors", docPayload);
        docResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var docResult = await DeserializeResponse<int>(docResponse);
        var doctorId = docResult!.Data;

        // 2. Register Doctor user
        await RegisterAndLoginAsync(docUsername, $"pay.doc.{suffix}@test.com", "SecurePass123!", "Doctor");
        ClearAuthToken();

        // 3. Register Patient user, login, create patient profile
        var patToken = await RegisterAndLoginAsync(patUsername, $"pay.pat.{suffix}@test.com", "SecurePass123!", "Patient");
        SetAuthToken(patToken);

        var patPayload = new
        {
            FirstName = "Pay",
            LastName = $"Patient_{suffix}",
            Email = $"pay.patient.{suffix}@test.com",
            PhoneNumber = $"+38349{suffix}10",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "20 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        };
        var patResponse = await Client.PostAsJsonAsync("/api/v1/patients", patPayload);
        patResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var patResult = await DeserializeResponse<int>(patResponse);
        var patientId = patResult!.Data;

        // 4. Re-login as patient, book appointment
        var patRelogin = await LoginAsync(patUsername, "SecurePass123!");
        SetAuthToken(patRelogin);

        var scheduledTime = GetNextWeekdayAt10Am();
        var bookPayload = new
        {
            PatientId = patientId,
            DoctorId = doctorId,
            ScheduledTime = scheduledTime.ToString("o"),
            Reason = "Integration test payment authorization check",
            AppointmentType = "Standard"
        };
        var bookResponse = await Client.PostAsJsonAsync("/api/v1/appointments", bookPayload);
        bookResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var bookResult = await DeserializeResponse<AppointmentDto>(bookResponse);
        var appointmentId = bookResult!.Data!.Id;

        // 5. Confirm as doctor
        var docRelogin = await LoginAsync(docUsername, "SecurePass123!");
        SetAuthToken(docRelogin);
        var confirmResponse = await Client.PutAsJsonAsync($"/api/v1/appointments/{appointmentId}/confirm",
            new { OverridePaymentRequirement = true });
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Create payment intent + process as patient
        var patRelogin2 = await LoginAsync(patUsername, "SecurePass123!");
        SetAuthToken(patRelogin2);

        var createIntentPayload = new { AppointmentId = appointmentId };
        var intentResponse = await Client.PostAsJsonAsync("/api/v1/payments/create-intent", createIntentPayload);
        intentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var intentResult = await DeserializeResponse<object>(intentResponse);

        // 7. Extract paymentIntentId from response using dynamic
        var intentData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(intentResponse.Content.ReadAsStringAsync().Result);
        var paymentIntentId = intentData.GetProperty("data").GetProperty("paymentIntentId").GetString()!;

        var processPayload = new { AppointmentId = appointmentId, PaymentIntentId = paymentIntentId };
        var processResponse = await Client.PostAsJsonAsync("/api/v1/payments/process", processPayload);
        processResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var processResult = await DeserializeResponse<int>(processResponse);

        return new PaymentScenarioContext(processResult!.Data, appointmentId, patUsername, docUsername, patientId, doctorId);
    }

    private async Task<string> CreateOtherPatientAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var username = $"other_pat_{suffix}";
        var token = await RegisterAndLoginAsync(username, $"other.pay.pat.{suffix}@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);
        var createResponse = await Client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Other",
            LastName = "PayPatient",
            Email = $"other.pay.patient.{suffix}@test.com",
            PhoneNumber = $"+38349{suffix}99",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "99 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        });
        var reloginToken = await LoginAsync(username, "SecurePass123!");
        SetAuthToken(reloginToken);
        return reloginToken;
    }

    private async Task<string> CreateOtherDoctorAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var username = $"other_doc_{suffix}";
        var adminSuffix = Guid.NewGuid().ToString("N")[..6];
        var adminToken = await RegisterAndLoginAsync(
            $"od_admin_{adminSuffix}", $"od.admin.{adminSuffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(adminToken);
        var docPayload = new
        {
            FirstName = "Other",
            LastName = "PayDoctor",
            Email = $"other.pay.doc.{suffix}@clinic.com",
            PhoneNumber = $"+38348{suffix}99",
            LicenseNumber = $"MED-OP-{suffix}",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        };
        await Client.PostAsJsonAsync("/api/v1/doctors", docPayload);
        var token = await RegisterAndLoginAsync(username, $"other.pay.doc.user.{suffix}@test.com", "SecurePass123!", "Doctor");
        SetAuthToken(token);
        return token;
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
}
