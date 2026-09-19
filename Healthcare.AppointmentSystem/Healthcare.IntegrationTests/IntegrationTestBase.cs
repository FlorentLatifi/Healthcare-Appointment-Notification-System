using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Healthcare.Presentation.API.Responses;
// JsonDocument used by ReadCreatedProfileIdAsync

namespace Healthcare.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    protected async Task<ApiResponse<T>?> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(content, JsonOptions);
    }

    /// <summary>
    /// Reads profile id from Create Patient/Doctor responses (supports legacy bare int and
    /// <c>ProfileCreatedResponse</c> with <c>id</c> + optional session token).
    /// </summary>
    protected static async Task<int> ReadCreatedProfileIdAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Profile creation returned {(int)response.StatusCode} ({response.StatusCode}): {content}");

        using var doc = JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data");
        if (data.ValueKind == JsonValueKind.Number)
            return data.GetInt32();
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("id", out var idEl))
            return idEl.GetInt32();
        throw new InvalidOperationException($"Could not parse created profile id from: {content}");
    }

    /// <summary>
    /// A phone number the API's validator accepts: the prefix followed by eight random digits.
    /// Built from digits rather than a GUID suffix, whose hex letters fail validation.
    /// </summary>
    protected static string UniquePhoneNumber(string prefix) =>
        $"{prefix}{Random.Shared.Next(0, 100_000_000):D8}";

    protected const string PreSeededAdminUsername = "testadmin";
    protected const string PreSeededAdminPassword = "SecurePass123!";

    protected async Task<string> LoginAsPreSeededAdminAsync()
    {
        return await LoginAsync(PreSeededAdminUsername, PreSeededAdminPassword);
    }

    protected async Task<string> LoginAsync(
        string username,
        string password)
    {
        var loginPayload = new { Username = username, Password = password };
        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
        await EnsureSuccessWithBodyAsync(loginResponse);

        var result = await DeserializeResponse<LoginResponse>(loginResponse);
        return result!.Data!.Token;
    }

    protected async Task<string> RegisterAndLoginAsync(
        string username,
        string email,
        string password,
        string role)
    {
        var registerPayload = new { Username = username, Email = email, Password = password, Role = role };
        var registerResponse = await Client.PostAsJsonAsync("/api/v1/auth/register", registerPayload);
        await EnsureSuccessWithBodyAsync(registerResponse);

        var loginPayload = new { Username = username, Password = password };
        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
        await EnsureSuccessWithBodyAsync(loginResponse);

        var result = await DeserializeResponse<LoginResponse>(loginResponse);
        return result!.Data!.Token;
    }

    /// <summary>
    /// Like <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, but the failure names the
    /// endpoint and includes the response body, which is where the API explains what it rejected.
    /// </summary>
    protected static async Task EnsureSuccessWithBodyAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath} " +
            $"returned {(int)response.StatusCode} ({response.StatusCode}): {body}",
            inner: null,
            statusCode: response.StatusCode);
    }

    protected const string TestPassword = "SecurePass123!";

    /// <summary>
    /// Registers a Doctor account and creates the doctor profile as that account, which is what
    /// links the two (an admin-created profile is not attached to any account). Leaves the client
    /// unauthenticated.
    /// </summary>
    protected async Task<(string Username, int DoctorId)> CreateDoctorAccountAsync(
        string specialty = "GeneralPractice")
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"doc_{suffix}";
        SetAuthToken(await RegisterAndLoginAsync(username, $"doc.{suffix}@test.com", TestPassword, "Doctor"));

        var response = await Client.PostAsJsonAsync("/api/v1/doctors", new
        {
            FirstName = "Test",
            LastName = $"Doctor_{suffix}",
            Email = $"doctor.{suffix}@clinic.com",
            PhoneNumber = UniquePhoneNumber("+38348"),
            LicenseNumber = $"MED-{suffix}",
            Specialty = specialty,
            ConsultationFeeAmount = 50.00m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10,
        });
        var doctorId = await ReadCreatedProfileIdAsync(response);

        ClearAuthToken();
        return (username, doctorId);
    }

    /// <summary>
    /// Registers a Patient account and creates its profile. The returned token comes from a fresh
    /// login, so it carries the patient_id claim that booking and record access depend on.
    /// Leaves the client unauthenticated.
    /// </summary>
    protected async Task<(string Username, int PatientId, string Token)> CreatePatientAccountAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"pat_{suffix}";
        SetAuthToken(await RegisterAndLoginAsync(username, $"pat.{suffix}@test.com", TestPassword, "Patient"));

        var response = await Client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Test",
            LastName = $"Patient_{suffix}",
            Email = $"patient.{suffix}@test.com",
            PhoneNumber = UniquePhoneNumber("+38349"),
            DateOfBirth = "1990-01-01",
            Gender = "Female",
            Street = "1 Test St",
            City = "Pristina",
            State = "Kosovo",
            PostalCode = "10000",
            Country = "Kosovo",
        });
        var patientId = await ReadCreatedProfileIdAsync(response);

        ClearAuthToken();
        var token = await LoginAsync(username, TestPassword);
        return (username, patientId, token);
    }

    protected void SetAuthToken(string token)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    protected void ClearAuthToken()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Re-issues the access token from the httpOnly refresh cookie (same path as the SPA after profile create).
    /// Reloads User from DB so patient_id / doctor_id claims match the newly linked profile.
    /// </summary>
    protected async Task<string> RefreshSessionAsync()
    {
        var payload = await RefreshSessionPayloadAsync();
        return payload!.Token;
    }

    protected async Task<LoginResponse?> RefreshSessionPayloadAsync()
    {
        var refreshResponse = await Client.PostAsync("/api/v1/auth/refresh", content: null);
        if (!refreshResponse.IsSuccessStatusCode)
        {
            var body = await refreshResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Token refresh failed with {(int)refreshResponse.StatusCode}: {body}");
        }

        var result = await DeserializeResponse<LoginResponse>(refreshResponse);
        SetAuthToken(result!.Data!.Token);
        return result.Data;
    }
}
