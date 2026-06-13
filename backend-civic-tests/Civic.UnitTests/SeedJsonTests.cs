using Civic.API.Models;
using Civic.API.Services;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class SeedJsonTests
{
    [Fact]
    public void Briefings_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<Briefing>>("Seed.briefings.json");

        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(4);
        items.Should().Contain(b => b.Slug == "congress-student-data-privacy-bill");

        var ssp = items.First(b => b.Slug == "congress-student-data-privacy-bill");
        ssp.WordsToKnow.Should().HaveCountGreaterOrEqualTo(3);
        ssp.WordsToKnow.Should().Contain(w => w.Term == "Bill");
        ssp.ValuesInConflict.Should().Contain("Privacy");
        ssp.Tags.Should().Contain("Congress");
    }

    [Fact]
    public void Concepts_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<Concept>>("Seed.concepts.json");

        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(3);
        items.Should().Contain(c => c.Slug == "committee-hearing");
        items.First(c => c.Slug == "committee-hearing").WhereYouSeeIt.Should().NotBeEmpty();
    }

    [Fact]
    public void ThinkDeepers_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<ThinkDeeper>>("Seed.think-deepers.json");

        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(2);
        items.Should().Contain(t => t.Slug == "student-data-privacy");
        items.First(t => t.Slug == "student-data-privacy").Values.Should().Contain("Privacy");
    }

    [Fact]
    public void Elections_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<Election>>("Seed.elections.json");

        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(1);
        items.Should().Contain(e => e.Slug == "us-general-2026");
        items.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Name));
        items.Should().OnlyContain(e => e.ScheduledAt != default);
        // National elections should not carry a region key.
        items.Where(e => e.Scope == ElectionScope.National)
            .Should().OnlyContain(e => string.IsNullOrEmpty(e.Region));
    }

    [Fact]
    public void QuizQuestions_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<QuizQuestion>>("Seed.quiz-questions.json");

        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(4);
        items.Should().OnlyContain(q => !string.IsNullOrWhiteSpace(q.ExternalId));
        items.Should().OnlyContain(q => q.Options.Length >= 2);
        items.Should().OnlyContain(q => q.CorrectAnswerIndex >= 0 && q.CorrectAnswerIndex < q.Options.Length);
        items.Should().OnlyContain(q => !string.IsNullOrWhiteSpace(q.Explanation));
    }

    [Fact]
    public void BillTimeline_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<BillTimelineStep>>("Seed.bill-timeline.json");

        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(5);
        items.Should().ContainSingle(s => s.Status == BillStepStatus.Current);
        items.Should().Contain(s => s.Status == BillStepStatus.Done);
        items.Should().Contain(s => s.Status == BillStepStatus.Upcoming);
        items.OrderBy(s => s.Order).First().ExternalId.Should().Be("ts-001");
    }

    [Fact]
    public void VirtualCandidates_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<VirtualCandidate>>("Seed.virtual-candidates.json");

        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(4);
        items.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Slug));
        items.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Name));
        items.Should().OnlyContain(c => c.Office == CandidateOffice.President);
        items.Should().OnlyContain(c => c.Bio.Length <= 600);
        items.Should().OnlyContain(c => c.PlatformPlanks.Count >= 4);
        items.Should().OnlyContain(c => c.AxisScores.Count >= 5);
        items.Should().OnlyContain(c => c.Sources.Count >= 1);
        items.Select(c => c.Slug).Should().OnlyHaveUniqueItems();

        // Ideological diversity across archetypes (PRD safety/bias-balance goal).
        items.Select(c => c.ArchetypeKey).Distinct().Should().HaveCountGreaterOrEqualTo(4);

        var sofia = items.First(c => c.Slug == "sofia-alvarez");
        sofia.DefaultTone.Should().Be(CampaignTone.Wonkish);
        sofia.PlatformPlanks.Should().Contain(p => p.IssueTags.Contains("privacy"));
        sofia.Sources.Should().Contain(s => s.Kind == SourceKind.PolicyDoc);
    }

    [Fact]
    public void ElectionCycles_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<ElectionCycle>>("Seed.election-cycles.json");

        items.Should().NotBeNull();
        items!.Should().ContainSingle(c => c.IsCurrent);
        items.Should().OnlyContain(c => c.ElectionDate != default);
        items.Should().OnlyContain(c => c.ElectionDate > c.PrimarySeasonStart);
    }

    [Fact]
    public void Briefings_IncludesTaxApportionmentInteractiveModel()
    {
        var items = SeedService.LoadJson<List<Briefing>>("Seed.briefings.json");

        items.Should().NotBeNull();
        var tax = items!.FirstOrDefault(b => b.Slug == "who-gets-your-tax-dollar");
        tax.Should().NotBeNull("the Tax Apportionment briefing must be seeded for the feed entry");
        tax!.Tags.Should().Contain("interactive_model");
        tax.KeyConcept.Should().Be("Tax apportionment");
        tax.WordsToKnow.Should().Contain(w => w.Term == "Apportionment");
        tax.ThinkDeeperQuestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Questions_IncludesFourTaxApportionmentIssueSpecificItems()
    {
        var items = SeedService.LoadJson<List<CivicQuestion>>("Seed.questions.json");

        items.Should().NotBeNull();
        var ponder = items!.Where(q => q.Topic == "Tax apportionment").ToList();
        ponder.Should().HaveCount(4);
        ponder.Should().OnlyContain(q => q.Type == CivicQuestionType.IssueSpecific);
        ponder.Should().OnlyContain(q => q.Choices.Count == 2);
        // Every choice must move at least one axis, and the set should touch the
        // federalism (authority) and fiscal (govt-role / economic-fairness) axes.
        ponder.SelectMany(q => q.Choices).Should().OnlyContain(c => c.AxisDeltas.Count >= 1);
        var axes = ponder.SelectMany(q => q.Choices).SelectMany(c => c.AxisDeltas).Select(d => d.AxisKey).ToHashSet();
        axes.Should().Contain("authority");
        axes.Should().Contain("govt-role");
        items.Select(q => q.ExternalId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void LoadJson_UnknownResource_Throws()
    {
        var act = () => SeedService.LoadJson<List<Briefing>>("Seed.does-not-exist.json");
        act.Should().Throw<InvalidOperationException>();
    }
}
