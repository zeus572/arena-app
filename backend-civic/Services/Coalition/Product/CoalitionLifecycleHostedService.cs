namespace Civic.API.Services.Coalition.Product;

/// <summary>
/// Drives <see cref="CoalitionLifecycleService.RunTickAsync"/> on an interval: resolve
/// overdue provisions, top up the active pool from briefings, re-balance leagues. Resolves
/// a scope per tick (the lifecycle/loop services are scoped). Interval from
/// <c>CoalitionLifecycle:TickMinutes</c> (default 30); first tick after a short delay.
/// </summary>
public class CoalitionLifecycleHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<CoalitionLifecycleHostedService> _log;

    public CoalitionLifecycleHostedService(
        IServiceScopeFactory scopes, IConfiguration config, ILogger<CoalitionLifecycleHostedService> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);

        var minutes = Math.Max(1, _config.GetValue("CoalitionLifecycle:TickMinutes", 30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<CoalitionLifecycleService>();
                await lifecycle.RunTickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Coalition lifecycle tick failed");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken); }
            catch (TaskCanceledException) { /* shutdown */ }
        }
    }
}
