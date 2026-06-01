namespace Civic.API.Services.Generation;

/// <summary>
/// JSON-mode response shapes the Claude calls deserialize into. These are
/// intentionally separate from the EF entities so the model doesn't have to
/// guess at DB-only fields (Id, timestamps, IssueOrder, provenance).
/// </summary>
public class GeneratedBriefingDto
{
    public string Headline { get; set; } = "";
    public string Institution { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Status { get; set; } = "";
    public string AudienceLevel { get; set; } = "";
    public string KeyConcept { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string Summary30 { get; set; } = "";
    public string Summary3Min { get; set; } = "";
    public string Summary10Min { get; set; } = "";
    public string WhoActed { get; set; } = "";
    public string WhatChanged { get; set; } = "";
    public string WhyItMatters { get; set; } = "";
    public List<GeneratedWord> WordsToKnow { get; set; } = new();
    public string Disagreement { get; set; } = "";
    public string StrongestArgumentFor { get; set; } = "";
    public string StrongestArgumentAgainst { get; set; } = "";
    public string[] ValuesInConflict { get; set; } = Array.Empty<string>();
    public string ThinkDeeperQuestion { get; set; } = "";
    public string[] RelatedConcepts { get; set; } = Array.Empty<string>();
    public string[] WhereToGoNext { get; set; } = Array.Empty<string>();
}

public class GeneratedWord
{
    public string Term { get; set; } = "";
    public string Definition { get; set; } = "";
}

public class GeneratedThinkDeeperDto
{
    public string Issue { get; set; } = "";
    public string FirstReactionPrompt { get; set; } = "";
    public string[] Values { get; set; } = Array.Empty<string>();
    public string StrongestArgumentA { get; set; } = "";
    public string StrongestArgumentB { get; set; } = "";
    public string WhatSideAMayMiss { get; set; } = "";
    public string WhatSideBMayMiss { get; set; } = "";
    public string[] WhatWouldChangeYourMind { get; set; } = Array.Empty<string>();
    public string CanBothBeTrue { get; set; } = "";
    public string BuildYourViewPrompt { get; set; } = "";
}

public class GeneratedConceptDto
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string PlainDefinition { get; set; } = "";
    public string WhyItMatters { get; set; } = "";
    public string[] WhereYouSeeIt { get; set; } = Array.Empty<string>();
    public string CurrentExample { get; set; } = "";
    public string CommonMisunderstanding { get; set; } = "";
    public string[] RelatedConcepts { get; set; } = Array.Empty<string>();
    public string TryItQuestion { get; set; } = "";
}

public class GeneratedQuizQuestionDto
{
    public string Topic { get; set; } = "";
    public string Question { get; set; } = "";
    public string[] Options { get; set; } = Array.Empty<string>();
    public int CorrectAnswerIndex { get; set; }
    public string Explanation { get; set; } = "";
    public string? RelatedConceptSlug { get; set; }
}

public class ContentJudgeDto
{
    public bool ShouldGenerateConcept { get; set; }
    public string? ConceptHint { get; set; }
    public bool ShouldGenerateQuiz { get; set; }
    public string? QuizHint { get; set; }
}

/// <summary>Cheap relevance gate: is this story civic/government-related enough to brief?</summary>
public class RelevanceJudgeDto
{
    public bool IsCivic { get; set; }
    public string? Reason { get; set; }
}
