using Civic.API.Models;
using Civic.API.Services;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class ScoringTests
{
    // We can construct ProfileScoringService with a null DbContext because the pure
    // Score / BlendArchetypes methods don't touch the DB. The catalog comes from the
    // same embedded JSON the production service uses.
    private static ProfileScoringService MakeService() =>
        new(db: null!, catalog: new CivicCatalog());

    private static CivicQuestion Question(
        string externalId,
        string axisKey,
        double deltaForA,
        double deltaForB)
    {
        return new CivicQuestion
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            Type = CivicQuestionType.SimplePairing,
            Prompt = "?",
            Choices = new()
            {
                new() { Key = "A", Label = "a", AxisDeltas = new() { new() { AxisKey = axisKey, Delta = deltaForA } } },
                new() { Key = "B", Label = "b", AxisDeltas = new() { new() { AxisKey = axisKey, Delta = deltaForB } } },
            },
        };
    }

    private static CivicAnswer Answer(
        CivicQuestion question,
        string choiceKey,
        AnswerConfidence c = AnswerConfidence.VerySure,
        AnswerIntensity i = AnswerIntensity.High)
    {
        return new CivicAnswer
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            QuestionId = question.Id,
            Question = question,
            SelectedChoiceKey = choiceKey,
            Confidence = c,
            Intensity = i,
        };
    }

    [Fact]
    public void Score_NoAnswers_AllAxesZeroWithEmptySupport()
    {
        var svc = MakeService();
        var result = svc.Score(Array.Empty<CivicAnswer>());

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(a => a.Score == 0 && a.SupportingAnswerIds.Length == 0);
    }

    [Fact]
    public void Score_SingleAnswer_PointsAxisInTheChoiceDirection()
    {
        var svc = MakeService();
        var q = Question("q-1", "govt-role", -0.5, +0.5);
        var answer = Answer(q, "B");

        var result = svc.Score(new[] { answer });
        var govtRole = result.Single(r => r.AxisKey == "govt-role");

        govtRole.Score.Should().BePositive();
        govtRole.Score.Should().BeApproximately(1.0, 0.0001,
            "weighted average of a single +0.5 delta is +1.0 after normalizing by |delta|*weight");
        govtRole.SupportingAnswerIds.Should().ContainSingle().Which.Should().Be(answer.Id);
    }

    [Fact]
    public void Score_HigherIntensity_OutweighsLowerIntensity()
    {
        var svc = MakeService();
        var q1 = Question("q-1", "speech", -0.6, +0.6);
        var q2 = Question("q-2", "speech", -0.6, +0.6);

        var heavyPro = Answer(q1, "B", AnswerConfidence.VerySure, AnswerIntensity.NonNegotiable);
        var lightCon = Answer(q2, "A", AnswerConfidence.NotSure, AnswerIntensity.Low);

        var result = svc.Score(new[] { heavyPro, lightCon });
        var speech = result.Single(r => r.AxisKey == "speech");

        speech.Score.Should().BePositive("the heavy non-negotiable B answer should dominate the weak low/not-sure A answer");
    }

    [Fact]
    public void Score_OppositeEqualAnswers_NetToNearZero()
    {
        var svc = MakeService();
        var q1 = Question("q-1", "risk", -0.5, +0.5);
        var q2 = Question("q-2", "risk", -0.5, +0.5);

        var a = Answer(q1, "A", AnswerConfidence.VerySure, AnswerIntensity.High);
        var b = Answer(q2, "B", AnswerConfidence.VerySure, AnswerIntensity.High);

        var result = svc.Score(new[] { a, b });
        var risk = result.Single(r => r.AxisKey == "risk");
        risk.Score.Should().BeApproximately(0, 0.0001);
        risk.SupportingAnswerIds.Should().HaveCount(2);
    }

    [Fact]
    public void Blend_NoSignal_ReturnsUniformDistribution()
    {
        var svc = MakeService();
        var emptyAxes = svc.Score(Array.Empty<CivicAnswer>());
        var blend = svc.BlendArchetypes(emptyAxes);

        blend.Should().NotBeEmpty();
        blend.Sum(b => b.Percent).Should().BeApproximately(100.0, 0.01);
        // All archetype percentages should be roughly equal
        var min = blend.Min(b => b.Percent);
        var max = blend.Max(b => b.Percent);
        (max - min).Should().BeLessThan(0.01);
    }

    [Fact]
    public void Blend_StrongPublicSystemsProfile_LeadsWithPublicSystemsArchetype()
    {
        var svc = MakeService();
        // Answers that strongly push govt-role high, economic-fairness high, and
        // community high should produce a profile that matches one of the two
        // public-systems archetypes — public-builder or fairness-advocate.
        var qGovt = Question("q-1", "govt-role", -0.5, +0.5);
        var qFair = Question("q-2", "economic-fairness", -0.5, +0.5);
        var qComm = Question("q-3", "community", -0.5, +0.5);

        var answers = new[]
        {
            Answer(qGovt, "B", AnswerConfidence.VerySure, AnswerIntensity.NonNegotiable),
            Answer(qFair, "B", AnswerConfidence.VerySure, AnswerIntensity.NonNegotiable),
            Answer(qComm, "B", AnswerConfidence.VerySure, AnswerIntensity.NonNegotiable),
        };

        var axes = svc.Score(answers);
        var blend = svc.BlendArchetypes(axes);

        blend.Sum(b => b.Percent).Should().BeApproximately(100.0, 0.01);
        blend[0].ArchetypeKey.Should().BeOneOf("public-builder", "fairness-advocate");
    }

    [Fact]
    public void Blend_StrongLibertyProfile_LeadsWithLibertyGuardian()
    {
        var svc = MakeService();
        var qGovt = Question("q-1", "govt-role", -0.5, +0.5);
        var qLib = Question("q-2", "liberty-safety", -0.5, +0.5);
        var qAuth = Question("q-3", "authority", -0.5, +0.5);
        var qSpeech = Question("q-4", "speech", -0.5, +0.5);

        var answers = new[]
        {
            Answer(qGovt, "A", AnswerConfidence.VerySure, AnswerIntensity.NonNegotiable),
            Answer(qLib, "A", AnswerConfidence.VerySure, AnswerIntensity.NonNegotiable),
            Answer(qAuth, "A", AnswerConfidence.VerySure, AnswerIntensity.NonNegotiable),
            Answer(qSpeech, "A", AnswerConfidence.VerySure, AnswerIntensity.NonNegotiable),
        };

        var axes = svc.Score(answers);
        var blend = svc.BlendArchetypes(axes);
        blend[0].ArchetypeKey.Should().Be("liberty-guardian");
    }

    [Theory]
    [InlineData(AnswerConfidence.NotSure, 0.5)]
    [InlineData(AnswerConfidence.SomewhatSure, 0.75)]
    [InlineData(AnswerConfidence.VerySure, 1.0)]
    public void ConfidenceWeight_MatchesExpectedScale(AnswerConfidence c, double expected)
    {
        ProfileScoringService.ConfidenceWeight(c).Should().Be(expected);
    }

    [Theory]
    [InlineData(AnswerIntensity.Low, 0.25)]
    [InlineData(AnswerIntensity.Medium, 0.5)]
    [InlineData(AnswerIntensity.High, 0.75)]
    [InlineData(AnswerIntensity.NonNegotiable, 1.0)]
    public void IntensityWeight_MatchesExpectedScale(AnswerIntensity i, double expected)
    {
        ProfileScoringService.IntensityWeight(i).Should().Be(expected);
    }
}
