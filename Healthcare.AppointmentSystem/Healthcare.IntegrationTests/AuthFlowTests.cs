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
        result.Data.Role.Should().Be("Patient");
        result.Data.Username.Should().Be(username);
        // New patient accounts are unlinked until they create a profile.
        result.Data.PatientId.Should().BeNull();
        result.Data.DoctorId.Should().BeNull();

        var setCookieHeader = Assert.Single(
            response.Headers.GetValues("Set-Cookie"));
        setCookieHeader.Should().Contain("refreshToken");
        // ASP.NET emits attribute names in various casings (HttpOnly vs httponly).
        setCookieHeader.Should().MatchRegex("(?i)httponly");
        setCookieHeader.Should().MatchRegex("(?i)samesite=lax");
        setCookieHeader.Should().MatchRegex("(?i)path=/api/v1");
        // Secure only when the request is HTTPS; WebApplicationFactory often uses HTTP.
        if (response.RequestMessage?.RequestUri?.Scheme == Uri.UriSchemeHttps)
            setCookieHeader.Should().MatchRegex("(?i)secure");
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

    [Fact]
    public async Task Register_WithAdminRole_Returns400()
    {
        var payload = new
        {
            Username = "wannabe_admin",
            Email = "wannabe.admin@test.com",
            Password = "SecurePass123!",
            Role = "Admin"
        };

        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Verify no user was created with that username
        var loginPayload = new { Username = "wannabe_admin", Password = "SecurePass123!" };
        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_SetsRefreshCookie_AndRefreshEndpointUsesIt()
    {
        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Username = PreSeededAdminUsername,
            Password = PreSeededAdminPassword
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResult = await DeserializeResponse<LoginResponse>(loginResponse);
        loginResult!.Data!.Token.Should().NotBeNullOrEmpty();
        var currentToken = loginResult.Data.Token;

        var refreshResponse = await Client.PostAsync("/api/v1/auth/refresh", null);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResult = await DeserializeResponse<LoginResponse>(refreshResponse);
        refreshResult!.Success.Should().BeTrue();
        refreshResult.Data!.Token.Should().NotBeNullOrEmpty();
        refreshResult.Data.Token.Should().NotBe(currentToken);
        // Refresh must return role/username for SPA session restore (blank-dashboard guard).
        refreshResult.Data.Role.Should().NotBeNullOrWhiteSpace();
        refreshResult.Data.Username.Should().NotBeNullOrWhiteSpace();

        var setCookieHeader = Assert.Single(
            refreshResponse.Headers.GetValues("Set-Cookie"));
        setCookieHeader.Should().Contain("refreshToken");
    }

    /// <summary>
    /// Regression: EF must not query UserSession.IsRevoked (computed) — only RevokedAt.
    /// Login → Set-Cookie → immediate refresh must return 200 + same role claims.
    /// </summary>
    [Fact]
    public async Task Refresh_AfterLogin_Returns200WithOriginalRoleClaims()
    {
        const string username = "refresh_role_user";
        const string password = "SecurePass123!";

        await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Username = username,
            Email = "refresh.role@test.com",
            Password = password,
            Role = "Patient"
        });

        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Username = username,
            Password = password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var setCookie = Assert.Single(loginResponse.Headers.GetValues("Set-Cookie"));
        setCookie.Should().Contain("refreshToken");

        var login = await DeserializeResponse<LoginResponse>(loginResponse);
        login!.Data!.Role.Should().Be("Patient");
        login.Data.Username.Should().Be(username);
        var accessBefore = login.Data.Token;

        var refreshResponse = await Client.PostAsync("/api/v1/auth/refresh", null);
        refreshResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            because: "UserSession active-session query must use RevokedAt, not IsRevoked (EF translation)");

        var body = await refreshResponse.Content.ReadAsStringAsync();
        var refresh = await DeserializeResponse<LoginResponse>(refreshResponse);
        refresh.Should().NotBeNull(body);
        refresh!.Success.Should().BeTrue(body);
        refresh.Data.Should().NotBeNull();
        refresh.Data!.Token.Should().NotBeNullOrEmpty();
        refresh.Data.Token.Should().NotBe(accessBefore);
        refresh.Data.Role.Should().Be("Patient");
        refresh.Data.Username.Should().Be(username);
        refresh.Data.PatientId.Should().BeNull("profile not created yet");
    }

    /// <summary>
    /// After CreatePatient, refresh must re-issue JWT with patientId claim for booking APIs.
    /// </summary>
    [Fact]
    public async Task Refresh_AfterCreatePatientProfile_IncludesPatientIdClaim()
    {
        const string username = "refresh_patient_link";
        const string password = "SecurePass123!";

        await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Username = username,
            Email = "refresh.patient.link@test.com",
            Password = password,
            Role = "Patient"
        });

        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Username = username,
            Password = password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await DeserializeResponse<LoginResponse>(loginResponse);
        login!.Data!.PatientId.Should().BeNull();
        SetAuthToken(login.Data.Token);

        var createResponse = await Client.PostAsJsonAsync("/api/v1/patients", new
        {
            FirstName = "Refresh",
            LastName = "Link",
            Email = "refresh.patient.profile@test.com",
            PhoneNumber = "+38349111222",
            DateOfBirth = "1991-04-12",
            Gender = "Female",
            Street = "1 Refresh St",
            City = "Pristina",
            State = "Kosovo",
            PostalCode = "10000",
            Country = "Kosovo"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await DeserializeResponse<ProfileCreatedResponse>(createResponse);
        var patientId = created!.Data!.Id;
        patientId.Should().BeGreaterThan(0);
        created.Data.Token.Should().NotBeNullOrEmpty("create should re-issue access token");
        created.Data.PatientId.Should().Be(patientId);

        // /Auth/refresh still works for normal rotation and returns the same claim.
        var refreshResponse = await Client.PostAsync("/api/v1/auth/refresh", null);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refresh = await DeserializeResponse<LoginResponse>(refreshResponse);
        refresh!.Success.Should().BeTrue();
        refresh.Data!.Role.Should().Be("Patient");
        refresh.Data.PatientId.Should().Be(patientId);
        refresh.Data.Token.Should().NotBeNullOrEmpty();
    }
}
