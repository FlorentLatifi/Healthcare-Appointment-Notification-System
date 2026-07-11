using Healthcare.Application.Observability;

namespace Healthcare.Presentation.API.Http;

/// <summary>
/// Propagates X-Correlation-Id on all outbound HttpClient requests.
/// </summary>
public sealed class CorrelationIdDelegatingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationContext.Current;
        if (!string.IsNullOrWhiteSpace(correlationId) &&
            !request.Headers.Contains(CorrelationContext.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationContext.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
