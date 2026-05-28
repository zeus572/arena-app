using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Models;

namespace Civic.API.Services;

public interface IProfileScoringService
{
    Task<UserProfile> RecomputeAsync(string userId, CancellationToken ct = default);
}

public class ProfileScoringService : IProfileScoringService
{
    private readonly CivicDbContext _db;
    private readonly ICivicCatalog _catalog;

    public ProfileScoringService(CivicDbContext db, ICivicCatalog catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public async Task<UserProfile> RecomputeAsync(string userId, CancellationToken ct = default)
    {
        var answers = await _db.CivicAnswers
            .Where(a => a.UserId == userId)
            .Include(a => a.Question)
            .ToListAsync(ct);

        // Completed budget sessions also feed scoring. Use only the most recent one
        // to avoid stacking old allocations on top of new ones.
        var latestBudget = await _db.BudgetSessions
            .Where(s => s.UserId == userId && s.CompletedAt != null)
            .Include(s => s.Allocations)
            .OrderByDescending(s => s.CompletedAt)
            .FirstOrDefaultAsync(ct);

        var existing = await _db.UserProfiles
            .Include(p => p.AxisScores)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var now = DateTime.UtcNow;
        UserProfile profile;
        if (existing is null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProfileVersion = 1,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.UserProfiles.Add(profile);
        }
        else
        {
            profile = existing;
            profile.UpdatedAt = now;
            profile.ProfileVersion += 1;

            // Delete the old axis scores in their own SaveChanges round-trip so EF can
            // cleanly reinsert new scores with the same (UserProfileId, AxisKey) keys.
            if (profile.AxisScores.Count > 0)
            {
                _db.ProfileAxisScores.RemoveRange(profile.AxisScores);
                profile.AxisScores.Clear();
                await _db.SaveChangesAsync(ct);
            }
        }

        var axisScores = Score(answers, latestBudget);
        foreach (var s in axisScores)
        {
            var row = new ProfileAxisScore
            {
                Id = Guid.NewGuid(),
                UserProfileId = profile.Id,
                AxisKey = s.AxisKey,
                Score = s.Score,
                Confidence = s.Confidence,
                Intensity = s.Intensity,
                SupportingAnswerIds = s.SupportingAnswerIds,
            };
            profile.AxisScores.Add(row);
            _db.ProfileAxisScores.Add(row);
        }

        profile.ArchetypeBlend.Clear();
        foreach (var b in BlendArchetypes(axisScores))
        {
            profile.ArchetypeBlend.Add(new ArchetypePercent
            {
                ArchetypeKey = b.ArchetypeKey,
                Percent = b.Percent,
            });
        }

        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public record AxisScoreResult(
        string AxisKey,
        double Score,
        double Confidence,
        double Intensity,
        Guid[] SupportingAnswerIds);

    public record ArchetypePercentResult(string ArchetypeKey, double Percent);

    public static double ConfidenceWeight(AnswerConfidence c) => c switch
    {
        AnswerConfidence.NotSure => 0.5,
        AnswerConfidence.SomewhatSure => 0.75,
        AnswerConfidence.VerySure => 1.0,
        _ => 0.5,
    };

    public static double IntensityWeight(AnswerIntensity i) => i switch
    {
        AnswerIntensity.Low => 0.25,
        AnswerIntensity.Medium => 0.5,
        AnswerIntensity.High => 0.75,
        AnswerIntensity.NonNegotiable => 1.0,
        _ => 0.5,
    };

    /// <summary>
    /// Walks a user's answers (and optionally their latest completed budget session)
    /// and aggregates a score per axis. Each answer contributes via its question's
    /// chosen-choice deltas; each budget allocation contributes via that category's
    /// axis deltas scaled by emphasis (50 points = neutral, 100 = full positive,
    /// 0 = full negative). Score per axis is a weighted average direction in [-1, +1].
    /// </summary>
    public List<AxisScoreResult> Score(
        IEnumerable<CivicAnswer> answers,
        BudgetSession? latestBudget = null)
    {
        var buckets = new Dictionary<string, AxisBucket>();

        foreach (var answer in answers)
        {
            if (answer.Question is null) continue;

            var choice = answer.Question.Choices.FirstOrDefault(c => c.Key == answer.SelectedChoiceKey);
            if (choice is null) continue;

            var confW = ConfidenceWeight(answer.Confidence);
            var intenW = IntensityWeight(answer.Intensity);
            var combined = confW * intenW;

            foreach (var d in choice.AxisDeltas)
            {
                if (!buckets.TryGetValue(d.AxisKey, out var bucket))
                {
                    bucket = new AxisBucket();
                    buckets[d.AxisKey] = bucket;
                }
                bucket.WeightedSum += d.Delta * combined;
                bucket.AbsWeightSum += Math.Abs(d.Delta) * combined;
                bucket.ConfSum += confW;
                bucket.IntenSum += intenW;
                bucket.AnswerIds.Add(answer.Id);
            }
        }

        if (latestBudget is { Allocations.Count: > 0 })
        {
            // Treat budget allocations as high-signal "answers". Each allocation's
            // weight is 1.0 (fully committed), and its direction depends on emphasis:
            // signal = (points / 50) - 1  → 0 points = -1, 50 points = 0, 100 points = +1.
            // We use full confidence + non-negotiable intensity weights for the conf/
            // intensity aggregates since the user explicitly built a 100-point budget.
            const double budgetWeight = 1.0;
            const double budgetConfW = 1.0;
            const double budgetIntenW = 1.0;

            // Baseline emphasis is 100 points / 10 categories = 10 points each.
            // Anything above that pushes the category's axes in the positive delta
            // direction; anything below pushes negative. Scale so that ~30 points
            // saturates the positive end and 0 points lands around -0.33.
            const double BaselinePoints = 10.0;
            const double SignalScale = 30.0;

            foreach (var alloc in latestBudget.Allocations)
            {
                var category = _catalog.BudgetCategoryFor(alloc.CategoryKey);
                if (category is null) continue;

                var signal = (alloc.Points - BaselinePoints) / SignalScale;
                signal = Math.Clamp(signal, -1.0, 1.0);

                foreach (var d in category.AxisDeltas)
                {
                    if (!buckets.TryGetValue(d.AxisKey, out var bucket))
                    {
                        bucket = new AxisBucket();
                        buckets[d.AxisKey] = bucket;
                    }
                    var contribution = signal * d.Delta;
                    bucket.WeightedSum += contribution * budgetWeight;
                    bucket.AbsWeightSum += Math.Abs(d.Delta) * budgetWeight;
                    bucket.ConfSum += budgetConfW;
                    bucket.IntenSum += budgetIntenW;
                    bucket.AnswerIds.Add(alloc.Id);
                }
            }
        }

        var results = new List<AxisScoreResult>();
        foreach (var axis in _catalog.Axes)
        {
            if (buckets.TryGetValue(axis.Key, out var bucket) && bucket.AbsWeightSum > 0)
            {
                var raw = bucket.WeightedSum / bucket.AbsWeightSum;
                var clamped = Math.Clamp(raw, -1.0, 1.0);
                var avgConf = bucket.ConfSum / bucket.AnswerIds.Count;
                var avgInten = bucket.IntenSum / bucket.AnswerIds.Count;
                results.Add(new AxisScoreResult(
                    axis.Key,
                    clamped,
                    avgConf,
                    avgInten,
                    bucket.AnswerIds.ToArray()));
            }
            else
            {
                results.Add(new AxisScoreResult(axis.Key, 0, 0, 0, Array.Empty<Guid>()));
            }
        }
        return results;
    }

    /// <summary>
    /// Blends archetypes by computing a similarity score between the user's axis vector
    /// and each archetype's expected axis vector, then applies softmax to produce
    /// percentages that sum to ~100.
    /// </summary>
    public List<ArchetypePercentResult> BlendArchetypes(IReadOnlyList<AxisScoreResult> axisScores)
    {
        if (axisScores.All(a => a.Score == 0 && a.SupportingAnswerIds.Length == 0))
        {
            // No signal yet — uniform blend across archetypes
            var n = _catalog.Archetypes.Count;
            if (n == 0) return new();
            var equal = 100.0 / n;
            return _catalog.Archetypes
                .Select(a => new ArchetypePercentResult(a.Key, equal))
                .ToList();
        }

        var userByAxis = axisScores.ToDictionary(s => s.AxisKey, s => s.Score);
        var rawScores = new List<(string Key, double Score)>();

        foreach (var arch in _catalog.Archetypes)
        {
            double dot = 0;
            double normUserSq = 0;
            double normArchSq = 0;

            foreach (var exp in arch.AxisVector)
            {
                var userScore = userByAxis.TryGetValue(exp.AxisKey, out var u) ? u : 0;
                dot += userScore * exp.ExpectedScore;
                normUserSq += userScore * userScore;
                normArchSq += exp.ExpectedScore * exp.ExpectedScore;
            }

            var denom = Math.Sqrt(normUserSq) * Math.Sqrt(normArchSq);
            var sim = denom > 0 ? dot / denom : 0;
            rawScores.Add((arch.Key, sim));
        }

        // Softmax with temperature; T=0.4 sharpens the distribution so a clear leader
        // emerges, while still showing meaningful blend percentages for runners-up.
        const double T = 0.4;
        var max = rawScores.Max(s => s.Score);
        var exps = rawScores
            .Select(s => (s.Key, E: Math.Exp((s.Score - max) / T)))
            .ToList();
        var sum = exps.Sum(e => e.E);
        if (sum <= 0)
        {
            return rawScores.Select(s => new ArchetypePercentResult(s.Key, 0)).ToList();
        }

        return exps
            .Select(e => new ArchetypePercentResult(e.Key, 100.0 * e.E / sum))
            .OrderByDescending(r => r.Percent)
            .ToList();
    }

    private class AxisBucket
    {
        public double WeightedSum;
        public double AbsWeightSum;
        public double ConfSum;
        public double IntenSum;
        public List<Guid> AnswerIds = new();
    }
}
