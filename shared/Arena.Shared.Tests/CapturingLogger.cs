using Microsoft.Extensions.Logging;

namespace Arena.Shared.Tests;

/// <summary>
/// Minimal <see cref="ILogger{T}"/> that records each log entry's level and its structured
/// state (the named-placeholder key/values that become App Insights customDimensions). Lets
/// tests assert on the telemetry data points the LLM clients emit — e.g. that a call logs
/// <c>LlmProvider</c>/<c>LlmModel</c>/<c>LlmOutcome</c> — rather than just the rendered text.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public sealed record Entry(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> State);

    public List<Entry> Entries { get; } = new();

    public IEnumerable<Entry> WithDimension(string key, object? value) =>
        Entries.Where(e => e.State.TryGetValue(key, out var v) && Equals(v, value));

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var dims = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (state is IReadOnlyList<KeyValuePair<string, object?>> kvps)
        {
            foreach (var kv in kvps)
            {
                dims[kv.Key] = kv.Value;
            }
        }
        Entries.Add(new Entry(logLevel, formatter(state, exception), dims));
    }
}
