using System.Net.Http.Json;
using Civic.API.Services.Coalition.Product;
using Civic.ApiTests;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Product-wiring integration test: drives the coalition loop end-to-end over real
/// HTTP against the civic_test DB (persistence + API + state machine), with
/// constructed agents (no LLM). Confirms an agent-only provision self-plays to
/// PASSED and a human+agent provision bridges to PASSED.
/// </summary>
[Collection("Database")]
public class CoalitionApiTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;
    private readonly HttpClient _client;

    public CoalitionApiTests(DatabaseFixture fx)
    {
        _fx = fx;
        _client = fx.Factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _fx.ResetMutableAsync(); // wipes coalition tables, then we reseed the demos
        using var scope = _fx.Factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<CoalitionSeeder>();
        await seeder.SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ProvisionDetailDto> GetDetailAsync(Guid id)
        => (await _client.GetFromJsonAsync<ProvisionDetailDto>($"/api/coalition/provisions/{id}"))!;

    private async Task<ProvisionDetailDto> PostAsync(string url, object? body = null)
    {
        var resp = await _client.PostAsJsonAsync(url, body ?? new { });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ProvisionDetailDto>())!;
    }

    [Fact]
    public async Task AgentOnlyProvision_SelfPlaysToPassed_OverHttp()
    {
        var provisions = await _client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        var ai = provisions!.Single(p => p.Slug == "ai-hiring-disclosure-demo");
        ai.State.Should().Be("Open");

        ProvisionDetailDto detail = await GetDetailAsync(ai.Id);
        for (var step = 0; step < 12 && detail.State != "Passed"; step++)
            detail = await PostAsync($"/api/coalition/provisions/{ai.Id}/agent-step");

        detail.State.Should().Be("Passed", "the three bridgeable agent corners should reach a coalition");
        detail.Outcome!.PlankVersionId.Should().NotBeNull();
        detail.SpectrumBar.CoveredBuckets.Should().Be(3, "the coalition spans the full composed spectrum");
        detail.SpectrumBar.Distance.Should().Be(0.0);
    }

    [Fact]
    public async Task HumanPlusAgentProvision_BridgesToPassed_OverHttp()
    {
        var provisions = await _client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        var dc = provisions!.Single(p => p.Slug == "data-center-grid-fee-demo");

        // Human joins the open (left) corner and takes a position.
        await PostAsync($"/api/coalition/provisions/{dc.Id}/join", new { bucket = "left" });
        var detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/positions",
            new { stance = "for, but only with a carve-out", intensity = "Medium", bucket = "left" });

        // Let the agent corner engage + table the grandfather carve-out.
        for (var step = 0; step < 6 && !detail.Versions.Any(v => HasGfExempt(v)); step++)
            detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/agent-step");

        var bridge = detail.Versions.First(HasGfExempt);

        // Human co-signs the carve-out; then let the agent co-sign too.
        detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/acceptances",
            new { versionId = bridge.Id, accept = true, intensity = "Medium" });
        for (var step = 0; step < 6 && detail.State != "Passed"; step++)
            detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/agent-step");

        detail.State.Should().Be("Passed", "the human + agent should bridge via the grandfather carve-out");
        detail.SpectrumBar.CoveredBuckets.Should().Be(2);
    }

    private static bool HasGfExempt(VersionDto v) =>
        v.Positions.TryGetValue("gf", out var label) && string.Equals(label, "exempt", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task Leagues_ShowBreadthFavoringStandings_AfterAnAgentCoalition()
    {
        var provisions = await _client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        var ai = provisions!.Single(p => p.Slug == "ai-hiring-disclosure-demo");
        // provision summaries carry the difficulty/gap badges now
        ai.Difficulty.Should().NotBeNullOrEmpty();
        ai.GapWidth.Should().BeGreaterThanOrEqualTo(0);

        var detail = await GetDetailAsync(ai.Id);
        for (var step = 0; step < 12 && detail.State != "Passed"; step++)
            detail = await PostAsync($"/api/coalition/provisions/{ai.Id}/agent-step");
        detail.State.Should().Be("Passed");

        var leagues = await _client.GetFromJsonAsync<List<LeagueDto>>("/api/coalition/leagues");
        leagues.Should().NotBeNullOrEmpty();
        var rows = leagues!.SelectMany(l => l.Standings).ToList();
        rows.Should().Contain(r => r.IsAgent && r.CoalitionsSigned >= 1 && r.TotalBreadth >= 3,
            "agents who signed the cross-spectrum coalition should appear in the breadth-favoring standings");
        // breadth-favoring: a signer of a broad coalition outscores a non-signer.
        var signer = rows.First(r => r.CoalitionsSigned >= 1);
        signer.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Me_ReflectsSignedPlank_AndLeaguePlacement()
    {
        var provisions = await _client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        var dc = provisions!.Single(p => p.Slug == "data-center-grid-fee-demo");

        await PostAsync($"/api/coalition/provisions/{dc.Id}/join", new { bucket = "left" });
        var detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/positions",
            new { stance = "carve-out please", intensity = "Medium", bucket = "left" });
        for (var step = 0; step < 6 && !detail.Versions.Any(HasGfExempt); step++)
            detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/agent-step");
        var bridge = detail.Versions.First(HasGfExempt);
        detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/acceptances",
            new { versionId = bridge.Id, accept = true, intensity = "Medium" });
        for (var step = 0; step < 6 && detail.State != "Passed"; step++)
            detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/agent-step");
        detail.State.Should().Be("Passed");

        var me = await _client.GetFromJsonAsync<MeDto>("/api/coalition/me");
        me.Should().NotBeNull();
        me!.Record.PlanksPassed.Should().BeGreaterThanOrEqualTo(1, "the player co-signed a passed plank");
        me.Record.TotalBreadth.Should().BeGreaterThanOrEqualTo(2);
        me.Cadence.Score.Should().BeGreaterThan(0, "the player was active today");
        me.Cadence.Last7Days.Should().HaveCount(7);
        me.LeagueId.Should().NotBeNullOrEmpty("the player is auto-placed in a league");
        me.Recommended.Should().NotBeNull();
        me.SkillLabel.Should().NotBeNullOrEmpty();
    }
}
