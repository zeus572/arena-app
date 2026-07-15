using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Bills;

/// <summary>
/// Turns ingested <see cref="Bill"/> rows into per-axis value positions via the
/// shared <see cref="ILlmClient"/> (one Sonnet call per bill), writing
/// <see cref="BillAxisPosition"/> rows and a neutral <c>SynthesisSummary</c>.
///
/// Mirrors <c>CivicContentGenerationService</c>: a status machine
/// (Ingested → Synthesizing → Synthesized/Failed) with a reset-stuck-items guard
/// on startup, and it requeues (rather than fails) a bill when the live LLM call
/// itself fails so a dead API doesn't burn every bill to Failed. Honors the
/// <c>Anthropic:Enabled</c> kill-switch via the client.
/// </summary>
public class BillSynthesisService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILlmClient _llm;
    private readonly ICivicCatalog _catalog;
    private readonly IOptionsMonitor<BillOptions> _opts;
    private readonly ILogger<BillSynthesisService> _log;
    private readonly StartupReadiness _readiness;

    public BillSynthesisService(
        IServiceScopeFactory scopes,
        ILlmClient llm,
        ICivicCatalog catalog,
        IOptionsMonitor<BillOptions> opts,
        ILogger<BillSynthesisService> log,
        StartupReadiness readiness)
    {
        _scopes = scopes;
        _llm = llm;
        _catalog = catalog;
        _opts = opts;
        _log = log;
        _readiness = readiness;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await _readiness.WaitUntilReadyAsync(stoppingToken); }
        catch (OperationCanceledException) { return; }

        try { await ResetStuckBillsAsync(stoppingToken); }
        catch (Exception ex) { _log.LogWarning(ex, "BillSynthesisService: ResetStuckBills failed, will retry naturally"); }

        await Task.Delay(TimeSpan.FromSeconds(35), stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SynthesizeBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "BillSynthesisService: batch failed");
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, _opts.CurrentValue.SynthesisIntervalMinutes));
            try { await Task.Delay(interval, stoppingToken); }
            catch (TaskCanceledException) { /* shutdown */ }
        }
    }

    /// <summary>Reset any bills stuck in Synthesizing from a crashed instance back to Ingested.</summary>
    private async Task ResetStuckBillsAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var stuck = await db.Bills.Where(b => b.SynthesisStatus == BillSynthesisStatus.Synthesizing).ToListAsync(ct);
        foreach (var b in stuck) b.SynthesisStatus = BillSynthesisStatus.Ingested;
        if (stuck.Count > 0) await db.SaveChangesAsync(ct);
    }

    /// <summary>Drives one batch tick. Public for deterministic tests.</summary>
    public async Task<int> SynthesizeBatchAsync(CancellationToken ct = default)
    {
        var opts = _opts.CurrentValue;

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var batch = await db.Bills
            .Where(b => b.SynthesisStatus == BillSynthesisStatus.Ingested
                        && b.AttemptCount < opts.MaxSynthesisAttempts)
            .OrderByDescending(b => b.LatestActionDate ?? b.IntroducedDate)
            .Take(Math.Max(1, opts.SynthesisBatchSize))
            .ToListAsync(ct);

        if (batch.Count == 0) return 0;

        var done = 0;
        foreach (var queued in batch)
        {
            var bill = await db.Bills.FirstOrDefaultAsync(b => b.Id == queued.Id, ct);
            if (bill is null) continue;

            try
            {
                bill.SynthesisStatus = BillSynthesisStatus.Synthesizing;
                bill.AttemptCount++;
                await db.SaveChangesAsync(ct);

                await SynthesizeOneAsync(db, bill, ct);

                bill.SynthesisStatus = BillSynthesisStatus.Synthesized;
                bill.SynthesizedAt = DateTime.UtcNow;
                bill.LastError = null;
                await db.SaveChangesAsync(ct);
                done++;
            }
            catch (Exception ex)
            {
                // Discard any partially-added positions before writing the status.
                db.ChangeTracker.Clear();
                var tracked = await db.Bills.FirstOrDefaultAsync(b => b.Id == queued.Id, ct);
                if (tracked is null) continue;

                if (ex is LlmException { Kind: LlmFailureKind.CallFailed })
                {
                    // Live call failed (out of credits, 5xx) — not this bill's fault. Requeue,
                    // un-count the attempt, and halt the batch rather than hammering a dead API.
                    tracked.SynthesisStatus = BillSynthesisStatus.Ingested;
                    if (tracked.AttemptCount > 0) tracked.AttemptCount--;
                    await db.SaveChangesAsync(ct);
                    _log.LogWarning(ex, "BillSynthesisService: LLM unavailable; requeued Bill {Id} and halting batch", queued.Id);
                    break;
                }

                if (ex is LlmException { Kind: LlmFailureKind.Unavailable })
                {
                    // Kill-switch off / no key — leave the bill Ingested for when the LLM returns.
                    tracked.SynthesisStatus = BillSynthesisStatus.Ingested;
                    if (tracked.AttemptCount > 0) tracked.AttemptCount--;
                    await db.SaveChangesAsync(ct);
                    _log.LogInformation("BillSynthesisService: LLM disabled/unconfigured; leaving bills unsynthesized");
                    break;
                }

                _log.LogWarning(ex, "BillSynthesisService: synthesis failed for Bill {Id}", queued.Id);
                tracked.SynthesisStatus = BillSynthesisStatus.Failed;
                tracked.LastError = ex.Message;
                await db.SaveChangesAsync(ct);
            }
        }

        _log.LogInformation("BillSynthesisService: synthesized {Done}/{Total} bills", done, batch.Count);
        return done;
    }

    private async Task SynthesizeOneAsync(CivicDbContext db, Bill bill, CancellationToken ct)
    {
        var (system, user) = BillPrompts.Synthesis(bill, _catalog);
        var result = await _llm.GenerateStructuredAsync<BillSynthesisResult>(system, user, LlmModelTier.Sonnet, ct: ct);

        // Replace any prior positions (defensive on re-synthesis).
        var existing = await db.BillAxisPositions.Where(p => p.BillId == bill.Id).ToListAsync(ct);
        if (existing.Count > 0) db.BillAxisPositions.RemoveRange(existing);

        bill.SynthesisSummary = Truncate(result.Summary, 2000);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in result.Positions)
        {
            if (string.IsNullOrWhiteSpace(p.AxisKey)) continue;
            // Drop axes the model invented that aren't in the live catalog.
            if (_catalog.AxisFor(p.AxisKey) is null) continue;
            if (!seen.Add(p.AxisKey)) continue;

            db.BillAxisPositions.Add(new BillAxisPosition
            {
                Id = Guid.NewGuid(),
                BillId = bill.Id,
                AxisKey = p.AxisKey,
                Score = Math.Clamp(p.Score, -1.0, 1.0),
                Confidence = Math.Clamp(p.Confidence, 0.0, 1.0),
                Rationale = Truncate(string.IsNullOrWhiteSpace(p.Rationale) ? "(no rationale provided)" : p.Rationale, 600),
                Evidence = string.IsNullOrWhiteSpace(p.Evidence) ? null : Truncate(p.Evidence!, 600),
            });
        }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
