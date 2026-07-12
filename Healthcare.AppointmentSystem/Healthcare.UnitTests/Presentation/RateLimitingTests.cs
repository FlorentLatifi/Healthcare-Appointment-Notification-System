using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Healthcare.UnitTests.Presentation;

/// <summary>
/// Dedicated factory so AuthPermitLimit=5 is baked into the process env before Program.Main.
/// Sharing AuthorizationTestWebApplicationFactory left AuthPermitLimit=10000 and disabled the 429 path.
/// </summary>
public sealed class RateLimitingTestWebApplicationFactory : AuthorizationTestWebApplicationFactory
{
    public RateLimitingTestWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "5");
        Environment.SetEnvironmentVariable("RateLimiting__GlobalPermitLimit", "10000");
        Environment.SetEnvironmentVariable("RateLimiting__WindowMinutes", "1");
    }
}

[Collection("RateLimitingSequential")]
public class RateLimitingTests : IClassFixture<RateLimitingTestWebApplicationFactory>
{
    private readonly RateLimitingTestWebApplicationFactory _factory;

    public RateLimitingTests(RateLimitingTestWebApplicationFactory factory)
    {
        // Re-assert before each host client is created (other fixtures may raise the limit).
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "5");
        _factory = factory;
    }

    [Fact]
    public async Task LoginEndpoint_AllowsUpToFiveRequests_AndFailsOnSixthRequest()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:AuthPermitLimit"] = "5",
                    ["RateLimiting:GlobalPermitLimit"] = "10000",
                    ["RateLimiting:WindowMinutes"] = "1",
                });
            });
        }).CreateClient();

        var loginPayload = new { Username = "nonexistentuser", Password = "wrongpassword" };

        for (int i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                $"request {i + 1}: {(await response.Content.ReadAsStringAsync())}");
        }

        var rateLimitedResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
        rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            await rateLimitedResponse.Content.ReadAsStringAsync());

        var apiResponse = await rateLimitedResponse.Content.ReadFromJsonAsync<ApiResponse>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Be("Rate limit exceeded");
        apiResponse.Errors.Should().ContainSingle().Which.Should().Contain("Too many requests");
    }

    [Fact]
    public async Task HealthEndpoint_IsNotRestrictedByAuthPolicy()
    {
        var client = _factory.CreateClient();

        for (int i = 0; i < 6; i++)
        {
            var response = await client.GetAsync("/health");
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }
    }
}
