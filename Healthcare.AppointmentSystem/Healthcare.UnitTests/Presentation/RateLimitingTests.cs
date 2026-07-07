using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Healthcare.UnitTests.Presentation;

[Collection("RateLimitingSequential")]
public class RateLimitingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("Jwt__Secret", "SuperSecretKeyForTestingRateLimiting123456!");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_mockkeyforratelimitingtests12345");
        Environment.SetEnvironmentVariable("Stripe__PublishableKey", "pk_test_mockkeyforratelimitingtests12345");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });
    }

    [Fact]
    public async Task LoginEndpoint_AllowsUpToFiveRequests_AndFailsOnSixthRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var loginPayload = new { Username = "nonexistentuser", Password = "wrongpassword" };

        // Act & Assert
        // First 5 requests should get 400 Bad Request (since the user does not exist), not 429.
        for (int i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/Auth/login", loginPayload);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // The 6th request should trigger rate limiting (429 Too Many Requests)
        var rateLimitedResponse = await client.PostAsJsonAsync("/api/v1/Auth/login", loginPayload);
        rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Verify the response format matches ApiResponse
        var apiResponse = await rateLimitedResponse.Content.ReadFromJsonAsync<ApiResponse>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Be("Rate limit exceeded");
        apiResponse.Errors.Should().ContainSingle().Which.Should().Contain("Too many requests");
    }

    [Fact]
    public async Task HealthEndpoint_IsNotRestrictedByAuthPolicy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act & Assert
        // Sending 6 requests to health endpoint should not trigger 429 (only AuthPolicy has a limit of 5)
        for (int i = 0; i < 6; i++)
        {
            var response = await client.GetAsync("/health");
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }
    }
}
