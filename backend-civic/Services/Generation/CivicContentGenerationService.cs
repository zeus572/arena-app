using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WireNewsItem = Arena.Shared.News.NewsItem;

namespace Civic.API.Services.Generation;

/// <summary>
/// Picks pending <see cref="NewsItem"/> rows and produces civic content via
/// the shared <see cref="ILlmClient"/>:
///   1. Sonnet → Briefing
///   2. Sonnet → ThinkDeeper (paired with the briefing)
///   3. Haiku judge → should we also produce a Concept and/or QuizQuestion?
///   4. Sonnet → Concept / QuizQuestion when the judge says yes
/// Honors <see cref="NewsOptions.MaxItemsPerDay"/> and <see cref="NewsOptions.BatchSize"/>.
/// </summary>
public class CivicContentGenerationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILlmClient _llm;
    private readonly IOptionsMonitor<NewsOptions> _opts;
    private readonly ILogger<CivicContentGenerationService> _log;

    public CivicContentGenerationService(
        IServiceScopeFactory scopes,
        ILlmClient llm,
        IOptionsMonitor<NewsOptions> opts,
        ILogger<CivicContentGenerationService> log)
    {
        _scopes = scopes;
        _llm = llm;
        _opts = opts;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Reset any items left in Generating from a previous crashed/restarted instance.
        // Swallow exceptions — a DB timeout here must not kill the host.
        try { await ResetStuckItemsAsync(stoppingToken); }
        catch (Exception ex) { _log.LogWarning(ex, "CivicContentGenerationService: ResetStuckItems failed, will retry naturally"); }

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "CivicContentGenerationService: batch failed");
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, _opts.CurrentValue.GenerationIntervalMinutes));
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException) { /* shutdown */ }
        }
    }

    /// <summary>Drives one batch tick. Public for deterministic tests.</summary>
    public async Task<int> GenerateBatchAsync(CancellationToken ct = default)
    {
        var opts = _opts.CurrentValue;

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var llm = _llm;

        // Daily cap: count Generated items in the trailing 24h.
        var sinceCutoff = DateTime.UtcNow - TimeSpan.FromHours(24);
        var generatedInLast24h = await db.NewsItems
            .CountAsync(n => n.Status == NewsItemStatus.Generated && n.ProcessedAt >= sinceCutoff, ct);

        var dailyRoom = Math.Max(0, opts.MaxItemsPerDay - generatedInLast24h);
        if (dailyRoom == 0)
        {
            _log.LogInformation("CivicContentGenerationService: daily cap ({Cap}) reached, sleeping", opts.MaxItemsPerDay);
            return 0;
        }

        var take = Math.Min(opts.BatchSize, dailyRoom);
        var batch = await db.NewsItems
            .Where(n => n.Status == NewsItemStatus.Ingested)
            .OrderBy(n => n.IngestedAt)
            .Take(take)
            .ToListAsync(ct);

        if (batch.Count == 0) return 0;

        var generated = 0;
        foreach (var item in batch)
        {
            try
            {
                item.Status = NewsItemStatus.Generating;
                item.AttemptCount++;
                await db.SaveChangesAsync(ct);

                var produced = await GenerateForItemAsync(db, llm, item, ct);

                if (produced)
                {
                    item.Status = NewsItemStatus.Generated;
                    item.ProcessedAt = DateTime.UtcNow;
                    item.LastError = null;
                    await db.SaveChangesAsync(ct);
                    generated++;
                }
                else
                {
                    // Off-topic for a civics platform — skip without generating a briefing.
                    item.Status = NewsItemStatus.Skipped;
                    item.ProcessedAt = DateTime.UtcNow;
                    item.LastError = null;
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Generation failed for NewsItem {Id}", item.Id);
                item.Status = NewsItemStatus.Failed;
                item.LastError = ex.Message;
                await db.SaveChangesAsync(ct);
            }
        }

        _log.LogInformation("CivicContentGenerationService: processed {Done}/{Total} items", generated, batch.Count);
        return generated;
    }

    /// <summary>
    /// Generates civic content for one news item. Returns false (and generates nothing) when the
    /// story isn't civic/government-relevant, so the caller can mark it Skipped.
    /// </summary>
    private async Task<bool> GenerateForItemAsync(
        CivicDbContext db,
        ILlmClient llm,
        NewsItem item,
        CancellationToken ct)
    {
        var wire = new WireNewsItem(
            item.ExternalId, item.Headline, item.Source, item.Url, item.Summary, item.PublishedAt);

        // 0. Relevance gate (cheap Haiku call) — skip non-civic stories before any Sonnet work.
        var (rSys, rUser) = CivicPrompts.RelevanceGate(wire);
        var relevance = await llm.GenerateStructuredAsync<RelevanceJudgeDto>(rSys, rUser, LlmModelTier.Haiku, maxTokens: 128, ct: ct);
        if (!relevance.IsCivic)
        {
            _log.LogInformation("Skipping non-civic NewsItem {Id} ({Headline}): {Reason}",
                item.Id, item.Headline, relevance.Reason);
            return false;
        }

        // 1. Briefing
        var (bSys, bUser) = CivicPrompts.Briefing(wire);
        var briefingDto = await llm.GenerateStructuredAsync<GeneratedBriefingDto>(bSys, bUser, LlmModelTier.Sonnet, ct: ct);
        var briefing = MapBriefing(briefingDto, item);
        db.Briefings.Add(briefing);

        // 2. ThinkDeeper paired with the briefing
        var (tSys, tUser) = CivicPrompts.ThinkDeeper(wire, briefingDto);
        var thinkDto = await llm.GenerateStructuredAsync<GeneratedThinkDeeperDto>(tSys, tUser, LlmModelTier.Sonnet, ct: ct);
        var think = MapThinkDeeper(thinkDto, item, briefing.Slug);
        db.ThinkDeepers.Add(think);

        // 3. Haiku judge — does this story warrant a Concept and/or QuizQuestion?
        var (jSys, jUser) = CivicPrompts.ContentJudge(wire, briefingDto);
        var judge = await llm.GenerateStructuredAsync<ContentJudgeDto>(jSys, jUser, LlmModelTier.Haiku, maxTokens: 256, ct: ct);

        if (judge.ShouldGenerateConcept && !string.IsNullOrWhiteSpace(judge.ConceptHint))
        {
            var (cSys, cUser) = CivicPrompts.Concept(wire, briefingDto, judge.ConceptHint!);
            var conceptDto = await llm.GenerateStructuredAsync<GeneratedConceptDto>(cSys, cUser, LlmModelTier.Sonnet, ct: ct);
            var conceptSlug = await UniqueConceptSlugAsync(db, conceptDto.Title, judge.ConceptHint!, ct);
            db.Concepts.Add(MapConcept(conceptDto, item, conceptSlug));
        }

        if (judge.ShouldGenerateQuiz && !string.IsNullOrWhiteSpace(judge.QuizHint))
        {
            var (qSys, qUser) = CivicPrompts.QuizQuestion(wire, briefingDto, judge.QuizHint!);
            var quizDto = await llm.GenerateStructuredAsync<GeneratedQuizQuestionDto>(qSys, qUser, LlmModelTier.Sonnet, ct: ct);
            db.QuizQuestions.Add(MapQuizQuestion(quizDto, item));
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---- mappers ----

    private static Briefing MapBriefing(GeneratedBriefingDto d, NewsItem source)
    {
        var slug = $"{Slugify.From(d.Headline)}-{source.PublishedAt:yyyyMMdd}";
        return new Briefing
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Headline = d.Headline,
            Institution = d.Institution,
            Branch = d.Branch,
            Status = d.Status,
            AudienceLevel = string.IsNullOrEmpty(d.AudienceLevel) ? "High School" : d.AudienceLevel,
            KeyConcept = d.KeyConcept,
            Tags = d.Tags ?? Array.Empty<string>(),
            Summary30 = d.Summary30,
            Summary3Min = d.Summary3Min,
            Summary10Min = d.Summary10Min,
            WhoActed = d.WhoActed,
            WhatChanged = d.WhatChanged,
            WhyItMatters = d.WhyItMatters,
            WordsToKnow = (d.WordsToKnow ?? new()).Select(w => new BriefingWord
            {
                Term = w.Term,
                Definition = w.Definition,
            }).ToList(),
            Disagreement = d.Disagreement,
            StrongestArgumentFor = d.StrongestArgumentFor,
            StrongestArgumentAgainst = d.StrongestArgumentAgainst,
            ValuesInConflict = d.ValuesInConflict ?? Array.Empty<string>(),
            ThinkDeeperQuestion = d.ThinkDeeperQuestion,
            RelatedConcepts = d.RelatedConcepts ?? Array.Empty<string>(),
            WhereToGoNext = d.WhereToGoNext ?? Array.Empty<string>(),
            IssueOrder = 0,
            CreatedAt = DateTime.UtcNow,
            GenerationSource = CivicGenerationSource.News,
            SourceNewsItemId = source.Id,
            Locality = source.Locality,
        };
    }

    private static ThinkDeeper MapThinkDeeper(GeneratedThinkDeeperDto d, NewsItem source, string briefingSlug) => new()
    {
        Id = Guid.NewGuid(),
        Slug = briefingSlug,
        Issue = d.Issue,
        FirstReactionPrompt = d.FirstReactionPrompt,
        Values = d.Values ?? Array.Empty<string>(),
        StrongestArgumentA = d.StrongestArgumentA,
        StrongestArgumentB = d.StrongestArgumentB,
        WhatSideAMayMiss = d.WhatSideAMayMiss,
        WhatSideBMayMiss = d.WhatSideBMayMiss,
        WhatWouldChangeYourMind = d.WhatWouldChangeYourMind ?? Array.Empty<string>(),
        CanBothBeTrue = d.CanBothBeTrue,
        BuildYourViewPrompt = d.BuildYourViewPrompt,
        GenerationSource = CivicGenerationSource.News,
        SourceNewsItemId = source.Id,
    };

    private static Concept MapConcept(GeneratedConceptDto d, NewsItem source, string slug) => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        Title = d.Title,
        Category = string.IsNullOrEmpty(d.Category) ? "Legislative process" : d.Category,
        PlainDefinition = d.PlainDefinition,
        WhyItMatters = d.WhyItMatters,
        WhereYouSeeIt = d.WhereYouSeeIt ?? Array.Empty<string>(),
        CurrentExample = d.CurrentExample,
        CommonMisunderstanding = d.CommonMisunderstanding,
        RelatedConcepts = d.RelatedConcepts ?? Array.Empty<string>(),
        TryItQuestion = d.TryItQuestion,
        GenerationSource = CivicGenerationSource.News,
        SourceNewsItemId = source.Id,
    };

    private static QuizQuestion MapQuizQuestion(GeneratedQuizQuestionDto d, NewsItem source) => new()
    {
        Id = Guid.NewGuid(),
        ExternalId = $"q-news-{source.Id.ToString("N")[..8]}",
        Topic = d.Topic,
        Question = d.Question,
        Options = d.Options ?? Array.Empty<string>(),
        CorrectAnswerIndex = d.CorrectAnswerIndex,
        Explanation = d.Explanation,
        RelatedConceptSlug = d.RelatedConceptSlug,
        Order = 100,
        GenerationSource = CivicGenerationSource.News,
        SourceNewsItemId = source.Id,
    };

    private async Task ResetStuckItemsAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var stuck = await db.NewsItems
            .Where(n => n.Status == NewsItemStatus.Generating)
            .ToListAsync(ct);
        if (stuck.Count == 0) return;
        foreach (var item in stuck) item.Status = NewsItemStatus.Ingested;
        await db.SaveChangesAsync(ct);
        _log.LogWarning("CivicContentGenerationService: reset {Count} stuck Generating items to Ingested", stuck.Count);
    }

    private static async Task<string> UniqueConceptSlugAsync(CivicDbContext db, string title, string hint, CancellationToken ct)
    {
        // Prefer the judge's hint, fall back to slugified title, then suffix
        // with a digit if it would collide with an existing seeded concept.
        var baseSlug = Slugify.From(string.IsNullOrWhiteSpace(hint) ? title : hint);
        var candidate = baseSlug;
        var i = 2;
        while (await db.Concepts.AnyAsync(c => c.Slug == candidate, ct))
        {
            candidate = $"{baseSlug}-{i++}";
        }
        return candidate;
    }
}
