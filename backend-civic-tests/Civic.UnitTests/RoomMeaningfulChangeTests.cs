using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// The meaningful-change rule — the single most product-defining decision in the Topic
/// Rooms expansion. A room that notifies on every edit is a news feed with extra steps.
/// </summary>
public class RoomMeaningfulChangeTests
{
    [Fact]
    public void EveryChangeType_IsExplicitlyClassified()
    {
        // The guard that matters. A bool-returning classifier with `_ => false` would
        // silently mark any newly added ChangeType as "do not notify" — and a suppressed
        // notification is invisible in testing, indistinguishable from a working system.
        foreach (var type in Enum.GetValues<ChangeType>())
        {
            var act = () => MeaningfulChange.Classify(type);
            act.Should().NotThrow($"{type} must be explicitly meaningful or minor");
        }
    }

    [Fact]
    public void EveryChangeType_HasALedgerLabel()
    {
        foreach (var type in Enum.GetValues<ChangeType>())
        {
            MeaningfulChange.Describe(type).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void MeaningfulTypes_AreExactlyTheSevenFromTheHandoff()
    {
        // An official body acts · a verified fact changes · a claim's evidence status moves ·
        // money advances a funding stage · a negotiation status changes · a prediction
        // resolves · a correction is issued. Nothing else notifies.
        MeaningfulChange.MeaningfulTypes.Should().BeEquivalentTo(new[]
        {
            ChangeType.OfficialAction,
            ChangeType.VerifiedFactChanged,
            ChangeType.ClaimStatusMoved,
            ChangeType.MoneyStageAdvanced,
            ChangeType.NegotiationStatusChanged,
            ChangeType.PredictionResolved,
            ChangeType.CorrectionIssued,
        });
    }

    [Theory]
    [InlineData(ChangeType.CommentaryAdded)]
    [InlineData(ChangeType.CopyEdit)]
    [InlineData(ChangeType.SourceAdded)]
    [InlineData(ChangeType.TypoFix)]
    [InlineData(ChangeType.FormattingChange)]
    [InlineData(ChangeType.RelationshipAdded)]
    public void ExplicitlyNotMeaningful_DoesNotNotify(ChangeType type)
    {
        // The handoff names these directly: new commentary about an old event, copy edits,
        // added sources on an existing fact, typo fixes. They are logged and counted, and
        // nobody is interrupted for them.
        MeaningfulChange.Classify(type).Should().Be(ChangeSignificance.Minor);
        MeaningfulChange.IsNotifiable(type).Should().BeFalse();
    }

    [Fact]
    public void CommentaryAdded_IsNotADevelopment()
    {
        // Design 1g's sidebar closes with exactly this line, so it is worth its own test:
        // "New commentary about an old event is not a development."
        MeaningfulChange.IsNotifiable(ChangeType.CommentaryAdded).Should().BeFalse();
    }

    [Fact]
    public void CorrectionIssued_IsMeaningfulAndLabelledAsACorrection()
    {
        // Corrections get their own visual treatment and are never folded into "updated" —
        // if the label ever collapsed to "Changed", that rule would quietly disappear.
        MeaningfulChange.Classify(ChangeType.CorrectionIssued)
            .Should().Be(ChangeSignificance.Meaningful);
        MeaningfulChange.Describe(ChangeType.CorrectionIssued).Should().Be("Corrected");
    }

    [Fact]
    public void SourceAdded_IsMinorButClaimStatusMoved_IsNot()
    {
        // The distinction the product turns on: adding another source to a fact that already
        // had one changes nothing for the reader. Moving that fact's evidence status does.
        MeaningfulChange.Classify(ChangeType.SourceAdded).Should().Be(ChangeSignificance.Minor);
        MeaningfulChange.Classify(ChangeType.ClaimStatusMoved).Should().Be(ChangeSignificance.Meaningful);
    }

    [Fact]
    public void CorrectionKind_CoversThePrd07Taxonomy()
    {
        Enum.GetNames<CorrectionKind>().Should().BeEquivalentTo(new[]
        {
            "Typographical", "Clarification", "Factual",
            "SourceCorrection", "Retraction", "MaterialFraming",
        });
    }

    [Fact]
    public void ChangeTypeNames_FitTheirPersistedColumn()
    {
        foreach (var type in Enum.GetValues<ChangeType>())
        {
            type.ToString().Length.Should().BeLessThanOrEqualTo(30);
        }
    }
}
