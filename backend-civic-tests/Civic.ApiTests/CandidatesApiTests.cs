using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class CandidatesApiTests
{
    private readonly DatabaseFixture _fixture;

    public CandidatesApiTests(DatabaseFixture fixture) => _fixture = fixture;

    private HttpClient Client() => _fixture.Factory.CreateClient();

    [Fact]
    public async Task List_ReturnsSeededPresidentialCandidates()
    {
        var items = await Client().GetFromJsonAsync<List<CandidateSummaryDto>>("/api/candidates");
        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(5);
        items.Should().OnlyContain(c => c.IsFictional);
        items.Should().Contain(c => c.Slug == "dana-okonkwo");
    }

    [Fact]
    public async Task List_FilterByOffice_OnlyPresident()
    {
        var items = await Client().GetFromJsonAsync<List<CandidateSummaryDto>>("/api/candidates?office=President");
        items!.Should().OnlyContain(c => c.Office == "President");
    }

    [Fact]
    public async Task List_UnknownOffice_Returns400()
    {
        var resp = await Client().GetAsync("/api/candidates?office=Emperor");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBySlug_ReturnsDetailWithPlanksAndValues()
    {
        var c = await Client().GetFromJsonAsync<CandidateDetailDto>("/api/candidates/sofia-alvarez");
        c.Should().NotBeNull();
        c!.Name.Should().Be("Sofia Alvarez");
        c.IsFictional.Should().BeTrue();
        c.PlatformPlanks.Should().HaveCountGreaterOrEqualTo(4);
        c.Values.Should().HaveCount(15); // all catalog axes are projected
        c.Values.Should().Contain(v => v.AxisKey == "time-horizon" && v.Score > 0);
    }

    [Fact]
    public async Task GetBySlug_Unknown_Returns404()
    {
        var resp = await Client().GetAsync("/api/candidates/nobody");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Values_ReturnsAllAxes()
    {
        var values = await Client().GetFromJsonAsync<List<CandidateValueDto>>("/api/candidates/marcus-reed/values");
        values!.Should().HaveCount(15);
        values.Should().Contain(v => v.AxisKey == "govt-role");
    }

    [Fact]
    public async Task Platform_ReturnsPlanks()
    {
        var planks = await Client().GetFromJsonAsync<List<PlatformPlankDto>>("/api/candidates/hank-whitfield/platform");
        planks!.Should().NotBeEmpty();
        planks.Should().OnlyContain(p => p.IssueTags.Length > 0);
    }

    [Fact]
    public async Task Sources_OrderedByPriority()
    {
        var sources = await Client().GetFromJsonAsync<List<CandidateSourceDto>>("/api/candidates/patricia-vance/sources");
        sources!.Should().NotBeEmpty();
        sources.Select(s => s.Priority).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task FollowAndMute_AreIdempotentAndReversible()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());

        (await client.PostAsync("/api/candidates/dana-okonkwo/follow", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        // Following again is a no-op (no unique violation surfaced to the client).
        (await client.PostAsync("/api/candidates/dana-okonkwo/follow", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync("/api/candidates/dana-okonkwo/follow")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await client.PostAsync("/api/candidates/dana-okonkwo/mute", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync("/api/candidates/dana-okonkwo/mute")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Follow_UnknownCandidate_Returns404()
    {
        var resp = await Client().PostAsync("/api/candidates/nobody/follow", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ElectionCycle_Current_Is2028General()
    {
        var cycle = await Client().GetFromJsonAsync<ElectionCycleDto>("/api/election/cycles/current");
        cycle.Should().NotBeNull();
        cycle!.Slug.Should().Be("2028-general");
        cycle.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task ElectionRaces_GroupCandidatesByOffice()
    {
        var races = await Client().GetFromJsonAsync<List<RaceDto>>("/api/election/races?office=President");
        races.Should().NotBeNull();
        var presidential = races!.Single(r => r.Office == "President");
        presidential.Candidates.Should().HaveCountGreaterOrEqualTo(5);
    }

    [Fact]
    public async Task CandidateMatches_NoProfile_HasProfileFalse()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());

        var matches = await client.GetFromJsonAsync<CandidateMatchesDto>("/api/me/candidate-matches");
        matches!.HasProfile.Should().BeFalse();
        matches.TopMatches.Should().BeEmpty();
    }
}
