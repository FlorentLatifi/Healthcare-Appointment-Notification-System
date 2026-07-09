using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Application.DTOs;
using Healthcare.Presentation.API.Responses;

namespace Healthcare.IntegrationTests;

public sealed class PatientFlowTests : IntegrationTestBase
{
    public PatientFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CreatePatient_ValidData_Returns201()
    {
        var token = await RegisterAndLoginAsync("pat_create", "pat.create@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);

        var payload = new
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@test.com",
            PhoneNumber = "+38349123456",
            DateOfBirth = "1990-05-15",
            Gender = "Male",
            Street = "123 Main St",
            City = "Pristina",
            State = "Kosovo",
            PostalCode = "10000",
            Country = "Kosovo"
        };

        var response = await Client.PostAsJsonAsync("/api/v1/patients", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await DeserializeResponse<int>(response);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreatePatient_DuplicateEmail_Returns400()
    {
        var token = await RegisterAndLoginAsync("pat_dup", "pat.dup@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);

        var payload = new
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@test.com",
            PhoneNumber = "+38349987654",
            DateOfBirth = "1992-08-20",
            Gender = "Female",
            Street = "456 Elm St",
            City = "Pristina",
            State = "Kosovo",
            PostalCode = "10000",
            Country = "Kosovo"
        };

        var first = await Client.PostAsJsonAsync("/api/v1/patients", payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync("/api/v1/patients", payload);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPatientById_ExistingPatient_ReturnsPatient()
    {
        var token = await RegisterAndLoginAsync("pat_get", "pat.get@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);

        var createPayload = new
        {
            FirstName = "Get",
            LastName = "Patient",
            Email = "get.patient@test.com",
            PhoneNumber = "+38349111111",
            DateOfBirth = "1985-03-10",
            Gender = "Male",
            Street = "789 Oak St",
            City = "Pristina",
            State = "Kosovo",
            PostalCode = "10000",
            Country = "Kosovo"
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/patients", createPayload);
        var createResult = await DeserializeResponse<int>(createResponse);
        var patientId = createResult!.Data;

        var getResponse = await Client.GetAsync($"/api/v1/patients/{patientId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await DeserializeResponse<PatientDto>(getResponse);
        getResult.Should().NotBeNull();
        getResult!.Success.Should().BeTrue();
        getResult.Data.Should().NotBeNull();
        getResult.Data!.Email.Should().Be("get.patient@test.com");
    }

    [Fact]
    public async Task GetPatientById_NonExisting_Returns404()
    {
        var token = await RegisterAndLoginAsync("doc_get404", "doc.get404@test.com", "SecurePass123!", "Doctor");
        SetAuthToken(token);

        var response = await Client.GetAsync("/api/v1/patients/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePatient_WithoutAuth_Returns401()
    {
        ClearAuthToken();

        var payload = new
        {
            FirstName = "Unauth",
            LastName = "User",
            Email = "unauth@test.com",
            PhoneNumber = "+38349999999",
            DateOfBirth = "1990-01-01",
            Gender = "Male",
            Street = "1 Main St",
            City = "Pristina",
            State = "Kosovo",
            PostalCode = "10000",
            Country = "Kosovo"
        };

        var response = await Client.PostAsJsonAsync("/api/v1/patients", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
