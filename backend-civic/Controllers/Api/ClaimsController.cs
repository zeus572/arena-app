using Civic.API.Data;
using Civic.API.Models.DTOs;
using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Controllers.Api;

/// <summary>
/// The claim ledger (PRD 04 §9, design 1n).
///
/// Claims are public and readable by anyone — the whole point of a claim ledger is that
/// the evidence trail is inspectable without an account. Status CHANGES are editorial and
/// live on AdminRoomsController.
///
/// False and Unsupported claims are served, not hidden. The ledger records that a claim
/// exists and what the evidence does about it; deleting one would erase the correction.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/claims")]
public class ClaimsController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ObjectLinkService _links;
    private readonly ObjectResolver _resolver;

    public ClaimsController(CivicDbContext db, ObjectLinkService links, ObjectResolver resolver)
    {
        _db = db;
        _links = links;
        _resolver = resolver;
    }

    /// <summary>
    /// The ledger, least-settled first by default — design 1n states the rationale plainly:
    /// "because that is where you are most likely to be misled."
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ClaimSummaryDto>>> List(
        [FromQuery] string? sort,
        [FromQuery] bool unsettledOnly = false,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);

        var query = _db.Claims.AsNoTracking();

        if (unsettledOnly)
        {
            query = query.Where(c => UnsettledStatuses.Contains(c.Status));
        }

        query = sort switch
        {
            "date" => query.OrderByDescending(c => c.FirstSeenAt),
            "reviewed" => query.OrderByDescending(c => c.LastReviewedAt),
            // Default: least settled first (design 1n). Written inline rather than as a
            // helper call so EF renders it as a CASE and the sort happens before Take —
            // sorting a truncated page would put the wrong claims at the top.
            _ => query
                .OrderByDescending(c =>
                    c.Status == ClaimStatus.Disputed ? 7 :
                    c.Status == ClaimStatus.PlausibleButUnresolved ? 6 :
                    c.Status == ClaimStatus.Unsupported ? 5 :
                    c.Status == ClaimStatus.Prediction ? 4 :
                    c.Status == ClaimStatus.Outdated ? 3 :
                    c.Status == ClaimStatus.False ? 2 :
                    c.Status == ClaimStatus.StronglySupported ? 1 : 0)
                .ThenByDescending(c => c.FirstSeenAt),
        };

        var claims = await query.Take(take).ToListAsync(ct);
        var counts = await EvidenceCountsAsync(claims.Select(c => c.Id).ToList(), ct);

        return Ok(claims.Select(c => ToSummary(c, counts)).ToList());
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ClaimDetailDto>> GetBySlug(string slug, CancellationToken ct)
    {
        var claim = await _db.Claims.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug, ct);
        if (claim is null) return NotFound();

        var self = new ObjectRef(ObjectType.Claim, claim.Id);

        var outgoing = await _links.OutgoingAsync(self, ct: ct);
        var incoming = await _links.DependentsAsync(self, ct: ct);

        var supportingIds = outgoing
            .Where(l => l.Relation == LinkRelation.SupportedBy && l.ToType == ObjectType.SourceRef)
            .Select(l => l.ToId).ToList();
        var contradictingIds = outgoing
            .Where(l => l.Relation == LinkRelation.ContradictedBy && l.ToType == ObjectType.SourceRef)
            .Select(l => l.ToId).ToList();

        var sourceIds = supportingIds.Concat(contradictingIds).Distinct().ToList();
        var sources = await _db.SourceRefs.AsNoTracking()
            .Where(s => sourceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        // Asserters (Claim -AssertedBy-> Actor) and appearances resolve through the same
        // helper; actor rows land in R2, until then those lists are simply empty.
        var asserterRefs = outgoing
            .Where(l => l.Relation == LinkRelation.AssertedBy)
            .Select(l => new ObjectRef(l.ToType, l.ToId)).ToList();
        var appearanceRefs = incoming
            .Select(l => new ObjectRef(l.FromType, l.FromId)).ToList();

        var resolved = await _resolver.ResolveAsync(asserterRefs.Concat(appearanceRefs), ct);

        var history = await _db.ClaimStatusHistories.AsNoTracking()
            .Where(h => h.ClaimId == claim.Id)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync(ct);

        var counts = new Dictionary<Guid, (int For, int Against)>
        {
            [claim.Id] = (supportingIds.Count, contradictingIds.Count),
        };

        var dto = new ClaimDetailDto
        {
            WhatWouldSettleIt = claim.WhatWouldSettleIt,
            GeographyScope = claim.GeographyScope,
            TimeScopeStart = claim.TimeScopeStart,
            TimeScopeEnd = claim.TimeScopeEnd,
            Confidence = claim.Confidence,
            FirstSeenAt = claim.FirstSeenAt,
            EvidenceFor = supportingIds.Where(sources.ContainsKey).Select(id => ToSourceDto(sources[id])).ToList(),
            EvidenceAgainst = contradictingIds.Where(sources.ContainsKey).Select(id => ToSourceDto(sources[id])).ToList(),
            AssertedBy = Appearances(outgoing.Where(l => l.Relation == LinkRelation.AssertedBy), resolved, outgoingDirection: true),
            AppearsIn = Appearances(incoming, resolved, outgoingDirection: false),
            StatusHistory = history.Select(h => new ClaimStatusHistoryDto
            {
                FromStatus = h.FromStatus?.ToString(),
                ToStatus = h.ToStatus.ToString(),
                ChangeKind = h.ChangeKind.ToString(),
                Rationale = h.Rationale,
                ChangedAt = h.ChangedAt,
                SourceCorrectedAt = h.SourceCorrectedAt,
            }).ToList(),
        };

        CopySummary(claim, counts, dto);
        return Ok(dto);
    }

    [HttpGet("{slug}/history")]
    public async Task<ActionResult<List<ClaimStatusHistoryDto>>> History(string slug, CancellationToken ct)
    {
        var claimId = await _db.Claims.AsNoTracking()
            .Where(c => c.Slug == slug).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        if (claimId is null) return NotFound();

        var rows = await _db.ClaimStatusHistories.AsNoTracking()
            .Where(h => h.ClaimId == claimId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync(ct);

        return Ok(rows.Select(h => new ClaimStatusHistoryDto
        {
            FromStatus = h.FromStatus?.ToString(),
            ToStatus = h.ToStatus.ToString(),
            ChangeKind = h.ChangeKind.ToString(),
            Rationale = h.Rationale,
            ChangedAt = h.ChangedAt,
            SourceCorrectedAt = h.SourceCorrectedAt,
        }).ToList());
    }

    // --- helpers ---------------------------------------------------------------------

    /// <summary>Statuses that mean "not settled" for the ledger's unsettled-only filter.</summary>
    private static readonly ClaimStatus[] UnsettledStatuses =
    {
        ClaimStatus.PlausibleButUnresolved,
        ClaimStatus.Disputed,
        ClaimStatus.Unsupported,
        ClaimStatus.Outdated,
        ClaimStatus.Prediction,
    };

    private async Task<Dictionary<Guid, (int For, int Against)>> EvidenceCountsAsync(
        List<Guid> claimIds, CancellationToken ct)
    {
        var rows = await _db.ObjectLinks.AsNoTracking()
            .Where(l => l.FromType == ObjectType.Claim
                     && claimIds.Contains(l.FromId)
                     && l.ValidTo == null
                     && (l.Relation == LinkRelation.SupportedBy || l.Relation == LinkRelation.ContradictedBy))
            .Select(l => new { l.FromId, l.Relation })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.FromId).ToDictionary(
            g => g.Key,
            g => (g.Count(x => x.Relation == LinkRelation.SupportedBy),
                  g.Count(x => x.Relation == LinkRelation.ContradictedBy)));
    }

    private static List<ClaimAppearanceDto> Appearances(
        IEnumerable<ObjectLink> links,
        IReadOnlyDictionary<ObjectRef, ObjectSummary> resolved,
        bool outgoingDirection)
    {
        var result = new List<ClaimAppearanceDto>();
        foreach (var link in links)
        {
            var key = outgoingDirection
                ? new ObjectRef(link.ToType, link.ToId)
                : new ObjectRef(link.FromType, link.FromId);

            if (!resolved.TryGetValue(key, out var summary)) continue;

            result.Add(new ClaimAppearanceDto
            {
                ObjectType = key.Type.ToString(),
                ObjectId = key.Id,
                Slug = summary.Slug,
                Label = summary.Label,
                Relation = link.Relation.ToString(),
            });
        }
        return result;
    }

    private static SourceRefDto ToSourceDto(SourceRef s) => new()
    {
        Id = s.Id,
        Url = s.Url,
        Title = s.Title,
        Author = s.Author,
        Organization = s.Organization,
        SourceType = s.SourceType.ToString(),
        IsPrimary = s.IsPrimary,
        PublishedAt = s.PublishedAt,
        RetrievedAt = s.RetrievedAt,
        Availability = s.Availability.ToString(),
        HasInterest = s.HasInterest,
        InterestNote = s.InterestNote,
    };

    private static ClaimSummaryDto ToSummary(Claim c, Dictionary<Guid, (int For, int Against)> counts)
    {
        var dto = new ClaimSummaryDto();
        CopySummary(c, counts, dto);
        return dto;
    }

    private static void CopySummary(
        Claim c, Dictionary<Guid, (int For, int Against)> counts, ClaimSummaryDto dto)
    {
        counts.TryGetValue(c.Id, out var n);
        dto.Id = c.Id;
        dto.Slug = c.Slug;
        dto.Text = c.Text;
        dto.Status = c.Status.ToString();
        dto.Kind = c.Kind.ToString();
        dto.EvidenceSummary = c.EvidenceSummary;
        dto.LastReviewedAt = c.LastReviewedAt;
        dto.StaleAsOf = c.StaleAsOf;
        dto.SupportingCount = n.For;
        dto.ContradictingCount = n.Against;
    }
}
