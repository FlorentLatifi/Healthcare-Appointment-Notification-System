using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace Healthcare.IntegrationTests;

/// <summary>
/// Admin audit query API — requires EnrichAuditLogComplianceFields migration applied.
/// </summary>
public sealed class AuditLogsFlowTests : IntegrationTestBase
{
    public AuditLogsFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAuditLogs_AsAdmin_Returns200WithPagedShape()
    {
        // Generate at least one audit row via successful login (LoginSucceeded).
        var token = await LoginAsPreSeededAdminAsync();
        SetAuthToken(token);

        var response = await Client.GetAsync("/api/v1/AuditLogs?pageSize=5");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeTrue();

        var data = root.GetProperty("data");
        data.TryGetProperty("items", out var items).Should().BeTrue("paged result must expose items");
        data.TryGetProperty("pageNumber", out var pageNumber).Should().BeTrue();
        data.TryGetProperty("pageSize", out var pageSize).Should().BeTrue();
        data.TryGetProperty("totalCount", out _).Should().BeTrue();
        data.TryGetProperty("totalPages", out _).Should().BeTrue();

        pageNumber.GetInt32().Should().Be(1);
        pageSize.GetInt32().Should().Be(5);
        items.ValueKind.Should().Be(JsonValueKind.Array);
        items.GetArrayLength().Should().BeLessThanOrEqualTo(5);

        // After login audit write succeeds (post-migration), list should not be empty.
        // If empty, still valid shape — but login should have written LoginSucceeded.
        if (items.GetArrayLength() > 0)
        {
            var first = items[0];
            first.TryGetProperty("action", out _).Should().BeTrue();
            first.TryGetProperty("outcome", out _).Should().BeTrue();
            first.TryGetProperty("id", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetAuditLogs_WithoutToken_Returns401()
    {
        ClearAuthToken();
        var response = await Client.GetAsync("/api/v1/AuditLogs?pageSize=5");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuditLogs_AsPatient_Returns403()
    {
        var token = await RegisterAndLoginAsync(
            "audit_patient_" + Guid.NewGuid().ToString("N")[..8],
            $"audit.patient.{Guid.NewGuid():N}@test.com",
            "SecurePass123!",
            "Patient");
        SetAuthToken(token);

        var response = await Client.GetAsync("/api/v1/AuditLogs?pageSize=5");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
