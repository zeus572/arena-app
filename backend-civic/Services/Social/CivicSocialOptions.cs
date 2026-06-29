namespace Civic.API.Services.Social;

/// <summary>
/// Civic-specific knobs for <see cref="CivicHighlightSelector"/>, bound from the "CivicSocial"
/// config section. (Engine/platform/resilience knobs live in the shared SocialPublisherOptions.)
/// </summary>
public sealed class CivicSocialOptions
{
    public const string SectionName = "CivicSocial";

    /// <summary>Public site used to build deep links in posts.</summary>
    public string PublicSiteUrl { get; set; } = "https://civersify.com";

    // ---- Bill outcomes ----
    /// <summary>Only post outcomes for bills whose deadline is within this many days (avoids replaying the archive).</summary>
    public int OutcomeLookbackDays { get; set; } = 7;
    public int MaxOutcomesPerTick { get; set; } = 3;

    // ---- Zeitgeist ----
    /// <summary>Minimum co-signs for a convergence to be post-worthy.</summary>
    public int ZeitgeistMinAccepts { get; set; } = 10;
    public int MaxZeitgeistPerTick { get; set; } = 1;

    // ---- Open bills (engagement) ----
    public int MaxOpenBillsPerDay { get; set; } = 1;

    /// <summary>Candidates scoring below this go to the review queue instead of auto-publishing.</summary>
    public double AutoPublishMin { get; set; } = 0.4;
}
