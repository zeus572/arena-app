using Civic.API.Data;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Rooms;

/// <summary>What a status change did, as rendered by design 1z.</summary>
public class PropagationResult
{
    public Guid ClaimId { get; set; }
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";

    /// <summary>Rooms whose changelog got an entry, with no human needed.</summary>
    public List<Guid> RoomsUpdated { get; set; } = new();

    /// <summary>Objects a person now has to look at.</summary>
    public List<ReviewFlag> Flagged { get; set; } = new();

    /// <summary>
    /// How many people saw a share card carrying the old wording.
    ///
    /// There is no share-card entity and no recall mechanism, and inventing one would be
    /// worse than counting honestly. Design 1z records the number; that is the whole
    /// commitment.
    /// </summary>
    public int ShareImpressionsWithOldWording { get; set; }

    public int TotalDependents => RoomsUpdated.Count + Flagged.Count;
}

/// <summary>
/// Correction fan-out (design 1z, PRD 04 §14.2).
///
/// This is the capability PRD 08 Gate 1 blocks the whole feature on: Theme Rooms do not
/// ship until a correction demonstrably propagates. It is also the reason the graph is one
/// polymorphic edge table — the dependency question is a single indexed scan here.
///
/// The split is exactly design 1z's: what changes automatically, and what a human has to
/// look at. Nothing in the second list is silently fixed.
/// </summary>
public class CorrectionPropagationService
{
    private readonly CivicDbContext _db;
    private readonly ObjectLinkService _links;
    private readonly RoomRevisionService _revisions;
    private readonly ILogger<CorrectionPropagationService> _log;

    public CorrectionPropagationService(
        CivicDbContext db,
        ObjectLinkService links,
        RoomRevisionService revisions,
        ILogger<CorrectionPropagationService> log)
    {
        _db = db;
        _links = links;
        _revisions = revisions;
        _log = log;
    }

    /// <summary>
    /// Move a claim's status and fan the consequences out.
    /// </summary>
    /// <param name="sourceCorrectedAt">
    /// When the ORIGINAL source issued its correction. Required for correction-driven moves,
    /// because the published metric is time-from-source-correction and it cannot be derived
    /// from anything we observe.
    /// </param>
    public async Task<PropagationResult> OnClaimStatusChangedAsync(
        Guid claimId,
        ClaimStatus toStatus,
        StatusChangeKind changeKind,
        string rationale,
        string actor,
        DateTime? sourceCorrectedAt = null,
        Guid? triggerSourceRefId = null,
        CancellationToken ct = default)
    {
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId, ct)
            ?? throw new InvalidOperationException($"Claim {claimId} not found.");

        var fromStatus = claim.Status;
        var result = new PropagationResult
        {
            ClaimId = claimId,
            FromStatus = fromStatus.ToString(),
            ToStatus = toStatus.ToString(),
            ShareImpressionsWithOldWording = claim.ShareImpressionCount,
        };

        // --- automatic ------------------------------------------------------------------
        // The mark and label update everywhere by doing NOTHING, because every surface
        // renders the status from this row. That is why room copy may never cache it.
        claim.Status = toStatus;
        claim.LastReviewedAt = DateTime.UtcNow;
        claim.ReviewedBy = actor;
        if (toStatus == ClaimStatus.Outdated && claim.StaleAsOf is null)
        {
            claim.StaleAsOf = DateTime.UtcNow;
        }

        _db.ClaimStatusHistories.Add(new ClaimStatusHistory
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangeKind = changeKind,
            Rationale = rationale,
            TriggerSourceRefId = triggerSourceRefId,
            SourceCorrectedAt = sourceCorrectedAt,
            ChangedBy = actor,
        });

        await _db.SaveChangesAsync(ct);

        // --- the dependency query -------------------------------------------------------
        // One indexed scan on (ToType, ToId). This single statement is the complete answer
        // to "what depends on this claim?", and the reason the graph is not typed tables.
        var dependents = await _links.DependentsAsync(new ObjectRef(ObjectType.Claim, claimId), ct: ct);

        var isCorrection = changeKind is StatusChangeKind.Correction or StatusChangeKind.Retraction;

        foreach (var group in dependents.GroupBy(l => l.FromType))
        {
            switch (group.Key)
            {
                case ObjectType.Room:
                    await PropagateToRoomsAsync(group.ToList(), claim, fromStatus, toStatus,
                        isCorrection, actor, result, ct);
                    break;

                case ObjectType.Interaction:
                    await FlagInteractionsAsync(group.ToList(), claim, fromStatus, toStatus, result, ct);
                    break;

                case ObjectType.Development:
                    await SyncDevelopmentsAsync(group.ToList(), claim, toStatus, result, ct);
                    break;

                case ObjectType.ConversationCluster:
                    await FlagAsync(group.Select(l => new ObjectRef(l.FromType, l.FromId)),
                        ReviewReason.DependsOnChangedClaim, ReviewAction.Review, claim,
                        $"Cluster framing references a claim now marked {toStatus}.", result, ct);
                    break;

                case ObjectType.Prediction:
                    await FlagAsync(group.Select(l => new ObjectRef(l.FromType, l.FromId)),
                        ReviewReason.DependsOnChangedClaim, ReviewAction.Review, claim,
                        $"Prediction is about a claim now marked {toStatus}.", result, ct);
                    break;
            }
        }

        // Share cards render live, so the wording corrects itself. What cannot be corrected
        // is the set of people who already read the old one — so we record how many.
        if (isCorrection && claim.ShareImpressionCount > 0)
        {
            _log.LogWarning(
                "Claim {ClaimId} corrected after {Impressions} share-card impressions of the "
              + "previous wording. Cards render live; prior readers cannot be reached.",
                claimId, claim.ShareImpressionCount);
        }

        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Claim {ClaimId} moved {From} -> {To} ({Kind}); {Rooms} rooms updated, {Flags} flagged.",
            claimId, fromStatus, toStatus, changeKind, result.RoomsUpdated.Count, result.Flagged.Count);

        return result;
    }

    /// <summary>
    /// A source was retracted or removed. Every claim resting on it needs a fresh look —
    /// PRD 04 §14.3 requires the platform be able to identify them.
    /// </summary>
    public async Task<PropagationResult> OnSourceWithdrawnAsync(
        Guid sourceRefId, SourceAvailability availability, string actor, CancellationToken ct = default)
    {
        var source = await _db.SourceRefs.FirstOrDefaultAsync(s => s.Id == sourceRefId, ct)
            ?? throw new InvalidOperationException($"Source {sourceRefId} not found.");

        source.Availability = availability;
        source.LastCheckedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var result = new PropagationResult { ClaimId = Guid.Empty };

        var dependents = await _links.DependentsAsync(
            new ObjectRef(ObjectType.SourceRef, sourceRefId), ct: ct);

        var claimRefs = dependents
            .Where(l => l.FromType == ObjectType.Claim)
            .Select(l => new ObjectRef(l.FromType, l.FromId));

        await FlagAsync(claimRefs, ReviewReason.SourceWithdrawn, ReviewAction.Review,
            trigger: null,
            detail: $"Source \"{source.Title}\" is now {availability}. "
                  + "Re-check whether this claim's status still holds.",
            result, ct,
            triggerType: ObjectType.SourceRef, triggerId: sourceRefId);

        await _db.SaveChangesAsync(ct);

        _log.LogWarning(
            "Source {SourceId} marked {Availability} by {Actor}; {Count} claims flagged.",
            sourceRefId, availability, actor, result.Flagged.Count);

        return result;
    }

    // ------------------------------------------------------------------ per-type handling

    private async Task PropagateToRoomsAsync(
        List<ObjectLink> links,
        Claim claim,
        ClaimStatus from,
        ClaimStatus to,
        bool isCorrection,
        string actor,
        PropagationResult result,
        CancellationToken ct)
    {
        foreach (var roomId in links.Select(l => l.FromId).Distinct())
        {
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, ct);
            if (room is null) continue;

            // Automatic: the changelog entry, on every room that cites the claim.
            await _revisions.CommitAsync(roomId, actor, new[]
            {
                new PendingChange(
                    isCorrection ? ChangeType.CorrectionIssued : ChangeType.ClaimStatusMoved,
                    Headline: isCorrection
                        ? $"Corrected: {Truncate(claim.Text, 200)}"
                        : $"Evidence status changed: {Truncate(claim.Text, 200)}",
                    WhyItMatters: $"This claim is now {to}. It was {from}.",
                    ObjectType: ObjectType.Claim,
                    ObjectId: claim.Id,
                    FromValue: from.ToString(),
                    ToValue: to.ToString(),
                    CorrectionKind: isCorrection ? Models.Rooms.CorrectionKind.Factual : null),
            }, ct: ct);

            result.RoomsUpdated.Add(roomId);

            // Human review: prose that asserts something the claim no longer supports.
            // A room's status sentence and essential facts are written around their claims,
            // so a status move can make the sentence wrong even though the mark beside it
            // has already updated itself.
            var isEssentialFact = links.Any(l =>
                l.FromId == roomId && l.Relation == LinkRelation.EssentialFact);

            var mentionsClaim = room is ThemeRoom theme &&
                theme.EssentialFacts.Any(f => f.ClaimId == claim.Id);

            if (isEssentialFact || mentionsClaim)
            {
                await FlagOneAsync(
                    new ObjectRef(ObjectType.Room, roomId),
                    ReviewReason.DependsOnChangedClaim,
                    ReviewAction.Rewrite,
                    claim,
                    $"This room presents the claim as an essential fact, and it is now {to}. "
                  + "The surrounding wording may no longer be accurate.",
                    result, ct);
            }
        }
    }

    private async Task FlagInteractionsAsync(
        List<ObjectLink> links,
        Claim claim,
        ClaimStatus from,
        ClaimStatus to,
        PropagationResult result,
        CancellationToken ct)
    {
        // An interaction whose correct answer depends on a claim's status is now serving a
        // possibly-wrong answer key. PRD 06 is explicit that a stale interaction must not
        // serve, so this is a Revalidate, not a Review.
        foreach (var interactionId in links.Select(l => l.FromId).Distinct())
        {
            await FlagOneAsync(
                new ObjectRef(ObjectType.Interaction, interactionId),
                ReviewReason.DependsOnChangedClaim,
                ReviewAction.Revalidate,
                claim,
                $"This interaction uses a claim that moved from {from} to {to}. "
              + "Its answer key and explanations may no longer be correct.",
                result, ct);
        }
    }

    private async Task SyncDevelopmentsAsync(
        List<ObjectLink> links,
        Claim claim,
        ClaimStatus to,
        PropagationResult result,
        CancellationToken ct)
    {
        // A development's row carries an evidence status. When it is backed by a claim, the
        // claim is the authority, so this is kept in step automatically rather than flagged.
        foreach (var devId in links.Select(l => l.FromId).Distinct())
        {
            var dev = await _db.Developments.FirstOrDefaultAsync(d => d.Id == devId, ct);
            if (dev is null || dev.EvidenceStatus == to) continue;

            dev.EvidenceStatus = to;

            // The prose still needs a look — "why it matters" was written when the claim
            // held a different status.
            await FlagOneAsync(
                new ObjectRef(ObjectType.Development, devId),
                ReviewReason.DependsOnChangedClaim,
                ReviewAction.Rewrite,
                claim,
                $"Development status synced to {to}. Its 'why it matters' was written when "
              + "the underlying claim stood differently.",
                result, ct);
        }
    }

    // ------------------------------------------------------------------ flag plumbing

    /// <summary>
    /// Sequential, not Task.WhenAll — DbContext is not thread-safe, and every one of these
    /// reads the flag table before writing.
    /// </summary>
    private async Task FlagAsync(
        IEnumerable<ObjectRef> targets,
        ReviewReason reason,
        ReviewAction action,
        Claim? trigger,
        string detail,
        PropagationResult result,
        CancellationToken ct,
        ObjectType? triggerType = null,
        Guid? triggerId = null)
    {
        foreach (var target in targets.Distinct())
        {
            await FlagOneAsync(
                target, reason, action, trigger, detail, result, ct, triggerType, triggerId);
        }
    }

    private async Task FlagOneAsync(
        ObjectRef target,
        ReviewReason reason,
        ReviewAction action,
        Claim? trigger,
        string detail,
        PropagationResult result,
        CancellationToken ct,
        ObjectType? triggerType = null,
        Guid? triggerId = null)
    {
        var tType = triggerType ?? (trigger is null ? null : ObjectType.Claim);
        var tId = triggerId ?? trigger?.Id;

        // Re-running propagation must not spam the queue. The filtered unique index on
        // unresolved flags is the real guard; this check keeps the common path quiet.
        var existing = await _db.ReviewFlags.FirstOrDefaultAsync(f =>
            f.ObjectType == target.Type
            && f.ObjectId == target.Id
            && f.Reason == reason
            && f.TriggerObjectId == tId
            && f.ResolvedAt == null, ct);

        if (existing is not null)
        {
            existing.Detail = detail;
            result.Flagged.Add(existing);
            return;
        }

        var flag = new ReviewFlag
        {
            Id = Guid.NewGuid(),
            ObjectType = target.Type,
            ObjectId = target.Id,
            Reason = reason,
            Action = action,
            TriggerObjectType = tType,
            TriggerObjectId = tId,
            Detail = detail,
        };

        _db.ReviewFlags.Add(flag);
        result.Flagged.Add(flag);
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
