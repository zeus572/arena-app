using System.Collections.Concurrent;

namespace Arena.Shared.Social.Resilience;

public enum CircuitState { Closed, Open, HalfOpen }

/// <summary>
/// Per-platform circuit breaker (SocialPublisher_Spec §4.4). Closed → normal; Open → skip the
/// platform entirely during cooldown; HalfOpen → allow a single probe. Breakers are fully
/// independent so one platform being down never affects another.
/// </summary>
public sealed class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly object _lock = new();

    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;

    public CircuitState State { get; private set; } = CircuitState.Closed;
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public DateTimeOffset? OpenedAt => State == CircuitState.Open ? _openedAt : null;

    public CircuitBreaker(int failureThreshold, TimeSpan openDuration)
    {
        _failureThreshold = failureThreshold;
        _openDuration = openDuration;
    }

    /// <summary>
    /// Whether a publish may be attempted now. Transitions Open → HalfOpen once the cooldown elapses
    /// (the next attempt is the single probe).
    /// </summary>
    public bool CanAttempt(DateTimeOffset now)
    {
        lock (_lock)
        {
            switch (State)
            {
                case CircuitState.Closed:
                case CircuitState.HalfOpen:
                    return true;
                case CircuitState.Open:
                    if (now >= _openedAt + _openDuration)
                    {
                        State = CircuitState.HalfOpen; // allow one probe
                        return true;
                    }
                    return false;
                default:
                    return true;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            State = CircuitState.Closed;
            LastErrorCode = null;
            LastErrorMessage = null;
        }
    }

    public void RecordFailure(DateTimeOffset now, string? errorCode = null, string? errorMessage = null)
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            LastErrorCode = errorCode;
            LastErrorMessage = errorMessage;

            // A probe failure in HalfOpen re-opens immediately; otherwise open at the threshold.
            if (State == CircuitState.HalfOpen || _consecutiveFailures >= _failureThreshold)
            {
                State = CircuitState.Open;
                _openedAt = now;
            }
        }
    }

    /// <summary>Force Open (e.g. credentials missing/invalid — §4.4 secrets/auth).</summary>
    public void Trip(DateTimeOffset now, string errorCode, string errorMessage)
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            State = CircuitState.Open;
            _openedAt = now;
            LastErrorCode = errorCode;
            LastErrorMessage = errorMessage;
        }
    }
}

/// <summary>Singleton store of per-platform breakers. State persists across heartbeat ticks.</summary>
public sealed class CircuitBreakerRegistry
{
    private readonly ConcurrentDictionary<string, CircuitBreaker> _breakers = new(StringComparer.Ordinal);
    private readonly SocialPublisherOptions _options;

    public CircuitBreakerRegistry(SocialPublisherOptions options) => _options = options;

    public CircuitBreaker Get(string platform) =>
        _breakers.GetOrAdd(platform, _ => new CircuitBreaker(
            _options.CircuitFailureThreshold,
            TimeSpan.FromMinutes(_options.CircuitOpenMinutes)));

    public IReadOnlyDictionary<string, CircuitBreaker> All => _breakers;
}
