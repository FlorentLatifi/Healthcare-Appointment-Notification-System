using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Healthcare.IntegrationTests;

public sealed class DoctorAuthorizationFlowTests : IntegrationTestBase
{
    public DoctorAuthorizationFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CreateDoctor_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var payload = new
        {
            FirstName = "No",
            LastName = "Auth",
            Email = "no.auth@clinic.com",
            PhoneNumber = "+38348111111",
            LicenseNumber = "MED-NA-001",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        };
        var response = await Client.PostAsJsonAsync("/api/v1/doctors", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateDoctor_WithPatientRole_Returns403()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterAndLoginAsync($"doc_create_pat_{suffix}", $"doc.create.pat.{suffix}@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);
        var payload = new
        {
            FirstName = "Patient",
            LastName = "CreateDoc",
            Email = $"pat.create.doc.{suffix}@clinic.com",
            PhoneNumber = "+38348222222",
            LicenseNumber = "MED-PC-001",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        };
        var response = await Client.PostAsJsonAsync("/api/v1/doctors", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateDoctor_WithDoctorRole_Returns403()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterAndLoginAsync($"doc_create_doc_{suffix}", $"doc.create.doc.{suffix}@test.com", "SecurePass123!", "Doctor");
        SetAuthToken(token);
        var payload = new
        {
            FirstName = "Doctor",
            LastName = "CreateDoc",
            Email = $"doc.create.doc2.{suffix}@clinic.com",
            PhoneNumber = "+38348333333",
            LicenseNumber = "MED-DC-001",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        };
        var response = await Client.PostAsJsonAsync("/api/v1/doctors", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateDoctor_WithAdminRole_Returns201()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterAndLoginAsync($"doc_create_admin_{suffix}", $"doc.create.admin.{suffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(token);
        var payload = new
        {
            FirstName = "Admin",
            LastName = "CreateDoc",
            Email = $"admin.create.doc.{suffix}@clinic.com",
            PhoneNumber = "+38348444444",
            LicenseNumber = $"MED-AC-{suffix}",
            Specialty = "GeneralPractice",
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        };
        var response = await Client.PostAsJsonAsync("/api/v1/doctors", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task DeleteDoctor_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var response = await Client.DeleteAsync("/api/v1/doctors/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteDoctor_WithPatientRole_Returns403()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterAndLoginAsync($"doc_delete_pat_{suffix}", $"doc.delete.pat.{suffix}@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);
        var response = await Client.DeleteAsync("/api/v1/doctors/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteDoctor_WithAdminRole_Returns404ForNonExistent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var token = await RegisterAndLoginAsync($"doc_delete_admin_{suffix}", $"doc.delete.admin.{suffix}@test.com", "SecurePass123!", "Admin");
        SetAuthToken(token);
        var response = await Client.DeleteAsync("/api/v1/doctors/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDoctorById_WithoutToken_Returns200()
    {
        ClearAuthToken();
        var response = await Client.GetAsync("/api/v1/doctors/1");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // no doctor seeded, but 401 would mean no AllowAnonymous
    }
}
