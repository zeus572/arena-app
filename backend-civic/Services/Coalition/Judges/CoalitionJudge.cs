using Arena.Shared.Llm;
using Civic.API.Services.Coalition.Curriculum;
using Microsoft.Extensions.Logging;

namespace Civic.API.Services.Coalition.Judges;

/// <summary>
/// LLM-backed coalition judge with structural/heuristic fallbacks. In dev (no API
/// key) every call throws <see cref="LlmException"/>; we catch it and fall back so
/// the product is fully usable, and a live key in prod simply upgrades the judgment.
/// </summary>
public class CoalitionJudge : ICoalitionJudge
{
    private readonly ILlmClient _llm;
    private readonly ILogger<CoalitionJudge> _log;

    private static readonly char[] WordSep = { ' ', '\n', '\t', '\r', '.', ',', ';', ':', '!', '?' };
    private static readonly string[] ConcessionMarkers =
        { "carve-out", "carve out", "exempt", "threshold", "only if", "in exchange", "compromise", "give up", "concede", "accept that" };

    public CoalitionJudge(ILlmClient llm, ILogger<CoalitionJudge> log)
    {
        _llm = llm;
        _log = log;
    }

    public async Task<GovernanceScore> ScoreContributionAsync(string text, IEnumerable<string> relevantAxes, CancellationToken ct = default)
    {
        var axes = relevantAxes.ToList();
        try
        {
            var (sys, user) = JudgePrompts.Governance(text, axes);
            var dto = await _llm.GenerateStructuredAsync<GovernanceScoreDto>(sys, user, LlmModelTier.Haiku, maxTokens: 200, ct: ct);
            return new GovernanceScore(
                Math.Clamp(dto.Governance, 0, 100),
                Math.Clamp(dto.ReasoningQuality, 0, 100),
                string.Equals(dto.Layer, "governance", StringComparison.OrdinalIgnoreCase),
                FromLlm: true);
        }
        catch (LlmException ex)
        {
            _log.LogDebug(ex, "Governance judge falling back to heuristic.");
            var isGov = GovernanceClassifier.IsGovernance(axes, text);
            var words = WordCount(text);
            var quality = Math.Clamp(25 + words, 0, 90);
            return new GovernanceScore(isGov ? 70 : 30, quality, isGov, FromLlm: false);
        }
    }

    public async Task<CommonGround> JudgeCommonGroundAsync(string statement, CancellationToken ct = default)
    {
        try
        {
            var (sys, user) = JudgePrompts.CommonGround(statement);
            var dto = await _llm.GenerateStructuredAsync<CommonGroundDto>(sys, user, LlmModelTier.Haiku, maxTokens: 220, ct: ct);
            return new CommonGround(dto.IsGenuine, dto.Concrete, dto.Costly, dto.CrossCutting, dto.Reason ?? "", FromLlm: true);
        }
        catch (LlmException ex)
        {
            _log.LogDebug(ex, "Common-ground judge falling back to heuristic.");
            var concrete = HasDigit(statement) || ConcessionMarkers.Any(m => statement.Contains(m, StringComparison.OrdinalIgnoreCase));
            var costly = ConcessionMarkers.Any(m => statement.Contains(m, StringComparison.OrdinalIgnoreCase));
            var genuine = concrete && WordCount(statement) >= 6;
            return new CommonGround(genuine, concrete, costly, CrossCutting: false,
                "heuristic (no LLM): concrete=" + concrete + ", costly=" + costly, FromLlm: false);
        }
    }

    public async Task<bool> IsAmendmentSubstantiveAsync(string priorText, string amendedText, bool vectorChanged, CancellationToken ct = default)
    {
        try
        {
            var (sys, user) = JudgePrompts.AmendmentSubstantive(priorText, amendedText);
            var dto = await _llm.GenerateStructuredAsync<AmendmentSubstantiveDto>(sys, user, LlmModelTier.Haiku, maxTokens: 160, ct: ct);
            return dto.Substantive;
        }
        catch (LlmException ex)
        {
            _log.LogDebug(ex, "Substantive judge falling back to structural vector-change.");
            return vectorChanged; // structural: the extracted vector changed
        }
    }

    public async Task<bool> HasTeethAsync(string plankText, int specificity, CancellationToken ct = default)
    {
        try
        {
            var (sys, user) = JudgePrompts.Teeth(plankText);
            var dto = await _llm.GenerateStructuredAsync<TeethDto>(sys, user, LlmModelTier.Haiku, maxTokens: 160, ct: ct);
            return dto.HasTeeth;
        }
        catch (LlmException ex)
        {
            _log.LogDebug(ex, "Teeth judge falling back to specificity.");
            return specificity >= 1;
        }
    }

    public async Task<SteelmanVerdict> JudgeSteelmanAsync(string provisionText, string steelmanText, CancellationToken ct = default)
    {
        try
        {
            var (sys, user) = JudgePrompts.Steelman(provisionText, steelmanText);
            var dto = await _llm.GenerateStructuredAsync<SteelmanDto>(sys, user, LlmModelTier.Haiku, maxTokens: 200, ct: ct);
            return new SteelmanVerdict(dto.ProponentWouldEndorse, Math.Clamp(dto.Quality, 0, 100), dto.Reason ?? "", FromLlm: true);
        }
        catch (LlmException ex)
        {
            _log.LogDebug(ex, "Steelman judge falling back to overlap heuristic.");
            var overlap = ContentOverlap(provisionText, steelmanText);
            var endorse = overlap >= 2 && WordCount(steelmanText) >= 5;
            var quality = Math.Clamp(30 + overlap * 12, 0, 85);
            return new SteelmanVerdict(endorse, quality, $"heuristic overlap={overlap}", FromLlm: false);
        }
    }

    // ---- heuristics ----
    private static int WordCount(string s) => s.Split(WordSep, StringSplitOptions.RemoveEmptyEntries).Length;
    private static bool HasDigit(string s) => s.Any(char.IsDigit);

    private static int ContentOverlap(string a, string b)
    {
        var sa = Tokens(a);
        var sb = Tokens(b);
        return sa.Count(sb.Contains);
    }

    private static HashSet<string> Tokens(string s) =>
        s.Split(WordSep, StringSplitOptions.RemoveEmptyEntries)
         .Select(w => w.ToLowerInvariant())
         .Where(w => w.Length > 3) // skip stopword-ish short tokens
         .ToHashSet();
}
