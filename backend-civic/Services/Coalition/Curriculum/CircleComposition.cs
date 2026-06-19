namespace Civic.API.Services.Coalition.Curriculum;

/// <summary>Age band for the age-banding safety layer (A8: no adult↔minor exposure where required).</summary>
public enum AgeBand { Minor, Adult }

/// <summary>A candidate circle member: their spectrum bucket (Values placement) and age band.</summary>
public sealed record CircleMemberSpec(string UserId, string SpectrumBucket, AgeBand Age);

/// <summary>A composed circle.</summary>
public sealed record ComposedCircle(IReadOnlyList<CircleMemberSpec> Members)
{
    public IReadOnlyCollection<string> Buckets =>
        Members.Select(m => m.SpectrumBucket).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public bool MixesAgeBands => Members.Select(m => m.Age).Distinct().Count() > 1;
}

/// <summary>
/// Phase 3.3 — structured-diverse circle composition (balanced spectrum, invisible
/// to users). Members are drafted so each circle spans as much of the intended
/// spectrum as possible, and age bands are NEVER mixed (an adult and a minor never
/// share a circle — the age-banding safety layer, A8). Pure.
/// </summary>
public static class CircleComposer
{
    public static IReadOnlyList<ComposedCircle> Compose(
        IReadOnlyList<CircleMemberSpec> pool,
        IReadOnlyList<string> targetSpectrum,
        int circleSize)
    {
        if (circleSize < 1) throw new ArgumentOutOfRangeException(nameof(circleSize));

        var circles = new List<ComposedCircle>();
        // Partition by age band first so circles are never mixed.
        foreach (var band in pool.GroupBy(m => m.Age))
            circles.AddRange(ComposeWithinBand(band.ToList(), targetSpectrum, circleSize));
        return circles;
    }

    private static IEnumerable<ComposedCircle> ComposeWithinBand(
        List<CircleMemberSpec> members, IReadOnlyList<string> targetSpectrum, int circleSize)
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

        var circleCount = (int)Math.Ceiling(total / (double)circleSize);
        var circles = Enumerable.Range(0, circleCount).Select(_ => new List<CircleMemberSpec>()).ToList();

        // Deal the bucket-grouped members round-robin across circles. Because consecutive
        // members share a bucket, each circle receives at most one per bucket before any circle
        // gets a second — maximizing the distinct buckets (spectrum span) each circle covers,
        // while keeping circle sizes balanced (<= circleSize).
        for (var k = 0; k < bucketGrouped.Count; k++)
            circles[k % circleCount].Add(bucketGrouped[k]);

        foreach (var c in circles.Where(c => c.Count > 0))
            yield return new ComposedCircle(c);
    }
}
