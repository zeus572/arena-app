namespace Civic.API.Services.Coalition.Judges;

// ---- LLM wire DTOs (deserialized from the judge calls) ----

public class GovernanceScoreDto
{
    public int Governance { get; set; }        // 0-100: institutions/mechanisms/tradeoffs vs identity/culture
    public int ReasoningQuality { get; set; }  // 0-100
    public string Layer { get; set; } = "governance"; // "governance" | "culture"
}

public class CommonGroundDto
{
    public bool IsGenuine { get; set; }
    public bool Concrete { get; set; }
    public bool Costly { get; set; }
    public bool CrossCutting { get; set; }
    public string? Reason { get; set; }
}

public class AmendmentSubstantiveDto
{
    public bool Substantive { get; set; }
    public string? Reason { get; set; }
}

public class TeethDto
{
    public bool HasTeeth { get; set; }
    public string? Reason { get; set; }
}

public class SteelmanDto
{
    public bool ProponentWouldEndorse { get; set; }
    public int Quality { get; set; } // 0-100
    public string? Reason { get; set; }
}

// ---- Domain results (what the rest of the app consumes) ----

public sealed record GovernanceScore(int Governance, int ReasoningQuality, bool IsGovernanceLayer, bool FromLlm);
public sealed record CommonGround(bool IsGenuine, bool Concrete, bool Costly, bool CrossCutting, string Reason, bool FromLlm);
public sealed record SteelmanVerdict(bool ProponentWouldEndorse, int Quality, string Reason, bool FromLlm);

/// <summary>
/// The discrete integrity / scoring judges (A5: rare, near-coalition-only LLM).
/// Every method degrades gracefully to a STRUCTURAL/heuristic fallback when the LLM
/// is unavailable (no API key in dev → LlmException), so the product keeps working;
/// the live key simply upgrades the judgment quality in prod.
/// </summary>
public interface ICoalitionJudge
{
    Task<GovernanceScore> ScoreContributionAsync(string text, IEnumerable<string> relevantAxes, CancellationToken ct = default);
    Task<CommonGround> JudgeCommonGroundAsync(string statement, CancellationToken ct = default);
    Task<bool> IsAmendmentSubstantiveAsync(string priorText, string amendedText, bool vectorChanged, CancellationToken ct = default);
    Task<bool> HasTeethAsync(string plankText, int specificity, CancellationToken ct = default);
    Task<SteelmanVerdict> JudgeSteelmanAsync(string provisionText, string steelmanText, CancellationToken ct = default);
}
