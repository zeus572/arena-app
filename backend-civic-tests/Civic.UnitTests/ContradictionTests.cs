using Civic.API.Models;
using Civic.API.Services;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class ContradictionTests
{
    private static ContradictionDetectionService MakeService() =>
        new(new CivicCatalog());

    private static CivicQuestion Q(string axisKey, double deltaA, double deltaB)
    {
        return new CivicQuestion
        {
            Id = Guid.NewGuid(),
            ExternalId = "q-" + Guid.NewGuid(),
            Type = CivicQuestionType.SimplePairing,
            Prompt = "?",
            Choices = new()
            {
                new() { Key = "A", Label = "a", AxisDeltas = new() { new() { AxisKey = axisKey, Delta = deltaA } } },
                new() { Key = "B", Label = "b", AxisDeltas = new() { new() { AxisKey = axisKey, Delta = deltaB } } },
            },
        };
    }

    private static CivicAnswer A(CivicQuestion q, string key, AnswerConfidence c) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            Question = q,
            QuestionId = q.Id,
            SelectedChoiceKey = key,
            Confidence = c,
            Intensity = AnswerIntensity.High,
        };

    [Fact]
    public void Detect_AllOneDirection_NoTensions()
    {
        var svc = MakeService();
        var q1 = Q("speech", -0.6, +0.6);
        var q2 = Q("speech", -0.5, +0.5);
        var answers = new[] { A(q1, "A", AnswerConfidence.VerySure), A(q2, "A", AnswerConfidence.VerySure) };

        var tensions = svc.Detect(answers);
        tensions.Should().BeEmpty();
    }

    [Fact]
    public void Detect_OpposingHighConfidenceAnswers_FlagsTension()
    {
        var svc = MakeService();
        var q1 = Q("speech", -0.6, +0.6); // chose A → speech -0.6
        var q2 = Q("speech", -0.6, +0.6); // chose B → speech +0.6
        var answers = new[] { A(q1, "A", AnswerConfidence.VerySure), A(q2, "B", AnswerConfidence.VerySure) };

        var tensions = svc.Detect(answers);
        tensions.Should().ContainSingle(t => t.AxisKey == "speech");
        var t = tensions.Single(t => t.AxisKey == "speech");
        t.AnswerIdsLow.Should().HaveCount(1);
        t.AnswerIdsHigh.Should().HaveCount(1);
    }

    [Fact]
    public void Detect_NotSureAnswers_Ignored()
    {
        var svc = MakeService();
        var q1 = Q("speech", -0.6, +0.6);
        var q2 = Q("speech", -0.6, +0.6);
        var answers = new[] { A(q1, "A", AnswerConfidence.NotSure), A(q2, "B", AnswerConfidence.NotSure) };

        var tensions = svc.Detect(answers);
        tensions.Should().BeEmpty();
    }

    [Fact]
    public void Detect_SmallDeltas_Ignored()
    {
        var svc = MakeService();
        // Deltas with |Δ| < 0.3 don't count as significant contributions per spec
        var q1 = Q("speech", -0.2, +0.2);
        var q2 = Q("speech", -0.2, +0.2);
        var answers = new[] { A(q1, "A", AnswerConfidence.VerySure), A(q2, "B", AnswerConfidence.VerySure) };

        var tensions = svc.Detect(answers);
        tensions.Should().BeEmpty();
    }
}

public class ExplanationTests
{
    private static (RuleBasedExplanationService svc, ICivicCatalog catalog) Make() =>
        (new RuleBasedExplanationService(), new CivicCatalog());

    [Fact]
    public void Insights_EmptyProfile_ReturnsFallbackBullet()
    {
        var (svc, catalog) = Make();
        var p = new UserProfile { AxisScores = new() };
        var insights = svc.InsightsForProfile(p, catalog);
        insights.Should().NotBeEmpty();
        insights[0].Should().Contain("more questions");
    }

    [Fact]
    public void Insights_StrongAxisScore_ProducesQualifiedSentence()
    {
        var (svc, catalog) = Make();
        var p = new UserProfile
        {
            ProfileVersion = 2,
            AxisScores = new()
            {
                new() { AxisKey = "govt-role", Score = 0.85, SupportingAnswerIds = new[] { Guid.NewGuid() } },
                new() { AxisKey = "speech", Score = -0.4, SupportingAnswerIds = new[] { Guid.NewGuid() } },
            },
        };
        var insights = svc.InsightsForProfile(p, catalog);
        insights.Should().Contain(s => s.Contains("strongly") && s.Contains("public builder", StringComparison.OrdinalIgnoreCase));
        insights.Should().Contain(s => s.Contains("moderately") && s.Contains("open expression", StringComparison.OrdinalIgnoreCase));
    }
}
