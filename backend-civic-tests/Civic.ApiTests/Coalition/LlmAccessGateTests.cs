using System.Security.Claims;
using Civic.API.Services.Coalition;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Judges;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Civic.ApiTests.Coalition;

/// <summary>
/// The pre-prod safety gate: only authenticated PREMIUM requests may trigger a coalition
/// LLM call; anonymous/free requests get the heuristic fallback (no LLM). Verifies the
/// policy itself and that every LLM seam consults it. Pure unit tests — no DB.
/// </summary>
public class LlmAccessGateTests
{
    private static IHttpContextAccessor Accessor(ClaimsPrincipal user) =>
        new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } };

    private static ClaimsPrincipal Premium() =>
        new(new ClaimsIdentity(new[] { new Claim("plan", "Premium") }, authenticationType: "jwt"));
    private static ClaimsPrincipal Free() =>
        new(new ClaimsIdentity(new[] { new Claim("plan", "Free") }, authenticationType: "jwt"));
    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity()); // not authenticated

    [Fact]
    public void Policy_AllowsPremium_DeniesFreeAndAnonymous()
    {
        new PremiumLlmAccessPolicy(Accessor(Premium())).CanUseLlm().Should().BeTrue();
        new PremiumLlmAccessPolicy(Accessor(Free())).CanUseLlm().Should().BeFalse();
        new PremiumLlmAccessPolicy(Accessor(Anonymous())).CanUseLlm().Should().BeFalse();
        // No request context = trusted background/system caller (scheduler) -> allowed.
        new PremiumLlmAccessPolicy(new HttpContextAccessor { HttpContext = null }).CanUseLlm().Should().BeTrue();
    }

    [Fact]
    public async Task Judge_Gated_UsesFallback_AndNeverCallsLlm_ForNonPremium()
    {
        var stub = new StubLlmClient().WithJson<GovernanceScoreDto>("{\"governance\":99,\"reasoningQuality\":99,\"layer\":\"governance\"}");
        var judge = new CoalitionJudge(stub, NullLogger<CoalitionJudge>.Instance, new DenyLlmPolicy());

        var r = await judge.ScoreContributionAsync("Regulate the marginal grid cost.", new[] { "market-vs-regulation" });

        r.FromLlm.Should().BeFalse("a non-premium request must not reach the LLM");
        stub.Calls.Should().BeEmpty("the gate prevents the LLM call entirely");
    }

    [Fact]
    public async Task AgentMapper_Gated_UsesHeuristic_ForNonPremium()
    {
        var stub = new StubLlmClient().WithJson<AgentProfileDto>("{\"positions\":{\"scope\":[\"all\"]},\"intensities\":{}}");
        var mapper = new AgentProfileMapper(stub, NullLogger<AgentProfileMapper>.Instance, new DenyLlmPolicy());
        var sqs = new[] { new Civic.API.Models.SubQuestion { Key = "scope", PositionOptions = new[] { "large-only", "all" } } };

        var profile = await mapper.DeriveAsync(new[] { new AgentAxisScore("x", 0.8) }, sqs);

        profile.FromLlm.Should().BeFalse();
        stub.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task TwoFramings_Gated_UsesFallback_ForNonPremium()
    {
        var stub = new StubLlmClient().WithJson<TwoFramingsDto>("{\"culturalFrame\":\"x\",\"governanceFrame\":\"y\"}");
        var svc = new TwoFramingsService(stub, NullLogger<TwoFramingsService>.Instance, new DenyLlmPolicy());
        var r = await svc.ForAsync("Title", "Governance proposition.", new[] { "axis" }, "the disagreement", new[] { "A", "B" });
        r.FromLlm.Should().BeFalse();
        r.GovernanceFrame.Should().Contain("Governance proposition");
        stub.Calls.Should().BeEmpty();
    }
}
