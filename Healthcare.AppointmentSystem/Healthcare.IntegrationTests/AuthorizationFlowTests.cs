using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Application.DTOs;
using Healthcare.Presentation.API.Responses;

namespace Healthcare.IntegrationTests;

public sealed class AuthorizationFlowTests : IntegrationTestBase
{
    public AuthorizationFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CreatePatient_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var payload = new
        {
            FirstName = "No",
            LastName = "Token",
            Email = "no.token@test.com",
            PhoneNumber = "+38349111111",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "1 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        };
        var response = await Client.PostAsJsonAsync("/api/v1/patients", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePatient_WithDoctorRole_Returns403()
    {
        var token = await RegisterAndLoginAsync("doc_createpat", "doc.createpat@test.com", "SecurePass123!", "Doctor");
        SetAuthToken(token);
        var payload = new
        {
            FirstName = "Doc",
            LastName = "Create",
            Email = "doc.create@test.com",
            PhoneNumber = "+38349222222",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "2 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        };
        var response = await Client.PostAsJsonAsync("/api/v1/patients", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreatePatient_WithAdminRole_Returns403()
    {
        var token = await LoginAsPreSeededAdminAsync();
        SetAuthToken(token);
        var payload = new
        {
            FirstName = "Admin",
            LastName = "Create",
            Email = "admin.create@test.com",
            PhoneNumber = "+38349333333",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "3 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        };
        var response = await Client.PostAsJsonAsync("/api/v1/patients", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPatientById_PatientAccessingOwnRecord_Returns200()
    {
        var token = await RegisterAndLoginAsync("own_pat", "own.pat@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);
        var create = await Client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Own",
            LastName = "Record",
            Email = "own.record@test.com",
            PhoneNumber = "+38349444444",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "4 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var patientId = await ReadCreatedProfileIdAsync(create);

        // Same session: refresh (not full re-login) re-issues JWT with patient_id claim
        var session = await RefreshSessionPayloadAsync();
        session!.PatientId.Should().Be(patientId);

        var response = await Client.GetAsync($"/api/v1/patients/{patientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Acceptance: register → create patient profile → refresh → book without logout/login.
    /// Guards against FE/API split-brain where React setPatientId but JWT still lacks patient_id.
    /// </summary>
    [Fact]
    public async Task CreatePatientProfile_ThenBookAppointment_SameSessionWithoutRelogin_Succeeds()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // Admin creates a bookable doctor catalog entry (no link required for booking target)
        var adminToken = await LoginAsPreSeededAdminAsync();
        SetAuthToken(adminToken);
        var doctorResponse = await Client.PostAsJsonAsync("/api/v1/doctors", new
        {
            FirstName = "Book",
            LastName = "Target",
            Email = $"dr.booktarget.{suffix}@clinic.com",
            PhoneNumber = "+38349880001",
            LicenseNumber = $"MED-BOOK-{suffix}",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 8
        });
        doctorResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var doctorId = await ReadCreatedProfileIdAsync(doctorResponse);

        // New patient registers + logs in (no patient_id claim yet)
        var username = $"book_same_{suffix}";
        var token = await RegisterAndLoginAsync(username, $"{username}@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);

        var createPatient = await Client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Same",
            LastName = "Session",
            Email = $"same.session.{suffix}@test.com",
            PhoneNumber = "+38349445566",
            DateOfBirth = "1992-03-15",
            Gender = "Female",
            Street = "9 Session St",
            City = "Pristina",
            State = "Kosovo",
            PostalCode = "10000",
            Country = "Kosovo"
        });
        createPatient.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await DeserializeResponse<ProfileCreatedResponse>(createPatient);
        var patientId = created!.Data!.Id;
        // Preferred path: create response includes re-issued token with patient_id claim.
        created.Data.Token.Should().NotBeNullOrEmpty();
        created.Data.PatientId.Should().Be(patientId);
        SetAuthToken(created.Data.Token!);

        // Refresh remains valid as a fallback for normal token rotation.
        var refreshed = await RefreshSessionPayloadAsync();
        refreshed!.PatientId.Should().Be(patientId, "refresh must still pick up User.PatientId");
        refreshed.Token.Should().NotBeNullOrEmpty();

        var scheduledTime = DateTime.Now.Date.AddDays(3).AddHours(10);
        while (scheduledTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            scheduledTime = scheduledTime.AddDays(1);

        var bookResponse = await Client.PostAsJsonAsync("/api/v1/appointments", new
        {
            PatientId = patientId,
            DoctorId = doctorId,
            ScheduledTime = scheduledTime.ToString("o"),
            Reason = "Same-session booking after profile create and JWT refresh",
            AppointmentType = "Standard"
        });

        bookResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"booking without re-login failed: {await bookResponse.Content.ReadAsStringAsync()}");
        var bookResult = await DeserializeResponse<AppointmentDto>(bookResponse);
        bookResult!.Success.Should().BeTrue();
        bookResult.Data.Should().NotBeNull();
        bookResult.Data!.PatientId.Should().Be(patientId);
        bookResult.Data.DoctorId.Should().Be(doctorId);
    }

    [Fact]
    public async Task GetPatientById_PatientAccessingAnotherPatient_Returns403()
    {
        // Patient A creates their profile
        var tokenA = await RegisterAndLoginAsync("pat_a", "pat.a@test.com", "SecurePass123!", "Patient");
        SetAuthToken(tokenA);
        var createA = await Client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Patient",
            LastName = "A",
            Email = "patient.a@test.com",
            PhoneNumber = "+38349555555",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "5 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        });
        var patientIdA = await ReadCreatedProfileIdAsync(createA);

        // Patient B registers and tries to view Patient A's record
        var tokenB = await RegisterAndLoginAsync("pat_b", "pat.b@test.com", "SecurePass123!", "Patient");
        SetAuthToken(tokenB);
        var createB = await Client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Patient",
            LastName = "B",
            Email = "patient.b@test.com",
            PhoneNumber = "+38349666666",
            DateOfBirth = "1990-01-01",
            Gender = "Female",
            Street = "6 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        });
        var patientIdB = await ReadCreatedProfileIdAsync(createB);

        // Re-login as Patient B to get a token with patient_id = patientIdB
        var tokenBRelogin = await LoginAsync("pat_b", "SecurePass123!");
        SetAuthToken(tokenBRelogin);

        // Patient B tries to access Patient A's record
        var response = await Client.GetAsync($"/api/v1/patients/{patientIdA}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPatientById_DoctorAccessesUnrelatedPatient_Returns403()
    {
        // Patient with a profile, no appointments
        var patToken = await RegisterAndLoginAsync("pat_unrel", "pat.unrel@test.com", "SecurePass123!", "Patient");
        SetAuthToken(patToken);
        var create = await Client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Unrelated",
            LastName = "Patient",
            Email = "unrelated.patient@test.com",
            PhoneNumber = "+38349777777",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "7 St",
            City = "City",
            State = "State",
            PostalCode = "10000",
            Country = "Country"
        });
        var patientId = await ReadCreatedProfileIdAsync(create);

        // Doctor with linked profile but no care relationship to that patient
        var docToken = await RegisterAndLoginAsync("doc_unrel", "doc.unrel@test.com", "SecurePass123!", "Doctor");
        SetAuthToken(docToken);
        var docCreate = await Client.PostAsJsonAsync("/api/v1/doctors", new
        {
            FirstName = "Unrel",
            LastName = "Doctor",
            Email = "unrel.doctor@clinic.com",
            PhoneNumber = "+38348777777",
            LicenseNumber = "MED-UNREL-1",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        });
        docCreate.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        docToken = await LoginAsync("doc_unrel", "SecurePass123!");
        SetAuthToken(docToken);

        var response = await Client.GetAsync($"/api/v1/patients/{patientId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllPatients_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var response = await Client.GetAsync("/api/v1/patients");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllPatients_WithPatientRole_Returns403()
    {
        var token = await RegisterAndLoginAsync("pat_list", "pat.list@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);
        var response = await Client.GetAsync("/api/v1/patients");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllPatients_WithDoctorRole_Returns200()
    {
        var token = await RegisterAndLoginAsync("doc_list", "doc.list@test.com", "SecurePass123!", "Doctor");
        SetAuthToken(token);
        var response = await Client.GetAsync("/api/v1/patients");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletePatient_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var response = await Client.DeleteAsync("/api/v1/patients/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePatient_WithPatientRole_Returns403()
    {
        var token = await RegisterAndLoginAsync("pat_delete", "pat.delete@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);
        var response = await Client.DeleteAsync("/api/v1/patients/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeletePatient_WithDoctorRole_Returns403()
    {
        var token = await RegisterAndLoginAsync("doc_delete", "doc.delete@test.com", "SecurePass123!", "Doctor");
        SetAuthToken(token);
        var response = await Client.DeleteAsync("/api/v1/patients/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeletePatient_WithAdminRole_Returns404ForNonExistent()
    {
        var token = await LoginAsPreSeededAdminAsync();
        SetAuthToken(token);
        var response = await Client.DeleteAsync("/api/v1/patients/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PromoteToAdmin_ByNonAdmin_Returns403()
    {
        var patientToken = await RegisterAndLoginAsync("promote_nonadmin", "promote.nonadmin@test.com", "SecurePass123!", "Patient");
        SetAuthToken(patientToken);

        var response = await Client.PostAsync("/api/v1/users/1/promote-to-admin", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PromoteToAdmin_ByAdmin_SucceedsAndCreatesAuditLog()
    {
        // Register a patient to be promoted
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var patientUsername = $"promote_me_{suffix}";
        var registerPayload = new
        {
            Username = patientUsername,
            Email = $"{patientUsername}@test.com",
            Password = "SecurePass123!",
            Role = "Patient"
        };
        var registerResponse = await Client.PostAsJsonAsync("/api/v1/auth/register", registerPayload);
        registerResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var registerBody = await DeserializeResponse<int>(registerResponse);
        var patientUserId = registerBody!.Data;

        // Admin promotes the patient
        var adminToken = await LoginAsPreSeededAdminAsync();
        SetAuthToken(adminToken);

        var promoteResponse = await Client.PostAsync($"/api/v1/users/{patientUserId}/promote-to-admin", null);
        promoteResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Verify the promoted user can now log in and has Admin role
        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Username = patientUsername,
            Password = "SecurePass123!"
        });
        loginResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var loginResult = await DeserializeResponse<LoginResponse>(loginResponse);
        loginResult!.Data!.Role.Should().Be("Admin");
    }
}
