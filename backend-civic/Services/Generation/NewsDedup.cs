using System.Text;

namespace Civic.API.Services.Generation;

/// <summary>
/// Headline-based near-duplicate detection for news ingestion.
///
/// A big breaking story — a senator dies, a bill passes, a court rules — is
/// carried by many outlets within the same window, each with its own wording:
/// "Sen. Lindsey Graham dies at 70", "Lindsey Graham, longtime senator, dead at
/// 70", "Graham, S.C. Republican, has died". Exact-headline dedupe (the prior
/// behavior) misses every one of these because no two are byte-identical, so
/// each outlet's copy became its own <see cref="Civic.API.Models.NewsItem"/> and
/// then its own briefing — the same story surfaced several times in the feed.
///
/// This compares the <em>significant</em> words of two headlines (proper nouns,
/// verbs, numbers — function words and newsroom filler stripped) using the
/// overlap coefficient (shared / fewer-of-the-two). Overlap is used rather than
/// Jaccard because the same story told by two outlets often differs a lot in
/// length (a terse wire byline vs a full editorial headline), which sinks
/// Jaccard but leaves overlap high. It is a cheap, deterministic heuristic — no
/// LLM — keeping ingestion Claude-free by design.
/// </summary>
internal static class NewsDedup
{
    /// <summary>
    /// Default similarity threshold (overlap coefficient) above which two
    /// headlines are treated as the same story. Tunable via
    /// <see cref="NewsOptions.HeadlineSimilarityThreshold"/>.
    /// </summary>
    public const double DefaultThreshold = 0.6;

    /// <summary>
    /// Two headlines must share at least this many significant words to be
    /// considered a duplicate, so a single word in common (e.g. "Trump",
    /// "Senate") can never collapse two otherwise-distinct stories.
    /// </summary>
    public const int MinSharedTokens = 2;

    // Function words + newsroom filler that carry no story identity. Kept small
    // and generic on purpose: over-stripping risks collapsing distinct stories.
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "and", "or", "but", "of", "to", "in", "on", "at", "for",
        "from", "by", "with", "as", "is", "are", "was", "were", "be", "been",
        "being", "it", "its", "this", "that", "these", "those", "he", "she",
        "they", "his", "her", "their", "them", "we", "you", "not", "no", "new",
        "after", "before", "over", "under", "amid", "about", "into", "out", "up",
        "down", "off", "than", "then", "who", "what", "when", "where", "how",
        "why", "will", "would", "can", "could", "may", "might", "says", "say",
        "said", "amp", "has", "have", "had", "do", "does", "did", "get", "gets",
        "got",
    };

    /// <summary>
    /// The significant lowercased word set of a headline (punctuation and
    /// stopwords removed, single characters dropped). Ordinal set — tokens are
    /// already lowercased, so no culture-sensitive comparison is needed.
    /// </summary>
    public static HashSet<string> Tokenize(string? headline)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(headline)) return set;

        var sb = new StringBuilder();
        foreach (var ch in headline)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                AddToken(set, sb);
            }
        }
        AddToken(set, sb);
        return set;
    }

    private static void AddToken(HashSet<string> set, StringBuilder sb)
    {
        if (sb.Length == 0) return;
        var token = sb.ToString();
        sb.Clear();
        if (token.Length < 2) return;          // drop single characters ("a", stray digits)
        if (Stopwords.Contains(token)) return;
        set.Add(token);
    }

    /// <summary>
    /// Overlap coefficient of two token sets: |A ∩ B| / min(|A|, |B|), and the
    /// count of shared tokens. Returns 0 (and 0 shared) when either set is empty.
    /// </summary>
    public static double Similarity(IReadOnlyCollection<string> a, IReadOnlyCollection<string> b, out int shared)
    {
        shared = 0;
        if (a.Count == 0 || b.Count == 0) return 0;

        // Iterate the smaller set, probe the larger — cheapest either way.
        var (small, large) = a.Count <= b.Count ? (a, b) : (b, a);
        var largeSet = large as HashSet<string> ?? new HashSet<string>(large, StringComparer.Ordinal);
        foreach (var token in small)
        {
            if (largeSet.Contains(token)) shared++;
        }
        return (double)shared / small.Count;
    }

    /// <summary>
    /// True when two headlines are near-duplicates: they share at least
    /// <see cref="MinSharedTokens"/> significant words and their overlap
    /// coefficient is at least <paramref name="threshold"/>.
    /// </summary>
    public static bool AreNearDuplicates(
        IReadOnlyCollection<string> a,
        IReadOnlyCollection<string> b,
        double threshold,
        int minShared = MinSharedTokens)
    {
        var similarity = Similarity(a, b, out var shared);
        return shared >= minShared && similarity >= threshold;
    }

    /// <summary>
    /// "Depth of story" ordering key used to pick the survivor among near-
    /// duplicates: the outlet that filed the most substantive copy wins. Summary
    /// body length is the signal available at ingestion (a fuller write-up means
    /// more depth); headline length breaks ties toward the more specific
    /// headline. Larger sorts first.
    /// </summary>
    public static (int SummaryLength, int HeadlineLength) DepthKey(string? summary, string? headline)
        => (summary?.Length ?? 0, headline?.Length ?? 0);
}
