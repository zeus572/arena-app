using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

public class RankingRollupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RankingService _ranking;
    private readonly ILogger<RankingRollupService> _logger;
    private readonly int _intervalMinutes;

    public RankingRollupService(
        IServiceScopeFactory scopeFactory,
        RankingService ranking,
        IConfiguration config,
        ILogger<RankingRollupService> logger)
    {
        _scopeFactory = scopeFactory;
        _ranking = ranking;
        _logger = logger;
        _intervalMinutes = config.GetValue("Ranking:RollupIntervalMinutes", 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RankingRollup started. Interval={Interval}min", _intervalMinutes);

        // Initial delay
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRollupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "RankingRollup tick failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
        }
    }

    private async Task RunRollupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();

        var debates = await db.Debates
            .Where(d => d.Status == DebateStatus.Active || d.Status == DebateStatus.Completed)
            .ToListAsync(ct);

        _logger.LogInformation("Computing ranking scores for {Count} debates", debates.Count);

        foreach (var debate in debates)
        {
            await _ranking.ComputeScoreAsync(db, debate);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Ranking rollup complete");
    }
}
