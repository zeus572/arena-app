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
}
