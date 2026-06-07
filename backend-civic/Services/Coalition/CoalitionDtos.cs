namespace Civic.API.Services.Coalition;

/// <summary>
/// LLM response shape for provision birth (Phase 0.2): a neutral-surface,
/// real-tradeoff proposition plus the initial sub-question set and the relevant
/// Values axes. Kept separate from the EF entities so the model never has to
/// reason about DB-only fields.
/// </summary>
public class GeneratedProvisionDto
{
    /// <summary>Short neutral title for the proposition.</summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// The provision text: a single concrete proposition, neutral on its
    /// surface, with a real tradeoff underneath, specific enough to position on.
    /// </summary>
    public string NeutralText { get; set; } = "";

    /// <summary>Relevant Values axes (slug-style), tagged at birth.</summary>
    public string[] RelevantAxes { get; set; } = Array.Empty<string>();

    /// <summary>The initial latent dimensions of disagreement.</summary>
    public List<GeneratedSubQuestionDto> SubQuestions { get; set; } = new();
}

public class GeneratedSubQuestionDto
{
    /// <summary>Stable slug identifying the sub-question within its provision.</summary>
    public string Key { get; set; } = "";

    /// <summary>The crux, phrased as a question.</summary>
    public string Prompt { get; set; } = "";

    /// <summary>One line naming the real tradeoff this crux turns on.</summary>
    public string? Tradeoff { get; set; }

    /// <summary>The discrete positions a version could take on this crux.</summary>
    public string[] PositionOptions { get; set; } = Array.Empty<string>();
}
