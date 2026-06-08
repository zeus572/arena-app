namespace Civic.API.Services.Campaign;

/// <summary>
/// JSON shape the campaign-post Claude call deserializes into. Only the body
/// is model-generated; tone/intensity/issue tags are decided deterministically
/// before the call.
/// </summary>
public class GeneratedCampaignPostDto
{
    /// <summary>The post body (a few sentences; see CivicCampaignOptions.BotPostMaxChars) citing a plank or source.</summary>
    public string Body { get; set; } = "";

    /// <summary>Title of the platform plank or source library item referenced.</summary>
    public string? CitedReference { get; set; }
}
