using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Healthcare.Presentation.API.Responses;

namespace Healthcare.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
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
}
