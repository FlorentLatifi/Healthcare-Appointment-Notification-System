using System.Diagnostics;
using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using MediatR;

namespace Healthcare.Application.Behaviors;

/// <summary>
/// Records command duration + success and maps known commands to business counters.
/// </summary>
public sealed class MetricsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IBusinessMetrics _metrics;

    public MetricsBehavior(IBusinessMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();

            var success = IsSuccess(response);
            _metrics.CommandExecuted(name, success, sw.Elapsed.TotalMilliseconds);

            if (success)
                RecordBusinessSuccess(name, request);

            return response;
        }
        catch
        {
            sw.Stop();
            _metrics.CommandExecuted(name, success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    private static bool IsSuccess(TResponse response)
    {
        if (response is null) return true;
        var type = response.GetType();
        if (type == typeof(Result) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>)))
        {
            var prop = type.GetProperty(nameof(Result.IsSuccess));
            if (prop?.GetValue(response) is bool ok)
                return ok;
        }
        return true;
    }

    private void RecordBusinessSuccess(string requestName, TRequest request)
    {
        switch (requestName)
        {
            case "BookAppointmentCommand":
                _metrics.AppointmentBooked(TryGetStringProperty(request, "AppointmentType"));
                break;
            case "CancelAppointmentCommand":
                _metrics.AppointmentCancelled();
                break;
            case "ConfirmAppointmentCommand":
                _metrics.AppointmentConfirmed();
                break;
            case "CompleteAppointmentCommand":
                _metrics.AppointmentCompleted();
                break;
            case "MarkNoShowAppointmentCommand":
                _metrics.AppointmentNoShow();
                break;
            case "ProcessPaymentCommand":
                _metrics.PaymentSucceeded();
                break;
            case "RefundPaymentCommand":
                _metrics.PaymentRefunded();
                break;
        }
    }

    private static string? TryGetStringProperty(TRequest request, string propertyName)
    {
        var prop = typeof(TRequest).GetProperty(propertyName);
        return prop?.GetValue(request)?.ToString();
    }
}
