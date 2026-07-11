namespace Healthcare.Adapters.Events;

/// <summary>
/// Lightweight circuit breaker for the outbox relay (process-local).
/// Opens after consecutive batch failures; half-open after break duration.
/// </summary>
public sealed class OutboxCircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private readonly object _gate = new();

    private int _consecutiveFailures;
    private DateTime? _openUntilUtc;
    private CircuitState _state = CircuitState.Closed;

    public OutboxCircuitBreaker(int failureThreshold, TimeSpan breakDuration)
    {
        _failureThreshold = Math.Max(1, failureThreshold);
        _breakDuration = breakDuration <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : breakDuration;
    }

    public CircuitState State
    {
        get
        {
            lock (_gate)
            {
                RefreshState_NoLock(DateTime.UtcNow);
                return _state;
            }
        }
    }

    public bool IsOpen => State == CircuitState.Open;

    /// <summary>
    /// Returns false when the circuit is open (caller should skip work and sleep).
    /// </summary>
    public bool TryEnter()
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            RefreshState_NoLock(now);
            return _state != CircuitState.Open;
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _openUntilUtc = null;
            _state = CircuitState.Closed;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openUntilUtc = DateTime.UtcNow.Add(_breakDuration);
            }
        }
    }

    private void RefreshState_NoLock(DateTime utcNow)
    {
        if (_state == CircuitState.Open &&
            _openUntilUtc is not null &&
            utcNow >= _openUntilUtc.Value)
        {
            // Half-open: allow one batch; success closes, failure re-opens.
            _state = CircuitState.HalfOpen;
            _openUntilUtc = null;
        }
    }

    public enum CircuitState
    {
        Closed = 0,
        Open = 1,
        HalfOpen = 2
    }
}
