using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// Interaction scoring and answer-key hygiene (PRD 06).
/// </summary>
public class InteractionScoringTests
{
    // ---------------------------------------------------------------- unscored by design

    [Fact]
    public void BeforeYouKnowAndVoteBeforeReading_AreUnscored()
    {
        // Not an omission. Before You Know is a commitment device, and Vote Before Reading
        // asks for a policy preference — scoring either would hand the product an
        // ideological answer key, which PRD 06 §8 forbids outright.
        InteractionScoring.IsScored(InteractionKind.BeforeYouKnow).Should().BeFalse();
        InteractionScoring.IsScored(InteractionKind.VoteBeforeReading).Should().BeFalse();
    }

    [Fact]
    public void BeforeYouKnow_ReturnsEveryOptionsExplanationNotJustTheChosenOne()
    {
        // The point is showing why the tempting wrong answers are tempting.
        var payload = new BeforeYouKnowPayload(
            "What share of the request has been spent?",
            new List<BykOption>
            {
                new("a", "All of it", "Spending requires an outlay, which has not happened."),
                new("b", "None of it", "Correct — the request sits at the first stage."),
            },
            CorrectOptionId: "b",
            RevealText: "Most readers pick 'all of it'.");

        var result = InteractionScoring.ScoreBeforeYouKnow(payload, new BeforeYouKnowResponse("a"));

        result.Scored.Should().BeFalse();
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Explanation));
    }

    [Fact]
    public void VoteBeforeReading_ScoresNothing()
    {
        var result = InteractionScoring.ScoreVoteBeforeReading(new VoteBeforeReadingResponse("Yes"));

        result.Scored.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    // ---------------------------------------------------------------- classify

    private static ClassifyStatementPayload Classify() => new(new List<ClassifyItem>
    {
        new("1", "The fiscal year begins October 1.", "Factual", "Set in statute."),
        new("2", "This is the worst budget in a decade.", "Opinion", "A value judgement."),
        new("3", "The delay suggests a deal is close.", "Interpretation", "An inference."),
        new("4", "The bill will pass by Friday.", "Prediction", "About the future."),
    });

    [Fact]
    public void ClassifyStatement_GivesPartialCredit()
    {
        // A reader who gets three of four right understands most of the distinction, and
        // an all-or-nothing score would tell them the opposite.
        var response = new ClassifyStatementResponse(new Dictionary<string, string>
        {
            ["1"] = "Factual",
            ["2"] = "Opinion",
            ["3"] = "Interpretation",
            ["4"] = "Factual",
        });

        var result = InteractionScoring.ScoreClassifyStatement(Classify(), response);

        result.Score.Should().Be(75);
        result.Items.Single(i => i.ItemId == "4").Correct.Should().BeFalse();
        result.Items.Single(i => i.ItemId == "4").CorrectLabel.Should().Be("Prediction");
    }

    [Fact]
    public void ClassifyStatement_ExplainsEveryItemRightOrWrong()
    {
        var response = new ClassifyStatementResponse(new Dictionary<string, string>
        {
            ["1"] = "Factual", ["2"] = "Opinion", ["3"] = "Interpretation", ["4"] = "Prediction",
        });

        var result = InteractionScoring.ScoreClassifyStatement(Classify(), response);

        result.Score.Should().Be(100);
        result.Items.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Explanation));
    }

    [Fact]
    public void ClassifyStatement_TreatsAMissingAnswerAsWrongNotAsACrash()
    {
        var result = InteractionScoring.ScoreClassifyStatement(
            Classify(), new ClassifyStatementResponse(new Dictionary<string, string>()));

        result.Score.Should().Be(0);
        result.Items.Should().HaveCount(4);
    }

    // ---------------------------------------------------------------- timeline

    private static TimelineBuilderPayload Timeline() => new(
        EventIds: new List<string> { "a", "b", "c", "d" },
        TrueOrder: new List<string> { "a", "b", "c", "d" },
        KnowabilityNotes: new Dictionary<string, string>
        {
            ["a"] = "Nothing about the later dispute was public yet.",
            ["b"] = "The first filing existed but had not been reported.",
        });

    [Fact]
    public void TimelineBuilder_ScoresAPerfectOrderAtFullMarks()
    {
        var result = InteractionScoring.ScoreTimelineBuilder(
            Timeline(), new TimelineBuilderResponse(new List<string> { "a", "b", "c", "d" }));

        result.Score.Should().Be(100);
    }

    [Fact]
    public void TimelineBuilder_ScoresAFullyReversedOrderAtZero()
    {
        var result = InteractionScoring.ScoreTimelineBuilder(
            Timeline(), new TimelineBuilderResponse(new List<string> { "d", "c", "b", "a" }));

        result.Score.Should().Be(0);
    }

    [Fact]
    public void TimelineBuilder_DoesNotCascadeOneMisplacementIntoTotalFailure()
    {
        // Pairwise rather than position-matching. Swapping one adjacent pair leaves the
        // reader's understanding of the sequence mostly right, and the score should say so
        // — position-matching would score this 50% for a single mistake.
        var result = InteractionScoring.ScoreTimelineBuilder(
            Timeline(), new TimelineBuilderResponse(new List<string> { "b", "a", "c", "d" }));

        result.Score.Should().BeGreaterThan(75);
        result.Score.Should().BeLessThan(100);
    }

    [Fact]
    public void TimelineBuilder_ReturnsTheKnowabilityPass()
    {
        // The payoff is the second pass, not the score.
        var result = InteractionScoring.ScoreTimelineBuilder(
            Timeline(), new TimelineBuilderResponse(new List<string> { "a", "b", "c", "d" }));

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Explanation));
    }

    // ---------------------------------------------------------------- redaction

    [Fact]
    public void Redaction_StripsTheAnswerKeyFromEveryKind()
    {
        var cases = new (InteractionKind Kind, string Payload)[]
        {
            (InteractionKind.BeforeYouKnow, InteractionJson.Serialize(new BeforeYouKnowPayload(
                "Q", new List<BykOption> { new("a", "A", "because") }, "a", "reveal"))),
            (InteractionKind.ClassifyStatement, InteractionJson.Serialize(Classify())),
            (InteractionKind.TimelineBuilder, InteractionJson.Serialize(Timeline())),
        };

        foreach (var (kind, payload) in cases)
        {
            var redacted = InteractionRedaction.ForPlayer(kind, payload)!.ToJsonString();

            foreach (var forbidden in InteractionRedaction.ForbiddenKeys)
            {
                redacted.Should().NotContain(forbidden,
                    "{0} must not leak {1}", kind, forbidden);
            }
        }
    }

    [Fact]
    public void Redaction_AlsoStripsTheExplanationsBeforeTheReveal()
    {
        var payload = InteractionJson.Serialize(new BeforeYouKnowPayload(
            "Q",
            new List<BykOption> { new("a", "An option", "THE GIVEAWAY EXPLANATION") },
            "a",
            "reveal"));

        var redacted = InteractionRedaction
            .ForPlayer(InteractionKind.BeforeYouKnow, payload)!.ToJsonString();

        redacted.Should().NotContain("GIVEAWAY");
        redacted.Should().Contain("An option");
    }

    [Fact]
    public void Redaction_FailsClosedForAnUnhandledKind()
    {
        // A kind nobody has written a redactor for returns nothing rather than leaking a
        // payload it does not understand. Failing closed is the only safe default.
        InteractionRedaction
            .ForPlayer(InteractionKind.ChartTrap, "{\"answer\":\"secret\"}")
            .Should().BeNull();
    }

    [Fact]
    public void EveryInteractionKindName_FitsItsPersistedColumn()
    {
        foreach (var kind in Enum.GetValues<InteractionKind>())
        {
            kind.ToString().Length.Should().BeLessThanOrEqualTo(30);
        }
    }
}
