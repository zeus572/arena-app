using System.Net.Http.Headers;
using System.Net.Http.Json;
using Civic.API.Services.Coalition.Product;
using Civic.ApiTests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        // Coalition acts now require a verified, signed-in user; authenticate as one.
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.MintAccessToken(Guid.NewGuid()));
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
    public async Task BirthFromBriefing_CreatesPlayableProvision()
    {
        Guid briefingId;
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Civic.API.Data.CivicDbContext>();
            // Birth from a NATIONAL briefing — a local one would inherit its Locality and
            // be invisible to the anonymous test user (locality hard-wall → 404).
            briefingId = (await db.Briefings.Where(b => b.Locality == null)
                .OrderBy(b => b.IssueOrder).FirstAsync()).Id;
        }

        var detail = await PostAsync("/api/coalition/birth", new { briefingId });

        detail.SubQuestions.Should().NotBeEmpty("a born provision has sub-questions to position on");
        detail.Versions.Should().Contain(v => v.Label == "As proposed", "birth seeds a base 'As proposed' version");
        detail.Participants.Should().Contain(p => p.IsAgent, "an agent counterpart is seeded so it's engageable");
        detail.Difficulty.Should().NotBeNullOrEmpty();
        new[] { "Open", "Contested" }.Should().Contain(detail.State);
    }

    [Fact]
    public async Task FreeformAmendment_ExtractsPositions_FromNaturalLanguage()
    {
        // No API key in tests -> extraction falls back to heuristic option-matching, which
        // still picks up the "exempt" / "large-only" labels in the player's prose.
        var provisions = await _client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        var dc = provisions!.Single(p => p.Slug == "data-center-grid-fee-demo");

        await PostAsync($"/api/coalition/provisions/{dc.Id}/join", new { bucket = "left" });
        await PostAsync($"/api/coalition/provisions/{dc.Id}/positions", new { stance = "for with carve-out", intensity = "Medium", bucket = "left" });

        var detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/amendments/freeform",
            new { text = "I'd sign it if existing facilities are exempt and it stays large-only." });

        var v = detail.Versions.First(HasGfExempt);
        v.Positions["gf"].Should().Be("exempt");
        v.Positions["scope"].Should().Be("large-only");
        // Note: a freeform amendment whose extracted vector matches an existing version
        // reuses that version (dedup by position-vector), so v.Text is the matched
        // version's wording, not necessarily the player's prose — the extraction of
        // positions from natural language is what this test verifies.
    }

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

        var circles = await _client.GetFromJsonAsync<List<CircleDto>>("/api/coalition/circles");
        circles.Should().NotBeNullOrEmpty();
        var rows = circles!.SelectMany(l => l.Standings).ToList();
        rows.Should().Contain(r => r.IsAgent && r.CoalitionsSigned >= 1 && r.TotalBreadth >= 3,
            "agents who signed the cross-spectrum coalition should appear in the breadth-favoring standings");
        // breadth-favoring: a signer of a broad coalition outscores a non-signer.
        var signer = rows.First(r => r.CoalitionsSigned >= 1);
        signer.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PointsEconomy_AwardsReasoningXp_AndScarce_OnPlaythrough()
    {
        var provisions = await _client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        var dc = provisions!.Single(p => p.Slug == "data-center-grid-fee-demo");

        await PostAsync($"/api/coalition/provisions/{dc.Id}/join", new { bucket = "left" });
        var detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/positions", new { stance = "for with carve-out", intensity = "Medium", bucket = "left" });
        // Decline the base first (bargain in), then propose + co-sign the carve-out -> the player moved.
        var baseV = detail.Versions.First(v => !HasGfExempt(v));
        await PostAsync($"/api/coalition/provisions/{dc.Id}/acceptances", new { versionId = baseV.Id, accept = false, intensity = "Medium" });
        detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/amendments/freeform",
            new { text = "I'd sign if existing facilities are exempt and it stays large-only." });
        var bridge = detail.Versions.First(HasGfExempt);
        detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/acceptances", new { versionId = bridge.Id, accept = true, intensity = "Medium" });
        for (var step = 0; step < 6 && detail.State != "Passed"; step++)
            detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/agent-step");
        detail.State.Should().Be("Passed");

        // A daily reaction-with-reason act earns reasoning XP.
        var act = await _client.PostAsJsonAsync($"/api/coalition/provisions/{dc.Id}/acts",
            new { type = "ReactionWithReason", payload = "Workable: the carve-out addresses the marginal-cost problem." });
        act.EnsureSuccessStatusCode();
        var actResult = (await act.Content.ReadFromJsonAsync<ActResultDto>())!;
        actResult.Points.Should().BeGreaterThan(0);
        actResult.Currency.Should().Be("reasoning");

        var me = await _client.GetFromJsonAsync<MeDto>("/api/coalition/me");
        me!.ReasoningXp.Should().BeGreaterThan(0, "position + amend + co-sign + reaction earn reasoning XP");
        me.ScarcePoints.Should().BeGreaterThan(0, "signing a passed coalition pays the scarce premium currency");
        me.TodayReasoning.Should().BeLessThanOrEqualTo(me.DailyReasoningCap, "the daily reasoning cap holds");
        me.Record.PlanksPassed.Should().BeGreaterThanOrEqualTo(1);
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
        me.CircleId.Should().NotBeNullOrEmpty("the player is auto-placed in a circle");
        me.Recommended.Should().NotBeNull();
        me.SkillLabel.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SpectrumBar_HasDirectionalCallToAction_AndUncoveredCorners()
    {
        var provisions = await _client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        var dc = provisions!.Single(p => p.Slug == "data-center-grid-fee-demo");

        // The demo seeds a single agent corner (right) that needs the grandfather carve-out
        // (gf=exempt). Join an opposing left corner and co-sign a no-carve-out version
        // (gf=none) — a region only widens on co-signs, not declines (AcceptanceSetDeriver) —
        // so the two corners genuinely disagree and at least one stays dark pre-coalition.
        await PostAsync($"/api/coalition/provisions/{dc.Id}/join", new { bucket = "left" });
        var seeded = await GetDetailAsync(dc.Id);
        var gfNone = seeded.Versions.First(v => v.Positions.TryGetValue("gf", out var g) && g == "none");
        var detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/acceptances",
            new { versionId = gfNone.Id, accept = true, intensity = "Medium" });

        detail.SpectrumBar.CallToAction.Should().NotBeNullOrEmpty();
        detail.SpectrumBar.DaysLeft.Should().NotBeNull();
        detail.SpectrumBar.UncoveredBuckets.Should().NotBeEmpty("at least one corner is dark before a coalition forms");
    }

    [Fact]
    public async Task Probes_SurfaceVariantsForTheCurrentPlayer()
    {
        var provisions = await _client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        var dc = provisions!.Single(p => p.Slug == "data-center-grid-fee-demo");

        await PostAsync($"/api/coalition/provisions/{dc.Id}/join", new { bucket = "left" });
        var detail = await GetDetailAsync(dc.Id);
        for (var step = 0; step < 6 && !detail.Versions.Any(HasGfExempt); step++)
            detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/agent-step");

        detail = await GetDetailAsync(dc.Id);
        detail.Probes.Should().NotBeEmpty("the player should be offered variants to co-sign");
        detail.Probes.Should().Contain(pr => pr.Prompt.Contains("co-sign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DiedProvision_LeavesNoBridgeArtifact()
    {
        var id = Guid.NewGuid();
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Civic.API.Data.CivicDbContext>();
            db.Provisions.Add(new Civic.API.Models.Provision
            {
                Id = id, Slug = $"died-{id:N}", Title = "Past-deadline provision",
                NeutralText = "x", State = Civic.API.Models.ProvisionState.Open,
                Deadline = DateTime.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        var detail = await PostAsync($"/api/coalition/provisions/{id}/positions",
            new { stance = "for", intensity = "Medium", bucket = "left" });

        detail.State.Should().Be("Died");
        detail.Outcome!.DiedReason.Should().Contain("No bridge");
    }

    [Fact]
    public async Task CoSignAndAmend_RecordActs_AttributedToTheVersion()
    {
        var provisions = await _client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        var dc = provisions!.Single(p => p.Slug == "data-center-grid-fee-demo");

        await PostAsync($"/api/coalition/provisions/{dc.Id}/join", new { bucket = "left" });
        await PostAsync($"/api/coalition/provisions/{dc.Id}/positions",
            new { stance = "for with carve-out", intensity = "Medium", bucket = "left" });

        // A freeform amendment creates a new version; the Amend act must point at it.
        var detail = await PostAsync($"/api/coalition/provisions/{dc.Id}/amendments/freeform",
            new { text = "I'd sign it if existing facilities are exempt and it stays large-only." });
        var bridge = detail.Versions.First(HasGfExempt);

        // Co-signing that version must record a CoSign act attributed to the same version.
        await PostAsync($"/api/coalition/provisions/{dc.Id}/acceptances",
            new { versionId = bridge.Id, accept = true, intensity = "Medium" });

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Civic.API.Data.CivicDbContext>();
        var acts = await db.CoalitionActs.Where(a => a.ProvisionId == dc.Id).ToListAsync();
        var versionIds = detail.Versions.Select(v => v.Id).ToHashSet();

        acts.Should().Contain(
            a => a.Type == Civic.API.Models.CoalitionActType.Amend && a.VersionId != null && versionIds.Contains(a.VersionId.Value),
            "a freeform amendment records an Amend act attributed to the version it created");
        acts.Should().Contain(
            a => a.Type == Civic.API.Models.CoalitionActType.CoSign && a.VersionId == bridge.Id,
            "co-signing a version records a CoSign act attributed to that exact version");
    }
}
