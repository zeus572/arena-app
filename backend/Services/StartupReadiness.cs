namespace Arena.API.Services;

public enum StartupStatus
{
    Initializing,
    Ready,
    Failed,
}

/// <summary>
/// Tracks whether startup-time database migration + seeding has completed.
/// Shared singleton between <see cref="DatabaseInitializerService"/> (which sets
/// the state) and the readiness gate / background services (which read it).
///
/// Migrations are moved off the startup critical path so Kestrel can begin
/// listening immediately and pass the platform warmup probe. This type is the
/// safeguard that ensures nothing operates against an un-migrated schema in the
/// meantime: the request pipeline holds API traffic and background workers park
/// until <see cref="IsReady"/> flips true.
/// </summary>
public sealed class StartupReadiness
{
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private volatile StartupStatus _status = StartupStatus.Initializing;

    public StartupStatus Status => _status;

    public bool IsReady => _status == StartupStatus.Ready;

    /// <summary>Error message captured when initialization failed (otherwise null).</summary>
    public string? Error { get; private set; }

    public void MarkReady()
    {
        _status = StartupStatus.Ready;
        _ready.TrySetResult();
    }

    public void MarkFailed(string error)
    {
        Error = error;
        _status = StartupStatus.Failed;
        // Intentionally leave the gate task uncompleted. Anything awaiting
        // readiness stays parked until the process restarts and a fresh
        // initialization attempt succeeds — we never wave through traffic
        // against a database we failed to migrate.
    }

    /// <summary>
    /// Completes once migrations + seeding have finished successfully. Honors the
    /// caller's cancellation token (e.g. app shutdown) so background workers don't
    /// hang if initialization never succeeds.
    /// </summary>
    public Task WaitUntilReadyAsync(CancellationToken ct) => _ready.Task.WaitAsync(ct);
}
