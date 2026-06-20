using Civic.API.Services.Coalition.Curriculum;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 3.3 gate: composed leagues span the intended spectrum; scoring rewards
/// breadth over volume; age-banding prevents adult↔minor exposure. Pure — no DB,
/// no LLM.
/// </summary>
public class LeagueCompositionTests
{
    private static readonly string[] Spectrum = { "left", "center", "right" };

    [Fact]
    public void ComposedLeagues_SpanTheSpectrum_AndNeverMixAgeBands()
    {
        // 9 adults + 6 minors, evenly across the three buckets.
        var pool = new List<CircleMemberSpec>();
        var n = 0;
        foreach (var bucket in Spectrum)
        {
            for (var i = 0; i < 3; i++) pool.Add(new CircleMemberSpec($"a{n++}", bucket, AgeBand.Adult));
            for (var i = 0; i < 2; i++) pool.Add(new CircleMemberSpec($"m{n++}", bucket, AgeBand.Minor));
        }

        var leagues = CircleComposer.Compose(pool, Spectrum, circleSize: 3);

        leagues.Should().NotBeEmpty();
        // Age-banding: no league mixes adults and minors.
        leagues.Should().OnlyContain(l => !l.MixesAgeBands, "adults and minors must never share a league");

        // Spectrum span: every full (size-3) league covers all three buckets.
        leagues.Where(l => l.Members.Count == 3)
            .Should().OnlyContain(l => l.Buckets.Count == 3, "a full league should span the whole spectrum");

        // No member is lost or duplicated.
        leagues.SelectMany(l => l.Members).Select(m => m.UserId).Should().BeEquivalentTo(pool.Select(p => p.UserId));
    }

    [Fact]
    public void Scoring_RewardsBreadthOverVolume()
    {
        // Breadth player: signed 2 broad (3-bucket) coalitions, bargained in twice, few acts.
        var breadthPlayer = new PlayerContribution("breadth", CoalitionsSigned: 2,
            TotalBreadthOfSignedCoalitions: 6, MovedCount: 2, RawActs: 8);
        // Volume player: tons of same-corner acts, never helped form a broad coalition.
        var volumePlayer = new PlayerContribution("volume", CoalitionsSigned: 0,
            TotalBreadthOfSignedCoalitions: 0, MovedCount: 0, RawActs: 100);

        BreadthFavoringScoring.Score(breadthPlayer)
            .Should().BeGreaterThan(BreadthFavoringScoring.Score(volumePlayer),
                "cross-cutting breadth must outscore raw volume");
    }

    [Fact]
    public void Standings_AreBreadthFavoring_OnASimulatedCohort()
    {
        var cohort = new[]
        {
            new PlayerContribution("bridgerA", 3, 9, 3, 12),  // broad + bridging
            new PlayerContribution("bridgerB", 2, 6, 2, 10),  // broad
            new PlayerContribution("grinder", 0, 0, 0, 200),  // pure volume
            new PlayerContribution("narrow", 4, 4, 0, 40),    // many narrow (1-bucket) coalitions
        };

        var standings = BreadthFavoringScoring.Standings(cohort);

        standings.First().UserId.Should().Be("bridgerA", "the broadest, bridging player climbs fastest");
        standings.Last().UserId.Should().Be("grinder", "pure volume sinks to the bottom");
        // The narrow-but-numerous player must not outrank a genuinely broad one.
        var rank = standings.Select((r, i) => (r.UserId, i)).ToDictionary(x => x.UserId, x => x.i);
        rank["bridgerB"].Should().BeLessThan(rank["narrow"]);
    }
}
