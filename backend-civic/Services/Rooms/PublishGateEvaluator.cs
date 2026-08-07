using Civic.API.Models;
using Civic.API.Models.Rooms;

namespace Civic.API.Services.Rooms;

/// <summary>Everything the gates need, hydrated once so evaluation stays pure.</summary>
public class RoomBundle
{
    public Room Room { get; set; } = null!;
    public List<Claim> Claims { get; set; } = new();
    public List<SourceRef> Sources { get; set; } = new();
    public List<Actor> Actors { get; set; } = new();
    public List<TimelineEvent> Timeline { get; set; } = new();
    public List<Development> Developments { get; set; } = new();
    /// <summary>Claim id -> the sources supporting it.</summary>
    public Dictionary<Guid, List<SourceRef>> EvidenceFor { get; set; } = new();
    /// <summary>Claim id -> the sources contradicting it.</summary>
    public Dictionary<Guid, List<SourceRef>> EvidenceAgainst { get; set; } = new();
    /// <summary>Claim ids that are the room's essential facts.</summary>
    public HashSet<Guid> EssentialFactClaimIds { get; set; } = new();
}

/// <summary>One gate's verdict.</summary>
public record GateFinding(
    PublishGateKey Gate,
    bool Passed,
    string Detail,
    bool RequiresNamedApproval = false);

/// <summary>
/// The nine blocking publish gates (design 1y, PRD 07 §16).
///
/// Pure over a hydrated <see cref="RoomBundle"/>, so almost all of it unit-tests with no
/// database. Publish returns 409 with the unmet gates rather than a boolean, because
/// "cannot publish" is useless without "here is what to fix".
///
/// Three gates additionally require a named human to click even when the automated check
/// passes — an automated pass is not editorial judgement.
/// </summary>
public class PublishGateEvaluator
{
    private readonly ICivicCatalog _catalog;

    public PublishGateEvaluator(ICivicCatalog catalog) => _catalog = catalog;

    /// <summary>Gates a machine can never fully clear on its own.</summary>
    public static readonly IReadOnlySet<PublishGateKey> RequireNamedApproval =
        new HashSet<PublishGateKey>
        {
            PublishGateKey.HeadlineNeutrality,
            PublishGateKey.TerminologyReview,
            PublishGateKey.YouthSafety,
        };

    public IReadOnlyList<GateFinding> Evaluate(RoomBundle bundle)
    {
        var findings = new List<GateFinding>
        {
            ProvenanceComplete(bundle),
            ClaimStatusConsistency(bundle),
            SourceDiversity(bundle),
            NumbersAndDates(bundle),
            TerminologyReview(bundle),
            HeadlineNeutrality(bundle),
            Accessibility(bundle),
            YouthSafety(bundle),
            InteractionAnswerValidation(bundle),
        };

        // Every gate must be represented, or "all gates pass" would be a lie of omission.
        if (findings.Select(f => f.Gate).Distinct().Count() != Enum.GetValues<PublishGateKey>().Length)
        {
            throw new InvalidOperationException(
                "PublishGateEvaluator did not return a finding for every gate.");
        }

        return findings;
    }

    // ---------------------------------------------------------------- the nine

    private static GateFinding ProvenanceComplete(RoomBundle b)
    {
        var unsourced = b.EssentialFactClaimIds
            .Where(id => !b.EvidenceFor.TryGetValue(id, out var srcs) || srcs.Count == 0)
            .ToList();

        return new GateFinding(
            PublishGateKey.ProvenanceComplete,
            unsourced.Count == 0,
            unsourced.Count == 0
                ? "Every essential fact traces to at least one source."
                : $"{unsourced.Count} essential fact(s) cite no source.");
    }

    private static GateFinding ClaimStatusConsistency(RoomBundle b)
    {
        // The rule design 1y blocks on: a claim with contradicting evidence of comparable
        // quality cannot publish above Disputed. "Comparable quality" is approximated by
        // source type — a primary document is not rebutted by a blog, but two pieces of
        // reporting do genuinely contradict each other.
        var offenders = new List<string>();

        foreach (var claim in b.Claims)
        {
            if (!IsAboveDisputed(claim.Status)) continue;
            if (!b.EvidenceAgainst.TryGetValue(claim.Id, out var against) || against.Count == 0) continue;

            var bestFor = b.EvidenceFor.TryGetValue(claim.Id, out var f) && f.Count > 0
                ? f.Min(s => Rank(s.SourceType))
                : int.MaxValue;
            var bestAgainst = against.Min(s => Rank(s.SourceType));

            if (bestAgainst <= bestFor)
            {
                offenders.Add($"'{Short(claim.Text)}' is {claim.Status} with comparable "
                            + "contradicting evidence");
            }
        }

        return new GateFinding(
            PublishGateKey.ClaimStatusConsistency,
            offenders.Count == 0,
            offenders.Count == 0
                ? "No claim outranks its contradicting evidence."
                : "Cannot publish above Disputed: " + string.Join("; ", offenders));
    }

    private static GateFinding SourceDiversity(RoomBundle b)
    {
        // Two independent organizations, OR one primary document. A primary document alone
        // is stronger than two outlets syndicating the same wire copy.
        var hasPrimary = b.Sources.Any(s => s.IsPrimary
            || s.SourceType is SourceType.PrimaryDocument or SourceType.GovernmentData);

        var organizations = b.Sources
            .Select(s => s.Organization)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var passed = hasPrimary || organizations >= 2;

        return new GateFinding(
            PublishGateKey.SourceDiversity,
            passed,
            passed
                ? hasPrimary ? "Carries a primary document." : $"{organizations} independent organizations."
                : "Needs two independent organizations, or one primary document.");
    }

    private static GateFinding NumbersAndDates(RoomBundle b)
    {
        var problems = new List<string>();
        var now = DateTime.UtcNow.Date;

        foreach (var dev in b.Developments.Where(d => d.OccurredAt.Date > now))
        {
            problems.Add($"development '{Short(dev.Headline)}' is dated in the future");
        }

        foreach (var ev in b.Timeline.Where(t => t.OccurredOn > DateOnly.FromDateTime(now)))
        {
            // The "Now" marker is the deliberate exception — it is a cap, not an event.
            if (ev.Marker == TimelineMarker.Now) continue;
            problems.Add($"timeline event '{Short(ev.Label)}' is dated in the future");
        }

        foreach (var claim in b.Claims.Where(c =>
                     c.TimeScopeStart is { } s && c.TimeScopeEnd is { } e && s > e))
        {
            problems.Add($"claim '{Short(claim.Text)}' has a time scope that ends before it starts");
        }

        return new GateFinding(
            PublishGateKey.NumbersAndDates,
            problems.Count == 0,
            problems.Count == 0 ? "Dates parse and are not in the future." : string.Join("; ", problems));
    }

    private GateFinding TerminologyReview(RoomBundle b)
    {
        var theme = b.Room as ThemeRoom;
        var declared = (theme?.TerminologyNotes ?? new List<TerminologyNote>())
            .Select(n => n.Term).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var text = string.Join(" \n ", new[]
        {
            b.Room.Title,
            b.Room.Dek,
            theme?.CurrentStatusSentence,
            theme?.TopUnresolvedQuestion,
            theme?.WatchNext,
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var undeclared = _catalog.ContestedTermsIn(text)
            .Where(t => t.RequiresNote && !declared.Contains(t.Term))
            .Select(t => t.Term)
            .ToList();

        return new GateFinding(
            PublishGateKey.TerminologyReview,
            undeclared.Count == 0,
            undeclared.Count == 0
                ? "Contested terms carry notes."
                : "Needs a terminology note for: " + string.Join(", ", undeclared),
            RequiresNamedApproval: true);
    }

    private static GateFinding HeadlineNeutrality(RoomBundle b)
    {
        // Three mechanical criteria. They cannot judge neutrality — that is why this gate
        // also requires a named human — but they catch the obvious failures.
        var problems = new List<string>();
        var title = b.Room.Title ?? "";

        if (title.Length > 120) problems.Add("title is over 120 characters");
        if (title.EndsWith('?') && b.Room is ThemeRoom)
        {
            problems.Add("a theme room title should name the subject, not ask a question");
        }
        if (title.Any(char.IsUpper) && title == title.ToUpperInvariant() && title.Length > 4)
        {
            problems.Add("title is in all caps");
        }
        if (string.IsNullOrWhiteSpace(b.Room.Dek)) problems.Add("no neutral subtitle");

        return new GateFinding(
            PublishGateKey.HeadlineNeutrality,
            problems.Count == 0,
            problems.Count == 0 ? "Headline checks pass." : string.Join("; ", problems),
            RequiresNamedApproval: true);
    }

    private static GateFinding Accessibility(RoomBundle b)
    {
        var missing = b.Timeline
            .Where(t => string.IsNullOrWhiteSpace(t.TextAlternative))
            .Select(t => Short(t.Label))
            .ToList();

        return new GateFinding(
            PublishGateKey.Accessibility,
            missing.Count == 0,
            missing.Count == 0
                ? "Every timeline event has a text alternative."
                : "Timeline events with no text alternative: " + string.Join(", ", missing));
    }

    private static GateFinding YouthSafety(RoomBundle b)
    {
        // Restricted content never publishes on the automated path, and elevated content
        // must at least declare a content note so the reader is warned before the body.
        var problems = new List<string>();

        if (b.Room.Sensitivity == SensitivityLevel.Restricted)
        {
            problems.Add("restricted content requires a named trust-and-safety sign-off");
        }

        if (b.Room.Sensitivity == SensitivityLevel.Elevated
            && string.IsNullOrWhiteSpace(b.Room.ContentNote))
        {
            problems.Add("elevated sensitivity requires a content note");
        }

        return new GateFinding(
            PublishGateKey.YouthSafety,
            problems.Count == 0,
            problems.Count == 0 ? "Sensitivity declared." : string.Join("; ", problems),
            RequiresNamedApproval: true);
    }

    private static GateFinding InteractionAnswerValidation(RoomBundle b)
    {
        // Interactions arrive in R4. Until then the gate reports honestly that there is
        // nothing to validate, rather than silently passing as though it had checked.
        return new GateFinding(
            PublishGateKey.InteractionAnswerValidation,
            true,
            "No interactions attached to this room.");
    }

    // ---------------------------------------------------------------- helpers

    private static bool IsAboveDisputed(ClaimStatus status)
        => status is ClaimStatus.Confirmed or ClaimStatus.StronglySupported;

    /// <summary>Lower is stronger. Used only to compare evidence on both sides of a claim.</summary>
    private static int Rank(SourceType type) => type switch
    {
        SourceType.PrimaryDocument => 0,
        SourceType.GovernmentData => 1,
        SourceType.Reporting => 2,
        SourceType.Analysis => 3,
        SourceType.DirectStatement => 4,
        SourceType.PublicReaction => 5,
        _ => 6,
    };

    private static string Short(string s) => s.Length <= 60 ? s : s[..60].TrimEnd() + "…";
}
