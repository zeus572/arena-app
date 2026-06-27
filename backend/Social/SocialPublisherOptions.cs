namespace Arena.API.Social;

/// <summary>
/// All tunable knobs for the SocialPublisher (SocialPublisher_Spec §7).
/// Bound from the "SocialPublisher" config section. No magic numbers live in code —
/// every threshold, weight, cap and timing value is named here.
/// </summary>
public sealed class SocialPublisherOptions
{
    public const string SectionName = "SocialPublisher";

    // ---- Cadence ----
    public int PublishEveryNTicks { get; set; } = 4;     // ride heartbeat, downsampled
    public int LookbackHours { get; set; } = 24;
    public int MaxPostsPerTick { get; set; } = 2;        // per platform

    // ---- Selection thresholds ----
    public double CoalitionBreadthMin { get; set; } = 0.6;   // 0..1 normalized breadth
    public double DebateEngagementMin { get; set; } = 0.5;   // normalized engagement
    public double FeaturePostBaseScore { get; set; } = 0.4;
    public double AutoPublishMin { get; set; } = 0.65;       // below → review queue
    public double ReviewPenaltyThreshold { get; set; } = 0.3;

    // ---- PostScore weights ----
    public double WQuality { get; set; } = 0.4;
    public double WEngagement { get; set; } = 0.3;
    public double WNovelty { get; set; } = 0.2;
    public double WRecency { get; set; } = 0.1;

    /// <summary>
    /// Ranking Engine components are emitted on a 0..10 scale (RankingService). The PostScore
    /// formula and all thresholds above are 0..1 normalized, so components are divided by this.
    /// Kept as a named constant (not a literal) so it tracks the ranking engine's scale.
    /// </summary>
    public double RankingComponentMax { get; set; } = 10.0;

    /// <summary>
    /// Debate formats eligible for DebateHighlight (§2 candidacy rule).
    /// </summary>
    public List<string> DebateHighlightFormats { get; set; } = new()
        { "common_ground", "roast", "tweet", "standard" };

    /// <summary>Debate format treated as a "coalition" result for CoalitionHighlight (§2.1 binding).</summary>
    public string CoalitionFormat { get; set; } = "common_ground";

    // ---- Platform limits ----
    public int BlueskyMaxGraphemes { get; set; } = 300;
    // XMaxChars deferred with XClient (§4.2)

    // ---- Per-platform daily caps (safety rail under platform rate limits) ----
    public int BlueskyDailyCap { get; set; } = 20;
    // XDailyCap deferred with XClient (§4.2)

    // ---- Resilience (see §4.4) ----
    public int PublisherTickBudgetMs { get; set; } = 5000;   // time-box on shared heartbeat thread
    public int CircuitFailureThreshold { get; set; } = 3;    // consecutive fails before Open
    public int CircuitOpenMinutes { get; set; } = 15;        // cooldown before HalfOpen probe
    public int MaxRetries { get; set; } = 4;
    public int MaxBackoffMinutes { get; set; } = 60;         // cap on exponential backoff

    /// <summary>Base unit for exponential backoff: delay = min(MaxBackoff, BackoffBaseSeconds * 2^(retry-1)) + jitter.</summary>
    public int BackoffBaseSeconds { get; set; } = 30;

    /// <summary>Per-platform daily cap lookup. Only Bluesky ships at launch.</summary>
    public int DailyCapFor(string platform) => platform switch
    {
        "bluesky" => BlueskyDailyCap,
        _ => MaxPostsPerTick, // conservative default for any future/synthetic platform
    };
}
