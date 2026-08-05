using Civic.API.Models.Rooms;

namespace Civic.API.Services.Rooms;

/// <summary>
/// What counts as a change worth interrupting someone for.
///
/// This is the single most product-defining rule in the Topic Rooms expansion: a room that
/// notifies on every edit is a news feed with extra steps. Seven change types are meaningful
/// — an official body acted, a verified fact changed, a claim's status moved, money advanced
/// a stage, a negotiation status changed, a prediction resolved, a correction was issued.
/// Everything else is logged and counted honestly, and nobody is told about it.
///
/// Pure and dependency-free so the rule is unit-testable on its own terms.
/// </summary>
public static class MeaningfulChange
{
    /// <summary>
    /// Returns an enum rather than a bool with a default arm on purpose.
    ///
    /// A <c>bool</c> switch with <c>_ =&gt; false</c> would silently classify any newly added
    /// ChangeType as "do not notify" — a suppressed notification is invisible in testing and
    /// looks exactly like a working system. With an exhaustive switch expression, an
    /// unhandled member throws, and <c>RoomMeaningfulChangeTests</c> walks every member.
    /// </summary>
    public static ChangeSignificance Classify(ChangeType type) => type switch
    {
        ChangeType.OfficialAction => ChangeSignificance.Meaningful,
        ChangeType.VerifiedFactChanged => ChangeSignificance.Meaningful,
        ChangeType.ClaimStatusMoved => ChangeSignificance.Meaningful,
        ChangeType.MoneyStageAdvanced => ChangeSignificance.Meaningful,
        ChangeType.NegotiationStatusChanged => ChangeSignificance.Meaningful,
        ChangeType.PredictionResolved => ChangeSignificance.Meaningful,
        ChangeType.CorrectionIssued => ChangeSignificance.Meaningful,

        ChangeType.CommentaryAdded => ChangeSignificance.Minor,
        ChangeType.CopyEdit => ChangeSignificance.Minor,
        ChangeType.SourceAdded => ChangeSignificance.Minor,
        ChangeType.TypoFix => ChangeSignificance.Minor,
        ChangeType.FormattingChange => ChangeSignificance.Minor,
        ChangeType.RelationshipAdded => ChangeSignificance.Minor,

        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type,
            "Unclassified ChangeType. Every member must be explicitly meaningful or minor — "
          + "defaulting would silently suppress a notification that mattered."),
    };

    /// <summary>Convenience for the notification fan-out. Same rule, no second source of truth.</summary>
    public static bool IsNotifiable(ChangeType type)
        => Classify(type) == ChangeSignificance.Meaningful;

    /// <summary>
    /// The short uppercase word in the delta ledger's 66px type column (design 1d).
    /// Corrections get their own label and are never labelled "Updated".
    /// </summary>
    public static string Describe(ChangeType type) => type switch
    {
        ChangeType.OfficialAction => "Acted",
        ChangeType.VerifiedFactChanged => "Changed",
        ChangeType.ClaimStatusMoved => "Status",
        ChangeType.MoneyStageAdvanced => "Money",
        ChangeType.NegotiationStatusChanged => "Talks",
        ChangeType.PredictionResolved => "Resolved",
        ChangeType.CorrectionIssued => "Corrected",

        ChangeType.CommentaryAdded => "Commentary",
        ChangeType.CopyEdit => "Edit",
        ChangeType.SourceAdded => "Source",
        ChangeType.TypoFix => "Typo",
        ChangeType.FormattingChange => "Format",
        ChangeType.RelationshipAdded => "Link",

        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unlabelled ChangeType."),
    };

    /// <summary>The seven meaningful types, for tests and for the admin queue's filters.</summary>
    public static IReadOnlyList<ChangeType> MeaningfulTypes { get; } =
        Enum.GetValues<ChangeType>()
            .Where(t => Classify(t) == ChangeSignificance.Meaningful)
            .ToList();
}
