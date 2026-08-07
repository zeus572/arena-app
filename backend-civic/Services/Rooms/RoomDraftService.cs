using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Rooms;

/// <summary>
/// Drafts candidate Story Rooms with the LLM (PRD 08 phase R7).
///
/// Status machine: Candidate → Drafting → Draft, with a reset-stuck guard on startup. It
/// copies <c>BillSynthesisService</c>'s failure triage verbatim, because that triage was
/// written after an incident and each arm is load-bearing:
///
///   CallFailed   → requeue, un-count the attempt, HALT the batch. The API is down; there
///                  is nothing to be gained by working through the queue against it.
///   Unavailable  → same, quietly. The kill-switch is off or there is no key.
///   BadResponse  → fail THIS item, keep the attempt, continue. The API is healthy and this
///                  one document is the problem. Requeueing it would pin a poison item at
///                  the head of the batch and stall everything behind it — which is exactly
///                  how a retry loop turned into a month of wasted spend last time.
///
/// <b>Nothing here ever publishes.</b> The pipeline's terminal state is Draft. Moving a room
/// to Published is a separate, human action through AdminRoomsController, and it runs the
/// publish gates. That is not a review queue this pipeline waits on — drafting neither
/// blocks on a reviewer nor produces anything a reader can see.
/// </summary>
public class RoomDraftService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILlmClient _llm;
    private readonly IOptionsMonitor<RoomDraftOptions> _opts;
    private readonly ILogger<RoomDraftService> _log;
    private readonly StartupReadiness _readiness;

    public RoomDraftService(
        IServiceScopeFactory scopes,
        ILlmClient llm,
        IOptionsMonitor<RoomDraftOptions> opts,
        ILogger<RoomDraftService> log,
        StartupReadiness readiness)
    {
        _scopes = scopes;
        _llm = llm;
        _opts = opts;
        _log = log;
        _readiness = readiness;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await _readiness.WaitUntilReadyAsync(stoppingToken); }
        catch (OperationCanceledException) { return; }

        try { await ResetStuckAsync(stoppingToken); }
        catch (Exception ex) { _log.LogWarning(ex, "RoomDraftService: ResetStuck failed, will retry naturally"); }

        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _opts.CurrentValue;

            try
            {
                if (opts.CandidatesEnabled) await RunCandidatePassAsync(stoppingToken);

                // Checked every tick, not captured at startup, so the switch can be thrown
                // without a restart when something is going wrong.
                if (opts.Enabled) await DraftBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "RoomDraftService: tick failed");
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, opts.DraftIntervalMinutes));
            try { await Task.Delay(interval, stoppingToken); }
            catch (TaskCanceledException) { /* shutdown */ }
        }
    }

    private async Task ResetStuckAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var stuck = await db.Rooms.Where(r => r.Status == RoomStatus.Drafting).ToListAsync(ct);
        foreach (var r in stuck) r.Status = RoomStatus.Candidate;
        if (stuck.Count > 0) await db.SaveChangesAsync(ct);
    }

    /// <summary>Runs the zero-cost candidate pass. Public for deterministic tests.</summary>
    public async Task<int> RunCandidatePassAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var candidates = scope.ServiceProvider.GetRequiredService<RoomCandidateService>();
        return await candidates.RunPassAsync(ct);
    }

    /// <summary>Drives one drafting batch. Public for deterministic tests.</summary>
    public async Task<int> DraftBatchAsync(CancellationToken ct = default)
    {
        var opts = _opts.CurrentValue;

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var extractor = scope.ServiceProvider.GetRequiredService<ClaimExtractionService>();
        var links = scope.ServiceProvider.GetRequiredService<ObjectLinkService>();

        var batch = await db.StoryRooms
            .Where(r => r.Status == RoomStatus.Candidate
                     && r.DraftAttemptCount < opts.MaxDraftAttempts)
            .OrderByDescending(r => r.EventTime)
            .Take(Math.Max(1, opts.DraftBatchSize))
            .ToListAsync(ct);

        if (batch.Count == 0) return 0;

        var done = 0;
        foreach (var queued in batch)
        {
            var room = await db.StoryRooms.FirstOrDefaultAsync(r => r.Id == queued.Id, ct);
            if (room is null) continue;

            try
            {
                room.Status = RoomStatus.Drafting;
                room.DraftAttemptCount++;
                await db.SaveChangesAsync(ct);

                await DraftOneAsync(db, extractor, links, room, ct);

                room.Status = RoomStatus.Draft;
                room.DraftedAt = DateTime.UtcNow;
                room.DraftPromptVersion = RoomPrompts.Version;
                room.LastError = null;
                await db.SaveChangesAsync(ct);
                done++;
            }
            catch (Exception ex)
            {
                db.ChangeTracker.Clear();
                var tracked = await db.StoryRooms.FirstOrDefaultAsync(r => r.Id == queued.Id, ct);
                if (tracked is null) continue;

                if (ex is LlmException { Kind: LlmFailureKind.CallFailed })
                {
                    tracked.Status = RoomStatus.Candidate;
                    if (tracked.DraftAttemptCount > 0) tracked.DraftAttemptCount--;
                    await db.SaveChangesAsync(ct);
                    _log.LogWarning(ex, "RoomDraftService: LLM unavailable; requeued {Slug} and halting batch", queued.Slug);
                    break;
                }

                if (ex is LlmException { Kind: LlmFailureKind.Unavailable })
                {
                    tracked.Status = RoomStatus.Candidate;
                    if (tracked.DraftAttemptCount > 0) tracked.DraftAttemptCount--;
                    await db.SaveChangesAsync(ct);
                    _log.LogInformation("RoomDraftService: LLM disabled/unconfigured; leaving candidates undrafted");
                    break;
                }

                if (ex is LlmException { Kind: LlmFailureKind.BadResponse })
                {
                    // This document, not the API. Keep the attempt so it cannot loop forever.
                    _log.LogWarning(ex, "RoomDraftService: unparseable response for {Slug}; keeping the attempt and continuing", queued.Slug);
                    tracked.Status = RoomStatus.Candidate;
                    tracked.LastError = ex.Message;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                _log.LogWarning(ex, "RoomDraftService: drafting failed for {Slug}", queued.Slug);
                tracked.Status = RoomStatus.Candidate;
                tracked.LastError = ex.Message;
                await db.SaveChangesAsync(ct);
            }
        }

        _log.LogInformation("RoomDraftService: drafted {Done}/{Total} candidates", done, batch.Count);
        return done;
    }

    private async Task DraftOneAsync(
        CivicDbContext db,
        ClaimExtractionService extractor,
        ObjectLinkService links,
        StoryRoom room,
        CancellationToken ct)
    {
        var theme = await ThemeForAsync(db, room, ct);
        var themeTitle = theme?.Title ?? "Untitled";

        ClaimExtractionService.ExtractionSource source;
        (string System, string User) prompt;

        if (room.SourceBriefingId is { } briefingId)
        {
            var briefing = await db.Briefings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == briefingId, ct)
                ?? throw new InvalidOperationException($"Briefing {briefingId} is gone.");
            source = ClaimExtractionService.From(briefing);
            prompt = RoomPrompts.DraftFromBriefing(briefing, themeTitle);
        }
        else if (room.SourceBillId is { } billId)
        {
            var bill = await db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == billId, ct)
                ?? throw new InvalidOperationException($"Bill {billId} is gone.");
            source = ClaimExtractionService.From(bill);
            prompt = RoomPrompts.DraftFromBill(bill, themeTitle);
        }
        else
        {
            // No prose to draft from and none to verify a passage against. This is the case
            // a news item would land in, which is why the candidate pass never creates one.
            throw new InvalidOperationException(
                $"Candidate {room.Slug} has no briefing or bill source; nothing to draft from.");
        }

        var result = await _llm.GenerateStructuredAsync<RoomDraftResult>(
            prompt.System, prompt.User, LlmModelTier.Sonnet, ct: ct);

        // The all-defaults guard. The client salvages JSON out of prose, so a refusal that
        // merely quotes the requested shape can deserialize into an empty object. Persisting
        // that would leave a Draft room with no content that never retries — silent dead
        // data, which is worse than a failure.
        if (string.IsNullOrWhiteSpace(result.Dek) && result.Claims.Count == 0)
        {
            throw new LlmException(
                "Parsed draft had no dek and no claims.", kind: LlmFailureKind.BadResponse);
        }

        room.Title = Truncate(Fallback(result.Title, room.Title), 300);
        room.Dek = Truncate(result.Dek, 1000);
        room.HowItWorksIntro = Truncate(result.HowItWorksIntro, 2000);
        room.StoryType = ParseEnum(result.StoryType, RoomTopicCategory.Legislative);
        room.DraftModelId = LlmModelTier.Sonnet.ToString();
        room.GenerationSource = CivicGenerationSource.Model;

        // Field-level provenance: design 1y renders an accent rule beside anything a model
        // proposed and a person has not yet accepted.
        room.Provenance = new List<FieldProvenance>
        {
            new() { Field = nameof(Room.Title), ProposedBy = ProvenanceOrigin.Model },
            new() { Field = nameof(Room.Dek), ProposedBy = ProvenanceOrigin.Model },
            new() { Field = nameof(StoryRoom.HowItWorksIntro), ProposedBy = ProvenanceOrigin.Model },
        };

        var claimIds = await extractor.CreateClaimsAsync(result.Claims, source, ct);

        room.WhyItMatters = result.WhyItMatters
            .Where(d => !string.IsNullOrWhiteSpace(d.Dimension) && !string.IsNullOrWhiteSpace(d.Text))
            .Select(d => new StoryDimension
            {
                Dimension = Truncate(d.Dimension, 40),
                Text = Truncate(d.Text, 1000),
                ClaimId = d.ClaimIndex is { } i && i >= 0 && i < claimIds.Count ? claimIds[i] : null,
            }).ToList();

        room.Stakeholders = result.Stakeholders
            .Where(s => !string.IsNullOrWhiteSpace(s.Group))
            .Select(s => new StakeholderImpact
            {
                Group = Truncate(s.Group, 120),
                ImpactSummary = Truncate(s.ImpactSummary, 500),
                Confidence = Math.Clamp(s.Confidence, 0.0, 1.0),
            }).ToList();

        room.NextSteps = result.NextSteps
            // A next step without an objective test is a guess wearing a schedule. Design 1o
            // prints "Confirmed if:" beside every one, so a step that cannot fill it is dropped.
            .Where(n => !string.IsNullOrWhiteSpace(n.Description)
                     && !string.IsNullOrWhiteSpace(n.VerificationCondition))
            .Select(n => new NextStep
            {
                Description = Truncate(n.Description, 500),
                VerificationCondition = Truncate(n.VerificationCondition, 500),
                ExpectedTiming = string.IsNullOrWhiteSpace(n.ExpectedTiming)
                    ? null : Truncate(n.ExpectedTiming!, 120),
            }).ToList();

        await db.SaveChangesAsync(ct);

        var roomRef = new ObjectRef(ObjectType.Room, room.Id);
        var ordinal = 0;
        foreach (var id in claimIds.Where(id => id is not null))
        {
            await links.LinkAsync(roomRef, LinkRelation.EssentialFact,
                new ObjectRef(ObjectType.Claim, id!.Value),
                ordinal: ordinal++, proposedBy: ProvenanceOrigin.Model, ct: ct);
            await links.LinkAsync(roomRef, LinkRelation.References,
                new ObjectRef(ObjectType.Claim, id.Value),
                proposedBy: ProvenanceOrigin.Model, ct: ct);
        }

        if (theme is not null)
        {
            await links.LinkAsync(
                new ObjectRef(ObjectType.Room, theme.Id), LinkRelation.Contains, roomRef,
                proposedBy: ProvenanceOrigin.Model, ct: ct);
        }

        var contested = extractor.ContestedTermsIn($"{room.Title} {room.Dek} {room.HowItWorksIntro}");
        if (contested.Count > 0)
        {
            // Recorded, not blocked. The terminology publish gate is what stops this
            // reaching a reader; noting it here just means the reviewer is not hunting.
            room.LastError = $"Contested terms in drafted copy: {string.Join(", ", contested)}.";
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>The theme room whose candidate pass created this, found by slug prefix.</summary>
    private static async Task<ThemeRoom?> ThemeForAsync(
        CivicDbContext db, StoryRoom room, CancellationToken ct)
    {
        var themes = await db.ThemeRooms.AsNoTracking().ToListAsync(ct);
        return themes
            .Where(t => room.Slug.StartsWith(t.Slug + "-", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.Slug.Length)
            .FirstOrDefault();
    }

    private static string Fallback(string value, string existing) =>
        string.IsNullOrWhiteSpace(value) ? existing : value;

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);
}
