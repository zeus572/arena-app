using Civic.API.Models;
using Civic.API.Services;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class BillSeedTests
{
    [Fact]
    public void Bills_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<Bill>>("Seed.bills.json");

        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(6);
        items.Should().OnlyContain(b => !string.IsNullOrWhiteSpace(b.ExternalId));
        items.Should().OnlyContain(b => !string.IsNullOrWhiteSpace(b.Title));
        items.Should().OnlyContain(b => !string.IsNullOrWhiteSpace(b.Summary));
        items.Should().OnlyContain(b => b.Number > 0);
        items.Select(b => b.ExternalId).Should().OnlyHaveUniqueItems();
        // ExternalId convention: {type}-{number}-{congress}
        items.Should().Contain(b => b.ExternalId == "hr-1-118");
    }

    [Fact]
    public void Axes_IncludeTheFiveNewValueDimensions()
    {
        var axes = SeedService.LoadJson<List<AxisDefinition>>("Seed.axes.json");

        axes.Should().NotBeNull();
        var keys = axes!.Select(a => a.Key).ToHashSet();
        keys.Should().Contain(new[] { "religion", "social-values", "nationalism", "environment", "immigration" });
        axes.Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a.LowLabel) && !string.IsNullOrWhiteSpace(a.HighLabel));
        axes.Select(a => a.Order).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Questions_ElicitEachNewAxis()
    {
        var questions = SeedService.LoadJson<List<CivicQuestion>>("Seed.questions.json");

        questions.Should().NotBeNull();
        var touchedAxes = questions!
            .SelectMany(q => q.Choices)
            .SelectMany(c => c.AxisDeltas)
            .Select(d => d.AxisKey)
            .ToHashSet();

        foreach (var axis in new[] { "religion", "social-values", "nationalism", "environment", "immigration" })
        {
            touchedAxes.Should().Contain(axis, $"the compass needs questions that score '{axis}'");
        }

        questions.Select(q => q.ExternalId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Archetypes_ReferenceOnlyKnownAxes()
    {
        var axes = SeedService.LoadJson<List<AxisDefinition>>("Seed.axes.json");
        var archetypes = SeedService.LoadJson<List<ArchetypeDefinition>>("Seed.archetypes.json");

        var axisKeys = axes!.Select(a => a.Key).ToHashSet();
        archetypes.Should().NotBeNull();
        archetypes!.SelectMany(a => a.AxisVector)
            .Should().OnlyContain(v => axisKeys.Contains(v.AxisKey),
                "every archetype expectation must point at a real axis");
    }
}
