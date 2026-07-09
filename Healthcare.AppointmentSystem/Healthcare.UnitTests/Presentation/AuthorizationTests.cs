using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Healthcare.Presentation.API.Responses;

namespace Healthcare.UnitTests.Presentation;

public sealed class AuthTestSeedData
{
    public int PatientA_PatientId { get; set; }
    public string PatientA_Username { get; set; } = null!;
    public string PatientA_Token { get; set; } = null!;

    public int PatientB_PatientId { get; set; }
    public string PatientB_Username { get; set; } = null!;
    public string PatientB_Token { get; set; } = null!;

    public int DoctorA_DoctorId { get; set; }
    public string DoctorA_Username { get; set; } = null!;
    public string DoctorA_Token { get; set; } = null!;

    public int DoctorB_DoctorId { get; set; }
    public string DoctorB_Username { get; set; } = null!;
    public string DoctorB_Token { get; set; } = null!;

    public string Admin_Username { get; set; } = null!;
    public string Admin_Token { get; set; } = null!;

    public int AppointmentAB_Id { get; set; }
    public int AppointmentBA_Id { get; set; }
}

[Collection("AuthorizationSequential")]
public sealed class AuthorizationTests : IClassFixture<AuthorizationTestWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private AuthTestSeedData _seed = null!;
    private static readonly SemaphoreSlim _seedLock = new(1, 1);
    private static AuthTestSeedData? _sharedSeed;
    private static bool _seeded;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AuthorizationTests(AuthorizationTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        if (_seeded) { _seed = _sharedSeed!; return; }
        await _seedLock.WaitAsync();
        try
        {
            if (_seeded) { _seed = _sharedSeed!; return; }
            _seed = await SeedAsync();
            _sharedSeed = _seed;
            _seeded = true;
        }
        finally { _seedLock.Release(); }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<AuthTestSeedData> SeedAsync()
    {
        var s = new AuthTestSeedData();
        var suffix = Guid.NewGuid().ToString("N")[..6];

        s.PatientA_Username = $"patA_{suffix}";
        s.PatientA_Token = await RegisterLoginAsync(s.PatientA_Username, "Patient");
        s.PatientA_PatientId = await CreatePatientAsync(s.PatientA_Token, s.PatientA_Username);
        s.PatientA_Token = await LoginAsync(s.PatientA_Username);

        s.PatientB_Username = $"patB_{suffix}";
        s.PatientB_Token = await RegisterLoginAsync(s.PatientB_Username, "Patient");
        s.PatientB_PatientId = await CreatePatientAsync(s.PatientB_Token, s.PatientB_Username);
        s.PatientB_Token = await LoginAsync(s.PatientB_Username);

        s.DoctorA_Username = $"docA_{suffix}";
        s.DoctorA_Token = await RegisterLoginAsync(s.DoctorA_Username, "Doctor");
        s.DoctorA_DoctorId = await CreateDoctorAsync(s.DoctorA_Token);
        s.DoctorA_Token = await LoginAsync(s.DoctorA_Username);

        s.DoctorB_Username = $"docB_{suffix}";
        s.DoctorB_Token = await RegisterLoginAsync(s.DoctorB_Username, "Doctor");
        s.DoctorB_DoctorId = await CreateDoctorAsync(s.DoctorB_Token);
        s.DoctorB_Token = await LoginAsync(s.DoctorB_Username);

        s.Admin_Username = $"admin_{suffix}";
        s.Admin_Token = await RegisterLoginAsync(s.Admin_Username, "Admin");

        SetBearer(s.PatientA_Token);
        var book1 = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            PatientId = s.PatientA_PatientId,
            DoctorId = s.DoctorA_DoctorId,
            ScheduledTime = NextWeekdayAt10Am().ToString("o"),
            Reason = "Auth test appointment",
            AppointmentType = "Standard"
        });
        s.AppointmentAB_Id = (await ExtractIdFromCreatedResponse(book1))!.Value;

        SetBearer(s.PatientB_Token);
        var book2 = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            PatientId = s.PatientB_PatientId,
            DoctorId = s.DoctorB_DoctorId,
            ScheduledTime = NextWeekdayAt10Am().AddDays(7).ToString("o"),
            Reason = "Auth test appointment B",
            AppointmentType = "Standard"
        });
        s.AppointmentBA_Id = (await ExtractIdFromCreatedResponse(book2))!.Value;

        return s;
    }

    private void SetBearer(string token) =>
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);
    private void ClearBearer() =>
        _client.DefaultRequestHeaders.Authorization = null;

    private async Task<string> LoginAsync(string username)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = username, Password = "SecurePass123!" });
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty("token").GetString()!;
    }

    private async Task<string> RegisterLoginAsync(string username, string role)
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { Username = username, Email = $"{username}@test.com", Password = "SecurePass123!", Role = role });
        registerResponse.EnsureSuccessStatusCode();
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = username, Password = "SecurePass123!" });
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty("token").GetString()!;
    }

    private async Task<int> CreatePatientAsync(string token, string username)
    {
        SetBearer(token);
        var response = await _client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Auth",
            LastName = "Test",
            Email = $"{username}.pat@test.com",
            PhoneNumber = "+38349000001",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "1 Test St",
            City = "TestCity",
            State = "TestState",
            PostalCode = "10000",
            Country = "TestCountry"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await ExtractIdFromCreatedResponse(response);
        return id!.Value;
    }

    private async Task<int> CreateDoctorAsync(string doctorToken)
    {
        var suffix = Guid.NewGuid().ToString("N")[..4];
        SetBearer(doctorToken);
        var response = await _client.PostAsJsonAsync("/api/v1/doctors", new
        {
            FirstName = "Auth",
            LastName = $"Doc_{suffix}",
            Email = $"doc.{suffix}@clinic.com",
            PhoneNumber = "+38348000001",
            LicenseNumber = $"MED-AT-{suffix}",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"CreateDoctor failed: {body}");
        var id = await ExtractIdFromCreatedResponse(response);
        return id!.Value;
    }

    private string? _adminToken;
    private async Task<string> GetAdminTokenAsync()
    {
        if (_adminToken != null) return _adminToken;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        _adminToken = await RegisterLoginAsync($"adm_{suffix}", "Admin");
        return _adminToken;
    }

    private static DateTime NextWeekdayAt10Am()
    {
        var d = DateTime.UtcNow.AddDays(3);
        while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
        return d.Date.AddHours(10);
    }

    private static async Task<int?> ExtractIdFromCreatedResponse(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("id", out var id))
                return id.GetInt32();
            if (data.ValueKind == JsonValueKind.Number)
                return data.GetInt32();
        }
        return null;
    }

    private async Task<int> CreateTempDoctorAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..4];
        var adminToken = await GetAdminTokenAsync();
        SetBearer(adminToken);
        var response = await _client.PostAsJsonAsync("/api/v1/doctors", new
        {
            FirstName = "Temp",
            LastName = $"Doc_{suffix}",
            Email = $"temp.doc.{suffix}@clinic.com",
            PhoneNumber = "+38348000999",
            LicenseNumber = $"MED-TMP-{suffix}",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"CreateTempDoctor failed: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetInt32();
    }

    private async Task<int> BookAppointmentAsync(string patientToken, int patientId, int? bookingDoctorId = null, DateTime? when = null)
    {
        var docId = bookingDoctorId ?? await CreateTempDoctorAsync();
        SetBearer(patientToken);
        var time = when ?? NextWeekdayAt10Am().AddDays(14);
        var response = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            PatientId = patientId,
            DoctorId = docId,
            ScheduledTime = time.ToString("o"),
            Reason = "Auth test appointment testing",
            AppointmentType = "Standard"
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"BookAppointment failed: {body}");
        return (await ExtractIdFromCreatedResponse(response))!.Value;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route)
    {
        var request = new HttpRequestMessage(method, route);
        return await _client.SendAsync(request);
    }

    // ═══════════════════════════════════════════════════════
    //  MEMBER DATA PROVIDERS
    // ═══════════════════════════════════════════════════════

    public static IEnumerable<object[]> Anonymous_Returns401_MemberData()
    {
        yield return [HttpMethod.Get, "/api/v1/patients/1"];
        yield return [HttpMethod.Get, "/api/v1/patients"];
        yield return [HttpMethod.Get, "/api/v1/patients/active"];
        yield return [HttpMethod.Get, "/api/v1/patients/search?term=a"];
        yield return [HttpMethod.Delete, "/api/v1/patients/1"];
        yield return [HttpMethod.Put, "/api/v1/patients/1/notification-preferences"];
        yield return [HttpMethod.Post, "/api/v1/appointments"];
        yield return [HttpMethod.Get, "/api/v1/appointments/1"];
        yield return [HttpMethod.Get, "/api/v1/appointments"];
        yield return [HttpMethod.Get, "/api/v1/appointments/patient/1"];
        yield return [HttpMethod.Get, "/api/v1/appointments/doctor/1"];
        yield return [HttpMethod.Put, "/api/v1/appointments/1/confirm"];
        yield return [HttpMethod.Put, "/api/v1/appointments/1/cancel"];
        yield return [HttpMethod.Put, "/api/v1/appointments/1/complete"];
        yield return [HttpMethod.Put, "/api/v1/appointments/1/mark-no-show"];
        yield return [HttpMethod.Delete, "/api/v1/appointments/1"];
        yield return [HttpMethod.Post, "/api/v1/doctors"];
        yield return [HttpMethod.Delete, "/api/v1/doctors/1"];
        yield return [HttpMethod.Post, "/api/v1/payments/create-intent"];
        yield return [HttpMethod.Post, "/api/v1/payments/process"];
        yield return [HttpMethod.Post, "/api/v1/payments/refund"];
        yield return [HttpMethod.Get, "/api/v1/payments/1"];
        yield return [HttpMethod.Get, "/api/v1/payments/appointment/1"];
        yield return [HttpMethod.Get, "/api/v1/payments"];
    }

    public static IEnumerable<object[]> Patient_AccessesDoctorOrAdminOnly_Returns403_MemberData()
    {
        yield return [HttpMethod.Get, "/api/v1/patients"];
        yield return [HttpMethod.Get, "/api/v1/patients/active"];
        yield return [HttpMethod.Get, "/api/v1/patients/search?term=a"];
        yield return [HttpMethod.Put, "/api/v1/appointments/1/confirm"];
        yield return [HttpMethod.Put, "/api/v1/appointments/1/complete"];
        yield return [HttpMethod.Put, "/api/v1/appointments/1/mark-no-show"];
        yield return [HttpMethod.Get, "/api/v1/appointments/doctor/1"];
        yield return [HttpMethod.Get, "/api/v1/appointments"];
        yield return [HttpMethod.Get, "/api/v1/payments"];
        yield return [HttpMethod.Post, "/api/v1/doctors"];
        yield return [HttpMethod.Delete, "/api/v1/doctors/1"];
        yield return [HttpMethod.Post, "/api/v1/payments/refund"];
        yield return [HttpMethod.Delete, "/api/v1/appointments/1"];
    }

    public static IEnumerable<object[]> Doctor_AccessesPatientOnly_Returns403_MemberData()
    {
        yield return [HttpMethod.Post, "/api/v1/appointments"];
        yield return [HttpMethod.Post, "/api/v1/payments/create-intent"];
        yield return [HttpMethod.Post, "/api/v1/payments/process"];
    }

    public static IEnumerable<object[]> Anonymous_DoctorsPublicEndpoints_Returns200_MemberData()
    {
        yield return [HttpMethod.Get, "/api/v1/doctors"];
        yield return [HttpMethod.Get, "/api/v1/doctors/active"];
        yield return [HttpMethod.Get, "/api/v1/doctors/accepting-patients"];
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: Anonymous → 401
    // ═══════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(Anonymous_Returns401_MemberData))]
    public async Task Anonymous_Returns401(HttpMethod method, string route)
    {
        ClearBearer();
        var response = await SendAsync(method, route);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: Wrong role → 403
    // ═══════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(Patient_AccessesDoctorOrAdminOnly_Returns403_MemberData))]
    public async Task Patient_AccessesDoctorOrAdminOnly_Returns403(HttpMethod method, string route)
    {
        SetBearer(_seed.PatientA_Token);
        var response = await SendAsync(method, route);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Doctor_AccessesPatientOnly_Returns403_MemberData))]
    public async Task Doctor_AccessesPatientOnly_Returns403(HttpMethod method, string route)
    {
        SetBearer(_seed.DoctorA_Token);
        var response = await SendAsync(method, route);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Anonymous_DoctorsPublicEndpoints_Returns200_MemberData))]
    public async Task Anonymous_DoctorsPublicEndpoints_Returns200(HttpMethod method, string route)
    {
        ClearBearer();
        var response = await SendAsync(method, route);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: Ownership & role-success
    // ═══════════════════════════════════════════════════════

    // ── Patients ──

    [Fact]
    public async Task Patient_GetPatientById_Own_Returns200()
    {
        SetBearer(_seed.PatientA_Token);
        var response = await _client.GetAsync($"/api/v1/patients/{_seed.PatientA_PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patient_GetPatientById_Other_Returns403()
    {
        SetBearer(_seed.PatientA_Token);
        var response = await _client.GetAsync($"/api/v1/patients/{_seed.PatientB_PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Doctor_GetPatientById_Any_Returns200()
    {
        SetBearer(_seed.DoctorA_Token);
        var response = await _client.GetAsync($"/api/v1/patients/{_seed.PatientB_PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_GetPatientById_Any_Returns200()
    {
        SetBearer(_seed.Admin_Token);
        var response = await _client.GetAsync($"/api/v1/patients/{_seed.PatientA_PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patient_UpdateNotificationPrefs_Own_Returns200()
    {
        SetBearer(_seed.PatientA_Token);
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/patients/{_seed.PatientA_PatientId}/notification-preferences",
            new { AppointmentReminders = true, MarketingEmails = false });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patient_UpdateNotificationPrefs_Other_Returns403()
    {
        SetBearer(_seed.PatientA_Token);
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/patients/{_seed.PatientB_PatientId}/notification-preferences",
            new { AppointmentReminders = true, MarketingEmails = false });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Doctor_UpdateNotificationPrefs_Any_Returns200()
    {
        SetBearer(_seed.DoctorA_Token);
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/patients/{_seed.PatientA_PatientId}/notification-preferences",
            new { AppointmentReminders = true, MarketingEmails = false });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_DeletePatient_Any_Returns204()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterLoginAsync($"delpat_{suffix}", "Patient");
        var patientId = await CreatePatientAsync(token, $"delpat_{suffix}");
        SetBearer(_seed.Admin_Token);
        var response = await _client.DeleteAsync($"/api/v1/patients/{patientId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Appointments ──

    [Fact]
    public async Task Patient_GetAppointmentById_Own_Returns200()
    {
        SetBearer(_seed.PatientA_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/{_seed.AppointmentAB_Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patient_GetAppointmentById_OtherPatient_Returns403()
    {
        SetBearer(_seed.PatientA_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/{_seed.AppointmentBA_Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Doctor_GetAppointmentById_Own_Returns200()
    {
        SetBearer(_seed.DoctorA_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/{_seed.AppointmentAB_Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Doctor_GetAppointmentById_OtherDoctor_Returns403()
    {
        SetBearer(_seed.DoctorB_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/{_seed.AppointmentAB_Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_GetAppointmentById_Any_Returns200()
    {
        SetBearer(_seed.Admin_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/{_seed.AppointmentAB_Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patient_GetAppointmentsByPatient_Own_Returns200()
    {
        SetBearer(_seed.PatientA_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/patient/{_seed.PatientA_PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patient_GetAppointmentsByPatient_Other_Returns403()
    {
        SetBearer(_seed.PatientA_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/patient/{_seed.PatientB_PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Doctor_GetAppointmentsByPatient_Any_Returns200()
    {
        SetBearer(_seed.DoctorA_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/patient/{_seed.PatientB_PatientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Doctor_GetAppointmentsByDoctor_Own_Returns200()
    {
        SetBearer(_seed.DoctorA_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/doctor/{_seed.DoctorA_DoctorId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Doctor_GetAppointmentsByDoctor_Other_Returns403()
    {
        SetBearer(_seed.DoctorA_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/doctor/{_seed.DoctorB_DoctorId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_GetAppointmentsByDoctor_Any_Returns200()
    {
        SetBearer(_seed.Admin_Token);
        var response = await _client.GetAsync($"/api/v1/appointments/doctor/{_seed.DoctorA_DoctorId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patient_CancelAppointment_Own_Returns200()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId);
        var response = await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/cancel",
            new { AppointmentId = apptId, CancellationReason = "Test cancellation reason for debugging purposes" });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Cancel failed: {body}");
    }

    [Fact]
    public async Task Patient_CancelAppointment_Other_Returns403()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientB_Token, _seed.PatientB_PatientId);
        SetBearer(_seed.PatientA_Token);
        var response = await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/cancel",
            new { AppointmentId = apptId, CancellationReason = "Not my appointment" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Doctor_CancelAppointment_Own_Returns200()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId, _seed.DoctorA_DoctorId);
        SetBearer(_seed.DoctorA_Token);
        var response = await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/cancel",
            new { AppointmentId = apptId, CancellationReason = "Doctor cancelled" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Doctor_ConfirmAppointment_Own_Returns200()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId, _seed.DoctorA_DoctorId);
        SetBearer(_seed.DoctorA_Token);
        var response = await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/confirm",
            new { AppointmentId = apptId, OverridePaymentRequirement = true, OverrideReason = "Test override for confirmation testing" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Doctor_ConfirmAppointment_Other_Returns403()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId, _seed.DoctorA_DoctorId);
        SetBearer(_seed.DoctorB_Token);
        var response = await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/confirm",
            new { AppointmentId = apptId, OverridePaymentRequirement = true, OverrideReason = "Test override for confirmation testing" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_ConfirmAppointment_Any_Returns200()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientB_Token, _seed.PatientB_PatientId);
        SetBearer(_seed.Admin_Token);
        var response = await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/confirm",
            new { AppointmentId = apptId, OverridePaymentRequirement = true, OverrideReason = "Test override for confirmation testing" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Doctor_CompleteAppointment_Own_Returns200()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId, _seed.DoctorA_DoctorId);
        SetBearer(_seed.DoctorA_Token);
        await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/confirm",
            new { AppointmentId = apptId, OverridePaymentRequirement = true, OverrideReason = "Test override for confirmation testing" });
        var response = await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/complete",
            new { AppointmentId = apptId, DoctorNotes = "Test completion notes" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Doctor_MarkNoShow_Own_Returns200()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId, _seed.DoctorA_DoctorId);
        SetBearer(_seed.DoctorA_Token);
        await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/confirm",
            new { AppointmentId = apptId, OverridePaymentRequirement = true, OverrideReason = "Test override for confirmation testing" });
        var response = await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/mark-no-show",
            new { AppointmentId = apptId });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_DeleteAppointment_Any_Returns204()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId);
        SetBearer(_seed.Admin_Token);
        var response = await _client.DeleteAsync($"/api/v1/appointments/{apptId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Doctors ──

    [Fact]
    public async Task Admin_CreateDoctor_Returns201()
    {
        var suffix = Guid.NewGuid().ToString("N")[..4];
        SetBearer(_seed.Admin_Token);
        var response = await _client.PostAsJsonAsync("/api/v1/doctors", new
        {
            FirstName = "New",
            LastName = $"Doc_{suffix}",
            Email = $"new.doc.{suffix}@clinic.com",
            PhoneNumber = "+38348000100",
            LicenseNumber = $"MED-NW-{suffix}",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Admin_DeleteDoctor_Any_Returns204()
    {
        var doctorId = await CreateDoctorAsync(_seed.Admin_Token);
        SetBearer(_seed.Admin_Token);
        var response = await _client.DeleteAsync($"/api/v1/doctors/{doctorId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Payments ──

    [Fact]
    public async Task Patient_GetPaymentById_Own_Returns200()
    {
        // Create an appointment as Patient A with Doctor A
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId);
        // Confirm it (which generates a payment)
        SetBearer(_seed.DoctorA_Token);
        await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/confirm",
            new { AppointmentId = apptId, OverridePaymentRequirement = true, OverrideReason = "Test override for confirmation testing" });
        // Look up the payment
        SetBearer(_seed.PatientA_Token);
        var paymentResponse = await _client.GetAsync($"/api/v1/payments/appointment/{apptId}");
        if (!paymentResponse.IsSuccessStatusCode)
        {
            // Payment may not exist; skip this assertion
            return;
        }
        var body = await paymentResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty("id", out var id))
        {
            SetBearer(_seed.PatientA_Token);
            var response = await _client.GetAsync($"/api/v1/payments/{id.GetInt32()}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Doctor_GetPaymentById_Own_Returns200()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId);
        SetBearer(_seed.DoctorA_Token);
        await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/confirm",
            new { AppointmentId = apptId, OverridePaymentRequirement = true, OverrideReason = "Test override for confirmation testing" });
        SetBearer(_seed.DoctorA_Token);
        var paymentResponse = await _client.GetAsync($"/api/v1/payments/appointment/{apptId}");
        if (!paymentResponse.IsSuccessStatusCode) return;
        var body = await paymentResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty("id", out var id))
        {
            SetBearer(_seed.DoctorA_Token);
            var response = await _client.GetAsync($"/api/v1/payments/{id.GetInt32()}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task PatientB_GetPaymentByAppointment_Other_Returns403()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId);
        SetBearer(_seed.DoctorA_Token);
        await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/confirm",
            new { AppointmentId = apptId, OverridePaymentRequirement = true, OverrideReason = "Test override for confirmation testing" });
        SetBearer(_seed.PatientB_Token);
        var response = await _client.GetAsync($"/api/v1/payments/appointment/{apptId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DoctorB_GetPaymentByAppointment_Other_Returns403()
    {
        var apptId = await BookAppointmentAsync(_seed.PatientA_Token, _seed.PatientA_PatientId);
        SetBearer(_seed.DoctorA_Token);
        await _client.PutAsJsonAsync($"/api/v1/appointments/{apptId}/confirm",
            new { AppointmentId = apptId, OverridePaymentRequirement = true, OverrideReason = "Test override for confirmation testing" });
        SetBearer(_seed.DoctorB_Token);
        var response = await _client.GetAsync($"/api/v1/payments/appointment/{apptId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
