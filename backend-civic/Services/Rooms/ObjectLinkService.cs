using Civic.API.Data;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Rooms;

/// <summary>
/// The only supported way to read and write <see cref="ObjectLink"/> rows.
///
/// Attaching goes through <see cref="LinkSchema"/>, and detaching retires an edge by
/// stamping <see cref="ObjectLink.ValidTo"/> rather than deleting it — the graph keeps its
/// history so "who was on this committee in March" stays answerable (PRD 04 §6.4).
/// </summary>
public class ObjectLinkService
{
    private readonly CivicDbContext _db;

    public ObjectLinkService(CivicDbContext db) => _db = db;

    /// <summary>
    /// Attach an edge, or return the existing open one. Idempotent: the filtered unique
    /// index on (From, Relation, To) WHERE ValidTo IS NULL makes a concurrent double-attach
    /// a no-op rather than a duplicate.
    /// </summary>
    public async Task<ObjectLink> LinkAsync(
        ObjectRef from,
        LinkRelation relation,
        ObjectRef to,
        int ordinal = 0,
        double confidence = 1.0,
        Guid? sourceRefId = null,
        ProvenanceOrigin proposedBy = ProvenanceOrigin.Human,
        string? verifiedBy = null,
        string? note = null,
        CancellationToken ct = default)
    {
        if (!LinkSchema.IsAllowed(from.Type, relation, to.Type))
        {
            throw new InvalidLinkException(from.Type, relation, to.Type);
        }

        var existing = await _db.Set<ObjectLink>().FirstOrDefaultAsync(
            l => l.FromType == from.Type && l.FromId == from.Id
              && l.Relation == relation
              && l.ToType == to.Type && l.ToId == to.Id
              && l.ValidTo == null, ct);

        if (existing is not null)
        {
            existing.Ordinal = ordinal;
            existing.Confidence = confidence;
            if (sourceRefId is not null) existing.SourceRefId = sourceRefId;
            if (note is not null) existing.Note = note;
            if (verifiedBy is not null)
            {
                existing.VerifiedBy = verifiedBy;
                existing.VerifiedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var link = new ObjectLink
        {
            Id = Guid.NewGuid(),
            FromType = from.Type,
            FromId = from.Id,
            Relation = relation,
            ToType = to.Type,
            ToId = to.Id,
            Ordinal = ordinal,
            Confidence = confidence,
            SourceRefId = sourceRefId,
            ProposedBy = proposedBy,
            VerifiedBy = verifiedBy,
            VerifiedAt = verifiedBy is null ? null : DateTime.UtcNow,
            Note = note,
        };

        _db.Set<ObjectLink>().Add(link);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race against another writer; the index did its job. Adopt theirs.
            _db.Entry(link).State = EntityState.Detached;
            var winner = await _db.Set<ObjectLink>().FirstOrDefaultAsync(
                l => l.FromType == from.Type && l.FromId == from.Id
                  && l.Relation == relation
                  && l.ToType == to.Type && l.ToId == to.Id
                  && l.ValidTo == null, ct);
            if (winner is null) throw;
            return winner;
        }

        return link;
    }

    /// <summary>Retire an edge by closing its validity window. Never deletes.</summary>
    public async Task<bool> UnlinkAsync(
        ObjectRef from, LinkRelation relation, ObjectRef to, CancellationToken ct = default)
    {
        var link = await _db.Set<ObjectLink>().FirstOrDefaultAsync(
            l => l.FromType == from.Type && l.FromId == from.Id
              && l.Relation == relation
              && l.ToType == to.Type && l.ToId == to.Id
              && l.ValidTo == null, ct);

        if (link is null) return false;

        link.ValidTo = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// THE fan-out query: every currently-valid edge pointing AT <paramref name="target"/>.
    ///
    /// This one indexed scan is the entire reason the graph is a single polymorphic table.
    /// When a claim's status moves, this is how the propagation service learns which rooms,
    /// interactions and developments have to be reviewed.
    /// </summary>
    public Task<List<ObjectLink>> DependentsAsync(
        ObjectRef target, LinkRelation? relation = null, CancellationToken ct = default)
    {
        var q = _db.Set<ObjectLink>().AsNoTracking()
            .Where(l => l.ToType == target.Type && l.ToId == target.Id && l.ValidTo == null);

        if (relation is { } r) q = q.Where(l => l.Relation == r);

        return q.OrderBy(l => l.FromType).ThenBy(l => l.Ordinal).ToListAsync(ct);
    }

    /// <summary>Every currently-valid edge leaving <paramref name="source"/>.</summary>
    public Task<List<ObjectLink>> OutgoingAsync(
        ObjectRef source, LinkRelation? relation = null, CancellationToken ct = default)
    {
        var q = _db.Set<ObjectLink>().AsNoTracking()
            .Where(l => l.FromType == source.Type && l.FromId == source.Id && l.ValidTo == null);

        if (relation is { } r) q = q.Where(l => l.Relation == r);

        return q.OrderBy(l => l.Ordinal).ThenBy(l => l.CreatedAt).ToListAsync(ct);
    }

    /// <summary>
    /// Outgoing edges from several sources at once, grouped by source. Used by the room
    /// read path so a room with ten section-fulls of references is still two queries
    /// (edges, then <see cref="ObjectResolver"/>) rather than N.
    /// </summary>
    public async Task<ILookup<ObjectRef, ObjectLink>> OutgoingManyAsync(
        IReadOnlyCollection<ObjectRef> sources, CancellationToken ct = default)
    {
        if (sources.Count == 0) return Array.Empty<ObjectLink>().ToLookup(_ => default(ObjectRef));

        var byType = sources.GroupBy(s => s.Type).ToList();
        var all = new List<ObjectLink>();

        foreach (var group in byType)
        {
            var type = group.Key;
            var ids = group.Select(g => g.Id).ToList();
            all.AddRange(await _db.Set<ObjectLink>().AsNoTracking()
                .Where(l => l.FromType == type && ids.Contains(l.FromId) && l.ValidTo == null)
                .ToListAsync(ct));
        }

        return all
            .OrderBy(l => l.Ordinal).ThenBy(l => l.CreatedAt)
            .ToLookup(l => new ObjectRef(l.FromType, l.FromId));
    }
}
