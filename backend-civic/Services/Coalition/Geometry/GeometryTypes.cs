namespace Civic.API.Services.Coalition.Geometry;

// =====================================================================
// LAYER 1 — Geometry. PURE COMPUTATION ONLY. No LLM calls anywhere in this
// namespace (principle A2/A5: continuous geometry runs over already-extracted
// structure and stays cheap). These types are in-memory snapshots; loading them
// from EF is the caller's concern, kept out of here to preserve purity and make
// the geometry trivially testable on synthetic data.
// =====================================================================

/// <summary>
/// A version as a point in sub-question space: a map of SubQuestion.Key ->
/// resolved position label. This is exactly the extracted vector from Phase 0.3
/// (<c>ProvisionVersion.ExtractedPositions</c> / <c>ExtractionResult.Positions</c>).
/// A key absent from the map means the version is silent on that sub-question.
/// </summary>
public sealed class VersionPoint
{
    public string Id { get; }
    public IReadOnlyDictionary<string, string> Positions { get; }

    public VersionPoint(string id, IReadOnlyDictionary<string, string> positions)
    {
        Id = id;
        // Normalize keys/values defensively; comparisons are case-insensitive.
        Positions = positions.ToDictionary(
            kv => kv.Key.Trim(),
            kv => kv.Value.Trim(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Number of sub-questions this version actually resolves (its specificity / teeth).</summary>
    public int Specificity => Positions.Count;

    /// <summary>Canonical string of the point, for equality/grouping by configuration.</summary>
    public string Canonical()
    {
        var parts = Positions
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key.ToLowerInvariant()}={kv.Value.ToLowerInvariant()}");
        return string.Join("|", parts);
    }
}

/// <summary>
/// A player's acceptance set, expressed as their acceptable REGION in
/// sub-question space (doc 06): for each sub-question they care about, the set of
/// position labels they would co-sign. A version is acceptable unless it resolves
/// some sub-question to a label outside the player's acceptable set for that
/// sub-question.
///
/// Convention (recorded assumption): SILENCE IS ACCEPTABLE — a version that does
/// not resolve a sub-question never violates a constraint on it. This keeps the
/// model monotone (more-specific versions can only lose acceptability) and makes
/// "teeth" (specificity) a separate concern handled by later layers, not smuggled
/// into membership. A key the player does not constrain imposes no restriction.
/// </summary>
public sealed class AcceptanceRegion
{
    private readonly Dictionary<string, HashSet<string>> _acceptableByKey;

    public AcceptanceRegion(IReadOnlyDictionary<string, IEnumerable<string>> acceptableByKey)
    {
        _acceptableByKey = acceptableByKey.ToDictionary(
            kv => kv.Key.Trim(),
            kv => new HashSet<string>(kv.Value.Select(v => v.Trim()), StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Convenience builder for tests / hand-construction.</summary>
    public static AcceptanceRegion FromConstraints(params (string key, string[] labels)[] constraints)
        => new(constraints.ToDictionary(c => c.key, c => (IEnumerable<string>)c.labels));

    /// <summary>An unconstrained region accepts every version.</summary>
    public static AcceptanceRegion Unconstrained() =>
        new(new Dictionary<string, IEnumerable<string>>());

    public bool IsUnconstrained => _acceptableByKey.Count == 0;

    public IReadOnlyCollection<string> ConstrainedKeys => _acceptableByKey.Keys;

    public IReadOnlySet<string> AcceptableLabels(string key) =>
        _acceptableByKey.TryGetValue(key, out var set) ? set : new HashSet<string>();

    /// <summary>Does this version sit inside the region (i.e. would the player co-sign it)?</summary>
    public bool Contains(VersionPoint version)
    {
        foreach (var (key, label) in version.Positions)
        {
            if (_acceptableByKey.TryGetValue(key, out var acceptable) && !acceptable.Contains(label))
            {
                return false; // version takes a position the player explicitly will not accept
            }
        }
        return true;
    }
}

/// <summary>
/// A player snapshot for geometry: their acceptance region (sub-question space)
/// and, for breadth, which segment of the league's composed Values spectrum they
/// occupy (<see cref="SpectrumBucket"/>). The bucket is supplied (Values-axis
/// scoring + leagues are other layers).
/// </summary>
public sealed record PlayerGeometry(
    string UserId,
    AcceptanceRegion Region,
    string? SpectrumBucket = null);
