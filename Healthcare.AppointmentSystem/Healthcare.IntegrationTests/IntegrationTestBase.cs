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
        using var doc = JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data");
        if (data.ValueKind == JsonValueKind.Number)
            return data.GetInt32();
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("id", out var idEl))
            return idEl.GetInt32();
        throw new InvalidOperationException($"Could not parse created profile id from: {content}");
    }

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
        loginResponse.EnsureSuccessStatusCode();

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
        registerResponse.EnsureSuccessStatusCode();

        var loginPayload = new { Username = username, Password = password };
        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
        loginResponse.EnsureSuccessStatusCode();

        var result = await DeserializeResponse<LoginResponse>(loginResponse);
        return result!.Data!.Token;
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
