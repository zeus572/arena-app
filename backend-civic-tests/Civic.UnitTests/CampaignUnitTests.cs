using Civic.API.Models;
using Civic.API.Services.Campaign;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class FragmentSplitterTests
{
    [Fact]
    public void Split_ClauseBoundaries_ProducesOrderedFragmentsWithAccurateOffsets()
    {
        var body = "When companies harvest student data, that's not innovation. It's surveillance.";
        var frags = FragmentSplitter.Split(body);

        frags.Should().HaveCountGreaterThan(1);
        frags.Select(f => f.Order).Should().BeInAscendingOrder();
        frags.Select(f => f.Order).Should().Equal(Enumerable.Range(0, frags.Count));

        // Every fragment's offsets must point back at its own text in the body.
        foreach (var f in frags)
        {
            body.Substring(f.Start, f.End - f.Start).Should().Be(f.Text);
        }
    }

    [Fact]
    public void Split_NoPunctuation_ReturnsSingleFragment()
    {
        var body = "Vote for a better future";
        var frags = FragmentSplitter.Split(body);
        frags.Should().HaveCount(1);
        frags[0].Text.Should().Be(body);
    }

    [Fact]
    public void Split_Empty_ReturnsEmpty()
    {
        FragmentSplitter.Split("   ").Should().BeEmpty();
    }
}

public class CampaignContentSanitizerTests
{
    [Fact]
    public void Clean_StripsMarkdownAndCollapsesWhitespace()
    {
        var (body, truncated) = CampaignContentSanitizer.Clean("**Bold** claim\n\nwith   spaces");
        body.Should().Be("Bold claim with spaces");
        truncated.Should().BeFalse();
    }

    [Fact]
    public void Clean_OverLimit_TruncatesToBoundaryWithinLimit()
    {
        var raw = string.Join(" ", Enumerable.Repeat("policy", 60)); // way over 160
        var (body, truncated) = CampaignContentSanitizer.Clean(raw);

        truncated.Should().BeTrue();
        body.Length.Should().BeLessThanOrEqualTo(CampaignContentSanitizer.MaxBodyLength);
        body.Should().NotEndWith(" ");
    }

    [Fact]
    public void Clean_PrefersSentenceBoundary()
    {
        var first = "We can fix this together.";
        var raw = first + " " + string.Join(" ", Enumerable.Repeat("extra", 40));
        var (body, truncated) = CampaignContentSanitizer.Clean(raw);

        truncated.Should().BeTrue();
        body.Should().Be(first);
    }

    [Fact]
    public void Clean_CapsEmoji()
    {
        var (body, _) = CampaignContentSanitizer.Clean("Great 🎉 day 🎉 for 🎉 reform 🎉");
        body.Count(ch => ch == '\uD83C').Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void ExceedsLimit_TrueWhenTooLong()
    {
        CampaignContentSanitizer.ExceedsLimit(new string('a', 200)).Should().BeTrue();
        CampaignContentSanitizer.ExceedsLimit("short").Should().BeFalse();
    }
}

public class CandidateSelectionTests
{
    [Fact]
    public void IssueMatchScore_FullOverlap_IsOne()
    {
        CandidateSelection.IssueMatchScore(new[] { "privacy", "education" }, new[] { "privacy" })
            .Should().Be(1.0);
    }

    [Fact]
    public void IssueMatchScore_NoOverlap_IsZero()
    {
        CandidateSelection.IssueMatchScore(new[] { "taxes" }, new[] { "climate" })
            .Should().Be(0.0);
    }

    [Fact]
    public void IssueMatchScore_CaseInsensitivePartial()
    {
        CandidateSelection.IssueMatchScore(new[] { "Student-Privacy" }, new[] { "privacy" })
            .Should().BeGreaterThan(0);
    }
}

public class ToneResolverTests
{
    private static VirtualCandidate Candidate() => new()
    {
        Id = Guid.NewGuid(),
        DefaultTone = CampaignTone.Casual,
        DefaultIntensity = 2,
        IssueTones = new()
        {
            new CandidateIssueTone { Issue = "climate", Tone = CampaignTone.Hopeful, Intensity = 4 },
        },
    };

    [Fact]
    public void Resolve_UsesIssueOverrideWhenMatched()
    {
        var (tone, intensity) = ToneResolver.Resolve(Candidate(), new[] { "climate" }, seed: 1);
        tone.Should().Be(CampaignTone.Hopeful);
        intensity.Should().BeInRange(3, 5);
    }

    [Fact]
    public void Resolve_FallsBackToDefault()
    {
        var (tone, _) = ToneResolver.Resolve(Candidate(), new[] { "taxes" }, seed: 1);
        tone.Should().Be(CampaignTone.Casual);
    }

    [Fact]
    public void Resolve_IsDeterministicForSameSeed()
    {
        var c = Candidate();
        ToneResolver.Resolve(c, new[] { "climate" }, 42)
            .Should().Be(ToneResolver.Resolve(c, new[] { "climate" }, 42));
    }

    [Fact]
    public void Resolve_IntensityStaysInRange()
    {
        var c = new VirtualCandidate { DefaultTone = CampaignTone.Angry, DefaultIntensity = 5 };
        for (var seed = 0; seed < 20; seed++)
        {
            var (_, intensity) = ToneResolver.Resolve(c, Array.Empty<string>(), seed);
            intensity.Should().BeInRange(1, 5);
        }
    }
}

public class CandidateMatchTests
{
    [Fact]
    public void Similarity_Identical_IsOne()
    {
        var v = new Dictionary<string, double> { ["a"] = 0.5, ["b"] = -0.3 };
        CandidateMatch.Similarity(v, v).Should().Be(1.0);
    }

    [Fact]
    public void Similarity_Opposed_IsZero()
    {
        var u = new Dictionary<string, double> { ["a"] = 1.0 };
        var c = new Dictionary<string, double> { ["a"] = -1.0 };
        CandidateMatch.Similarity(u, c).Should().Be(0.0);
    }

    [Fact]
    public void Similarity_NoSharedAxes_IsZero()
    {
        CandidateMatch.Similarity(
            new Dictionary<string, double> { ["a"] = 1 },
            new Dictionary<string, double> { ["b"] = 1 }).Should().Be(0.0);
    }

    [Fact]
    public void AgreesOnAxis_SameSideWithinTolerance()
    {
        CandidateMatch.AgreesOnAxis(0.6, 0.5).Should().BeTrue();
        CandidateMatch.AgreesOnAxis(0.6, -0.5).Should().BeFalse();
        CandidateMatch.AgreesOnAxis(0.05, 0.05).Should().BeFalse(); // no clear lean
    }
}
