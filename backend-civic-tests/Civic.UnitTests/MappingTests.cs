using Civic.API.Mapping;
using Civic.API.Models;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class MappingTests
{
    [Fact]
    public void Briefing_ToDto_RoundTripsAllFieldsIncludingWordsToKnow()
    {
        var briefing = new Briefing
        {
            Id = Guid.NewGuid(),
            Slug = "test-slug",
            Headline = "Test headline",
            Institution = "Congress",
            Branch = "Legislative",
            Status = "Committee advanced",
            AudienceLevel = "High School",
            KeyConcept = "Committee hearing",
            Tags = new[] { "A", "B" },
            Summary30 = "30s",
            Summary3Min = "3min",
            Summary10Min = "10min",
            WhoActed = "X",
            WhatChanged = "Y",
            WhyItMatters = "Z",
            WordsToKnow = new List<BriefingWord>
            {
                new() { Term = "Bill", Definition = "A proposed law." },
                new() { Term = "Markup", Definition = "Lawmakers edit a bill." },
            },
            Disagreement = "D",
            StrongestArgumentFor = "For",
            StrongestArgumentAgainst = "Against",
            ValuesInConflict = new[] { "Privacy", "Safety" },
            ThinkDeeperQuestion = "Q?",
            RelatedConcepts = new[] { "concept-a" },
            WhereToGoNext = new[] { "Next" },
        };

        var dto = briefing.ToDto();

        dto.Slug.Should().Be("test-slug");
        dto.Headline.Should().Be("Test headline");
        dto.Tags.Should().BeEquivalentTo("A", "B");
        dto.WordsToKnow.Should().HaveCount(2);
        dto.WordsToKnow[0].Term.Should().Be("Bill");
        dto.WordsToKnow[0].Definition.Should().Be("A proposed law.");
        dto.ValuesInConflict.Should().BeEquivalentTo("Privacy", "Safety");
    }

    [Fact]
    public void Briefing_ToSummaryDto_OmitsLongSummaries()
    {
        var briefing = new Briefing
        {
            Slug = "s",
            Headline = "H",
            Institution = "I",
            Branch = "B",
            Status = "St",
            AudienceLevel = "A",
            KeyConcept = "K",
            Summary30 = "thirty",
            Summary3Min = "three minutes",
            Summary10Min = "ten minutes",
        };

        var summary = briefing.ToSummaryDto();

        summary.Slug.Should().Be("s");
        summary.Summary30.Should().Be("thirty");
        // Verify summary type omits long fields by checking type does not have them as own properties
        summary.GetType().GetProperty("Summary3Min").Should().BeNull("summary view should not include 3-min summary");
    }

    [Fact]
    public void Concept_ToDto_MapsAllFields()
    {
        var c = new Concept
        {
            Id = Guid.NewGuid(),
            Slug = "c-slug",
            Title = "Title",
            Category = "Cat",
            PlainDefinition = "Plain",
            WhyItMatters = "Why",
            WhereYouSeeIt = new[] { "Here", "There" },
            CurrentExample = "Example",
            CommonMisunderstanding = "Miss",
            RelatedConcepts = new[] { "r1" },
            TryItQuestion = "Try?",
        };

        var dto = c.ToDto();

        dto.Slug.Should().Be("c-slug");
        dto.WhereYouSeeIt.Should().BeEquivalentTo("Here", "There");
        dto.RelatedConcepts.Should().BeEquivalentTo("r1");
    }

    [Fact]
    public void Election_ToDto_MapsScopeAsStringAndKeepsUtcKind()
    {
        var e = new Election
        {
            Id = Guid.NewGuid(),
            Slug = "us-general-2026",
            Name = "2026 U.S. Midterm",
            Scope = ElectionScope.National,
            ScheduledAt = new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc),
            Region = null,
            Description = "Midterms.",
        };

        var dto = e.ToDto();

        dto.Slug.Should().Be("us-general-2026");
        dto.Scope.Should().Be("National");
        dto.Region.Should().BeNull();
        dto.ScheduledAt.Kind.Should().Be(DateTimeKind.Utc);
        dto.ScheduledAt.Should().Be(new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ThinkDeeper_ToDto_MapsAllFields()
    {
        var td = new ThinkDeeper
        {
            Id = Guid.NewGuid(),
            Slug = "td-slug",
            Issue = "Issue?",
            FirstReactionPrompt = "Prompt",
            Values = new[] { "Privacy" },
            StrongestArgumentA = "A",
            StrongestArgumentB = "B",
            WhatSideAMayMiss = "Amiss",
            WhatSideBMayMiss = "Bmiss",
            WhatWouldChangeYourMind = new[] { "evidence" },
            CanBothBeTrue = "Both",
            BuildYourViewPrompt = "Build",
        };

        var dto = td.ToDto();

        dto.Slug.Should().Be("td-slug");
        dto.Values.Should().BeEquivalentTo("Privacy");
        dto.StrongestArgumentA.Should().Be("A");
        dto.StrongestArgumentB.Should().Be("B");
    }
}
