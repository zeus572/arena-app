namespace Civic.API.Services.Daily;

/// <summary>
/// Rejects Whose Value candidates whose argument text names its own answer.
///
/// Bill axis rationales are written ABOUT an axis, so many of them say the axis out
/// loud — "this centralizes authority", "a precautionary requirement". Those make the
/// round trivial, so they're filtered at generation rather than rewritten (there are
/// thousands of candidate rows and no reason to spend an LLM call salvaging one).
/// </summary>
public static class AxisLeakFilter
{
    /// <summary>Stems shorter than this aren't distinctive enough to match on.</summary>
    private const int StemLength = 6;

    /// <summary>
    /// Words that appear in an axis name/label but are far too common in civic writing
    /// to be a real tell — nearly every bill rationale contains "government" or "public",
    /// so matching on them would reject almost the entire corpus while giving nothing
    /// away about WHICH axis is the answer. Tune this list if the reject rate looks wrong.
    /// </summary>
    private static readonly HashSet<string> NotATell = new(StringComparer.OrdinalIgnoreCase)
    {
        "government", "governance", "public", "social", "society", "community",
        "national", "nation", "change", "market", "economic", "economy",
        "people", "person", "individual", "future", "present", "world",
        "first", "second", "informed", "guided", "aware", "outcome", "role",
    };

    /// <summary>
    /// True when <paramref name="text"/> gives away the axis. Checks the axis name and
    /// both pole labels, word by word, on a leading-stem match so inflections
    /// ("centralized" / "centralizes" / "centralization") are all caught.
    /// </summary>
    public static bool Leaks(string text, string axisName, string lowLabel, string highLabel)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var haystack = text.ToLowerInvariant();
        foreach (var stem in Stems(axisName, lowLabel, highLabel))
            if (haystack.Contains(stem, StringComparison.Ordinal)) return true;

        return false;
    }

    /// <summary>The distinctive stems for an axis — exposed so tests can assert on them.</summary>
    public static IReadOnlyList<string> Stems(string axisName, string lowLabel, string highLabel)
    {
        var stems = new List<string>();
        foreach (var source in new[] { axisName, lowLabel, highLabel })
        {
            foreach (var word in Words(source))
            {
                if (word.Length < StemLength || NotATell.Contains(word)) continue;
                var stem = word[..StemLength];
                if (!stems.Contains(stem)) stems.Add(stem);
            }
        }
        return stems;
    }

    private static IEnumerable<string> Words(string source) =>
        source.Split(new[] { ' ', '-', '/', '&', ',', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
              .Select(w => w.Trim().ToLowerInvariant())
              .Where(w => w.Length > 0);
}
