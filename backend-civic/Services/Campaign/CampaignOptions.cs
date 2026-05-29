namespace Civic.API.Services.Campaign;

/// <summary>
/// Cost and cadence controls for candidate post generation. Bound from the
/// "Campaign" configuration section.
/// </summary>
public class CampaignOptions
{
    /// <summary>Master switch for the background generation loop.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Minutes between background generation ticks.</summary>
    public int GenerationIntervalMinutes { get; set; } = 30;

    /// <summary>A candidate may post at most this many times in <see cref="CooldownWindowHours"/>.</summary>
    public int MaxPostsPerWindow { get; set; } = 2;

    public int CooldownWindowHours { get; set; } = 6;

    /// <summary>Hard per-candidate daily post budget (proxy for LLM spend).</summary>
    public int MaxPostsPerDay { get; set; } = 5;

    /// <summary>Cap on intensity-5 posts per candidate per day.</summary>
    public int MaxIntensity5PerDay { get; set; } = 1;

    /// <summary>How many candidates fan out per triggering briefing.</summary>
    public int MaxCandidatesPerBriefing { get; set; } = 4;

    /// <summary>Briefings published within this many hours are eligible triggers.</summary>
    public int BriefingLookbackHours { get; set; } = 48;
}
