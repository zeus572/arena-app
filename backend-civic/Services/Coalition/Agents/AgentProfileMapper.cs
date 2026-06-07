using Arena.Shared.Llm;
using Civic.API.Models;
using Microsoft.Extensions.Logging;

namespace Civic.API.Services.Coalition.Agents;

/// <summary>A Values-axis score for an agent (e.g. celebrity/historical figure). Value in [-1, 1].</summary>
public sealed record AgentAxisScore(string Axis, double Value);

/// <summary>The structured projection of an agent's Values onto a provision.</summary>
public sealed record AgentProfile(Dictionary<string, string[]> Region, Dictionary<string, string> Intensities, bool FromLlm);

/// <summary>
/// Derives an agent's acceptance region + per-sub-question intensities from its
/// Values profile (the once-per-(agent,provision) LLM step). Falls back to a
/// heuristic mapping when no key, so self-play works in dev.
/// </summary>
public interface IAgentProfileMapper
{
    Task<AgentProfile> DeriveAsync(
        IReadOnlyList<AgentAxisScore> values,
        IReadOnlyList<SubQuestion> subQuestions,
        CancellationToken ct = default);
}

public class AgentProfileDto
{
    public Dictionary<string, string[]> Positions { get; set; } = new();
    public Dictionary<string, string> Intensities { get; set; } = new();
}

public class AgentProfileMapper : IAgentProfileMapper
{
    private readonly ILlmClient _llm;
    private readonly ILogger<AgentProfileMapper> _log;
    private readonly Civic.API.Services.Coalition.ILlmAccessPolicy? _policy;

    public AgentProfileMapper(ILlmClient llm, ILogger<AgentProfileMapper> log, Civic.API.Services.Coalition.ILlmAccessPolicy? policy = null)
    {
        _llm = llm;
        _log = log;
        _policy = policy;
    }

    public async Task<AgentProfile> DeriveAsync(
        IReadOnlyList<AgentAxisScore> values, IReadOnlyList<SubQuestion> subQuestions, CancellationToken ct = default)
    {
        try
        {
            _policy?.EnsureAllowed(); // gate: only premium users trigger the live mapper (else heuristic)
            var (sys, user) = BuildPrompt(values, subQuestions);
            var dto = await _llm.GenerateStructuredAsync<AgentProfileDto>(sys, user, LlmModelTier.Haiku, maxTokens: 500, ct: ct);
            // Keep only known sub-question keys / valid options.
            var region = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var intens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sq in subQuestions)
            {
                if (dto.Positions.TryGetValue(sq.Key, out var labels) && labels.Length > 0)
                    region[sq.Key] = labels;
                if (dto.Intensities.TryGetValue(sq.Key, out var it))
                    intens[sq.Key] = it;
            }
            if (region.Count == 0) return Heuristic(values, subQuestions); // empty -> fall back
            return new AgentProfile(region, intens, FromLlm: true);
        }
        catch (LlmException ex)
        {
            _log.LogDebug(ex, "Agent profile mapper falling back to heuristic.");
            return Heuristic(values, subQuestions);
        }
    }

    /// <summary>
    /// Heuristic: the agent's overall lean (mean axis value) decides which end of each
    /// sub-question's option list it accepts; |lean| sets intensity. Moderates accept both ends.
    /// </summary>
    private static AgentProfile Heuristic(IReadOnlyList<AgentAxisScore> values, IReadOnlyList<SubQuestion> subQuestions)
    {
        var lean = values.Count == 0 ? 0.0 : values.Average(v => v.Value);
        var region = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var intens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var intensity = Math.Abs(lean) switch
        {
            >= 0.7 => "NonNegotiable",
            >= 0.4 => "High",
            >= 0.15 => "Medium",
            _ => "Low",
        };

        foreach (var sq in subQuestions)
        {
            var opts = sq.PositionOptions;
            if (opts.Length == 0) continue;
            string[] acceptable;
            if (Math.Abs(lean) < 0.15) acceptable = opts;                  // moderate: open to all
            else if (lean < 0) acceptable = new[] { opts[0] };             // left-lean: first option
            else acceptable = new[] { opts[^1] };                          // right-lean: last option
            region[sq.Key] = acceptable;
            intens[sq.Key] = intensity;
        }
        return new AgentProfile(region, intens, FromLlm: false);
    }

    private static (string System, string User) BuildPrompt(IReadOnlyList<AgentAxisScore> values, IReadOnlyList<SubQuestion> subQuestions)
    {
        var axes = string.Join("\n", values.Select(v => $"- {v.Axis}: {v.Value:0.00} (range -1..1)"));
        var sqs = string.Join("\n", subQuestions.Select(q =>
            $"- key=\"{q.Key}\": {q.Prompt} [options: {string.Join(", ", q.PositionOptions)}]"));
        return
        (
            """
            You project a public figure's Values profile onto a provision: for each sub-question,
            which option label(s) would they accept, and how hard do they hold it (Low/Medium/High/
            NonNegotiable)? Respond with ONLY JSON. No prose.
            Shape: {"positions": {"<key>": ["<acceptable option>", ...]}, "intensities": {"<key>": "Low|Medium|High|NonNegotiable"}}
            """,
            $$"""
            Values axes:
            {{axes}}

            Sub-questions:
            {{sqs}}

            Produce the JSON.
            """
        );
    }
}
