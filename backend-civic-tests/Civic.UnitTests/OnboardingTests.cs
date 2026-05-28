using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Services;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class OnboardingTests
{
    [Fact]
    public void Questions_EmbeddedResource_DeserializesToExpectedShape()
    {
        var items = SeedService.LoadJson<List<CivicQuestion>>("Seed.questions.json");

        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(10);
        items.Should().Contain(q => q.Type == CivicQuestionType.SimplePairing);
        // After iter 5 we also seed pressure-test and forced-tradeoff questions.
        items.Should().Contain(q => q.Type == CivicQuestionType.PressureTest);
        items.Should().Contain(q => q.Type == CivicQuestionType.ForcedTradeoff);
        items.Should().OnlyContain(q => q.Choices.Count >= 2);
        items.Should().OnlyContain(q => q.Choices.All(c => c.AxisDeltas.Count > 0));

        var first = items.OrderBy(q => q.Order).First();
        first.ExternalId.Should().NotBeNullOrWhiteSpace();
        first.Prompt.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Question_ToDto_OmitsAxisDeltasFromClientPayload()
    {
        var q = new CivicQuestion
        {
            Id = Guid.NewGuid(),
            ExternalId = "q-test",
            Type = CivicQuestionType.SimplePairing,
            Prompt = "P?",
            Order = 1,
            Choices = new List<QuestionChoice>
            {
                new()
                {
                    Key = "A",
                    Label = "Option A",
                    AxisDeltas = new List<AxisDelta>
                    {
                        new() { AxisKey = "govt-role", Delta = -0.5 },
                    },
                },
            },
        };

        var dto = q.ToDto();
        dto.Type.Should().Be("SimplePairing");
        dto.Choices.Should().HaveCount(1);
        dto.Choices[0].Key.Should().Be("A");
        dto.Choices[0].Label.Should().Be("Option A");
        dto.GetType().GetProperty("AxisDeltas").Should().BeNull("client payload must not leak scoring deltas");
        dto.Choices[0].GetType().GetProperty("AxisDeltas").Should().BeNull();
    }

    [Fact]
    public void Answer_ToDto_RoundTripsEnumStrings()
    {
        var a = new CivicAnswer
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            QuestionId = Guid.NewGuid(),
            SelectedChoiceKey = "A",
            Confidence = AnswerConfidence.VerySure,
            Intensity = AnswerIntensity.NonNegotiable,
            ReasoningChoice = "fair",
            FreeTextReasoning = "because.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var dto = a.ToDto("q-ext-1");
        dto.Confidence.Should().Be("VerySure");
        dto.Intensity.Should().Be("NonNegotiable");
        dto.QuestionExternalId.Should().Be("q-ext-1");
        dto.SelectedChoiceKey.Should().Be("A");
    }
}
