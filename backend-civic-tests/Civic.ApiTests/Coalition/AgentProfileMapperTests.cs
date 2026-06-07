using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// IAgentProfileMapper: uses the LLM when available, else a heuristic lean→option
/// mapping so self-play works in dev. Pure unit tests — no DB.
/// </summary>
public class AgentProfileMapperTests
{
    private static SubQuestion SQ(string key, params string[] opts) =>
        new() { Key = key, Prompt = key + "?", PositionOptions = opts };

    private static readonly SubQuestion[] Sqs = { SQ("scope", "large-only", "all"), SQ("gf", "exempt", "none") };

    [Fact]
    public async Task UsesLlm_WhenAvailable()
    {
        var llm = new StubLlmClient().WithJson<AgentProfileDto>(
            "{\"positions\":{\"scope\":[\"large-only\"],\"gf\":[\"exempt\"]},\"intensities\":{\"scope\":\"High\",\"gf\":\"NonNegotiable\"}}");
        var mapper = new AgentProfileMapper(llm, NullLogger<AgentProfileMapper>.Instance);

        var profile = await mapper.DeriveAsync(new[] { new AgentAxisScore("market-vs-regulation", 0.6) }, Sqs);

        profile.FromLlm.Should().BeTrue();
        profile.Region["scope"].Should().Equal("large-only");
        profile.Intensities["gf"].Should().Be("NonNegotiable");
    }

    [Fact]
    public async Task Heuristic_RightLean_PicksLastOption_HighIntensity()
    {
        var mapper = new AgentProfileMapper(new KeylessLlmClient(), NullLogger<AgentProfileMapper>.Instance);
        var profile = await mapper.DeriveAsync(new[] { new AgentAxisScore("market-vs-regulation", 0.8) }, Sqs);

        profile.FromLlm.Should().BeFalse();
        profile.Region["scope"].Should().Equal("all");   // last option for a strong right lean
        profile.Region["gf"].Should().Equal("none");
        profile.Intensities["scope"].Should().Be("NonNegotiable"); // |lean| >= 0.7
    }

    [Fact]
    public async Task Heuristic_LeftLean_PicksFirstOption()
    {
        var mapper = new AgentProfileMapper(new KeylessLlmClient(), NullLogger<AgentProfileMapper>.Instance);
        var profile = await mapper.DeriveAsync(new[] { new AgentAxisScore("market-vs-regulation", -0.5) }, Sqs);
        profile.Region["scope"].Should().Equal("large-only"); // first option for a left lean
        profile.Intensities["scope"].Should().Be("High");     // |lean| in [0.4, 0.7)
    }

    [Fact]
    public async Task Heuristic_Moderate_AcceptsBothOptions()
    {
        var mapper = new AgentProfileMapper(new KeylessLlmClient(), NullLogger<AgentProfileMapper>.Instance);
        var profile = await mapper.DeriveAsync(new[] { new AgentAxisScore("market-vs-regulation", 0.05) }, Sqs);
        profile.Region["scope"].Should().BeEquivalentTo(new[] { "large-only", "all" }); // open to all
    }
}
