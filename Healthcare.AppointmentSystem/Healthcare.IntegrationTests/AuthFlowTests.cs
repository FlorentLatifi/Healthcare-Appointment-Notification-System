using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Presentation.API.Responses;

namespace Healthcare.IntegrationTests;

public sealed class AuthFlowTests : IntegrationTestBase
{
    public AuthFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Register_NewUser_Returns201()
    {
        var payload = new
        {
            Username = "testpatient",
            Email = "patient@test.com",
            Password = "SecurePass123!",
            Role = "Patient"
        };

        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await DeserializeResponse<int>(response);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns400()
    {
        var payload = new
        {
            Username = "duplicate_user",
            Email = "dup@test.com",
            Password = "SecurePass123!",
            Role = "Patient"
        };

        var first = await Client.PostAsJsonAsync("/api/v1/auth/register", payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync("/api/v1/auth/register", payload);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwtToken()
    {
        var username = "login_user";
        var email = "login@test.com";
        var password = "SecurePass123!";

        await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Username = username,
            Email = email,
            Password = password,
            Role = "Patient"
        });

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Username = username,
            Password = password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<LoginResponse>(response);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Token.Should().NotBeNullOrEmpty();
        result.Data.RefreshToken.Should().NotBeNullOrEmpty();
        result.Data.Role.Should().Be("Patient");
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns400()
    {
        var username = "wrongpass_user";
        await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Username = username,
            Email = "wrongpass@test.com",
            Password = "SecurePass123!",
            Role = "Patient"
        });

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Username = username,
            Password = "WrongPassword!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ReturnsUserInfo()
    {
        var token = await RegisterAndLoginAsync("me_user", "me@test.com", "SecurePass123!", "Patient");
        SetAuthToken(token);

        var response = await Client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
