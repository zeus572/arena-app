using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;
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
    private readonly ProvisionStateMachine _sm = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public CoalitionLoopService(CivicDbContext db) => _db = db;

    // ---------------------------------------------------------------- reads

    public async Task<IReadOnlyList<ProvisionSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var provisions = await _db.Provisions.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var result = new List<ProvisionSummaryDto>();
        foreach (var p in provisions)
        {
            var state = await LoadStateAsync(p.Id, ct);
            var bar = state is null ? null : SpectrumBarBuilder.Build(state);
            result.Add(new ProvisionSummaryDto(p.Id, p.Slug, p.Title, p.State.ToString(),
                bar?.Distance ?? 1.0, bar?.CoveredBuckets ?? 0, bar?.TotalBuckets ?? 0, p.Deadline));
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

        return new ProvisionDetailDto(
            p.Id, p.Slug, p.Title, p.NeutralText, p.State.ToString(), p.RelevantAxes, p.Deadline,
            subQs.Select(s => new SubQuestionDto(s.Key, s.Prompt, s.TradeoffDescription, s.PositionOptions, s.Origin.ToString())).ToList(),
            versions,
            participants.Select(c => new ParticipantDto(c.UserId, c.SpectrumBucket, c.IsAgent, positionedUsers.Contains(c.UserId))).ToList(),
            barDto, outcome,
            currentUserId,
            currentUserId is not null && participants.Any(c => string.Equals(c.UserId, currentUserId, StringComparison.OrdinalIgnoreCase)));
    }

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
        return await GetDetailAsync(provisionId, userId, ct);
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
        return await GetDetailAsync(provisionId, userId, ct);
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
        if (p.State != newState)
        {
            p.State = newState;
            await _db.SaveChangesAsync(ct);
        }
    }

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
        CancellationToken ct, Dictionary<string, ProvisionVersion>? cache = null)
    {
        var canon = Canonical(positions);
        if (cache is not null && cache.TryGetValue(canon, out var cached)) return cached;

        var loaded = await _db.ProvisionVersions
            .Where(v => v.ProvisionId == provisionId).ToListAsync(ct);
        var match = loaded.FirstOrDefault(v => Canonical(v.ExtractedPositions) == canon);
        if (match is not null) { cache?.TryAdd(canon, match); return match; }

        var text = "Version — " + string.Join("; ", positions.OrderBy(k => k.Key).Select(k => $"{k.Key} = {k.Value}"));
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
