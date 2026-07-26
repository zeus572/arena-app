using Civic.API.Data;
using Civic.API.Models.Daily;
using Civic.API.Services.Daily.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Daily;

public class DailyGamesOptions
{
    /// <summary>Master switch for generation. Reads and plays are unaffected.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How far ahead to keep the buffer, so a failed generator has slack to recover.</summary>
    public int DaysAhead { get; set; } = 2;

    /// <summary>Delay before the first pass, to stay out of the way of startup warmup.</summary>
    public int StartupDelaySeconds { get; set; } = 45;

    /// <summary>How often to re-check the buffer.</summary>
    public int IntervalMinutes { get; set; } = 360;

    /// <summary>
    /// Auto-approve kinds whose generator sets RequiresReview. Development convenience so a
    /// dev box has a full slate without anyone visiting the admin queue. Never enable in prod.
    /// </summary>
    public bool AutoApproveReviewedKinds { get; set; }
}

/// <summary>
/// Keeps the puzzle buffer topped up. Runs on a timer like the other civic content
/// generators, produces <see cref="DailyGamesOptions.DaysAhead"/> days ahead for every
/// kind, and is idempotent — the unique (Kind, PuzzleDate, Locality) index means a second
/// pass over the same day is a no-op.
///
/// Generation is the only place a daily game may touch an LLM (and today none of the six
/// actually does). Play is always pure computation.
/// </summary>
public class DailyPuzzleGenerationService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IOptions<DailyGamesOptions> _options;
    private readonly ILogger<DailyPuzzleGenerationService> _logger;

    public DailyPuzzleGenerationService(
        IServiceProvider services,
        IOptions<DailyGamesOptions> options,
        ILogger<DailyPuzzleGenerationService> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;
        if (!opts.Enabled)
        {
            _logger.LogInformation("Daily games generation is disabled.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(opts.StartupDelaySeconds), stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateBufferAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // One bad pass must never take the host down — the buffer exists so the
                // next pass can recover.
                _logger.LogError(ex, "Daily games generation pass failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(opts.IntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>Generate any missing puzzle for today through today+DaysAhead.</summary>
    public async Task GenerateBufferAsync(CancellationToken ct)
    {
        var opts = _options.Value;
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var generators = scope.ServiceProvider.GetServices<IDailyPuzzleGenerator>().ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var offset = 0; offset <= opts.DaysAhead; offset++)
        {
            var date = today.AddDays(offset);
            foreach (var generator in generators)
            {
                if (ct.IsCancellationRequested) return;
                await GenerateOneAsync(db, generator, date, opts, ct);
            }
        }

        if (opts.AutoApproveReviewedKinds) await PromoteExistingDraftsAsync(db, today, ct);
    }

    /// <summary>
    /// Development convenience: promote drafts that are already sitting in the queue.
    /// Generation is idempotent, so a puzzle created before auto-approve was switched on
    /// would otherwise stay Draft forever and never appear on a dev box's hub.
    /// Only runs when <see cref="DailyGamesOptions.AutoApproveReviewedKinds"/> is set,
    /// which must never be true in prod.
    /// </summary>
    private async Task PromoteExistingDraftsAsync(CivicDbContext db, DateOnly today, CancellationToken ct)
    {
        var drafts = await db.DailyPuzzles
            .Where(p => p.Status == DailyPuzzleStatus.Draft && p.PuzzleDate >= today)
            .ToListAsync(ct);
        if (drafts.Count == 0) return;

        foreach (var draft in drafts) draft.Status = DailyPuzzleStatus.Live;
        await db.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Auto-approved {Count} draft puzzle(s) — DailyGames:AutoApproveReviewedKinds is on. " +
            "This bypasses the human review queue and must not be enabled outside Development.",
            drafts.Count);
    }

    private async Task GenerateOneAsync(
        CivicDbContext db, IDailyPuzzleGenerator generator, DateOnly date,
        DailyGamesOptions opts, CancellationToken ct)
    {
        var exists = await db.DailyPuzzles.AnyAsync(
            p => p.Kind == generator.Kind && p.PuzzleDate == date && p.Locality == null, ct);
        if (exists) return;

        DailyPuzzle? puzzle;
        try
        {
            puzzle = await generator.GenerateAsync(date, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A single game failing must not fail the batch — this mirrors the
            // "BadResponse fails just this item, not the batch" rule the LLM pipeline uses.
            _logger.LogError(ex, "{Kind} generator threw for {Date}", generator.Kind, date);
            return;
        }

        if (puzzle is null) return;

        puzzle.Id = Guid.NewGuid();
        puzzle.Edition = await NextEditionAsync(db, generator.Kind, ct);
        puzzle.Status = generator.RequiresReview && !opts.AutoApproveReviewedKinds
            ? DailyPuzzleStatus.Draft
            : DailyPuzzleStatus.Live;

        db.DailyPuzzles.Add(puzzle);

        try
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Generated {Kind} #{Edition} for {Date} ({Status})",
                puzzle.Kind, puzzle.Edition, date, puzzle.Status);
        }
        catch (DbUpdateException ex)
        {
            // Another instance won the race on the unique index — that's the intended
            // outcome, not a failure.
            db.Entry(puzzle).State = EntityState.Detached;
            _logger.LogInformation(ex, "{Kind} for {Date} already generated by another pass",
                generator.Kind, date);
        }
    }

    private static async Task<int> NextEditionAsync(CivicDbContext db, DailyGameKind kind, CancellationToken ct)
    {
        var max = await db.DailyPuzzles.Where(p => p.Kind == kind)
            .Select(p => (int?)p.Edition).MaxAsync(ct);
        return (max ?? 0) + 1;
    }
}
