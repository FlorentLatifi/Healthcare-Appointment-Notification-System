using System.Diagnostics;

namespace Healthcare.Application.Observability;

/// <summary>
/// Process-wide correlation id (AsyncLocal) for HTTP, MediatR, background work, and outbound calls.
/// Prefer <see cref="Activity"/> baggage when available; falls back to AsyncLocal then TraceId.
/// </summary>
public static class CorrelationContext
{
    public const string HeaderName = "X-Correlation-Id";
    public const string BaggageKey = "correlation.id";
    public const string HttpContextItemKey = "CorrelationId";
    public const string TagName = "correlation.id";

    private static readonly AsyncLocal<string?> CurrentValue = new();

    public static string? Current
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CurrentValue.Value))
                return CurrentValue.Value;

            var activity = Activity.Current;
            if (activity is not null)
            {
                var baggage = activity.GetBaggageItem(BaggageKey);
                if (!string.IsNullOrWhiteSpace(baggage))
                    return baggage;

                var tag = activity.GetTagItem(TagName)?.ToString();
                if (!string.IsNullOrWhiteSpace(tag))
                    return tag;

                if (activity.TraceId != default)
                    return activity.TraceId.ToString();
            }

            return null;
        }
        set => CurrentValue.Value = value;
    }

    /// <summary>Sets AsyncLocal + Activity baggage/tag when an activity exists.</summary>
    public static IDisposable BeginScope(string correlationId)
    {
        var previous = CurrentValue.Value;
        CurrentValue.Value = correlationId;

        var activity = Activity.Current;
        string? previousBaggage = null;
        if (activity is not null)
        {
            previousBaggage = activity.GetBaggageItem(BaggageKey);
            activity.SetBaggage(BaggageKey, correlationId);
            activity.SetTag(TagName, correlationId);
        }

        return new Scope(previous, previousBaggage);
    }

    public static string GetOrCreate()
    {
        var existing = Current;
        if (!string.IsNullOrWhiteSpace(existing))
            return existing!;

        var created = Guid.NewGuid().ToString("N");
        Current = created;
        return created;
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _previous;
        private readonly string? _previousBaggage;
        private bool _disposed;

        public Scope(string? previous, string? previousBaggage)
        {
            _previous = previous;
            _previousBaggage = previousBaggage;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CurrentValue.Value = _previous;
            var activity = Activity.Current;
            if (activity is null) return;
            if (_previousBaggage is null)
                activity.SetBaggage(BaggageKey, string.Empty);
            else
                activity.SetBaggage(BaggageKey, _previousBaggage);
        }
    }
}
