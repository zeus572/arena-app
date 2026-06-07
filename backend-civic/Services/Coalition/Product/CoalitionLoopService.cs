using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Civic.API.Data;
using Civic.API.Models;
using Arena.Shared.Llm;
using Civic.API.Services.Coalition;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Curriculum;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;
using Civic.API.Services.Generation;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Coalition.Product;

/// <summary>
/// Product-wiring bridge: rebuilds the pure in-memory <see cref="ProvisionLoopState"/>
/// from EF rows, applies human/agent acts (persisting them), recomputes the state via
/// the (pure) state machine, and exposes read models for the API/UI. The state
/// machine and geometry remain LLM-free; this layer only does persistence + mapping.
/// </summary>
public class CoalitionLoopService
{
    private readonly CivicDbContext _db;
    private readonly IExtractionService _extraction;
    private readonly ProvisionBirthService _birth;
    private readonly Judges.ICoalitionJudge _judge;
    private readonly ProvisionStateMachine _sm = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public CoalitionLoopService(CivicDbContext db, IExtractionService extraction, ProvisionBirthService birth, Judges.ICoalitionJudge judge)
    {
        _db = db;
        _extraction = extraction;
        _birth = birth;
        _judge = judge;
    }

    // ---------------------------------------------------------------- reads

    public async Task<IReadOnlyList<ProvisionSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var provisions = await _db.Provisions.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var result = new List<ProvisionSummaryDto>();
        foreach (var p in provisions)
        {
            var state = await LoadStateAsync(p.Id, ct);
            var bar = state is null ? null : SpectrumBarBuilder.Build(state);
            var (gap, gov) = await ProvisionMetaAsync(p, ct);
            result.Add(new ProvisionSummaryDto(p.Id, p.Slug, p.Title, p.State.ToString(),
                bar?.Distance ?? 1.0, bar?.CoveredBuckets ?? 0, bar?.TotalBuckets ?? 0, p.Deadline,
                gap, DifficultyLabel(gap), gov));
        }
        return result;
    }

    public async Task<ProvisionDetailDto?> GetDetailAsync(Guid provisionId, string? currentUserId, CancellationToken ct = default)
    {
        var p = await LoadProvisionAsync(provisionId, ct);
        if (p is null) return null;
        var state = await LoadStateAsync(provisionId, ct);
        var bar = SpectrumBarBuilder.Build(state!);

        var subQs = await _db.SubQuestions.Where(s => s.ProvisionId == provisionId).OrderBy(s => s.OrderIndex).ToListAsync(ct);
        var participants = await _db.CoalitionParticipants.Where(c => c.ProvisionId == provisionId).ToListAsync(ct);
        var positionedUsers = p.Positions.Select(x => x.UserId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var acceptsByVersion = p.AcceptanceRecords.GroupBy(a => a.VersionId)
            .ToDictionary(g => g.Key, g => (Accepts: g.Count(a => a.Accept), Declines: g.Count(a => !a.Accept)));

        var versions = p.Versions.OrderBy(v => v.CreatedAt).Select(v =>
        {
            acceptsByVersion.TryGetValue(v.Id, out var counts);
            return new VersionDto(v.Id, v.Label, v.Text, new Dictionary<string, string>(v.ExtractedPositions),
                v.ExtractedPositions.Count, v.AuthorUserId, counts.Accepts, counts.Declines);
        }).ToList();

        // Outcome isn't persisted; re-derive it from data for resolved provisions.
        OutcomeDto? outcome = null;
        if (p.State == ProvisionState.Passed && _sm.ResolvePassOutcome(state!) is { } o)
        {
            Guid? plankId = o.Plank is not null && Guid.TryParse(o.Plank.Id, out var g) ? g : null;
            outcome = new OutcomeDto(o.FinalState.ToString(), plankId, o.Signers?.ToArray(),
                o.Breadth?.CoveredBuckets ?? 0, o.Specificity, o.MovedSigners, o.DiedReason);
        }
        else if (p.State == ProvisionState.Forked)
        {
            var fork = _sm.DetectFork(state!);
            outcome = new OutcomeDto("Forked", null, null,
                fork.Basins.Count, 0, 0, $"forked into {fork.Basins.Count} basins");
        }
        else if (p.State == ProvisionState.Died)
        {
            outcome = new OutcomeDto("Died", null, null, 0, 0, 0, "no spanning coalition by the deadline");
        }

        var barDto = ToBarDto(bar);
        var (gap, gov) = await ProvisionMetaAsync(p, ct);

        return new ProvisionDetailDto(
            p.Id, p.Slug, p.Title, p.NeutralText, p.State.ToString(), p.RelevantAxes, p.Deadline,
            subQs.Select(s => new SubQuestionDto(s.Key, s.Prompt, s.TradeoffDescription, s.PositionOptions, s.Origin.ToString())).ToList(),
            versions,
            participants.Select(c => new ParticipantDto(c.UserId, c.SpectrumBucket, c.IsAgent, positionedUsers.Contains(c.UserId))).ToList(),
            barDto, outcome,
            currentUserId,
            currentUserId is not null && participants.Any(c => string.Equals(c.UserId, currentUserId, StringComparison.OrdinalIgnoreCase)),
            gap, DifficultyLabel(gap), gov);
    }

    private async Task<(double gap, bool governance)> ProvisionMetaAsync(Provision p, CancellationToken ct)
    {
        var (agents, baseV) = await BuildAllAgentsAsync(p.Id, ct);
        var gap = (baseV is not null && agents.Count > 0)
            ? GapWidthEstimator.NormalizedGap(agents, baseV)
            : 0.0;
        var governance = GovernanceClassifier.IsGovernance(p.RelevantAxes, p.Title);
        return (gap, governance);
    }

    private static string DifficultyLabel(double gap) =>
        gap < 0.34 ? "Narrow" : gap < 0.67 ? "Moderate" : "Wide";

    private static SpectrumBarDto ToBarDto(SpectrumBarView bar)
    {
        Guid? leading = bar.LeadingVersionId is not null && Guid.TryParse(bar.LeadingVersionId, out var g) ? g : null;
        return new SpectrumBarDto(
            bar.Cells.Select(c => new SpectrumCellDto(c.Bucket, c.Covered)).ToList(),
            bar.CoveredBuckets, bar.TotalBuckets, bar.Distance, bar.Deadline, leading);
    }

    // ---------------------------------------------------------------- writes (human)

    public async Task JoinAsync(Guid provisionId, string userId, string? bucket, CancellationToken ct = default)
    {
        await EnsureParticipantAsync(provisionId, userId, bucket ?? "center", isAgent: false, ct);
        await _db.SaveChangesAsync(ct);
        await LogActivityAsync(userId, ct);
    }

    public async Task<ProvisionDetailDto?> TakePositionAsync(Guid provisionId, string userId, PositionRequest req, CancellationToken ct = default)
    {
        if (await LoadProvisionAsync(provisionId, ct) is null) return null;
        await EnsureParticipantAsync(provisionId, userId, req.Bucket ?? "center", isAgent: false, ct);

        var existing = await _db.ProvisionPositions.FirstOrDefaultAsync(x => x.ProvisionId == provisionId && x.UserId == userId, ct);
        if (existing is null)
            _db.ProvisionPositions.Add(new ProvisionPosition
            {
                Id = Guid.NewGuid(), ProvisionId = provisionId, UserId = userId,
                Stance = req.Stance, Intensity = ParseIntensity(req.Intensity), ReasoningTag = req.ReasoningTag,
            });
        else { existing.Stance = req.Stance; existing.Intensity = ParseIntensity(req.Intensity); existing.ReasoningTag = req.ReasoningTag; }

        await _db.SaveChangesAsync(ct);
        await RecomputeAndSaveAsync(provisionId, ct);
        await RecordActAsync(userId, provisionId, CoalitionActType.Position, req.Stance, ct);
        return await GetDetailAsync(provisionId, userId, ct);
    }

    public async Task<ProvisionDetailDto?> ProposeAmendmentAsync(Guid provisionId, string userId, AmendmentRequest req, CancellationToken ct = default)
    {
        if (await LoadProvisionAsync(provisionId, ct) is null) return null;
        await EnsureParticipantAsync(provisionId, userId, "center", isAgent: false, ct);

        var positions = new Dictionary<string, string>(req.Positions, StringComparer.OrdinalIgnoreCase);
        await FindOrCreateVersionAsync(provisionId, positions, req.Label, userId, ct);
        await _db.SaveChangesAsync(ct);
        await RecomputeAndSaveAsync(provisionId, ct);
        await RecordActAsync(userId, provisionId, CoalitionActType.Amend,
            string.Join("; ", positions.Select(k => $"{k.Key}={k.Value}")), ct);
        return await GetDetailAsync(provisionId, userId, ct);
    }

    /// <summary>
    /// Free-form amendment (spec A2): the player writes natural-language text; the extraction
    /// tier maps it to sub-question positions (LLM in prod; heuristic option-matching in dev),
    /// surfacing any emergent sub-question (A4). The version stores the player's actual prose.
    /// </summary>
    public async Task<ProvisionDetailDto?> ProposeFreeformAmendmentAsync(Guid provisionId, string userId, string text, CancellationToken ct = default)
    {
        var p = await LoadProvisionAsync(provisionId, ct);
        if (p is null) return null;
        await EnsureParticipantAsync(provisionId, userId, "center", isAgent: false, ct);

        var subQs = await _db.SubQuestions.Where(s => s.ProvisionId == provisionId).ToListAsync(ct);
        ExtractionResult extraction;
        try { extraction = await _extraction.ExtractAsync(text, subQs, ct); }
        catch (LlmException) { extraction = HeuristicExtract(text, subQs); }

        var positions = new Dictionary<string, string>(extraction.Positions, StringComparer.OrdinalIgnoreCase);
        // Fold any newly-surfaced sub-question's position into this version's vector.
        foreach (var ns in extraction.NewSubQuestions)
            if (!string.IsNullOrWhiteSpace(ns.Key) && !string.IsNullOrWhiteSpace(ns.PositionInThisText))
                positions[ns.Key] = ns.PositionInThisText!;

        var version = await FindOrCreateVersionAsync(provisionId, positions, label: "amendment", userId, ct, freeformText: text);
        await _db.SaveChangesAsync(ct);

        // Persist emergent sub-questions (A4) introduced by this version.
        var order = subQs.Count;
        foreach (var ns in extraction.NewSubQuestions)
        {
            if (string.IsNullOrWhiteSpace(ns.Key) || subQs.Any(s => Eq(s.Key, ns.Key))) continue;
            _db.SubQuestions.Add(new SubQuestion
            {
                Id = Guid.NewGuid(), ProvisionId = provisionId, Key = ns.Key, Prompt = ns.Prompt,
                TradeoffDescription = ns.Tradeoff, PositionOptions = ns.PositionOptions,
                Origin = SubQuestionOrigin.Emergent, IntroducedByVersionId = version.Id, OrderIndex = order++,
            });
        }
        await _db.SaveChangesAsync(ct);

        await RecomputeAndSaveAsync(provisionId, ct);
        await RecordActAsync(userId, provisionId, CoalitionActType.Amend, text, ct);
        return await GetDetailAsync(provisionId, userId, ct);
    }

    /// <summary>Dev fallback when no LLM: match any sub-question option label appearing in the text.</summary>
    private static ExtractionResult HeuristicExtract(string text, IReadOnlyList<SubQuestion> subQuestions)
    {
        var lower = text.ToLowerInvariant();
        var positions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sq in subQuestions)
            foreach (var opt in sq.PositionOptions)
                if (lower.Contains(opt.ToLowerInvariant())) { positions[sq.Key] = opt; break; }
        return new ExtractionResult { Positions = positions, NewSubQuestions = new() };
    }

    public async Task<ProvisionDetailDto?> CastAcceptanceAsync(Guid provisionId, string userId, AcceptanceRequest req, CancellationToken ct = default)
    {
        if (await LoadProvisionAsync(provisionId, ct) is null) return null;
        await EnsureParticipantAsync(provisionId, userId, "center", isAgent: false, ct);

        var version = await _db.ProvisionVersions.FirstOrDefaultAsync(v => v.Id == req.VersionId && v.ProvisionId == provisionId, ct);
        if (version is null) return null;

        await UpsertAcceptanceAsync(provisionId, userId, version.Id, req.Accept, ParseIntensity(req.Intensity), ct);
        await _db.SaveChangesAsync(ct);
        await RecomputeAndSaveAsync(provisionId, ct);
        if (req.Accept) await RecordActAsync(userId, provisionId, CoalitionActType.CoSign, null, ct);
        return await GetDetailAsync(provisionId, userId, ct);
    }

    // ---------------------------------------------------------------- birth from a briefing

    /// <summary>
    /// Birth a provision from a briefing (system-extracted path). Uses the extraction-tier LLM
    /// (ProvisionBirthService) in prod; on no-key falls back to a heuristic provision built from
    /// the briefing's fields so the catalog still produces playable provisions in dev. Adds a base
    /// version and a single agent counterpart so it's immediately engageable.
    /// </summary>
    public async Task<ProvisionDetailDto?> BirthFromBriefingAsync(Guid briefingId, string? currentUserId, CancellationToken ct = default)
    {
        var briefing = await _db.Briefings.FirstOrDefaultAsync(b => b.Id == briefingId, ct);
        if (briefing is null) return null;

        Provision provision;
        try { provision = await _birth.BirthFromBriefingAsync(briefing, ct); }
        catch (LlmException) { provision = await _birth.MapAndPersistAsync(HeuristicBirthDto(briefing), briefing, ct); }

        var subQs = await _db.SubQuestions.Where(s => s.ProvisionId == provision.Id).OrderBy(s => s.OrderIndex).ToListAsync(ct);
        if (subQs.Count > 0)
        {
            var basePositions = subQs.ToDictionary(s => s.Key, s => s.PositionOptions.FirstOrDefault() ?? "default", StringComparer.OrdinalIgnoreCase);
            await FindOrCreateVersionAsync(provision.Id, basePositions, "base", null, ct);

            // One agent counterpart (opposite end) so a human has someone to bridge with.
            var rightRegion = subQs.Where(s => s.PositionOptions.Length > 0)
                .ToDictionary(s => s.Key, s => new[] { s.PositionOptions[^1] });
            _db.CoalitionParticipants.Add(new CoalitionParticipant
            {
                Id = Guid.NewGuid(), ProvisionId = provision.Id, UserId = "agent:counterpoint",
                SpectrumBucket = "right", IsAgent = true,
                RegionJson = RegionToJson(rightRegion),
                IntensitiesJson = IntensitiesToJson(subQs.ToDictionary(s => s.Key, _ => "Medium")),
            });
            await _db.SaveChangesAsync(ct);
        }

        await RecomputeAndSaveAsync(provision.Id, ct);
        return await GetDetailAsync(provision.Id, currentUserId, ct);
    }

    private static GeneratedProvisionDto HeuristicBirthDto(Briefing b)
    {
        var axes = b.ValuesInConflict.Take(2).Select(v => Slugify.From(v)).Where(s => s.Length > 0).ToArray();
        return new GeneratedProvisionDto
        {
            Title = b.Headline.Length > 90 ? b.Headline[..90] : b.Headline,
            NeutralText = string.IsNullOrWhiteSpace(b.Summary30) ? b.WhatChanged : b.Summary30,
            RelevantAxes = axes.Length > 0 ? axes : new[] { "governance" },
            SubQuestions =
            {
                new GeneratedSubQuestionDto { Key = "scope", Prompt = "Who/what is covered?", PositionOptions = new[] { "narrow", "broad" } },
                new GeneratedSubQuestionDto { Key = "authority", Prompt = "Who should decide?", PositionOptions = new[] { "local", "national" } },
            },
        };
    }

    // ---------------------------------------------------------------- writes (agent ballast)

    /// <summary>Run one round of agent acts (each agent picks one act), persisting them.</summary>
    public async Task<ProvisionDetailDto?> AgentStepAsync(Guid provisionId, string? currentUserId, CancellationToken ct = default)
    {
        var state = await LoadStateAsync(provisionId, ct);
        if (state is null) return null;
        var agents = await LoadAgentsAsync(provisionId, ct);

        var versionCache = await VersionCacheAsync(provisionId, ct);

        foreach (var agent in agents)
        {
            if (state.IsTerminal) break;
            var act = AgentActPolicy.ChooseAct(agent, state);
            if (act is null) continue;
            _sm.Apply(state, act);                       // keep in-memory state current for the next agent
            await PersistActAsync(provisionId, act, versionCache, ct);
        }

        await _db.SaveChangesAsync(ct);
        await RecomputeAndSaveAsync(provisionId, ct);
        if (currentUserId is not null) await LogActivityAsync(currentUserId, ct);
        return await GetDetailAsync(provisionId, currentUserId, ct);
    }

    // ---------------------------------------------------------------- state (re)build

    private async Task<Provision?> LoadProvisionAsync(Guid provisionId, CancellationToken ct) =>
        await _db.Provisions
            .Include(p => p.Versions)
            .Include(p => p.Positions)
            .Include(p => p.AcceptanceRecords)
            .FirstOrDefaultAsync(p => p.Id == provisionId, ct);

    private async Task<ProvisionLoopState?> LoadStateAsync(Guid provisionId, CancellationToken ct)
    {
        var p = await LoadProvisionAsync(provisionId, ct);
        if (p is null) return null;
        var participants = await _db.CoalitionParticipants.Where(c => c.ProvisionId == provisionId)
            .OrderBy(c => c.CreatedAt).ToListAsync(ct);

        // version points keyed by DB id string
        var versions = p.Versions.OrderBy(v => v.CreatedAt)
            .Select(v => new VersionPoint(v.Id.ToString(), v.ExtractedPositions)).ToList();
        var versionById = versions.ToDictionary(v => v.Id, StringComparer.OrdinalIgnoreCase);

        // per-user acceptance signals (for human region derivation + movement)
        var signalsByUser = p.AcceptanceRecords
            .Where(a => versionById.ContainsKey(a.VersionId.ToString()))
            .GroupBy(a => a.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                g => g.OrderBy(a => a.CreatedAt)
                      .Select(a => new AcceptanceSignal(versionById[a.VersionId.ToString()], a.Accept, a.CreatedAt))
                      .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var players = new List<PlayerGeometry>();
        foreach (var c in participants)
        {
            AcceptanceRegion region;
            if (c.IsAgent && !string.IsNullOrWhiteSpace(c.RegionJson))
                region = RegionFromJson(c.RegionJson);
            else
                region = AcceptanceSetDeriver.Derive(signalsByUser.TryGetValue(c.UserId, out var sigs) ? sigs : new());
            players.Add(new PlayerGeometry(c.UserId, region, c.SpectrumBucket));
        }

        var spectrum = new ComposedSpectrum(participants.Select(c => c.SpectrumBucket));

        var state = new ProvisionLoopState(provisionId.ToString(), players, spectrum,
            start: DateTime.UtcNow, lifetime: null)
        {
            State = p.State,
            Deadline = p.Deadline,
        };
        state.Versions.AddRange(versions);
        foreach (var u in p.Positions.Select(x => x.UserId)) state.Positioned.Add(u);
        foreach (var (user, sigs) in signalsByUser)
            foreach (var s in sigs)
                state.Acceptances.Add(new LoopAcceptance(user, s.Version, s.Accept, AnswerIntensity.Medium, s.At ?? DateTime.UtcNow));

        return state;
    }

    private async Task<List<CoalitionAgent>> LoadAgentsAsync(Guid provisionId, CancellationToken ct)
    {
        var agents = await _db.CoalitionParticipants.Where(c => c.ProvisionId == provisionId && c.IsAgent)
            .OrderBy(c => c.CreatedAt).ToListAsync(ct);
        return agents.Select(c => new CoalitionAgent(
            c.UserId, c.SpectrumBucket,
            string.IsNullOrWhiteSpace(c.RegionJson) ? AcceptanceRegion.Unconstrained() : RegionFromJson(c.RegionJson),
            IntensitiesFromJson(c.IntensitiesJson))).ToList();
    }

    private async Task RecomputeAndSaveAsync(Guid provisionId, CancellationToken ct)
    {
        var state = await LoadStateAsync(provisionId, ct);
        if (state is null) return;
        var newState = _sm.Evaluate(state);
        var p = await _db.Provisions.FirstAsync(x => x.Id == provisionId, ct);
        var old = p.State;
        if (p.State != newState)
        {
            p.State = newState;
            await _db.SaveChangesAsync(ct);
        }

        // Deposit payouts on resolution (idempotent).
        if (newState == ProvisionState.Passed && old != ProvisionState.Passed)
            await AwardPassRewardsAsync(state, ct);
        else if (newState == ProvisionState.Died && old != ProvisionState.Died)
            await AwardDiedPayoutAsync(provisionId, ct);
    }

    private async Task AwardPassRewardsAsync(ProvisionLoopState state, CancellationToken ct)
    {
        var outcome = _sm.ResolvePassOutcome(state);
        if (outcome?.Plank is null || outcome.Signers is null) return;
        var pid = Guid.Parse(state.ProvisionId);
        var breadth = outcome.Breadth?.CoveredBuckets ?? 0;
        foreach (var signer in outcome.Signers)
        {
            if (await _db.CoalitionActs.AnyAsync(a => a.ProvisionId == pid && a.UserId == signer && a.Type == CoalitionActType.CoalitionPassReward, ct))
                continue;
            _db.CoalitionActs.Add(new CoalitionAct
            {
                Id = Guid.NewGuid(), UserId = signer, ProvisionId = pid, Type = CoalitionActType.CoalitionPassReward,
                Currency = "scarce", Points = CoalitionPoints.BasePoints(CoalitionActType.CoalitionPassReward) + breadth * 5,
                GovernanceScore = 70, QualityScore = 70,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task AwardDiedPayoutAsync(Guid provisionId, CancellationToken ct)
    {
        // Dead provisions still pay reasoning points to the humans who engaged (doc 03).
        var participants = await _db.CoalitionParticipants
            .Where(c => c.ProvisionId == provisionId && !c.IsAgent).Select(c => c.UserId).ToListAsync(ct);
        foreach (var u in participants)
        {
            if (await _db.CoalitionActs.AnyAsync(a => a.ProvisionId == provisionId && a.UserId == u && a.Type == CoalitionActType.DiedReasoningPayout, ct))
                continue;
            _db.CoalitionActs.Add(new CoalitionAct
            {
                Id = Guid.NewGuid(), UserId = u, ProvisionId = provisionId, Type = CoalitionActType.DiedReasoningPayout,
                Currency = "reasoning", Points = CoalitionPoints.BasePoints(CoalitionActType.DiedReasoningPayout),
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Record an act in the ledger and award points: judge governance+quality (LLM in prod,
    /// heuristic in dev), apply the currency rules + diminishing returns. Returns points awarded.
    /// </summary>
    public async Task<(int Points, string Currency)> RecordActAsync(string userId, Guid? provisionId, CoalitionActType type, string? payload, CancellationToken ct = default)
    {
        string[] axes = Array.Empty<string>();
        var provisionText = "";
        if (provisionId is Guid pid)
        {
            var p = await _db.Provisions.FirstOrDefaultAsync(x => x.Id == pid, ct);
            if (p is not null) { axes = p.RelevantAxes; provisionText = p.NeutralText; }
        }

        int governance = 50, quality = 50;
        if (!string.IsNullOrWhiteSpace(payload))
        {
            if (type == CoalitionActType.Steelman)
            {
                var v = await _judge.JudgeSteelmanAsync(provisionText, payload, ct);
                quality = v.Quality; governance = 65;
            }
            else
            {
                var s = await _judge.ScoreContributionAsync(payload, axes, ct);
                governance = s.Governance; quality = s.ReasoningQuality;
            }
        }

        var basePts = CoalitionPoints.BasePoints(type);
        if (CoalitionPoints.QualityGated(type))
            basePts = Math.Max(1, (int)Math.Round(basePts * Math.Max(0.2, quality / 100.0)));

        var currency = CoalitionPoints.Currency(type);
        int points;
        if (currency == "reasoning")
        {
            var today = DateTime.UtcNow.Date;
            var todays = await _db.CoalitionActs
                .Where(a => a.UserId == userId && a.Currency == "reasoning" && a.CreatedAt >= today).ToListAsync(ct);
            points = CoalitionPoints.ApplyDiminishing(basePts, todays.Count, todays.Sum(a => a.Points));
        }
        else points = basePts;

        _db.CoalitionActs.Add(new CoalitionAct
        {
            Id = Guid.NewGuid(), UserId = userId, ProvisionId = provisionId, Type = type,
            Payload = payload is null ? null : (payload.Length > 4000 ? payload[..4000] : payload),
            GovernanceScore = governance, QualityScore = quality, Points = points, Currency = currency,
        });
        await _db.SaveChangesAsync(ct);
        await LogActivityAsync(userId, ct);
        return (points, currency);
    }

    // ---------------------------------------------------------------- Layer 3 gamification

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Build CoalitionAgents for ALL participants (agents from JSON; humans from derived region).</summary>
    private async Task<(List<CoalitionAgent> Agents, VersionPoint? BaseVersion)> BuildAllAgentsAsync(Guid provisionId, CancellationToken ct)
    {
        var p = await LoadProvisionAsync(provisionId, ct);
        if (p is null) return (new(), null);
        var participants = await _db.CoalitionParticipants.Where(c => c.ProvisionId == provisionId).ToListAsync(ct);

        var versions = p.Versions.OrderBy(v => v.CreatedAt)
            .Select(v => new VersionPoint(v.Id.ToString(), v.ExtractedPositions)).ToList();
        var versionById = versions.ToDictionary(v => v.Id, StringComparer.OrdinalIgnoreCase);
        var signalsByUser = p.AcceptanceRecords
            .Where(a => versionById.ContainsKey(a.VersionId.ToString()))
            .GroupBy(a => a.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                g => g.OrderBy(a => a.CreatedAt)
                      .Select(a => new AcceptanceSignal(versionById[a.VersionId.ToString()], a.Accept, a.CreatedAt)).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var agents = participants.Select(c =>
        {
            var region = c.IsAgent && !string.IsNullOrWhiteSpace(c.RegionJson)
                ? RegionFromJson(c.RegionJson)
                : AcceptanceSetDeriver.Derive(signalsByUser.TryGetValue(c.UserId, out var s) ? s : new());
            return new CoalitionAgent(c.UserId, c.SpectrumBucket, region, IntensitiesFromJson(c.IntensitiesJson));
        }).ToList();

        var baseV = versions.FirstOrDefault(v => v.Specificity >= 1);
        return (agents, baseV);
    }

    public async Task LogActivityAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (await _db.CoalitionActivityDays.AnyAsync(a => a.UserId == userId && a.Day == today, ct)) return;
        _db.CoalitionActivityDays.Add(new CoalitionActivityDay { Id = Guid.NewGuid(), UserId = userId, Day = today });
        await _db.SaveChangesAsync(ct);
    }

    private sealed record ProvisionWorld(Provision P, ProvisionLoopState State, double Gap, bool Governance, CoalitionOutcome? Pass);

    private async Task<List<ProvisionWorld>> LoadWorldAsync(CancellationToken ct)
    {
        var provisions = await _db.Provisions.ToListAsync(ct);
        var worlds = new List<ProvisionWorld>();
        foreach (var p in provisions)
        {
            var state = await LoadStateAsync(p.Id, ct);
            if (state is null) continue;
            var (agents, baseV) = await BuildAllAgentsAsync(p.Id, ct);
            var gap = (baseV is not null && agents.Count > 0) ? GapWidthEstimator.NormalizedGap(agents, baseV) : 0.0;
            var gov = GovernanceClassifier.IsGovernance(p.RelevantAxes, p.Title);
            var pass = p.State == ProvisionState.Passed ? _sm.ResolvePassOutcome(state) : null;
            worlds.Add(new ProvisionWorld(p, state, gap, gov, pass));
        }
        return worlds;
    }

    private double UserSkill(string userId, IReadOnlyList<ProvisionWorld> worlds)
    {
        var history = new LeagueHistory(worlds
            .Where(w => w.State.Players.Any(pl => Eq(pl.UserId, userId)))
            .Select(w => new LeagueOutcome(w.Gap, w.Pass?.Signers?.Any(s => Eq(s, userId)) == true))
            .ToList());
        return GroupSkill.Estimate(history);
    }

    public async Task<MeDto> GetMeAsync(string userId, CancellationToken ct = default)
    {
        var worlds = await LoadWorldAsync(ct);

        var myPlanks = worlds
            .Where(w => w.Pass?.Plank is not null && w.Pass.Signers?.Any(s => Eq(s, userId)) == true)
            .Select(w => (w, plank: new PassedPlank(w.Gap, w.Pass!.Breadth?.CoveredBuckets ?? 0, w.Pass.Specificity, w.Pass.MovedSigners, w.Governance)))
            .ToList();

        var rec = CampaignMilestones.Accrue(myPlanks.Select(x => x.plank).ToList());
        var recordDto = new CampaignRecordDto(rec.PlanksPassed, rec.TotalBreadth, rec.AvgBreadth, rec.TotalMovedSigners, rec.GovernanceRatio, rec.WeightedScore);

        var skill = UserSkill(userId, worlds);
        var cadence = await CadenceAsync(userId, ct);
        var member = await EnsureLeagueMembershipAsync(userId, skill, ct);

        string? leagueId = null, leagueName = null; double tier = 0; var movement = "Stay";
        if (member is not null)
        {
            var lg = await _db.CoalitionLeagues.FirstOrDefaultAsync(l => l.Id == member.LeagueId, ct);
            leagueId = member.LeagueId.ToString(); leagueName = lg?.Name; tier = lg?.GapTier ?? 0;
            movement = PromotionService.Decide(skill, tier).ToString();
        }

        var served = DifficultyLadder.TargetGap(skill);
        var recommended = worlds
            .Where(w => w.P.State is ProvisionState.Open or ProvisionState.Contested or ProvisionState.NearCoalition)
            .OrderBy(w => Math.Abs(w.Gap - served))
            .Take(3)
            .Select(w => new RecommendedProvisionDto(w.P.Id, w.P.Title, w.P.State.ToString(), w.Gap, DifficultyLabel(w.Gap)))
            .ToList();

        var recentPlanks = myPlanks
            .Select(x => new PlankDto(x.w.P.Id, x.w.P.Title, x.plank.Breadth, x.plank.GapWidthAtBirth, x.plank.IsGovernance))
            .ToList();

        // Points ledger totals.
        var acts = await _db.CoalitionActs.Where(a => a.UserId == userId).ToListAsync(ct);
        var today = DateTime.UtcNow.Date;
        var reasoningXp = acts.Where(a => a.Currency == "reasoning").Sum(a => a.Points);
        var scarce = acts.Where(a => a.Currency == "scarce").Sum(a => a.Points);
        var todayReasoning = acts.Where(a => a.Currency == "reasoning" && a.CreatedAt >= today).Sum(a => a.Points);

        return new MeDto(userId, skill, SkillLabel(skill), recordDto, cadence, leagueId, leagueName, tier, movement,
            recentPlanks, recommended, reasoningXp, scarce, todayReasoning, CoalitionPoints.DailyReasoningCap);
    }

    private async Task<CadenceDto> CadenceAsync(string userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since = today.AddDays(-6);
        var active = (await _db.CoalitionActivityDays
            .Where(a => a.UserId == userId && a.Day >= since)
            .Select(a => a.Day).ToListAsync(ct)).ToHashSet();
        var days = new bool[7];
        for (var i = 0; i < 7; i++) days[i] = active.Contains(since.AddDays(i));
        return new CadenceDto(CampaignCadence.Score(days), days);
    }

    private async Task<CoalitionLeagueMember?> EnsureLeagueMembershipAsync(string userId, double skill, CancellationToken ct)
    {
        var member = await _db.CoalitionLeagueMembers.FirstOrDefaultAsync(m => m.UserId == userId, ct);
        if (member is not null) return member;

        var leagues = await _db.CoalitionLeagues.ToListAsync(ct);
        if (leagues.Count == 0)
        {
            await ComposeLeaguesAsync(4, ct);
            member = await _db.CoalitionLeagueMembers.FirstOrDefaultAsync(m => m.UserId == userId, ct);
            if (member is not null) return member;
            leagues = await _db.CoalitionLeagues.ToListAsync(ct);
        }
        if (leagues.Count == 0) return null;

        var served = DifficultyLadder.TargetGap(skill);
        var best = leagues.OrderBy(l => Math.Abs(l.GapTier - served)).First();
        var bucket = await _db.CoalitionParticipants.Where(c => c.UserId == userId)
            .Select(c => c.SpectrumBucket).FirstOrDefaultAsync(ct) ?? "center";
        member = new CoalitionLeagueMember { Id = Guid.NewGuid(), LeagueId = best.Id, UserId = userId, SpectrumBucket = bucket, AgeBand = "Adult" };
        _db.CoalitionLeagueMembers.Add(member);
        await _db.SaveChangesAsync(ct);
        return member;
    }

    public async Task ComposeLeaguesAsync(int size = 4, CancellationToken ct = default)
    {
        _db.CoalitionLeagueMembers.RemoveRange(_db.CoalitionLeagueMembers);
        _db.CoalitionLeagues.RemoveRange(_db.CoalitionLeagues);
        await _db.SaveChangesAsync(ct);

        var participants = await _db.CoalitionParticipants.ToListAsync(ct);
        var pool = participants
            .GroupBy(c => c.UserId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(c => new LeagueMemberSpec(c.UserId, c.SpectrumBucket, AgeBand.Adult))
            .ToList();
        if (pool.Count == 0) return;

        var spectrum = pool.Select(m => m.SpectrumBucket).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var composed = LeagueComposer.Compose(pool, spectrum, size);
        var worlds = await LoadWorldAsync(ct);

        var i = 1;
        foreach (var cl in composed)
        {
            var tier = cl.Members.Count == 0 ? 0.5 : Math.Clamp(cl.Members.Average(m => UserSkill(m.UserId, worlds)), 0.15, 0.9);
            var league = new CoalitionLeague { Id = Guid.NewGuid(), Name = $"League {i++}", GapTier = tier };
            foreach (var m in cl.Members)
                league.Members.Add(new CoalitionLeagueMember { Id = Guid.NewGuid(), UserId = m.UserId, SpectrumBucket = m.SpectrumBucket, AgeBand = m.Age.ToString() });
            _db.CoalitionLeagues.Add(league);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeagueDto>> GetLeaguesAsync(CancellationToken ct = default)
    {
        var leagues = await _db.CoalitionLeagues.Include(l => l.Members).ToListAsync(ct);
        if (leagues.Count == 0)
        {
            await ComposeLeaguesAsync(4, ct);
            leagues = await _db.CoalitionLeagues.Include(l => l.Members).ToListAsync(ct);
        }

        var worlds = await LoadWorldAsync(ct);
        var acts = await ActsAsync(ct);
        var contrib = BuildContributions(worlds, acts);
        var agentUsers = (await _db.CoalitionParticipants.Where(c => c.IsAgent).Select(c => c.UserId).Distinct().ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<LeagueDto>();
        foreach (var l in leagues.OrderBy(l => l.GapTier))
        {
            var contribs = l.Members
                .Select(m => contrib.TryGetValue(m.UserId, out var c) ? c : new PlayerContribution(m.UserId, 0, 0, 0, 0))
                .ToList();
            var standings = BreadthFavoringScoring.Standings(contribs);
            var rows = standings.Select((s, idx) =>
            {
                var c = contribs.First(x => Eq(x.UserId, s.UserId));
                var isAgent = agentUsers.Contains(s.UserId);
                return new StandingRowDto(idx + 1, s.UserId, Pretty(s.UserId, isAgent), isAgent, s.Score,
                    c.CoalitionsSigned, c.TotalBreadthOfSignedCoalitions, c.MovedCount);
            }).ToList();
            result.Add(new LeagueDto(l.Id.ToString(), l.Name, l.GapTier, DifficultyLabel(l.GapTier),
                l.Members.Select(m => m.SpectrumBucket).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), rows));
        }
        return result;
    }

    private Dictionary<string, PlayerContribution> BuildContributions(IReadOnlyList<ProvisionWorld> worlds, Dictionary<string, int> acts)
    {
        var signed = new Dictionary<string, (int s, int b, int m)>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in worlds)
        {
            if (w.Pass?.Plank is null || w.Pass.Signers is null) continue;
            foreach (var u in w.Pass.Signers)
            {
                var cur = signed.GetValueOrDefault(u);
                cur.s += 1;
                cur.b += w.Pass.Breadth?.CoveredBuckets ?? 0;
                cur.m += LoopMovement.MovedToward(w.State.SignalsFor(u), w.Pass.Plank) ? 1 : 0;
                signed[u] = cur;
            }
        }
        var users = signed.Keys.Union(acts.Keys, StringComparer.OrdinalIgnoreCase);
        return users.ToDictionary(u => u,
            u => new PlayerContribution(u, signed.GetValueOrDefault(u).s, signed.GetValueOrDefault(u).b, signed.GetValueOrDefault(u).m, acts.GetValueOrDefault(u)),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, int>> ActsAsync(CancellationToken ct)
    {
        var pos = await _db.ProvisionPositions.Select(x => x.UserId).ToListAsync(ct);
        var amd = await _db.ProvisionVersions.Where(v => v.AuthorUserId != null).Select(v => v.AuthorUserId!).ToListAsync(ct);
        var acc = await _db.AcceptanceRecords.Select(a => a.UserId).ToListAsync(ct);
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in pos.Concat(amd).Concat(acc)) d[u] = d.GetValueOrDefault(u) + 1;
        return d;
    }

    private static string Pretty(string userId, bool isAgent)
    {
        if (isAgent)
        {
            var name = userId.Replace("agent:", "", StringComparison.OrdinalIgnoreCase);
            return char.ToUpperInvariant(name[0]) + name[1..] + " (agent)";
        }
        return "Player " + userId[..Math.Min(6, userId.Length)];
    }

    private static string SkillLabel(double skill) =>
        skill < 0.34 ? "Rookie" : skill < 0.67 ? "Bridger" : "Veteran";

    // ---------------------------------------------------------------- persistence helpers

    private async Task<Dictionary<string, ProvisionVersion>> VersionCacheAsync(Guid provisionId, CancellationToken ct)
    {
        var versions = await _db.ProvisionVersions.Where(v => v.ProvisionId == provisionId).ToListAsync(ct);
        return versions.ToDictionary(v => Canonical(v.ExtractedPositions), v => v, StringComparer.Ordinal);
    }

    private async Task PersistActAsync(Guid provisionId, LoopAct act, Dictionary<string, ProvisionVersion> versionCache, CancellationToken ct)
    {
        switch (act)
        {
            case TakePositionAct pos:
                var existing = await _db.ProvisionPositions.FirstOrDefaultAsync(x => x.ProvisionId == provisionId && x.UserId == pos.ActorId, ct);
                if (existing is null)
                    _db.ProvisionPositions.Add(new ProvisionPosition
                    {
                        Id = Guid.NewGuid(), ProvisionId = provisionId, UserId = pos.ActorId,
                        Stance = pos.Stance, Intensity = pos.Intensity, ReasoningTag = pos.ReasoningTag,
                    });
                break;

            case ProposeAmendmentAct amend:
                await FindOrCreateVersionAsync(provisionId, new Dictionary<string, string>(amend.Version.Positions), label: "carve-out", amend.ActorId, ct, versionCache);
                break;

            case CastAcceptanceAct acc:
                var canon = Canonical(acc.Version.Positions);
                if (!versionCache.TryGetValue(canon, out var ver))
                {
                    ver = await FindOrCreateVersionAsync(provisionId, new Dictionary<string, string>(acc.Version.Positions), label: null, acc.ActorId, ct, versionCache);
                }
                await UpsertAcceptanceAsync(provisionId, acc.ActorId, ver.Id, acc.Accept, acc.Intensity, ct);
                break;
        }
    }

    private async Task<ProvisionVersion> FindOrCreateVersionAsync(
        Guid provisionId, Dictionary<string, string> positions, string? label, string? authorUserId,
        CancellationToken ct, Dictionary<string, ProvisionVersion>? cache = null, string? freeformText = null)
    {
        var canon = Canonical(positions);
        if (cache is not null && cache.TryGetValue(canon, out var cached)) return cached;

        var loaded = await _db.ProvisionVersions
            .Where(v => v.ProvisionId == provisionId).ToListAsync(ct);
        var match = loaded.FirstOrDefault(v => Canonical(v.ExtractedPositions) == canon);
        if (match is not null) { cache?.TryAdd(canon, match); return match; }

        var text = !string.IsNullOrWhiteSpace(freeformText)
            ? freeformText.Trim()
            : "Version — " + string.Join("; ", positions.OrderBy(k => k.Key).Select(k => $"{k.Key} = {k.Value}"));
        var version = new ProvisionVersion
        {
            Id = Guid.NewGuid(), ProvisionId = provisionId, AuthorUserId = authorUserId, Label = label,
            Text = text, TextHash = Sha(text), ExtractedPositions = positions,
            IsExtracted = true, ExtractionModel = "manual", ExtractedAt = DateTime.UtcNow,
        };
        _db.ProvisionVersions.Add(version);
        cache?.TryAdd(canon, version);
        return version;
    }

    private async Task UpsertAcceptanceAsync(Guid provisionId, string userId, Guid versionId, bool accept, AnswerIntensity intensity, CancellationToken ct)
    {
        var existing = await _db.AcceptanceRecords.FirstOrDefaultAsync(a => a.UserId == userId && a.VersionId == versionId, ct);
        if (existing is null)
            _db.AcceptanceRecords.Add(new AcceptanceRecord
            {
                Id = Guid.NewGuid(), ProvisionId = provisionId, VersionId = versionId, UserId = userId,
                Accept = accept, Intensity = intensity,
            });
        else { existing.Accept = accept; existing.Intensity = intensity; }
    }

    public async Task EnsureParticipantAsync(Guid provisionId, string userId, string bucket, bool isAgent, CancellationToken ct)
    {
        var exists = await _db.CoalitionParticipants.AnyAsync(c => c.ProvisionId == provisionId && c.UserId == userId, ct);
        if (!exists)
            _db.CoalitionParticipants.Add(new CoalitionParticipant
            {
                Id = Guid.NewGuid(), ProvisionId = provisionId, UserId = userId, SpectrumBucket = bucket, IsAgent = isAgent,
            });
    }

    // ---------------------------------------------------------------- json + hashing

    private static AcceptanceRegion RegionFromJson(string json)
    {
        var map = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json, Json) ?? new();
        return new AcceptanceRegion(map.ToDictionary(kv => kv.Key, kv => (IEnumerable<string>)kv.Value));
    }

    private static IReadOnlyDictionary<string, AnswerIntensity>? IntensitiesFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json, Json) ?? new();
        return map.ToDictionary(kv => kv.Key, kv => ParseIntensity(kv.Value));
    }

    public static string RegionToJson(IReadOnlyDictionary<string, string[]> region) => JsonSerializer.Serialize(region);
    public static string IntensitiesToJson(IReadOnlyDictionary<string, string> intensities) => JsonSerializer.Serialize(intensities);

    private static AnswerIntensity ParseIntensity(string? s) =>
        Enum.TryParse<AnswerIntensity>(s, ignoreCase: true, out var i) ? i : AnswerIntensity.Medium;

    private static string Canonical(IReadOnlyDictionary<string, string> positions) =>
        string.Join("|", positions.OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => $"{k.Key.ToLowerInvariant()}={k.Value.ToLowerInvariant()}"));

    private static string Sha(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
