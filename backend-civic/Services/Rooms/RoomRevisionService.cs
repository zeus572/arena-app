using System.Text.Json;
using Civic.API.Data;
using Civic.API.Models.DTOs;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Rooms;

/// <summary>One change to record. Significance is computed, never supplied.</summary>
public record PendingChange(
    ChangeType Type,
    string Headline,
    string? WhyItMatters = null,
    ObjectType? ObjectType = null,
    Guid? ObjectId = null,
    string? FromValue = null,
    string? ToValue = null,
    CorrectionKind? CorrectionKind = null);

/// <summary>
/// The only supported way to change a room.
///
/// Every writer — the admin controller, the drafting service, the correction propagator —
/// goes through <see cref="CommitAsync"/>, which is what guarantees no edit can happen
/// without a typed changelog entry. If a caller could bump Room.Revision directly, the
/// "since your last visit" ribbon would start lying, silently.
/// </summary>
public class RoomRevisionService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly CivicDbContext _db;
    private readonly ILogger<RoomRevisionService> _log;

    public RoomRevisionService(CivicDbContext db, ILogger<RoomRevisionService> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>
    /// Bump the room's revision and write its changelog in one save.
    ///
    /// <paramref name="snapshot"/> is persisted only when at least one change is meaningful:
    /// a few KB a handful of times per room per month, which is what lets diff mode stay a
    /// pure frontend decision later instead of a schema migration.
    /// </summary>
    public async Task<RoomRevision> CommitAsync(
        Guid roomId,
        string actor,
        IReadOnlyList<PendingChange> changes,
        string? summary = null,
        object? snapshot = null,
        CancellationToken ct = default)
    {
        if (changes.Count == 0)
        {
            throw new ArgumentException(
                "A revision with no changes would bump the counter without explaining why.",
                nameof(changes));
        }

        for (var attempt = 0; ; attempt++)
        {
            var room = await _db.Set<Room>().FirstOrDefaultAsync(r => r.Id == roomId, ct)
                ?? throw new InvalidOperationException($"Room {roomId} not found.");

            var anyMeaningful = changes.Any(c => MeaningfulChange.IsNotifiable(c.Type));
            var now = DateTime.UtcNow;

            room.Revision += 1;
            room.UpdatedAt = now;
            if (anyMeaningful) room.LastMeaningfulUpdateAt = now;

            var revision = new RoomRevision
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                Revision = room.Revision,
                IsMeaningful = anyMeaningful,
                Summary = summary ?? changes[0].Headline,
                CreatedBy = actor,
                CreatedAt = now,
                SnapshotJson = anyMeaningful && snapshot is not null
                    ? JsonSerializer.Serialize(snapshot, SnapshotJsonOptions)
                    : null,
            };
            _db.Add(revision);

            foreach (var change in changes)
            {
                _db.Add(new ChangeLogEntry
                {
                    Id = Guid.NewGuid(),
                    RoomRevisionId = revision.Id,
                    RoomId = room.Id,
                    RevisionNumber = room.Revision,
                    Type = change.Type,
                    // Computed, never taken from the caller.
                    IsMeaningful = MeaningfulChange.IsNotifiable(change.Type),
                    Headline = change.Headline,
                    WhyItMatters = change.WhyItMatters,
                    ObjectType = change.ObjectType,
                    ObjectId = change.ObjectId,
                    FromValue = change.FromValue,
                    ToValue = change.ToValue,
                    CorrectionKind = change.CorrectionKind,
                    CreatedAt = now,
                });
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                return revision;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                // Both the admin controller and the propagation service write rooms. One
                // retry from a clean read is cheaper than a lock and covers the realistic case.
                _log.LogInformation(
                    "Revision commit for room {RoomId} lost a race; retrying once.", roomId);
                _db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>
    /// What changed for this reader since <paramref name="sinceRevision"/>.
    ///
    /// Corrections come back in their OWN array, not merged into the meaningful list. The
    /// handoff is explicit that corrections are never folded into "updated", and enforcing
    /// that in the API shape means the frontend structurally cannot get it wrong.
    /// </summary>
    public async Task<RoomDeltaDto> DeltaAsync(
        Guid roomId, int sinceRevision, CancellationToken ct = default)
    {
        var current = await _db.Set<Room>().AsNoTracking()
            .Where(r => r.Id == roomId).Select(r => r.Revision).FirstOrDefaultAsync(ct);

        var delta = new RoomDeltaDto { FromRevision = sinceRevision, ToRevision = current };

        if (sinceRevision >= current) return delta;

        var entries = await _db.ChangeLogEntries.AsNoTracking()
            .Where(e => e.RoomId == roomId && e.RevisionNumber > sinceRevision)
            .OrderByDescending(e => e.RevisionNumber).ThenBy(e => e.CreatedAt)
            .ToListAsync(ct);

        foreach (var e in entries)
        {
            if (!e.IsMeaningful)
            {
                delta.WithheldCount++;
                var bucket = delta.WithheldByType.FirstOrDefault(w => w.Type == e.Type.ToString());
                if (bucket is null)
                {
                    delta.WithheldByType.Add(new WithheldChangeDto { Type = e.Type.ToString(), Count = 1 });
                }
                else
                {
                    bucket.Count++;
                }
                continue;
            }

            var dto = ToDto(e);
            if (e.Type == ChangeType.CorrectionIssued) delta.Corrections.Add(dto);
            else delta.MeaningfulChanges.Add(dto);
        }

        return delta;
    }

    /// <summary>Record that this reader has seen the room at <paramref name="revision"/>.</summary>
    public async Task MarkSeenAsync(
        string userId, Guid roomId, int revision, CancellationToken ct = default)
    {
        var state = await _db.UserRoomStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.RoomId == roomId, ct);

        if (state is null)
        {
            state = new UserRoomState { Id = Guid.NewGuid(), UserId = userId, RoomId = roomId };
            _db.UserRoomStates.Add(state);
        }

        // Never move backwards: a reader opening an old share link has not un-seen the room.
        if (revision > state.LastSeenRevision) state.LastSeenRevision = revision;
        state.LastVisitedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two tabs racing the first visit; the unique index settled it. Nothing to do.
            _db.ChangeTracker.Clear();
        }
    }

    internal static ChangeLogEntryDto ToDto(ChangeLogEntry e) => new()
    {
        Type = e.Type.ToString(),
        Label = MeaningfulChange.Describe(e.Type),
        IsMeaningful = e.IsMeaningful,
        Headline = e.Headline,
        WhyItMatters = e.WhyItMatters,
        ObjectType = e.ObjectType?.ToString(),
        ObjectId = e.ObjectId,
        FromValue = e.FromValue,
        ToValue = e.ToValue,
        CorrectionKind = e.CorrectionKind?.ToString(),
        Revision = e.RevisionNumber,
        CreatedAt = e.CreatedAt,
    };
}
