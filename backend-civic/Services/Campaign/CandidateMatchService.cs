using Civic.API.Data;
using Civic.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Campaign;

public record CandidateMatchItem(VirtualCandidate Candidate, double Score, string Reason);

public record CandidateMatchResult(
    bool HasProfile,
    IReadOnlyList<CandidateMatchItem> TopMatches,
    IReadOnlyList<CandidateMatchItem> ProductiveChallenges,
    IReadOnlyList<CandidateMatchItem> SurprisingAgreements);

public interface ICandidateMatchService
{
    Task<CandidateMatchResult> GetMatchesAsync(string userId, CancellationToken ct = default);
}

public class CandidateMatchService : ICandidateMatchService
{
    private readonly CivicDbContext _db;
    private readonly ICivicCatalog _catalog;

    public CandidateMatchService(CivicDbContext db, ICivicCatalog catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public async Task<CandidateMatchResult> GetMatchesAsync(string userId, CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.AxisScores)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var empty = Array.Empty<CandidateMatchItem>();
        if (profile is null || profile.AxisScores.Count == 0)
        {
            return new CandidateMatchResult(false, empty, empty, empty);
        }

        var user = profile.AxisScores.ToDictionary(s => s.AxisKey, s => s.Score);

        var candidates = await _db.VirtualCandidates
            .Include(c => c.AxisScores)
            .ToListAsync(ct);

        var scored = candidates
            .Select(c =>
            {
                var cand = c.AxisScores.ToDictionary(s => s.AxisKey, s => s.Score);
                return (Candidate: c, Vector: cand, Score: CandidateMatch.Similarity(user, cand));
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored.Count == 0)
        {
            return new CandidateMatchResult(true, empty, empty, empty);
        }

        var topMatches = scored.Take(3)
            .Select(x => new CandidateMatchItem(x.Candidate, x.Score,
                $"Aligns with you across {SharedAxisCount(user, x.Vector)} value axes."))
            .ToList();

        // The user's single strongest-held value axis.
        var topAxis = user.OrderByDescending(kv => Math.Abs(kv.Value)).First();
        var axisName = _catalog.AxisFor(topAxis.Key)?.Name ?? topAxis.Key;

        // Productive challenge: shares your top value's direction, but is in the
        // less-aligned half overall — agrees on what matters, argues elsewhere.
        var medianScore = scored[scored.Count / 2].Score;
        var productive = scored
            .Where(x => x.Vector.TryGetValue(topAxis.Key, out var v)
                        && Math.Sign(v) == Math.Sign(topAxis.Value) && Math.Abs(topAxis.Value) > 0.15)
            .Where(x => x.Score <= medianScore)
            .OrderByDescending(x => x.Vector.TryGetValue(topAxis.Key, out var v) ? Math.Sign(v) == Math.Sign(topAxis.Value) : false)
            .Take(2)
            .Select(x => new CandidateMatchItem(x.Candidate, x.Score,
                $"Shares your lean on {axisName}, but reaches different conclusions overall."))
            .ToList();

        // Surprising agreement: low overall match, yet agrees on >=2 axes you hold.
        var surprising = scored
            .Where(x => x.Score < 0.5)
            .Select(x => (x.Candidate, x.Score, Agree: CountAgreements(user, x.Vector)))
            .Where(x => x.Agree >= 2)
            .OrderByDescending(x => x.Agree)
            .Take(2)
            .Select(x => new CandidateMatchItem(x.Candidate, x.Score,
                $"You'd expect to clash, but you agree on {x.Agree} specific values."))
            .ToList();

        return new CandidateMatchResult(true, topMatches, productive, surprising);
    }

    private static int SharedAxisCount(
        IReadOnlyDictionary<string, double> user, IReadOnlyDictionary<string, double> cand) =>
        user.Keys.Count(cand.ContainsKey);

    private static int CountAgreements(
        IReadOnlyDictionary<string, double> user, IReadOnlyDictionary<string, double> cand) =>
        user.Count(kv => cand.TryGetValue(kv.Key, out var v) && CandidateMatch.AgreesOnAxis(kv.Value, v));
}
