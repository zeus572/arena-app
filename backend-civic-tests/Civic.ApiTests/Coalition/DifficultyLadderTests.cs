using Civic.API.Services.Coalition.Curriculum;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// Phase 3.2 gate: served gap width tracks group skill on simulated league
/// histories — a new league gets near-overlapping (narrow-gap) provisions; a
/// veteran league gets wider gaps. Pure — no DB, no LLM.
/// </summary>
public class DifficultyLadderTests
{
    // A fixed curriculum pool spanning narrow → wide gaps.
    private static readonly double[] Pool = { 0.1, 0.3, 0.5, 0.7, 0.9 };

    private static LeagueHistory History(int attempts, int closed, double maxClosedGap)
    {
        var outcomes = new List<LeagueOutcome>();
        for (var i = 0; i < closed; i++) outcomes.Add(new LeagueOutcome(maxClosedGap, true));
        for (var i = 0; i < attempts - closed; i++) outcomes.Add(new LeagueOutcome(maxClosedGap, false));
        return new LeagueHistory(outcomes);
    }

    [Fact]
    public void NewLeague_GetsNarrowGap_VeteranLeague_GetsWiderGap()
    {
        var newLeague = new LeagueHistory(Array.Empty<LeagueOutcome>());
        var veteran = History(attempts: 20, closed: 18, maxClosedGap: 0.9);

        var newSkill = GroupSkill.Estimate(newLeague);
        var vetSkill = GroupSkill.Estimate(veteran);
        newSkill.Should().Be(0.0);
        vetSkill.Should().BeGreaterThan(newSkill);

        var newGap = DifficultyLadder.ServedGap(newSkill, Pool);
        var vetGap = DifficultyLadder.ServedGap(vetSkill, Pool);

        newGap.Should().Be(0.1, "a brand-new league should get near-overlapping acceptance sets");
        vetGap.Should().BeGreaterThan(newGap, "a veteran league should be served wider gaps");
    }

    [Fact]
    public void ServedGapWidth_IsMonotoneInGroupSkill_OnSimulatedHistories()
    {
        // Simulated leagues of increasing track record.
        var histories = new[]
        {
            new LeagueHistory(Array.Empty<LeagueOutcome>()),  // new
            History(10, 3, 0.2),                              // weak
            History(10, 6, 0.45),                             // improving
            History(10, 8, 0.7),                              // strong
            History(20, 19, 0.95),                            // veteran
        };

        var servedGaps = histories
            .Select(h => DifficultyLadder.ServedGap(GroupSkill.Estimate(h), Pool))
            .ToList();

        for (var i = 1; i < servedGaps.Count; i++)
            servedGaps[i].Should().BeGreaterThanOrEqualTo(servedGaps[i - 1],
                "served gap width must not decrease as group skill grows");

        servedGaps.First().Should().BeLessThan(servedGaps.Last(), "the ladder widens overall");
    }

    [Fact]
    public void Serve_PicksClosestGapToSkillTarget()
    {
        // skill 0.5 -> target 0.5 -> the 0.5 provision is served.
        DifficultyLadder.ServedGap(0.5, Pool).Should().Be(0.5);
        // skill 0.65 -> nearest is 0.7.
        DifficultyLadder.ServedGap(0.65, Pool).Should().Be(0.7);
    }
}
