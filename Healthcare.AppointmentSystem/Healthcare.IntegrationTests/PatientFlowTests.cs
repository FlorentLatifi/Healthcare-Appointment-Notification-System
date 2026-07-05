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
        var response = await Client.GetAsync("/api/v1/patients/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
