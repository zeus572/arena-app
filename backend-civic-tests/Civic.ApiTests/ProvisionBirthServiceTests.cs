using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Coalition;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Arena.Shared.Llm;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Phase 0.2 MECHANICS test: drives <see cref="ProvisionBirthService"/> against
/// the shared civic_test DB with a stub <see cref="ILlmClient"/>. This proves the
/// pipeline wiring (briefing -> persisted provision + Birth sub-questions + axis
/// tags + 7-day deadline + source linkage), NOT the neutral-surface/real-tradeoff
/// JUDGMENT — that is a human gate, and exemplar outputs for the 4 sample
/// briefings are recorded in BUILD_LOG.md for review (no live API key here).
/// </summary>
[Collection("Database")]
public class ProvisionBirthServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;
    private readonly Guid _briefingId = Guid.NewGuid();

    public ProvisionBirthServiceTests(DatabaseFixture fx) => _fx = fx;

    public async Task InitializeAsync() => await _fx.ResetMutableAsync();

    public async Task DisposeAsync()
    {
        // Briefings are in the Respawner ignore-list (treated as read-only
        // catalog), so clean up the one this test inserted, plus any provisions
        // born from it.
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        db.Provisions.RemoveRange(await db.Provisions.Where(p => p.SourceBriefingId == _briefingId).ToListAsync());
        db.Briefings.RemoveRange(await db.Briefings.Where(b => b.Id == _briefingId).ToListAsync());
        await db.SaveChangesAsync();
    }

    private async Task<Briefing> SeedBriefingAsync()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var b = new Briefing
        {
            Id = _briefingId,
            Slug = $"student-data-privacy-{_briefingId:N}",
            Headline = "Congress Advances a Bill on Student Data Privacy",
            Institution = "Congress",
            Branch = "Legislative",
            Status = "Committee advanced",
            AudienceLevel = "High School",
            KeyConcept = "Committee hearing",
            Tags = new[] { "Congress", "Privacy", "Schools" },
            Summary30 = "A House committee advanced a student data privacy bill.",
            Summary3Min = "...", Summary10Min = "...",
            WhoActed = "A House committee.",
            WhatChanged = "The bill advanced but is not law.",
            WhyItMatters = "Who should set the rules: Congress, states, districts, or companies?",
            Disagreement = "How strict, who enforces, and whether national rules override local choices.",
            StrongestArgumentFor = "A national baseline prevents weak-local-standard exploitation.",
            StrongestArgumentAgainst = "Small districts need flexibility; national rules move slowly.",
            ValuesInConflict = new[] { "Privacy", "Innovation", "Local control", "National consistency" },
            ThinkDeeperQuestion = "When should privacy rules be national vs. local?",
            GenerationSource = CivicGenerationSource.Seed,
        };
        db.Briefings.Add(b);
        await db.SaveChangesAsync();
        return b;
    }

    private ProvisionBirthService BuildSvc(IServiceScope scope, ILlmClient llm) =>
        new(scope.ServiceProvider.GetRequiredService<CivicDbContext>(),
            llm,
            NullLogger<ProvisionBirthService>.Instance);

    [Fact]
    public async Task Birth_HappyPath_PersistsProvisionWithSubQuestionsAxesAndLinkage()
    {
        var briefing = await SeedBriefingAsync();
        var llm = new StubLlmClient().WithJson<GeneratedProvisionDto>(ProvisionJson());

        using var scope = _fx.Factory.Services.CreateScope();
        var svc = BuildSvc(scope, llm);
        var (born, _) = await svc.BirthFromBriefingAsync(briefing);

        // Birth used the extraction call exactly once, on the Sonnet tier.
        llm.Calls.Should().ContainSingle(c => c.Type == nameof(GeneratedProvisionDto))
            .Which.Tier.Should().Be(LlmModelTier.Sonnet);

        using var verifyScope = _fx.Factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var p = await db.Provisions.Include(x => x.SubQuestions)
            .SingleAsync(x => x.Id == born.Id);

        p.NeutralText.Should().NotBeNullOrWhiteSpace();
        p.State.Should().Be(ProvisionState.Open);
        p.SourceBriefingId.Should().Be(_briefingId);
        p.SourceBriefingSlug.Should().Be(briefing.Slug);
        p.GenerationSource.Should().Be(CivicGenerationSource.Seed);
        p.Deadline.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(5));

        // Gate shape: >= 1 real-tradeoff sub-question, all Birth-origin, all with
        // a stable unique key.
        p.SubQuestions.Should().HaveCountGreaterThanOrEqualTo(1);
        p.SubQuestions.Should().OnlyContain(s => s.Origin == SubQuestionOrigin.Birth);
        p.SubQuestions.Select(s => s.Key).Should().OnlyHaveUniqueItems();
        p.SubQuestions.Should().OnlyContain(s => s.Key.Length > 0 && s.Prompt.Length > 0);

        // Axis tags survived.
        p.RelevantAxes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Birth_DedupesSubQuestionKeys_AndSkipsEmptyPrompts()
    {
        var briefing = await SeedBriefingAsync();
        // Two sub-questions slugify to the same key; one has an empty prompt.
        var json = """
        {
          "title": "Test provision",
          "neutralText": "A concrete neutral proposition with a real tradeoff.",
          "relevantAxes": ["local-vs-national"],
          "subQuestions": [
            { "key": "enforcement", "prompt": "Who enforces?", "positionOptions": ["federal","state"] },
            { "key": "enforcement", "prompt": "Who pays to enforce?", "positionOptions": ["federal","local"] },
            { "key": "", "prompt": "", "positionOptions": [] }
          ]
        }
        """;
        var llm = new StubLlmClient().WithJson<GeneratedProvisionDto>(json);

        using var scope = _fx.Factory.Services.CreateScope();
        var (born, _) = await BuildSvc(scope, llm).BirthFromBriefingAsync(briefing);

        using var verifyScope = _fx.Factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var subQs = await db.SubQuestions.Where(s => s.ProvisionId == born.Id).ToListAsync();

        subQs.Should().HaveCount(2, "empty-prompt sub-questions are skipped");
        subQs.Select(s => s.Key).Should().OnlyHaveUniqueItems("colliding keys are made unique");
    }

    private static string ProvisionJson() => """
    {
      "title": "National baseline rules for student data privacy",
      "neutralText": "Congress should set a national baseline standard for how schools and education-technology vendors collect, retain, and share student data, while leaving stricter local rules permitted.",
      "relevantAxes": ["local-vs-national", "innovation-vs-precaution"],
      "subQuestions": [
        {
          "key": "rule-floor-vs-ceiling",
          "prompt": "Is the national standard a floor (states may go stricter) or a ceiling (it preempts local rules)?",
          "tradeoff": "A floor preserves local control; a ceiling guarantees uniformity for vendors.",
          "positionOptions": ["floor", "ceiling"]
        },
        {
          "key": "enforcement-authority",
          "prompt": "Who enforces the standard?",
          "tradeoff": "Federal enforcement is uniform but slow; local enforcement is responsive but uneven.",
          "positionOptions": ["federal-agency", "state-ag", "private-right-of-action"]
        },
        {
          "key": "retention-limit",
          "prompt": "How long may student data be retained?",
          "tradeoff": "Short retention protects privacy; longer retention aids continuity and analytics.",
          "positionOptions": ["delete-on-graduation", "fixed-years", "vendor-discretion"]
        }
      ]
    }
    """;
}
