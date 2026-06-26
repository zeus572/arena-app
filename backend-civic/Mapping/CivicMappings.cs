using Civic.API.Models;
using Civic.API.Models.DTOs;

namespace Civic.API.Mapping;

public static class CivicMappings
{
    public static BriefingSummaryDto ToSummaryDto(this Briefing b) => new()
    {
        Id = b.Id,
        Slug = b.Slug,
        Headline = b.Headline,
        Institution = b.Institution,
        Branch = b.Branch,
        Status = b.Status,
        AudienceLevel = b.AudienceLevel,
        KeyConcept = b.KeyConcept,
        Tags = b.Tags,
        Summary30 = b.Summary30,
        CreatedAt = b.CreatedAt,
        ThinkDeeperQuestion = b.ThinkDeeperQuestion,
        Locality = b.Locality,
    };

    /// <summary>
    /// Maps a briefing to its detail DTO. Pass the originating <paramref name="source"/> NewsItem
    /// (resolved from <see cref="Briefing.SourceNewsItemId"/>) to surface original-article
    /// attribution; omit it for hand-seeded briefings that have no upstream source. Pass the
    /// <paramref name="coalition"/> born from this briefing to surface a participate call-out.
    /// </summary>
    public static BriefingDto ToDto(this Briefing b, NewsItem? source = null, Provision? coalition = null) => new()
    {
        Id = b.Id,
        Slug = b.Slug,
        Headline = b.Headline,
        Institution = b.Institution,
        Branch = b.Branch,
        Status = b.Status,
        AudienceLevel = b.AudienceLevel,
        KeyConcept = b.KeyConcept,
        Tags = b.Tags,
        Summary30 = b.Summary30,
        CreatedAt = b.CreatedAt,
        Summary3Min = b.Summary3Min,
        Summary10Min = b.Summary10Min,
        WhoActed = b.WhoActed,
        WhatChanged = b.WhatChanged,
        WhyItMatters = b.WhyItMatters,
        WordsToKnow = b.WordsToKnow
            .Select(w => new BriefingWordDto { Term = w.Term, Definition = w.Definition })
            .ToList(),
        Disagreement = b.Disagreement,
        StrongestArgumentFor = b.StrongestArgumentFor,
        StrongestArgumentAgainst = b.StrongestArgumentAgainst,
        ValuesInConflict = b.ValuesInConflict,
        ThinkDeeperQuestion = b.ThinkDeeperQuestion,
        RelatedConcepts = b.RelatedConcepts,
        WhereToGoNext = b.WhereToGoNext,
        SourceUrl = source?.Url,
        SourcePublisher = source?.Source,
        SourcePublishedAt = source is null ? null : DateTime.SpecifyKind(source.PublishedAt, DateTimeKind.Utc),
        Locality = b.Locality,
        CoalitionProvisionId = coalition?.Id,
        CoalitionProvisionState = coalition?.State.ToString(),
    };

    public static ConceptDto ToDto(this Concept c) => new()
    {
        Id = c.Id,
        Slug = c.Slug,
        Title = c.Title,
        Category = c.Category,
        PlainDefinition = c.PlainDefinition,
        WhyItMatters = c.WhyItMatters,
        WhereYouSeeIt = c.WhereYouSeeIt,
        CurrentExample = c.CurrentExample,
        CommonMisunderstanding = c.CommonMisunderstanding,
        RelatedConcepts = c.RelatedConcepts,
        TryItQuestion = c.TryItQuestion,
    };

    public static QuestionDto ToDto(this CivicQuestion q) => new()
    {
        Id = q.Id,
        ExternalId = q.ExternalId,
        Type = q.Type.ToString(),
        Prompt = q.Prompt,
        Topic = q.Topic,
        Order = q.Order,
        Choices = q.Choices
            .Select(c => new QuestionChoiceDto { Key = c.Key, Label = c.Label })
            .ToList(),
    };

    public static AnswerDto ToDto(this CivicAnswer a, string questionExternalId) => new()
    {
        Id = a.Id,
        QuestionId = a.QuestionId,
        QuestionExternalId = questionExternalId,
        SelectedChoiceKey = a.SelectedChoiceKey,
        Confidence = a.Confidence.ToString(),
        Intensity = a.Intensity.ToString(),
        ReasoningChoice = a.ReasoningChoice,
        FreeTextReasoning = a.FreeTextReasoning,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
    };

    public static QuizQuestionDto ToDto(this QuizQuestion q) => new()
    {
        Id = q.Id,
        ExternalId = q.ExternalId,
        Topic = q.Topic,
        Question = q.Question,
        Options = q.Options,
        CorrectAnswerIndex = q.CorrectAnswerIndex,
        Explanation = q.Explanation,
        RelatedConceptSlug = q.RelatedConceptSlug,
        Order = q.Order,
    };

    public static BillTimelineStepDto ToDto(this BillTimelineStep s) => new()
    {
        Id = s.Id,
        ExternalId = s.ExternalId,
        Label = s.Label,
        Description = s.Description,
        Branch = s.Branch,
        Status = s.Status.ToString(),
        Order = s.Order,
    };

    public static ElectionDto ToDto(this Election e) => new()
    {
        Id = e.Id,
        Slug = e.Slug,
        Name = e.Name,
        Scope = e.Scope.ToString(),
        ScheduledAt = DateTime.SpecifyKind(e.ScheduledAt, DateTimeKind.Utc),
        Region = e.Region,
        Description = e.Description,
    };

    public static ThinkDeeperDto ToDto(this ThinkDeeper t) => new()
    {
        Id = t.Id,
        Slug = t.Slug,
        Issue = t.Issue,
        FirstReactionPrompt = t.FirstReactionPrompt,
        Values = t.Values,
        StrongestArgumentA = t.StrongestArgumentA,
        StrongestArgumentB = t.StrongestArgumentB,
        WhatSideAMayMiss = t.WhatSideAMayMiss,
        WhatSideBMayMiss = t.WhatSideBMayMiss,
        WhatWouldChangeYourMind = t.WhatWouldChangeYourMind,
        CanBothBeTrue = t.CanBothBeTrue,
        BuildYourViewPrompt = t.BuildYourViewPrompt,
    };
}
