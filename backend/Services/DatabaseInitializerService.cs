namespace Arena.API.Services;

/// <summary>
/// Runs EF Core migrations + reference-data seeding in the background, AFTER the
/// host has started and Kestrel is accepting connections. Previously this work
/// ran inline before <c>app.Run()</c>, which meant the slow first
/// managed-identity → Postgres token handshake blocked the container warmup
/// probe and pushed cold starts past the platform's 230s kill threshold —
/// turning every deploy/restart into a multi-minute (sometimes flapping) outage.
///
/// Safeguards, because migrations are critical to correctness:
///   • The initializer is retried with backoff so a transient token/connection
///     failure doesn't permanently break a fresh container.
///   • Each attempt has a timeout so a hung migration can't park the app forever.
///     (Postgres DDL is transactional, so a cancelled migration rolls back clean.)
///   • On unrecoverable failure we stop the app so the platform recycles the
///     container for another clean attempt rather than lingering half-dead.
///   • <see cref="StartupReadiness"/> only flips to Ready on success; the request
///     pipeline's readiness gate and the background workers both block on it, so
///     no traffic or worker ever touches an un-migrated schema.
/// </summary>
public sealed class DatabaseInitializerService : BackgroundService
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(300);

    private readonly IServiceProvider _services;
    private readonly StartupReadiness _readiness;
    private readonly ILogger<DatabaseInitializerService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly Func<IServiceProvider, CancellationToken, Task> _initialize;

    public DatabaseInitializerService(
        IServiceProvider services,
        StartupReadiness readiness,
        ILogger<DatabaseInitializerService> logger,
        IHostApplicationLifetime lifetime,
        Func<IServiceProvider, CancellationToken, Task> initialize)
    {
        _services = services;
        _readiness = readiness;
        _logger = logger;
        _lifetime = lifetime;
        _initialize = initialize;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately so host startup completes and Kestrel begins
        // accepting connections; everything below runs while the app is live.
        await Task.Yield();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(AttemptTimeout);

            try
            {
                _logger.LogInformation(
                    "Database initialization attempt {Attempt}/{Max} starting...", attempt, MaxAttempts);

                await _initialize(_services, timeoutCts.Token);

                _readiness.MarkReady();
                _logger.LogInformation(
                    "Database migration + seeding complete; app is READY to serve traffic.");
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // App is shutting down — exit quietly without marking failed.
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                var backoff = TimeSpan.FromSeconds(attempt * 5);
                _logger.LogWarning(ex,
                    "Database initialization attempt {Attempt}/{Max} failed; retrying in {Backoff}s.",
                    attempt, MaxAttempts, backoff.TotalSeconds);
                try { await Task.Delay(backoff, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
            catch (Exception ex)
            {
                _readiness.MarkFailed(ex.Message);
                _logger.LogCritical(ex,
                    "Database initialization FAILED after {Max} attempts. Stopping the app so the "
                    + "platform recycles the container for a fresh attempt.", MaxAttempts);
                _lifetime.StopApplication();
                return;
            }
        }
    }
}
