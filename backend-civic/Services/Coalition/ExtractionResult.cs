namespace Civic.API.Services.Coalition;

/// <summary>
/// The output of the extraction function (Phase 0.3): for a free-form version,
/// the resolved positions on the currently-known sub-questions, plus any NEW
/// sub-question the text introduced that wasn't in the known set (A4).
/// </summary>
public class ExtractionResult
{
    /// <summary>Map of known SubQuestion.Key -> resolved position label. Only
    /// includes sub-questions the text actually takes a position on.</summary>
    public Dictionary<string, string> Positions { get; set; } = new();

    /// <summary>Cruxes the text raised that were absent from the known list.</summary>
    public List<ExtractedNewSubQuestion> NewSubQuestions { get; set; } = new();
}

public class ExtractedNewSubQuestion
{
    public string Key { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string? Tradeoff { get; set; }
    public string[] PositionOptions { get; set; } = Array.Empty<string>();
    /// <summary>The position THIS text takes on the newly-surfaced crux.</summary>
    public string? PositionInThisText { get; set; }
}

// ----- LLM wire shapes (deserialized from the Extract call) -----

public class ExtractionResultDto
{
    public Dictionary<string, string> Positions { get; set; } = new();
    public List<ExtractionNewSubQuestionDto> NewSubQuestions { get; set; } = new();
}

public class ExtractionNewSubQuestionDto
{
    public string Key { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string? Tradeoff { get; set; }
    public string[] PositionOptions { get; set; } = Array.Empty<string>();
    public string? PositionInThisText { get; set; }
}
