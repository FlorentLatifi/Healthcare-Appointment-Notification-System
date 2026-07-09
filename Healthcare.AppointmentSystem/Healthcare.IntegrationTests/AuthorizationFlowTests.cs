using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

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
        var token = await RegisterAndLoginAsync("admin_createpat", "admin.createpat@test.com", "SecurePass123!", "Admin");
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
        var created = await DeserializeResponse<int>(create);
        var patientId = created!.Data;

        // Re-login to get a token with patient_id claim
        var newToken = await LoginAsync("own_pat", "SecurePass123!");
        SetAuthToken(newToken);

        var response = await Client.GetAsync($"/api/v1/patients/{patientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var createdA = await DeserializeResponse<int>(createA);
        var patientIdA = createdA!.Data;

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
        var createdB = await DeserializeResponse<int>(createB);
        var patientIdB = createdB!.Data;

        // Re-login as Patient B to get a token with patient_id = patientIdB
        var tokenBRelogin = await LoginAsync("pat_b", "SecurePass123!");
        SetAuthToken(tokenBRelogin);

        // Patient B tries to access Patient A's record
        var response = await Client.GetAsync($"/api/v1/patients/{patientIdA}");
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
        var token = await RegisterAndLoginAsync("admin_delete", "admin.delete@test.com", "SecurePass123!", "Admin");
        SetAuthToken(token);
        var response = await Client.DeleteAsync("/api/v1/patients/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
