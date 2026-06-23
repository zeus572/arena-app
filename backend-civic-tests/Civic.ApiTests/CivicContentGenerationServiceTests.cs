using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Generation;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Drives <see cref="CivicContentGenerationService.GenerateBatchAsync"/>
/// against the shared civic_test DB with a stub <see cref="Arena.Shared.Llm.ILlmClient"/>.
/// Each test inserts its own NewsItem (with a unique Id) and cleans up the
/// generated rows in DisposeAsync so the shared seeded catalog stays intact.
/// </summary>
[Collection("Database")]
public class CivicContentGenerationServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;
    private readonly Guid _newsItemId = Guid.NewGuid();

    public CivicContentGenerationServiceTests(DatabaseFixture fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        await _fx.ResetMutableAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var briefings = await db.Briefings.Where(b => b.SourceNewsItemId == _newsItemId).ToListAsync();
        foreach (var b in briefings)
        {
            await db.Entry(b).Collection(x => x.WordsToKnow).LoadAsync();
        }
        db.Briefings.RemoveRange(briefings);
        db.ThinkDeepers.RemoveRange(await db.ThinkDeepers.Where(t => t.SourceNewsItemId == _newsItemId).ToListAsync());
        db.Concepts.RemoveRange(await db.Concepts.Where(c => c.SourceNewsItemId == _newsItemId).ToListAsync());
        db.QuizQuestions.RemoveRange(await db.QuizQuestions.Where(q => q.SourceNewsItemId == _newsItemId).ToListAsync());
        db.NewsItems.RemoveRange(await db.NewsItems.Where(n => n.Id == _newsItemId).ToListAsync());
        await db.SaveChangesAsync();
    }

    private CivicContentGenerationService BuildSvc(Arena.Shared.Llm.ILlmClient llm)
    {
        var scopes = _fx.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        return new CivicContentGenerationService(
            scopes,
            llm,
            new TestOptionsMonitor<NewsOptions>(new NewsOptions
            {
                BatchSize = 5,
                MaxItemsPerDay = 100,
                GenerationIntervalMinutes = 30,
            }),
            NullLogger<CivicContentGenerationService>.Instance);
    }

    private async Task SeedIngestedAsync()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        db.NewsItems.Add(new NewsItem
        {
            Id = _newsItemId,
            ExternalId = $"ext-{_newsItemId:N}",
            Headline = "Agency proposes new rule for AI hiring tools",
            Source = "TEST",
            Url = "https://example.com/test",
            Summary = "An agency proposed a rule.",
            PublishedAt = DateTime.UtcNow.AddHours(-1),
            IngestedAt = DateTime.UtcNow,
            Status = NewsItemStatus.Ingested,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GenerateBatch_HappyPath_WritesBriefingAndThinkDeeper()
    {
        var llm = new StubLlmClient()
            .WithJson("RelevanceJudgeDto", "{\"isCivic\":true,\"reason\":\"agency rulemaking\"}")
            .WithJson("GeneratedBriefingDto", BriefingJson())
            .WithJson("GeneratedThinkDeeperDto", ThinkDeeperJson())
            .WithJson("ContentJudgeDto", "{\"shouldGenerateConcept\":false,\"shouldGenerateQuiz\":false}");
        await SeedIngestedAsync();

        var done = await BuildSvc(llm).GenerateBatchAsync();

        done.Should().Be(1);
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.Briefings.SingleAsync(b => b.SourceNewsItemId == _newsItemId))
            .GenerationSource.Should().Be(CivicGenerationSource.News);
        (await db.ThinkDeepers.SingleAsync(t => t.SourceNewsItemId == _newsItemId))
            .GenerationSource.Should().Be(CivicGenerationSource.News);
        (await db.NewsItems.SingleAsync(n => n.Id == _newsItemId))
            .Status.Should().Be(NewsItemStatus.Generated);
    }

    [Fact]
    public async Task GenerateBatch_HaikuApprovesConceptAndQuiz_WritesBoth()
    {
        var llm = new StubLlmClient()
            .WithJson("RelevanceJudgeDto", "{\"isCivic\":true,\"reason\":\"agency rulemaking\"}")
            .WithJson("GeneratedBriefingDto", BriefingJson())
            .WithJson("GeneratedThinkDeeperDto", ThinkDeeperJson())
            .WithJson("ContentJudgeDto", "{\"shouldGenerateConcept\":true,\"conceptHint\":\"agency-rulemaking-news\",\"shouldGenerateQuiz\":true,\"quizHint\":\"Which branch acted?\"}")
            .WithJson("GeneratedConceptDto", ConceptJson())
            .WithJson("GeneratedQuizQuestionDto", QuizJson());
        await SeedIngestedAsync();

        await BuildSvc(llm).GenerateBatchAsync();

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.Concepts.AnyAsync(c => c.SourceNewsItemId == _newsItemId)).Should().BeTrue();
        (await db.QuizQuestions.AnyAsync(q => q.SourceNewsItemId == _newsItemId)).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBatch_NonCivicStory_IsSkippedWithoutBriefing()
    {
        // Relevance gate says not civic — no briefing/thinkdeeper should be written.
        var llm = new StubLlmClient()
            .WithJson("RelevanceJudgeDto", "{\"isCivic\":false,\"reason\":\"pet-care health guide\"}");
        await SeedIngestedAsync();

        var done = await BuildSvc(llm).GenerateBatchAsync();

        done.Should().Be(0);
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.Briefings.AnyAsync(b => b.SourceNewsItemId == _newsItemId)).Should().BeFalse();
        (await db.NewsItems.SingleAsync(n => n.Id == _newsItemId))
            .Status.Should().Be(NewsItemStatus.Skipped);
        // Only the cheap relevance call should have run — no Sonnet briefing call.
        llm.Calls.Should().ContainSingle(c => c.Type == "RelevanceJudgeDto");
        llm.Calls.Should().NotContain(c => c.Type == "GeneratedBriefingDto");
    }

    [Fact]
    public async Task GenerateBatch_LlmThrows_MarksNewsItemFailed()
    {
        var llm = new StubLlmClient(); // no responses → throws on first call
        await SeedIngestedAsync();

        var done = await BuildSvc(llm).GenerateBatchAsync();

        done.Should().Be(0);
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var item = await db.NewsItems.SingleAsync(n => n.Id == _newsItemId);
        item.Status.Should().Be(NewsItemStatus.Failed);
        item.LastError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateBatch_LiveLlmOutageMidItem_DiscardsOrphanBriefing_AndRequeuesItem()
    {
        // Relevance + briefing succeed, then the LLM goes out of credits on the ThinkDeeper call.
        var llm = new OutageLlmClient(new Dictionary<string, string>
        {
            ["RelevanceJudgeDto"] = "{\"isCivic\":true,\"reason\":\"agency rulemaking\"}",
            ["GeneratedBriefingDto"] = BriefingJson(),
        });
        await SeedIngestedAsync();

        var done = await BuildSvc(llm).GenerateBatchAsync();

        done.Should().Be(0);
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        // The briefing was Added before the failing call — it must NOT be flushed as a half-story.
        (await db.Briefings.AnyAsync(b => b.SourceNewsItemId == _newsItemId))
            .Should().BeFalse("a mid-generation live outage must not persist an orphan briefing");
        // The item is requeued (not permanently Failed) so it retries once credits return.
        (await db.NewsItems.SingleAsync(n => n.Id == _newsItemId))
            .Status.Should().Be(NewsItemStatus.Ingested, "a live LLM outage requeues the story rather than failing it");
    }

    [Fact]
    public async Task GenerateBatch_DailyCapZero_DoesNothing()
    {
        await SeedIngestedAsync();
        var llm = new StubLlmClient();
        var scopes = _fx.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var svc = new CivicContentGenerationService(
            scopes,
            llm,
            new TestOptionsMonitor<NewsOptions>(new NewsOptions { BatchSize = 5, MaxItemsPerDay = 0 }),
            NullLogger<CivicContentGenerationService>.Instance);

        var done = await svc.GenerateBatchAsync();
        done.Should().Be(0);
    }

    private static string BriefingJson() => """
    {
      "headline": "Agency proposes new rule for AI hiring tools",
      "institution": "Executive",
      "branch": "Executive",
      "status": "Notice of proposed rulemaking",
      "audienceLevel": "High School",
      "keyConcept": "Agency rulemaking",
      "tags": ["AI", "Hiring", "Regulation"],
      "summary30": "An executive-branch agency has proposed a rule.",
      "summary3Min": "Three minute version of the explainer.",
      "summary10Min": "Ten minute version of the explainer.",
      "whoActed": "The agency",
      "whatChanged": "A proposed rule has been published.",
      "whyItMatters": "It could change how employers use AI in hiring.",
      "wordsToKnow": [{ "term": "Rulemaking", "definition": "How an agency creates regulations." }],
      "disagreement": "Critics and supporters disagree on scope.",
      "strongestArgumentFor": "Protects job candidates from bias.",
      "strongestArgumentAgainst": "Adds compliance cost without proving harm.",
      "valuesInConflict": ["Fairness", "Innovation"],
      "thinkDeeperQuestion": "When should agencies regulate emerging tech?",
      "relatedConcepts": ["agency-rulemaking", "notice-and-comment"],
      "whereToGoNext": ["Read the proposed rule.", "Submit a public comment."]
    }
    """;

    private static string ThinkDeeperJson() => """
    {
      "issue": "Should agencies regulate AI hiring tools now?",
      "firstReactionPrompt": "What's your gut take?",
      "values": ["Fairness", "Innovation"],
      "strongestArgumentA": "Yes - bias risks are real.",
      "strongestArgumentB": "No - premature rules will stifle progress.",
      "whatSideAMayMiss": "Compliance costs hit small employers hardest.",
      "whatSideBMayMiss": "Most harms are documented and ongoing.",
      "whatWouldChangeYourMind": ["Audit data showing systemic bias.", "Evidence of measurable cost."],
      "canBothBeTrue": "Bias and over-regulation can coexist as risks.",
      "buildYourViewPrompt": "Write your own position in 5 sentences."
    }
    """;

    private static string ConceptJson() => """
    {
      "title": "Agency rulemaking",
      "category": "Executive process",
      "plainDefinition": "Agencies make detailed rules to carry out laws.",
      "whyItMatters": "Most day-to-day government action lives here.",
      "whereYouSeeIt": ["EPA emissions rules", "FDA drug labels", "FTC ad rules"],
      "currentExample": "The AI hiring rule shows this in action.",
      "commonMisunderstanding": "People think Congress writes all the rules.",
      "relatedConcepts": ["notice-and-comment", "executive-order"],
      "tryItQuestion": "Why might Congress delegate rule-writing to agencies?"
    }
    """;

    private static string QuizJson() => """
    {
      "topic": "Which branch acted?",
      "question": "A federal agency proposes a hiring rule. Which branch is most directly involved?",
      "options": ["Legislative", "Executive", "Judicial", "State"],
      "correctAnswerIndex": 1,
      "explanation": "Federal agencies sit inside the executive branch.",
      "relatedConceptSlug": "agency-rulemaking"
    }
    """;
}
