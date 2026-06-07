using Civic.API.Services.Coalition.Judges;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// The coalition LLM judges: when the LLM is available (StubLlmClient) they return
/// its verdict; when it is not (KeylessLlmClient throws LlmException, as in dev) they
/// degrade gracefully to structural/heuristic fallbacks without throwing. Pure unit
/// tests — no DB.
/// </summary>
public class CoalitionJudgeTests
{
    private static CoalitionJudge WithLlm(StubLlmClient llm) => new(llm, NullLogger<CoalitionJudge>.Instance);
    private static CoalitionJudge Keyless() => new(new KeylessLlmClient(), NullLogger<CoalitionJudge>.Instance);

    [Fact]
    public async Task Governance_UsesLlm_WhenAvailable()
    {
        var llm = new StubLlmClient().WithJson<GovernanceScoreDto>(
            "{\"governance\":82,\"reasoningQuality\":71,\"layer\":\"governance\"}");
        var r = await WithLlm(llm).ScoreContributionAsync("Charge marginal grid cost above 50MW.", new[] { "market-vs-regulation" });
        r.FromLlm.Should().BeTrue();
        r.Governance.Should().Be(82);
        r.IsGovernanceLayer.Should().BeTrue();
    }

    [Fact]
    public async Task Governance_FallsBack_WhenNoKey()
    {
        var r = await Keyless().ScoreContributionAsync(
            "The agency should regulate the marginal grid cost with a clear threshold.",
            new[] { "market-vs-regulation" });
        r.FromLlm.Should().BeFalse();
        r.IsGovernanceLayer.Should().BeTrue("governance axes/markers dominate");
        r.Governance.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task CommonGround_Llm_And_Fallback()
    {
        var llm = new StubLlmClient().WithJson<CommonGroundDto>(
            "{\"isGenuine\":true,\"concrete\":true,\"costly\":true,\"crossCutting\":true,\"reason\":\"names a specific carve-out\"}");
        var withLlm = await WithLlm(llm).JudgeCommonGroundAsync("We agree on a 50MW threshold with a grandfather carve-out.");
        withLlm.FromLlm.Should().BeTrue();
        withLlm.CrossCutting.Should().BeTrue();

        // Fallback: a concrete, concession-bearing statement reads as genuine; a platitude does not.
        var concrete = await Keyless().JudgeCommonGroundAsync("Apply a 50MW threshold with a carve-out for existing plants.");
        concrete.FromLlm.Should().BeFalse();
        concrete.Concrete.Should().BeTrue();
        concrete.IsGenuine.Should().BeTrue();

        var platitude = await Keyless().JudgeCommonGroundAsync("We all love our country.");
        platitude.IsGenuine.Should().BeFalse("a vibes platitude is not concrete common ground");
    }

    [Fact]
    public async Task Substantive_Llm_And_StructuralFallback()
    {
        var llm = new StubLlmClient().WithJson<AmendmentSubstantiveDto>("{\"substantive\":false,\"reason\":\"reworded\"}");
        (await WithLlm(llm).IsAmendmentSubstantiveAsync("a", "b", vectorChanged: true)).Should().BeFalse("LLM overrides");

        // No key -> structural: trust the vector-changed signal.
        (await Keyless().IsAmendmentSubstantiveAsync("a", "b", vectorChanged: true)).Should().BeTrue();
        (await Keyless().IsAmendmentSubstantiveAsync("a", "b", vectorChanged: false)).Should().BeFalse();
    }

    [Fact]
    public async Task Teeth_Llm_And_SpecificityFallback()
    {
        var llm = new StubLlmClient().WithJson<TeethDto>("{\"hasTeeth\":false,\"reason\":\"vague\"}");
        (await WithLlm(llm).HasTeethAsync("plank", specificity: 3)).Should().BeFalse("LLM overrides");

        (await Keyless().HasTeethAsync("plank", specificity: 2)).Should().BeTrue();
        (await Keyless().HasTeethAsync("plank", specificity: 0)).Should().BeFalse();
    }

    [Fact]
    public async Task Steelman_Llm_And_OverlapFallback()
    {
        var llm = new StubLlmClient().WithJson<SteelmanDto>("{\"proponentWouldEndorse\":true,\"quality\":80,\"reason\":\"fair\"}");
        var r = await WithLlm(llm).JudgeSteelmanAsync("Data centers should pay marginal grid cost.", "Operators paying marginal cost is fair because they impose that load.");
        r.FromLlm.Should().BeTrue();
        r.Quality.Should().Be(80);

        // Fallback: strong content overlap -> endorsed; unrelated -> not.
        var good = await Keyless().JudgeSteelmanAsync(
            "Data centers should pay the marginal grid cost they impose.",
            "Charging data centers the marginal grid cost they impose is reasonable.");
        good.FromLlm.Should().BeFalse();
        good.ProponentWouldEndorse.Should().BeTrue();

        var bad = await Keyless().JudgeSteelmanAsync(
            "Data centers should pay the marginal grid cost they impose.",
            "Pineapple belongs on pizza.");
        bad.ProponentWouldEndorse.Should().BeFalse();
    }
}
