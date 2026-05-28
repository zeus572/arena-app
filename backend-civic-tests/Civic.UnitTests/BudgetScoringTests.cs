using Civic.API.Models;
using Civic.API.Services;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class BudgetScoringTests
{
    private static ProfileScoringService MakeService() =>
        new(db: null!, catalog: new CivicCatalog());

    private static BudgetSession SessionWith(params (string CategoryKey, int Points)[] allocations) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            CompletedAt = DateTime.UtcNow,
            Allocations = allocations
                .Select(a => new BudgetAllocation
                {
                    Id = Guid.NewGuid(),
                    CategoryKey = a.CategoryKey,
                    Points = a.Points,
                })
                .ToList(),
        };

    [Fact]
    public void Budget_AllZero_AllAxesAtZero()
    {
        var svc = MakeService();
        var emptyBudget = SessionWith(); // no allocations
        var result = svc.Score(Array.Empty<CivicAnswer>(), emptyBudget);
        result.Should().OnlyContain(r => r.Score == 0);
    }

    [Fact]
    public void Budget_FullEmphasisOnHealthcare_PushesGovtRolePositive()
    {
        var svc = MakeService();
        var budget = SessionWith(
            ("healthcare", 100),
            ("defense", 0),
            ("tax-relief", 0));
        var result = svc.Score(Array.Empty<CivicAnswer>(), budget);

        var govtRole = result.Single(r => r.AxisKey == "govt-role");
        govtRole.Score.Should().BePositive(
            "healthcare's +0.5 govt-role delta with full +1 emphasis should dominate over zero-emphasis defense/tax-relief deltas");

        var economic = result.Single(r => r.AxisKey == "economic-fairness");
        economic.Score.Should().BePositive(
            "healthcare's +0.4 plus tax-relief's -0.5 at -1 emphasis both push economic-fairness positive");
    }

    [Fact]
    public void Budget_FullEmphasisOnTaxRelief_PushesGovtRoleNegative()
    {
        var svc = MakeService();
        var budget = SessionWith(
            ("tax-relief", 100),
            ("healthcare", 0));
        var result = svc.Score(Array.Empty<CivicAnswer>(), budget);

        var govtRole = result.Single(r => r.AxisKey == "govt-role");
        govtRole.Score.Should().BeNegative();
    }

    [Fact]
    public void Budget_BaselineAllocation_LeavesAxesAtZero()
    {
        var svc = MakeService();
        // 10 points per category = baseline mean for a 10-category 100-point budget
        var budget = SessionWith(
            ("healthcare", 10),
            ("defense", 10));
        var result = svc.Score(Array.Empty<CivicAnswer>(), budget);
        result.Should().OnlyContain(r => Math.Abs(r.Score) < 0.0001);
    }

    [Fact]
    public void Budget_AndAnswers_CombineSensibly()
    {
        var svc = MakeService();
        var q = new CivicQuestion
        {
            Id = Guid.NewGuid(),
            ExternalId = "q-1",
            Type = CivicQuestionType.SimplePairing,
            Prompt = "?",
            Choices = new()
            {
                new() { Key = "A", Label = "a", AxisDeltas = new() { new() { AxisKey = "govt-role", Delta = -0.5 } } },
                new() { Key = "B", Label = "b", AxisDeltas = new() { new() { AxisKey = "govt-role", Delta = +0.5 } } },
            },
        };
        var ans = new CivicAnswer
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            Question = q,
            QuestionId = q.Id,
            SelectedChoiceKey = "B",
            Confidence = AnswerConfidence.VerySure,
            Intensity = AnswerIntensity.NonNegotiable,
        };
        var budget = SessionWith(("healthcare", 100));

        var result = svc.Score(new[] { ans }, budget);
        var govt = result.Single(r => r.AxisKey == "govt-role");
        govt.SupportingAnswerIds.Should().HaveCount(2,
            "one answer + one budget allocation both contribute to govt-role");
        govt.Score.Should().BePositive();
    }
}
