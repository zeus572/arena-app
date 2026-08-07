using Civic.API.Models.Rooms;
using Civic.API.Services;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// The nine blocking publish gates (design 1y, PRD 07 §16).
///
/// Pure over a hydrated bundle, so the whole gate suite runs with no database. Each test
/// takes a complete bundle and removes exactly one thing.
/// </summary>
public class PublishGateTests
{
    private static PublishGateEvaluator Evaluator() => new(new CivicCatalog());

    private static SourceRef Source(
        string org, SourceType type = SourceType.Reporting, bool primary = false) => new()
    {
        Id = Guid.NewGuid(),
        Url = $"https://example.test/{Guid.NewGuid():N}",
        UrlHash = Guid.NewGuid().ToString("N"),
        Title = "A source",
        Organization = org,
        SourceType = type,
        IsPrimary = primary,
    };

    private static Claim Claim(ClaimStatus status = ClaimStatus.Confirmed) => new()
    {
        Id = Guid.NewGuid(),
        Slug = "a-claim",
        NormalizedTextHash = Guid.NewGuid().ToString("N"),
        Text = "A checkable assertion about the appropriations process.",
        Status = status,
        WhatWouldSettleIt = "A published record.",
    };

    /// <summary>A bundle that passes every gate. Each test breaks exactly one thing.</summary>
    private static RoomBundle Complete()
    {
        var claim = Claim();
        var primary = Source("GPO", SourceType.PrimaryDocument, primary: true);
        var reporting = Source("A Newspaper");

        var room = new ThemeRoom
        {
            Id = Guid.NewGuid(),
            Slug = "federal-appropriations",
            Title = "Federal appropriations",
            Dek = "How Congress decides what the federal government may obligate.",
            Sensitivity = SensitivityLevel.Standard,
            CurrentStatusSentence = "Four of five funding stages remain empty.",
        };

        return new RoomBundle
        {
            Room = room,
            Claims = new List<Claim> { claim },
            Sources = new List<SourceRef> { primary, reporting },
            Timeline = new List<TimelineEvent>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RoomId = room.Id,
                    OccurredOn = new DateOnly(1974, 7, 12),
                    Label = "Budget Act",
                    TextAlternative = "1974: Congress creates CBO.",
                },
            },
            Developments = new List<Development>(),
            EvidenceFor = new Dictionary<Guid, List<SourceRef>>
            {
                [claim.Id] = new() { primary },
            },
            EvidenceAgainst = new Dictionary<Guid, List<SourceRef>> { [claim.Id] = new() },
            EssentialFactClaimIds = new HashSet<Guid> { claim.Id },
        };
    }

    private static GateFinding Find(IReadOnlyList<GateFinding> findings, PublishGateKey gate)
        => findings.Single(f => f.Gate == gate);

    [Fact]
    public void ACompleteBundle_PassesEveryGate()
    {
        var findings = Evaluator().Evaluate(Complete());

        findings.Should().OnlyContain(f => f.Passed,
            "the fixture is meant to be the baseline every other test breaks");
    }

    [Fact]
    public void EveryGate_IsAlwaysEvaluated()
    {
        // "All gates pass" must never be true by omission.
        var findings = Evaluator().Evaluate(Complete());

        findings.Select(f => f.Gate).Should().BeEquivalentTo(Enum.GetValues<PublishGateKey>());
    }

    [Fact]
    public void ProvenanceComplete_FailsWhenAnEssentialFactCitesNothing()
    {
        var b = Complete();
        b.EvidenceFor[b.Claims[0].Id] = new List<SourceRef>();

        Find(Evaluator().Evaluate(b), PublishGateKey.ProvenanceComplete).Passed.Should().BeFalse();
    }

    [Fact]
    public void ClaimStatusConsistency_BlocksAConfirmedClaimWithComparableContradictingEvidence()
    {
        // Design 1y's blocking rule, and the one gate that encodes an editorial principle
        // rather than a completeness check.
        var b = Complete();
        var claim = b.Claims[0];
        claim.Status = ClaimStatus.Confirmed;
        b.EvidenceFor[claim.Id] = new List<SourceRef> { Source("Agency", SourceType.GovernmentData) };
        b.EvidenceAgainst[claim.Id] = new List<SourceRef>
        {
            Source("Another agency", SourceType.GovernmentData),
        };

        var finding = Find(Evaluator().Evaluate(b), PublishGateKey.ClaimStatusConsistency);

        finding.Passed.Should().BeFalse();
        finding.Detail.Should().Contain("Disputed");
    }

    [Fact]
    public void ClaimStatusConsistency_AllowsAPrimaryDocumentToOutrankWeakerContradiction()
    {
        // "Comparable quality" is the operative phrase. A statute is not rebutted by a
        // press release, and a gate that said otherwise would make Confirmed unreachable.
        var b = Complete();
        var claim = b.Claims[0];
        claim.Status = ClaimStatus.Confirmed;
        b.EvidenceFor[claim.Id] = new List<SourceRef>
        {
            Source("GPO", SourceType.PrimaryDocument, primary: true),
        };
        b.EvidenceAgainst[claim.Id] = new List<SourceRef>
        {
            Source("A campaign", SourceType.DirectStatement),
        };

        Find(Evaluator().Evaluate(b), PublishGateKey.ClaimStatusConsistency)
            .Passed.Should().BeTrue();
    }

    [Fact]
    public void ClaimStatusConsistency_LetsADisputedClaimCarryContradictingEvidence()
    {
        // Disputed MEANS credible sources contradict each other. Blocking it would be
        // exactly backwards.
        var b = Complete();
        var claim = b.Claims[0];
        claim.Status = ClaimStatus.Disputed;
        b.EvidenceAgainst[claim.Id] = new List<SourceRef>
        {
            Source("GPO", SourceType.PrimaryDocument, primary: true),
        };

        Find(Evaluator().Evaluate(b), PublishGateKey.ClaimStatusConsistency)
            .Passed.Should().BeTrue();
    }

    [Fact]
    public void SourceDiversity_FailsOnASingleNonPrimaryOrganization()
    {
        var b = Complete();
        b.Sources = new List<SourceRef> { Source("A Newspaper") };

        Find(Evaluator().Evaluate(b), PublishGateKey.SourceDiversity).Passed.Should().BeFalse();
    }

    [Fact]
    public void SourceDiversity_AcceptsOnePrimaryDocumentAlone()
    {
        // A statute on its own is stronger than two outlets syndicating the same wire copy.
        var b = Complete();
        b.Sources = new List<SourceRef> { Source("GPO", SourceType.PrimaryDocument, primary: true) };

        Find(Evaluator().Evaluate(b), PublishGateKey.SourceDiversity).Passed.Should().BeTrue();
    }

    [Fact]
    public void NumbersAndDates_RejectsADevelopmentDatedInTheFuture()
    {
        var b = Complete();
        b.Developments.Add(new Development
        {
            Id = Guid.NewGuid(),
            RoomId = b.Room.Id,
            OccurredAt = DateTime.UtcNow.AddDays(3),
            Headline = "Something that has not happened",
            WhyItMatters = "It would matter, if it had.",
            InclusionReason = "An official body acted.",
        });

        Find(Evaluator().Evaluate(b), PublishGateKey.NumbersAndDates).Passed.Should().BeFalse();
    }

    [Fact]
    public void NumbersAndDates_AllowsTheNowMarkerToSitInTheFuture()
    {
        // "Now" is a cap on the right-hand end of the timeline, not an event.
        var b = Complete();
        b.Timeline.Add(new TimelineEvent
        {
            Id = Guid.NewGuid(),
            RoomId = b.Room.Id,
            OccurredOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Label = "Now",
            Marker = TimelineMarker.Now,
            TextAlternative = "The present day.",
        });

        Find(Evaluator().Evaluate(b), PublishGateKey.NumbersAndDates).Passed.Should().BeTrue();
    }

    [Fact]
    public void TerminologyReview_BlocksAContestedTermWithNoNote()
    {
        var b = Complete();
        ((ThemeRoom)b.Room).CurrentStatusSentence =
            "The government is spending the money now.";

        var finding = Find(Evaluator().Evaluate(b), PublishGateKey.TerminologyReview);

        finding.Passed.Should().BeFalse();
        finding.Detail.Should().Contain("spending");
    }

    [Fact]
    public void TerminologyReview_PassesOnceTheTermIsDeclared()
    {
        var b = Complete();
        var theme = (ThemeRoom)b.Room;
        theme.CurrentStatusSentence = "The government is spending the money now.";
        theme.TerminologyNotes.Add(new TerminologyNote
        {
            Term = "spending",
            Note = "Reserved for outlays; earlier stages are named explicitly.",
        });

        Find(Evaluator().Evaluate(b), PublishGateKey.TerminologyReview).Passed.Should().BeTrue();
    }

    [Fact]
    public void Accessibility_FailsWhenATimelineEventHasNoTextAlternative()
    {
        var b = Complete();
        b.Timeline[0].TextAlternative = null;

        Find(Evaluator().Evaluate(b), PublishGateKey.Accessibility).Passed.Should().BeFalse();
    }

    [Fact]
    public void HeadlineNeutrality_FailsWithoutANeutralSubtitle()
    {
        var b = Complete();
        b.Room.Dek = "";

        Find(Evaluator().Evaluate(b), PublishGateKey.HeadlineNeutrality).Passed.Should().BeFalse();
    }

    [Fact]
    public void YouthSafety_RequiresAContentNoteForElevatedSensitivity()
    {
        var b = Complete();
        b.Room.Sensitivity = SensitivityLevel.Elevated;
        b.Room.ContentNote = null;

        Find(Evaluator().Evaluate(b), PublishGateKey.YouthSafety).Passed.Should().BeFalse();

        b.Room.ContentNote = "This story describes a funding lapse and its effects on services.";
        Find(Evaluator().Evaluate(b), PublishGateKey.YouthSafety).Passed.Should().BeTrue();
    }

    [Fact]
    public void YouthSafety_NeverAutoPassesRestrictedContent()
    {
        var b = Complete();
        b.Room.Sensitivity = SensitivityLevel.Restricted;
        b.Room.ContentNote = "A content note is not sufficient here.";

        Find(Evaluator().Evaluate(b), PublishGateKey.YouthSafety).Passed.Should().BeFalse();
    }

    [Fact]
    public void ThreeGates_AlwaysRequireANamedHuman()
    {
        // An automated pass is not editorial judgement. Neutrality, terminology and youth
        // safety cannot be settled by a string check, so the machine result is advisory.
        var findings = Evaluator().Evaluate(Complete());

        findings.Where(f => f.RequiresNamedApproval).Select(f => f.Gate)
            .Should().BeEquivalentTo(new[]
            {
                PublishGateKey.HeadlineNeutrality,
                PublishGateKey.TerminologyReview,
                PublishGateKey.YouthSafety,
            });

        PublishGateEvaluator.RequireNamedApproval.Should().BeEquivalentTo(
            findings.Where(f => f.RequiresNamedApproval).Select(f => f.Gate));
    }

    [Fact]
    public void InteractionAnswerValidation_ReportsHonestlyThatThereIsNothingToCheck()
    {
        // It passes because no interactions are attached yet, and it says so rather than
        // silently reporting a check it did not run.
        var finding = Find(Evaluator().Evaluate(Complete()),
            PublishGateKey.InteractionAnswerValidation);

        finding.Passed.Should().BeTrue();
        finding.Detail.Should().Contain("No interactions");
    }

    [Fact]
    public void GateKeyNames_FitTheirPersistedColumn()
    {
        foreach (var gate in Enum.GetValues<PublishGateKey>())
        {
            gate.ToString().Length.Should().BeLessThanOrEqualTo(40);
        }
    }
}
