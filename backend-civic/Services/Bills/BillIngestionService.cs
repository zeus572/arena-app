using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Bills;

/// <summary>
/// Periodically pulls recent bills from <see cref="IBillSource"/> (Congress.gov)
/// and upserts <see cref="Bill"/> rows. Idempotent by <c>ExternalId</c> — a
/// repeat ingestion of the same bill is a no-op. Does not call Claude (synthesis
/// is <see cref="BillSynthesisService"/>'s job).
///
/// Modeled on <c>NewsIngestionService</c>. Safe when no API key is configured:
/// the source returns nothing and the seeded bills already cover the experience.
/// </summary>
public class BillIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IBillSource _source;
    private readonly IOptionsMonitor<BillOptions> _opts;
    private readonly ILogger<BillIngestionService> _log;
    private readonly StartupReadiness _readiness;

    public BillIngestionService(
        IServiceScopeFactory scopes,
        IBillSource source,
        IOptionsMonitor<BillOptions> opts,
        ILogger<BillIngestionService> log,
        StartupReadiness readiness)
    {
        _scopes = scopes;
        _source = source;
        _opts = opts;
        _log = log;
        _readiness = readiness;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await _readiness.WaitUntilReadyAsync(stoppingToken); }
        catch (OperationCanceledException) { return; }

        // Delay the first tick so startup isn't blocked on a network call.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IngestOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "BillIngestionService: tick failed");
            }

            var interval = TimeSpan.FromHours(Math.Max(1, _opts.CurrentValue.IngestIntervalHours));
            try { await Task.Delay(interval, stoppingToken); }
            catch (TaskCanceledException) { /* shutdown */ }
        }
    }

    /// <summary>
    /// Public so tests can drive a single tick deterministically. Pulls recent
    /// bills from the source and inserts the ones not already stored by
    /// <c>ExternalId</c>. Returns the number of fresh bills inserted.
    /// </summary>
    public async Task<int> IngestOnceAsync(CancellationToken ct = default)
    {
        var opts = _opts.CurrentValue;
        if (!opts.Enabled)
        {
            _log.LogInformation("BillIngestionService: live ingestion disabled (Bills:Enabled=false)");
            return 0;
        }

        var fetched = await _source.FetchRecentAsync(opts.Congress, Math.Max(1, opts.MaxBillsPerRun), ct);
        if (fetched.Count == 0) return 0;

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var externalIds = fetched.Select(b => b.ExternalId).ToList();
        var existing = await db.Bills
            .Where(b => externalIds.Contains(b.ExternalId))
            .Select(b => b.ExternalId)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Collapse duplicates within the fetched batch too (defensive).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toAdd = new List<Bill>();
        foreach (var bill in fetched)
        {
            if (existingSet.Contains(bill.ExternalId) || !seen.Add(bill.ExternalId)) continue;
            toAdd.Add(bill);
        }

        if (toAdd.Count > 0)
        {
            db.Bills.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
            _log.LogInformation("BillIngestionService: inserted {Count} fresh bills", toAdd.Count);
        }

        return toAdd.Count;
    }
}
