using Civic.API.Data;
using Civic.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Campaign;

public interface ICandidateSelectionService
{
    /// <summary>
    /// Selects candidates who should respond to a briefing: scored by issue
    /// overlap, filtered by cooldown + daily budget, diversified across parties,
    /// capped at the configured fan-out.
    /// </summary>
    Task<IReadOnlyList<VirtualCandidate>> SelectForBriefingAsync(
        Briefing briefing, CancellationToken ct = default);

    /// <summary>Whether a candidate is currently allowed to post (cooldown + budget).</summary>
    Task<bool> CanPostAsync(Guid candidateId, bool intensity5 = false, CancellationToken ct = default);
}

public class CandidateSelectionService : ICandidateSelectionService
{
    private readonly CivicDbContext _db;
    private readonly IOptionsMonitor<CampaignOptions> _opts;

    public CandidateSelectionService(CivicDbContext db, IOptionsMonitor<CampaignOptions> opts)
    {
        _db = db;
        _opts = opts;
    }

    public async Task<IReadOnlyList<VirtualCandidate>> SelectForBriefingAsync(
        Briefing briefing, CancellationToken ct = default)
    {
        var opts = _opts.CurrentValue;

        var candidates = await _db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.Sources)
            .Include(c => c.IssueTones)
            .ToListAsync(ct);

        var briefingTags = briefing.Tags
            .Concat(briefing.ValuesInConflict)
            .Append(briefing.KeyConcept)
            .ToList();

        var scored = new List<(VirtualCandidate Candidate, double Score)>();
        foreach (var c in candidates)
        {
            if (!await CanPostAsync(c.Id, intensity5: false, ct)) continue;
            var score = CandidateSelection.IssueMatchScore(CandidateSelection.CandidateTags(c), briefingTags);
            if (score <= 0) continue;
            scored.Add((c, score));
        }

        // Highest overlap first; diversify so we don't fan out to one party only.
        var ordered = scored.OrderByDescending(s => s.Score).ToList();
        var picked = new List<VirtualCandidate>();
        var partiesUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (candidate, _) in ordered)
        {
            if (picked.Count >= opts.MaxCandidatesPerBriefing) break;
            if (partiesUsed.Add(candidate.Party)) picked.Add(candidate);
        }

        // Backfill with the next-best regardless of party if we have room.
        foreach (var (candidate, _) in ordered)
        {
            if (picked.Count >= opts.MaxCandidatesPerBriefing) break;
            if (!picked.Contains(candidate)) picked.Add(candidate);
        }

        return picked;
    }

    public async Task<bool> CanPostAsync(Guid candidateId, bool intensity5 = false, CancellationToken ct = default)
    {
        var opts = _opts.CurrentValue;
        var now = DateTime.UtcNow;

        var dayCutoff = now.AddHours(-24);
        var postsToday = await _db.CampaignPosts
            .CountAsync(p => p.CandidateId == candidateId && p.CreatedAt >= dayCutoff, ct);
        if (postsToday >= opts.MaxPostsPerDay) return false;

        var windowCutoff = now.AddHours(-opts.CooldownWindowHours);
        var postsInWindow = await _db.CampaignPosts
            .CountAsync(p => p.CandidateId == candidateId && p.CreatedAt >= windowCutoff, ct);
        if (postsInWindow >= opts.MaxPostsPerWindow) return false;

        if (intensity5)
        {
            var fived = await _db.CampaignPosts
                .CountAsync(p => p.CandidateId == candidateId && p.Intensity == 5 && p.CreatedAt >= dayCutoff, ct);
            if (fived >= opts.MaxIntensity5PerDay) return false;
        }

        return true;
    }
}
