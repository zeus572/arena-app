namespace Civic.API.Services.Coalition.Curriculum;

/// <summary>Age band for the age-banding safety layer (A8: no adult↔minor exposure where required).</summary>
public enum AgeBand { Minor, Adult }

/// <summary>A candidate league member: their spectrum bucket (Values placement) and age band.</summary>
public sealed record LeagueMemberSpec(string UserId, string SpectrumBucket, AgeBand Age);

/// <summary>A composed league.</summary>
public sealed record ComposedLeague(IReadOnlyList<LeagueMemberSpec> Members)
{
    public IReadOnlyCollection<string> Buckets =>
        Members.Select(m => m.SpectrumBucket).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public bool MixesAgeBands => Members.Select(m => m.Age).Distinct().Count() > 1;
}

/// <summary>
/// Phase 3.3 — structured-diverse league composition (balanced spectrum, invisible
/// to users). Members are drafted so each league spans as much of the intended
/// spectrum as possible, and age bands are NEVER mixed (an adult and a minor never
/// share a league — the age-banding safety layer, A8). Pure.
/// </summary>
public static class LeagueComposer
{
    public static IReadOnlyList<ComposedLeague> Compose(
        IReadOnlyList<LeagueMemberSpec> pool,
        IReadOnlyList<string> targetSpectrum,
        int leagueSize)
    {
        if (leagueSize < 1) throw new ArgumentOutOfRangeException(nameof(leagueSize));

        var leagues = new List<ComposedLeague>();
        // Partition by age band first so leagues are never mixed.
        foreach (var band in pool.GroupBy(m => m.Age))
            leagues.AddRange(ComposeWithinBand(band.ToList(), targetSpectrum, leagueSize));
        return leagues;
    }

    private static IEnumerable<ComposedLeague> ComposeWithinBand(
        List<LeagueMemberSpec> members, IReadOnlyList<string> targetSpectrum, int leagueSize)
    {
        var total = members.Count;
        if (total == 0) yield break;

        // Group members by bucket in target-spectrum order (unknown buckets last), preserving
        // input order within a bucket.
        var order = targetSpectrum
            .Concat(members.Select(m => m.SpectrumBucket))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var bucketGrouped = order
            .SelectMany(b => members.Where(m => string.Equals(m.SpectrumBucket, b, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var leagueCount = (int)Math.Ceiling(total / (double)leagueSize);
        var leagues = Enumerable.Range(0, leagueCount).Select(_ => new List<LeagueMemberSpec>()).ToList();

        // Deal the bucket-grouped members round-robin across leagues. Because consecutive
        // members share a bucket, each league receives at most one per bucket before any league
        // gets a second — maximizing the distinct buckets (spectrum span) each league covers,
        // while keeping league sizes balanced (<= leagueSize).
        for (var k = 0; k < bucketGrouped.Count; k++)
            leagues[k % leagueCount].Add(bucketGrouped[k]);

        foreach (var l in leagues.Where(l => l.Count > 0))
            yield return new ComposedLeague(l);
    }
}
