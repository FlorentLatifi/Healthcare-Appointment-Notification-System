using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Healthcare.Adapters.Payments;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Payments;
using Healthcare.Presentation.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Presentation.Controllers;

public class WebhooksControllerTests
{
    private const string TestWebhookSecret = "whsec_test_secret_key_for_signing";
    private readonly Mock<IPaymentReconciliationService> _reconciliationMock;
    private readonly Mock<ILogger<WebhooksController>> _loggerMock;
    private readonly StripeSettings _stripeSettings;
    private readonly WebhooksController _controller;

    public WebhooksControllerTests()
    {
        _reconciliationMock = new Mock<IPaymentReconciliationService>();
        _loggerMock = new Mock<ILogger<WebhooksController>>();
        _stripeSettings = new StripeSettings
        {
            SecretKey = "sk_test_dummy",
            PublishableKey = "pk_test_dummy",
            WebhookSecret = TestWebhookSecret,
            DefaultCurrency = "USD"
        };

        _controller = new WebhooksController(
            _stripeSettings, _reconciliationMock.Object, _loggerMock.Object);
    }

    private static string ComputeStripeSignature(string secret, string json)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{timestamp}.{json}";
        var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signature = BitConverter.ToString(hash).Replace("-", "").ToLower();
        return $"t={timestamp},v1={signature}";
    }

    private static void SetRequestBody(WebhooksController controller, string json, string signatureHeader)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.Headers["Stripe-Signature"] = signatureHeader;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static string BuildPaymentIntentSucceededEvent(int appointmentId, string paymentIntentId, string chargeId)
    {
        return $@"{{""id"":""evt_{Guid.NewGuid():N}"",""object"":""event"",""request"":null,""type"":""payment_intent.succeeded"",""data"":{{""object"":{{""id"":""{paymentIntentId}"",""object"":""payment_intent"",""amount"":5000,""currency"":""usd"",""status"":""succeeded"",""metadata"":{{""appointment_id"":""{appointmentId}""}},""latest_charge"":""{chargeId}"",""payment_method_types"":[""card""]}}}}}}";
    }

    [Fact]
    public async Task HandleStripeWebhook_WithValidSignature_ShouldReconcilePayment()
    {
        var json = BuildPaymentIntentSucceededEvent(42, "pi_123", "ch_1");

        var signature = ComputeStripeSignature(TestWebhookSecret, json);
        SetRequestBody(_controller, json, signature);

        _reconciliationMock
            .Setup(r => r.ReconcilePaymentAsync(
                42, "pi_123", true, "ch_1", "card", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(99));

        var result = await _controller.HandleStripeWebhook(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();

        _reconciliationMock.Verify(
            r => r.ReconcilePaymentAsync(
                42, "pi_123", true, "ch_1", "card", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStripeWebhook_WithInvalidSignature_ShouldReturnBadRequest()
    {
        var json = BuildPaymentIntentSucceededEvent(1, "pi_invalid", "ch_x");

        SetRequestBody(_controller, json, "t=1234567890,v1=invalid_signature");

        var result = await _controller.HandleStripeWebhook(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _reconciliationMock.Verify(
            r => r.ReconcilePaymentAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleStripeWebhook_WhenCalledTwiceForSamePayment_ShouldBeIdempotent()
    {
        var json = BuildPaymentIntentSucceededEvent(7, "pi_789", "ch_2");

        var signature = ComputeStripeSignature(TestWebhookSecret, json);

        _reconciliationMock
            .Setup(r => r.ReconcilePaymentAsync(
                7, "pi_789", true, "ch_2", "card", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(99));

        SetRequestBody(_controller, json, signature);
        var firstResult = await _controller.HandleStripeWebhook(CancellationToken.None);
        firstResult.Should().BeOfType<OkObjectResult>();

        SetRequestBody(_controller, json, signature);
        var secondResult = await _controller.HandleStripeWebhook(CancellationToken.None);
        secondResult.Should().BeOfType<OkObjectResult>();

        _reconciliationMock.Verify(
            r => r.ReconcilePaymentAsync(
                7, "pi_789", true, "ch_2", "card", null, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
