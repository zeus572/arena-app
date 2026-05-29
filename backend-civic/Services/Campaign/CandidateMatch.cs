namespace Civic.API.Services.Campaign;

/// <summary>Pure values-profile matching math. Axis scores are in [-1, 1].</summary>
public static class CandidateMatch
{
    /// <summary>
    /// 0..1 similarity between a user's axis scores and a candidate's, over the
    /// axes they share. 1 = identical, 0 = maximally opposed.
    /// </summary>
    public static double Similarity(
        IReadOnlyDictionary<string, double> user,
        IReadOnlyDictionary<string, double> candidate)
    {
        var shared = user.Keys.Where(candidate.ContainsKey).ToList();
        if (shared.Count == 0) return 0;

        var totalDiff = shared.Sum(k => Math.Abs(user[k] - candidate[k]));
        var avgDiff = totalDiff / shared.Count; // 0..2
        return Math.Clamp(1 - avgDiff / 2.0, 0, 1);
    }

    /// <summary>
    /// True when both agree on an axis (same side, within tolerance). Used to
    /// surface "surprising agreement" on otherwise-divergent candidates.
    /// </summary>
    public static bool AgreesOnAxis(double userScore, double candidateScore, double tolerance = 0.35)
    {
        if (Math.Abs(userScore) < 0.15) return false; // user has no clear lean here
        return Math.Sign(userScore) == Math.Sign(candidateScore)
            && Math.Abs(userScore - candidateScore) <= tolerance;
    }
}
