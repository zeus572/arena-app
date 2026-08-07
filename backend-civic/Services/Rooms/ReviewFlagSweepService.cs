using Civic.API.Data;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Rooms;

/// <summary>
/// Watches the review queue and escalates what has been sitting too long.
///
/// It HIDES NOTHING. That is deliberate and worth stating loudly, because the obvious
/// implementation of "hide unreviewed content after six hours" is a background job, and
/// that implementation is wrong twice over: it yanks content out from under someone
/// mid-read, and it races the reviewer who is at that moment fixing the thing. Hiding is
/// a read-time decision in <see cref="RoomVisibility"/>, keyed on whether the reader's
/// session predates the flag.
///
/// What this service does is notice, log, and mark the room so the admin queue shouts.
/// Nothing is ever deleted.
/// </summary>
public class ReviewFlagSweepService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    private readonly IServiceScopeFactory _scopes;
    private readonly StartupReadiness _readiness;
    private readonly ILogger<ReviewFlagSweepService> _log;

    public ReviewFlagSweepService(
        IServiceScopeFactory scopes,
        StartupReadiness readiness,
        ILogger<ReviewFlagSweepService> log)
    {
        _scopes = scopes;
        _readiness = readiness;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _readiness.WaitUntilReadyAsync(ct);

        try
        {
            await Task.Delay(StartupDelay, ct);
        }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Review flag sweep failed; will retry next tick.");
            }

            try
            {
                await Task.Delay(Interval, ct);
            }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>Public so tests can drive one tick deterministically.</summary>
    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var now = DateTime.UtcNow;
        var open = await db.ReviewFlags
            .Where(f => f.ResolvedAt == null)
            .ToListAsync(ct);

        var pastGrace = open.Where(f => now - f.CreatedAt >= RoomVisibility.UnreviewedGrace).ToList();
        var pastEscalation = open.Where(f => RoomVisibility.NeedsEscalation(f, now)).ToList();

        if (pastGrace.Count > 0)
        {
            _log.LogWarning(
                "{Count} review flag(s) unreviewed past the {Hours}h grace period; the affected "
              + "objects are now hidden from new sessions.",
                pastGrace.Count, RoomVisibility.UnreviewedGrace.TotalHours);
        }

        // Rooms carrying something unreviewed for a full day are marked so the admin queue
        // surfaces them. The room stays readable — the flagged OBJECT is what hides.
        var roomIds = pastEscalation
            .Where(f => f.ObjectType == ObjectType.Room)
            .Select(f => f.ObjectId)
            .Distinct()
            .ToList();

        if (roomIds.Count > 0)
        {
            var rooms = await db.Rooms
                .Where(r => roomIds.Contains(r.Id) && r.Status == RoomStatus.Published)
                .ToListAsync(ct);

            foreach (var room in rooms)
            {
                room.Status = RoomStatus.CorrectionRequired;
                _log.LogError(
                    "Room {Slug} has an unreviewed flag older than {Hours}h; marked "
                  + "CorrectionRequired.", room.Slug, RoomVisibility.EscalationAfter.TotalHours);
            }

            await db.SaveChangesAsync(ct);
        }

        return pastGrace.Count;
    }
}
