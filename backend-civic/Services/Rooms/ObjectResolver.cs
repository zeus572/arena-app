using Civic.API.Data;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Rooms;

/// <summary>A display-ready stub for one graph object, enough to render a link or a row.</summary>
public class ObjectSummary
{
    public ObjectType Type { get; set; }
    public Guid Id { get; set; }
    /// <summary>Addressable slug where the type has one; otherwise the id as a string.</summary>
    public string Slug { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Sublabel { get; set; }
    /// <summary>Evidence status word for claims; lifecycle status elsewhere. Null when N/A.</summary>
    public string? Status { get; set; }
}

/// <summary>
/// Hydrates <see cref="ObjectRef"/>s into display stubs with ONE query per object type.
///
/// This exists because the polymorphic edge table cannot be navigated with Include(). Every
/// read path goes through here; no controller loops over refs issuing per-row lookups.
/// </summary>
public class ObjectResolver
{
    private readonly CivicDbContext _db;

    public ObjectResolver(CivicDbContext db) => _db = db;

    /// <summary>
    /// Object types that have no resolver yet because the entity does not exist at this
    /// phase. <c>ObjectResolverTests</c> asserts this set plus the switch below covers every
    /// <see cref="ObjectType"/> member — so adding an object type and forgetting to resolve
    /// it fails a test rather than silently rendering a blank row.
    /// </summary>
    public static readonly IReadOnlySet<ObjectType> NotYetResolvable = new HashSet<ObjectType>
    {
        ObjectType.Room,                 // R1
        ObjectType.Actor,                // R2
        ObjectType.TimelineEvent,        // R2
        ObjectType.Development,          // R2
        ObjectType.Interaction,          // R4
        ObjectType.Prediction,           // R5
        ObjectType.MoneyItem,            // R6
        ObjectType.ConversationCluster,  // out of scope (PRD 08 Gate 3)
    };

    /// <summary>Types <see cref="ResolveTypeAsync"/> has a real case for. Kept beside
    /// <see cref="NotYetResolvable"/> so the two together must cover the whole enum.</summary>
    public static readonly IReadOnlySet<ObjectType> Resolvable = new HashSet<ObjectType>
    {
        ObjectType.Claim,
        ObjectType.SourceRef,
        ObjectType.Concept,
        ObjectType.Bill,
        ObjectType.NewsItem,
        ObjectType.Briefing,
        ObjectType.Provision,
    };

    public async Task<IReadOnlyDictionary<ObjectRef, ObjectSummary>> ResolveAsync(
        IEnumerable<ObjectRef> refs, CancellationToken ct = default)
    {
        var result = new Dictionary<ObjectRef, ObjectSummary>();

        foreach (var group in refs.Distinct().GroupBy(r => r.Type))
        {
            var ids = group.Select(r => r.Id).ToList();
            foreach (var summary in await ResolveTypeAsync(group.Key, ids, ct))
            {
                result[new ObjectRef(summary.Type, summary.Id)] = summary;
            }
        }

        return result;
    }

    private async Task<List<ObjectSummary>> ResolveTypeAsync(
        ObjectType type, List<Guid> ids, CancellationToken ct)
    {
        switch (type)
        {
            case ObjectType.Claim:
                return await _db.Set<Claim>().Where(c => ids.Contains(c.Id))
                    .Select(c => new ObjectSummary
                    {
                        Type = ObjectType.Claim,
                        Id = c.Id,
                        Slug = c.Slug,
                        Label = c.Text,
                        Status = c.Status.ToString(),
                    }).ToListAsync(ct);

            case ObjectType.SourceRef:
                return await _db.Set<SourceRef>().Where(s => ids.Contains(s.Id))
                    .Select(s => new ObjectSummary
                    {
                        Type = ObjectType.SourceRef,
                        Id = s.Id,
                        Slug = s.Id.ToString(),
                        Label = s.Title,
                        Sublabel = s.Organization,
                        Status = s.SourceType.ToString(),
                    }).ToListAsync(ct);

            case ObjectType.Concept:
                return await _db.Concepts.Where(c => ids.Contains(c.Id))
                    .Select(c => new ObjectSummary
                    {
                        Type = ObjectType.Concept,
                        Id = c.Id,
                        Slug = c.Slug,
                        Label = c.Title,
                        Sublabel = c.Category,
                    }).ToListAsync(ct);

            case ObjectType.Bill:
                return await _db.Bills.Where(b => ids.Contains(b.Id))
                    .Select(b => new ObjectSummary
                    {
                        Type = ObjectType.Bill,
                        Id = b.Id,
                        Slug = b.ExternalId,
                        Label = b.ShortTitle ?? b.Title,
                        Sublabel = b.Sponsor,
                        Status = b.Status.ToString(),
                    }).ToListAsync(ct);

            case ObjectType.NewsItem:
                return await _db.NewsItems.Where(n => ids.Contains(n.Id))
                    .Select(n => new ObjectSummary
                    {
                        Type = ObjectType.NewsItem,
                        Id = n.Id,
                        Slug = n.ExternalId,
                        Label = n.Headline,
                        Sublabel = n.Publisher ?? n.Source,
                    }).ToListAsync(ct);

            case ObjectType.Briefing:
                return await _db.Briefings.Where(b => ids.Contains(b.Id))
                    .Select(b => new ObjectSummary
                    {
                        Type = ObjectType.Briefing,
                        Id = b.Id,
                        Slug = b.Slug,
                        Label = b.Headline,
                        Sublabel = b.Institution,
                    }).ToListAsync(ct);

            case ObjectType.Provision:
                return await _db.Provisions.Where(p => ids.Contains(p.Id))
                    .Select(p => new ObjectSummary
                    {
                        Type = ObjectType.Provision,
                        Id = p.Id,
                        Slug = p.Slug,
                        Label = p.Title,
                        Status = p.State.ToString(),
                    }).ToListAsync(ct);

            default:
                // Everything remaining is in NotYetResolvable; the test keeps that true.
                return new List<ObjectSummary>();
        }
    }
}
