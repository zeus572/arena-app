using Civic.API.Data;
using Civic.API.Models.DTOs;
using Civic.API.Models.Rooms;
using Civic.API.Services;
using Civic.API.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Controllers.Api;

/// <summary>
/// The editorial console for Topic Rooms (designs 1y and 1z).
///
/// Gated by the "Admin" policy — the same email allowlist as the daily-games review queue.
///
/// On separated duties: PRD 07 §14 and design 1y assume an author, an editor and a
/// trust-and-safety reviewer. There is one operator. The gates still earn their place —
/// they force an explicit, recorded, per-revision sign-off, and that record IS the audit
/// artifact — but recording the same name three times is the honest implementation, and no
/// fake reviewer role is invented to pretend otherwise.
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/admin/rooms")]
public class AdminRoomsController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly PublishGateEvaluator _gates;
    private readonly CorrectionPropagationService _propagation;
    private readonly ObjectLinkService _links;
    private readonly ObjectResolver _resolver;

    public AdminRoomsController(
        CivicDbContext db,
        ICurrentUserService user,
        PublishGateEvaluator gates,
        CorrectionPropagationService propagation,
        ObjectLinkService links,
        ObjectResolver resolver)
    {
        _db = db;
        _user = user;
        _gates = gates;
        _propagation = propagation;
        _links = links;
        _resolver = resolver;
    }

    // ------------------------------------------------------------------ drafting pipeline

    /// <summary>
    /// What the R7 pipeline has produced and where it got stuck.
    ///
    /// Read-only, and deliberately not a queue. Nothing in the pipeline waits on this
    /// endpoint being called: the candidate pass and the draft pass both run to completion
    /// on their own, and their terminal state is Draft. Reviewing is something an operator
    /// may do, not a step the machinery blocks on — the thing that actually keeps drafts
    /// away from readers is that Draft is not a published status, not that someone is
    /// expected to look here.
    /// </summary>
    [HttpGet("pipeline")]
    public async Task<ActionResult<RoomPipelineDto>> Pipeline(
        [FromQuery] int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);

        var rows = await _db.StoryRooms.AsNoTracking()
            .Where(r => r.Status == RoomStatus.Candidate
                     || r.Status == RoomStatus.Drafting
                     || r.Status == RoomStatus.Draft)
            .OrderByDescending(r => r.DraftedAt ?? r.EventTime)
            .Take(take)
            .ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();

        // Claim counts come from the edges rather than a column, same as everywhere else.
        var claimCounts = (await _db.Set<ObjectLink>().AsNoTracking()
                .Where(l => l.FromType == ObjectType.Room
                         && ids.Contains(l.FromId)
                         && l.Relation == LinkRelation.EssentialFact
                         && l.ToType == ObjectType.Claim
                         && l.ValidTo == null)
                .Select(l => l.FromId)
                .ToListAsync(ct))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(new RoomPipelineDto
        {
            CandidateCount = rows.Count(r => r.Status == RoomStatus.Candidate),
            DraftingCount = rows.Count(r => r.Status == RoomStatus.Drafting),
            DraftCount = rows.Count(r => r.Status == RoomStatus.Draft),
            // A candidate that has burned its attempts will never be retried. It is not an
            // error anywhere; it just silently stops, so it is surfaced as its own number.
            ExhaustedCount = rows.Count(r =>
                r.Status == RoomStatus.Candidate && r.DraftAttemptCount >= 3),
            Items = rows.Select(r => new RoomPipelineItemDto
            {
                Slug = r.Slug,
                Title = r.Title,
                Status = r.Status.ToString(),
                SourceKind = r.SourceBriefingId is not null ? "Briefing"
                    : r.SourceBillId is not null ? "Bill"
                    : "None",
                EventTime = r.EventTime,
                DraftedAt = r.DraftedAt,
                DraftAttemptCount = r.DraftAttemptCount,
                DraftModelId = r.DraftModelId,
                DraftPromptVersion = r.DraftPromptVersion,
                LastError = r.LastError,
                ClaimCount = claimCounts.TryGetValue(r.Id, out var n) ? n : 0,
            }).ToList(),
        });
    }

    // ------------------------------------------------------------------ gates & publish

    /// <summary>Every gate's current verdict for a room, plus who has cleared what.</summary>
    [HttpGet("{slug}/gates")]
    public async Task<ActionResult<RoomGatesDto>> Gates(string slug, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Slug == slug, ct);
        if (room is null) return NotFound();

        return Ok(await EvaluateGatesAsync(room, ct));
    }

    /// <summary>
    /// Record that a named person cleared a gate for the room's CURRENT revision.
    ///
    /// Editing the room afterwards bumps the revision and re-opens every gate, because the
    /// sign-off attested to text that no longer exists.
    /// </summary>
    [HttpPost("{slug}/gates/{gate}/clear")]
    public async Task<IActionResult> ClearGate(
        string slug, string gate, [FromBody] ClearGateRequest? body, CancellationToken ct)
    {
        if (!Enum.TryParse<PublishGateKey>(gate, ignoreCase: true, out var key))
        {
            return BadRequest(new { error = "Unknown gate.", code = "bad_gate" });
        }

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Slug == slug, ct);
        if (room is null) return NotFound();

        var actor = _user.GetCurrentUserId();

        var existing = await _db.PublishGateResults.FirstOrDefaultAsync(
            g => g.RoomId == room.Id && g.Gate == key && g.RoomRevision == room.Revision, ct);

        if (existing is null)
        {
            existing = new PublishGateResult
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                Gate = key,
                RoomRevision = room.Revision,
            };
            _db.PublishGateResults.Add(existing);
        }

        existing.Passed = true;
        existing.ClearedBy = actor;
        existing.ClearedAt = DateTime.UtcNow;
        existing.Detail = body?.Note ?? "Cleared by reviewer.";

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Publish, if every blocking gate is met.
    ///
    /// Returns 409 with the unmet gates rather than a bare failure — "cannot publish" is
    /// useless without "here is what to fix".
    /// </summary>
    [HttpPost("{slug}/publish")]
    public async Task<ActionResult<RoomGatesDto>> Publish(string slug, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Slug == slug, ct);
        if (room is null) return NotFound();

        var gates = await EvaluateGatesAsync(room, ct);

        if (!gates.CanPublish) return Conflict(gates);

        room.Status = RoomStatus.Published;
        room.PublishedAt ??= DateTime.UtcNow;
        room.LastReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        gates.Status = room.Status.ToString();
        return Ok(gates);
    }

    [HttpPost("{slug}/unpublish")]
    public async Task<IActionResult> Unpublish(string slug, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Slug == slug, ct);
        if (room is null) return NotFound();

        room.Status = RoomStatus.Draft;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ------------------------------------------------------------------ corrections

    /// <summary>
    /// Move a claim's evidence status and fan the consequences out (design 1z).
    ///
    /// <c>sourceCorrectedAt</c> is required for correction and retraction moves: the
    /// published service-level metric is time-from-SOURCE-correction, and that cannot be
    /// derived from anything we observe.
    /// </summary>
    [HttpPost("claims/{claimSlug}/status")]
    public async Task<ActionResult<PropagationDto>> ChangeClaimStatus(
        string claimSlug, [FromBody] ChangeClaimStatusRequest body, CancellationToken ct)
    {
        if (!Enum.TryParse<ClaimStatus>(body.Status, ignoreCase: true, out var status))
        {
            return BadRequest(new { error = "Unknown claim status.", code = "bad_status" });
        }

        if (!Enum.TryParse<StatusChangeKind>(body.ChangeKind, ignoreCase: true, out var kind))
        {
            return BadRequest(new { error = "Unknown change kind.", code = "bad_change_kind" });
        }

        if (string.IsNullOrWhiteSpace(body.Rationale))
        {
            return BadRequest(new { error = "A rationale is required.", code = "rationale_required" });
        }

        if (kind is StatusChangeKind.Correction or StatusChangeKind.Retraction
            && body.SourceCorrectedAt is null)
        {
            return BadRequest(new
            {
                error = "sourceCorrectedAt is required for a correction or retraction — the "
                      + "published metric is time-from-source-correction and cannot be derived.",
                code = "source_corrected_at_required",
            });
        }

        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Slug == claimSlug, ct);
        if (claim is null) return NotFound();

        var result = await _propagation.OnClaimStatusChangedAsync(
            claim.Id, status, kind, body.Rationale, _user.GetCurrentUserId(),
            body.SourceCorrectedAt, body.TriggerSourceRefId, ct);

        return Ok(await ToDtoAsync(result, ct));
    }

    /// <summary>The fan-out view for one claim (design 1z), without re-running it.</summary>
    [HttpGet("propagation/{claimSlug}")]
    public async Task<ActionResult<PropagationDto>> Propagation(string claimSlug, CancellationToken ct)
    {
        var claim = await _db.Claims.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == claimSlug, ct);
        if (claim is null) return NotFound();

        var dependents = await _links.DependentsAsync(new ObjectRef(ObjectType.Claim, claim.Id), ct: ct);
        var flags = await _db.ReviewFlags.AsNoTracking()
            .Where(f => f.TriggerObjectId == claim.Id && f.ResolvedAt == null)
            .ToListAsync(ct);

        var refs = dependents.Select(l => new ObjectRef(l.FromType, l.FromId)).ToList();
        var resolved = await _resolver.ResolveAsync(refs, ct);

        return Ok(new PropagationDto
        {
            ClaimSlug = claim.Slug,
            ClaimText = claim.Text,
            CurrentStatus = claim.Status.ToString(),
            ShareImpressionsWithOldWording = claim.ShareImpressionCount,
            Dependents = refs.Select(r => new PropagationTargetDto
            {
                ObjectType = r.Type.ToString(),
                ObjectId = r.Id,
                Label = resolved.TryGetValue(r, out var s) ? s.Label : "(unresolved)",
                Slug = resolved.TryGetValue(r, out var s2) ? s2.Slug : r.Id.ToString(),
            }).ToList(),
            Flags = flags.Select(ToFlagDto).ToList(),
        });
    }

    /// <summary>Mark a source retracted or removed; every claim resting on it gets flagged.</summary>
    [HttpPost("sources/{id:guid}/withdraw")]
    public async Task<ActionResult<PropagationDto>> WithdrawSource(
        Guid id, [FromBody] WithdrawSourceRequest body, CancellationToken ct)
    {
        if (!Enum.TryParse<SourceAvailability>(body.Availability, ignoreCase: true, out var availability))
        {
            return BadRequest(new { error = "Unknown availability.", code = "bad_availability" });
        }

        var result = await _propagation.OnSourceWithdrawnAsync(
            id, availability, _user.GetCurrentUserId(), ct);

        return Ok(await ToDtoAsync(result, ct));
    }

    // ------------------------------------------------------------------ review queue

    /// <summary>
    /// The review queue. Oldest first, because the six-hour clock is what matters here.
    /// </summary>
    [HttpGet("flags")]
    public async Task<ActionResult<List<ReviewFlagDto>>> Flags(
        [FromQuery] bool resolved = false, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 300);

        var flags = await _db.ReviewFlags.AsNoTracking()
            .Where(f => resolved ? f.ResolvedAt != null : f.ResolvedAt == null)
            .OrderBy(f => f.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        return Ok(flags.Select(ToFlagDto).ToList());
    }

    [HttpPost("flags/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveFlag(
        Guid id, [FromBody] ResolveFlagRequest body, CancellationToken ct)
    {
        if (!Enum.TryParse<ReviewResolution>(body.Resolution, ignoreCase: true, out var resolution)
            || resolution == ReviewResolution.Pending)
        {
            return BadRequest(new { error = "Unknown resolution.", code = "bad_resolution" });
        }

        // Deciding no change was needed is a real editorial judgement, so it has to be
        // written down. Otherwise "Overridden" becomes the button people click to clear
        // the queue without reading it.
        if (resolution == ReviewResolution.Overridden && string.IsNullOrWhiteSpace(body.Note))
        {
            return BadRequest(new
            {
                error = "Overriding a flag requires a note explaining why no change was needed.",
                code = "override_note_required",
            });
        }

        var flag = await _db.ReviewFlags.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (flag is null) return NotFound();

        flag.Resolution = resolution;
        flag.ResolvedAt = DateTime.UtcNow;
        flag.ResolvedBy = _user.GetCurrentUserId();
        flag.ResolutionNote = body.Note;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ------------------------------------------------------------------ metrics & integrity

    /// <summary>
    /// The trust metrics PRD 01 §11 and design 1z commit to publishing.
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<RoomMetricsDto>> Metrics(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var openFlags = await _db.ReviewFlags.AsNoTracking()
            .Where(f => f.ResolvedAt == null).ToListAsync(ct);

        // Time from the SOURCE's correction to ours — not from when we noticed.
        var lags = await _db.ClaimStatusHistories.AsNoTracking()
            .Where(h => h.SourceCorrectedAt != null)
            .Select(h => new { h.ChangedAt, h.SourceCorrectedAt })
            .ToListAsync(ct);

        var hours = lags
            .Select(l => (l.ChangedAt - l.SourceCorrectedAt!.Value).TotalHours)
            .Where(h => h >= 0)
            .OrderBy(h => h)
            .ToList();

        var essentialFactLinks = await _db.ObjectLinks.AsNoTracking()
            .Where(l => l.Relation == LinkRelation.EssentialFact && l.ValidTo == null)
            .Select(l => l.ToId).ToListAsync(ct);

        var sourced = await _db.ObjectLinks.AsNoTracking()
            .Where(l => l.FromType == ObjectType.Claim
                     && essentialFactLinks.Contains(l.FromId)
                     && l.Relation == LinkRelation.SupportedBy
                     && l.ValidTo == null)
            .Select(l => l.FromId).Distinct().CountAsync(ct);

        return Ok(new RoomMetricsDto
        {
            OpenFlags = openFlags.Count,
            FlagsPastGrace = openFlags.Count(f => now - f.CreatedAt >= RoomVisibility.UnreviewedGrace),
            FlagsPastEscalation = openFlags.Count(f => RoomVisibility.NeedsEscalation(f, now)),
            RoomsNeedingCorrection = await _db.Rooms
                .CountAsync(r => r.Status == RoomStatus.CorrectionRequired, ct),
            MedianHoursFromSourceCorrection = hours.Count == 0 ? null : hours[hours.Count / 2],
            EssentialFacts = essentialFactLinks.Distinct().Count(),
            EssentialFactsWithASource = sourced,
            CorrectionsIssued = await _db.ClaimStatusHistories
                .CountAsync(h => h.ChangeKind == StatusChangeKind.Correction
                              || h.ChangeKind == StatusChangeKind.Retraction, ct),
        });
    }

    /// <summary>
    /// Dangling edges — the price of a polymorphic graph, paid down by looking.
    ///
    /// Graph objects are never hard-deleted, so this should stay at zero; a non-zero result
    /// means something bypassed that rule.
    /// </summary>
    [HttpGet("integrity")]
    public async Task<ActionResult<List<DanglingEdgeDto>>> Integrity(CancellationToken ct)
    {
        var edges = await _db.ObjectLinks.AsNoTracking()
            .Where(l => l.ValidTo == null)
            .ToListAsync(ct);

        var refs = edges
            .Select(l => new ObjectRef(l.FromType, l.FromId))
            .Concat(edges.Select(l => new ObjectRef(l.ToType, l.ToId)))
            .Distinct()
            .Where(r => !ObjectResolver.NotYetResolvable.Contains(r.Type))
            .ToList();

        var resolved = await _resolver.ResolveAsync(refs, ct);

        var dangling = new List<DanglingEdgeDto>();
        foreach (var edge in edges)
        {
            foreach (var (r, end) in new[]
            {
                (new ObjectRef(edge.FromType, edge.FromId), "From"),
                (new ObjectRef(edge.ToType, edge.ToId), "To"),
            })
            {
                if (ObjectResolver.NotYetResolvable.Contains(r.Type)) continue;
                if (resolved.ContainsKey(r)) continue;

                dangling.Add(new DanglingEdgeDto
                {
                    EdgeId = edge.Id,
                    Relation = LinkSchema.Describe(edge.FromType, edge.Relation, edge.ToType),
                    MissingEnd = end,
                    MissingType = r.Type.ToString(),
                    MissingId = r.Id,
                });
            }
        }

        return Ok(dangling);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<RoomGatesDto> EvaluateGatesAsync(Room room, CancellationToken ct)
    {
        var bundle = await HydrateAsync(room, ct);
        var findings = _gates.Evaluate(bundle);

        var cleared = await _db.PublishGateResults.AsNoTracking()
            .Where(g => g.RoomId == room.Id && g.RoomRevision == room.Revision && g.Passed)
            .ToDictionaryAsync(g => g.Gate, ct);

        var dto = new RoomGatesDto
        {
            Slug = room.Slug,
            Revision = room.Revision,
            Status = room.Status.ToString(),
        };

        foreach (var f in findings)
        {
            cleared.TryGetValue(f.Gate, out var sign);

            dto.Gates.Add(new GateDto
            {
                Gate = f.Gate.ToString(),
                AutomatedPass = f.Passed,
                RequiresNamedApproval = f.RequiresNamedApproval,
                ClearedBy = sign?.ClearedBy,
                ClearedAt = sign?.ClearedAt,
                Detail = f.Detail,
                // A gate is met when the automated check passes AND, where editorial
                // judgement is required, a named person has actually clicked.
                Met = f.Passed && (!f.RequiresNamedApproval || sign is not null),
            });
        }

        return dto;
    }

    private async Task<RoomBundle> HydrateAsync(Room room, CancellationToken ct)
    {
        var outgoing = await _links.OutgoingAsync(new ObjectRef(ObjectType.Room, room.Id), ct: ct);

        var claimIds = outgoing
            .Where(l => l.ToType == ObjectType.Claim)
            .Select(l => l.ToId).Distinct().ToList();

        var claims = await _db.Claims.AsNoTracking()
            .Where(c => claimIds.Contains(c.Id)).ToListAsync(ct);

        var evidence = await _db.ObjectLinks.AsNoTracking()
            .Where(l => l.FromType == ObjectType.Claim
                     && claimIds.Contains(l.FromId)
                     && l.ToType == ObjectType.SourceRef
                     && l.ValidTo == null
                     && (l.Relation == LinkRelation.SupportedBy
                      || l.Relation == LinkRelation.ContradictedBy))
            .ToListAsync(ct);

        var sourceIds = evidence.Select(l => l.ToId).Distinct().ToList();
        var sources = await _db.SourceRefs.AsNoTracking()
            .Where(s => sourceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        List<SourceRef> For(Guid claimId) => evidence
            .Where(l => l.FromId == claimId && l.Relation == LinkRelation.SupportedBy)
            .Select(l => sources.TryGetValue(l.ToId, out var s) ? s : null)
            .Where(s => s is not null).Select(s => s!).ToList();

        List<SourceRef> Against(Guid claimId) => evidence
            .Where(l => l.FromId == claimId && l.Relation == LinkRelation.ContradictedBy)
            .Select(l => sources.TryGetValue(l.ToId, out var s) ? s : null)
            .Where(s => s is not null).Select(s => s!).ToList();

        return new RoomBundle
        {
            Room = room,
            Claims = claims,
            Sources = sources.Values.ToList(),
            Timeline = await _db.TimelineEvents.AsNoTracking()
                .Where(t => t.RoomId == room.Id).ToListAsync(ct),
            Developments = await _db.Developments.AsNoTracking()
                .Where(d => d.RoomId == room.Id).ToListAsync(ct),
            EvidenceFor = claimIds.ToDictionary(id => id, For),
            EvidenceAgainst = claimIds.ToDictionary(id => id, Against),
            EssentialFactClaimIds = outgoing
                .Where(l => l.Relation == LinkRelation.EssentialFact)
                .Select(l => l.ToId).ToHashSet(),
        };
    }

    private async Task<PropagationDto> ToDtoAsync(PropagationResult result, CancellationToken ct)
    {
        var claim = result.ClaimId == Guid.Empty
            ? null
            : await _db.Claims.AsNoTracking().FirstOrDefaultAsync(c => c.Id == result.ClaimId, ct);

        var refs = result.Flagged
            .Select(f => new ObjectRef(f.ObjectType, f.ObjectId))
            .Concat(result.RoomsUpdated.Select(id => new ObjectRef(ObjectType.Room, id)))
            .Distinct().ToList();

        var resolved = await _resolver.ResolveAsync(refs, ct);

        return new PropagationDto
        {
            ClaimSlug = claim?.Slug ?? "",
            ClaimText = claim?.Text ?? "",
            CurrentStatus = result.ToStatus,
            PreviousStatus = result.FromStatus,
            ShareImpressionsWithOldWording = result.ShareImpressionsWithOldWording,
            RoomsUpdated = result.RoomsUpdated.Count,
            Dependents = refs.Select(r => new PropagationTargetDto
            {
                ObjectType = r.Type.ToString(),
                ObjectId = r.Id,
                Label = resolved.TryGetValue(r, out var s) ? s.Label : "(unresolved)",
                Slug = resolved.TryGetValue(r, out var s2) ? s2.Slug : r.Id.ToString(),
            }).ToList(),
            Flags = result.Flagged.Select(ToFlagDto).ToList(),
        };
    }

    private static ReviewFlagDto ToFlagDto(ReviewFlag f) => new()
    {
        Id = f.Id,
        ObjectType = f.ObjectType.ToString(),
        ObjectId = f.ObjectId,
        Reason = f.Reason.ToString(),
        Action = f.Action.ToString(),
        Detail = f.Detail,
        Resolution = f.Resolution.ToString(),
        CreatedAt = f.CreatedAt,
        ResolvedAt = f.ResolvedAt,
        ResolvedBy = f.ResolvedBy,
        HoursOpen = f.ResolvedAt is null
            ? (DateTime.UtcNow - f.CreatedAt).TotalHours
            : (f.ResolvedAt.Value - f.CreatedAt).TotalHours,
    };
}

// ---------------------------------------------------------------------- requests

public class ClearGateRequest
{
    public string? Note { get; set; }
}

public class ChangeClaimStatusRequest
{
    public string Status { get; set; } = "";
    public string ChangeKind { get; set; } = "NewEvidence";
    public string Rationale { get; set; } = "";
    /// <summary>Required for Correction and Retraction.</summary>
    public DateTime? SourceCorrectedAt { get; set; }
    public Guid? TriggerSourceRefId { get; set; }
}

public class WithdrawSourceRequest
{
    public string Availability { get; set; } = "Retracted";
}

public class ResolveFlagRequest
{
    public string Resolution { get; set; } = "Reviewed";
    public string? Note { get; set; }
}

// ---------------------------------------------------------------------- responses

public class GateDto
{
    public string Gate { get; set; } = "";
    /// <summary>Whether the mechanical check passed.</summary>
    public bool AutomatedPass { get; set; }
    /// <summary>Whether a human must also sign, regardless of the automated result.</summary>
    public bool RequiresNamedApproval { get; set; }
    public string? ClearedBy { get; set; }
    public DateTime? ClearedAt { get; set; }
    public string Detail { get; set; } = "";
    /// <summary>The gate is satisfied for this revision.</summary>
    public bool Met { get; set; }
}

public class RoomGatesDto
{
    public string Slug { get; set; } = "";
    public int Revision { get; set; }
    public string Status { get; set; } = "";
    public List<GateDto> Gates { get; set; } = new();
    public bool CanPublish => Gates.All(g => g.Met);
    public List<string> Unmet => Gates.Where(g => !g.Met).Select(g => g.Gate).ToList();
}

public class ReviewFlagDto
{
    public Guid Id { get; set; }
    public string ObjectType { get; set; } = "";
    public Guid ObjectId { get; set; }
    public string Reason { get; set; } = "";
    /// <summary>Rewrite | Revalidate | Review | Logged.</summary>
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Resolution { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    /// <summary>Drives the six-hour line in the queue.</summary>
    public double HoursOpen { get; set; }
}

public class PropagationTargetDto
{
    public string ObjectType { get; set; } = "";
    public Guid ObjectId { get; set; }
    public string Slug { get; set; } = "";
    public string Label { get; set; } = "";
}

public class PropagationDto
{
    public string ClaimSlug { get; set; } = "";
    public string ClaimText { get; set; } = "";
    public string CurrentStatus { get; set; } = "";
    public string? PreviousStatus { get; set; }
    public int RoomsUpdated { get; set; }
    /// <summary>Cards render live; this counts who saw the old wording and cannot be reached.</summary>
    public int ShareImpressionsWithOldWording { get; set; }
    public List<PropagationTargetDto> Dependents { get; set; } = new();
    public List<ReviewFlagDto> Flags { get; set; } = new();
}

public class RoomMetricsDto
{
    public int OpenFlags { get; set; }
    public int FlagsPastGrace { get; set; }
    public int FlagsPastEscalation { get; set; }
    public int RoomsNeedingCorrection { get; set; }
    /// <summary>Time from the SOURCE's correction to ours. Null until there is one.</summary>
    public double? MedianHoursFromSourceCorrection { get; set; }
    public int EssentialFacts { get; set; }
    public int EssentialFactsWithASource { get; set; }
    public int CorrectionsIssued { get; set; }
}

public class DanglingEdgeDto
{
    public Guid EdgeId { get; set; }
    public string Relation { get; set; } = "";
    public string MissingEnd { get; set; } = "";
    public string MissingType { get; set; } = "";
    public Guid MissingId { get; set; }
}
