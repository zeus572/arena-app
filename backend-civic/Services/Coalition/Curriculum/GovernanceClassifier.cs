namespace Civic.API.Services.Coalition.Curriculum;

/// <summary>
/// Heuristically classifies a provision as GOVERNANCE (institutional / economic /
/// structural policy) vs CULTURE (identity / values / social) from its Values axes
/// and title — used for the governance-vs-culture ratio without a schema change.
/// Approximate by design; a stored tag can replace it later.
/// </summary>
public static class GovernanceClassifier
{
    private static readonly string[] CultureMarkers =
    {
        "culture", "tradition", "identity", "speech", "religion", "religious", "family",
        "moral", "values", "patriot", "flag", "gender", "abortion", "guns", "immigration-culture",
    };

    private static readonly string[] GovernanceMarkers =
    {
        "market", "regulation", "regulate", "tax", "fiscal", "budget", "federal", "local",
        "national", "institution", "economic", "economy", "governance", "innovation",
        "precaution", "authority", "antitrust", "infrastructure", "spending", "trade",
        "privacy", "safety", "transparency", "oversight",
    };

    public static bool IsGovernance(IEnumerable<string> relevantAxes, string title)
    {
        var hay = (string.Join(" ", relevantAxes) + " " + title).ToLowerInvariant();
        var culture = CultureMarkers.Count(m => hay.Contains(m));
        var governance = GovernanceMarkers.Count(m => hay.Contains(m));
        // Default to governance unless culture markers clearly dominate.
        return governance >= culture;
    }
}
