using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class ElectionsControllerTests
{
    private readonly HttpClient _client;

    public ElectionsControllerTests(DatabaseFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task List_ReturnsUpcomingSeededElections_SoonestFirst()
    {
        var resp = await _client.GetAsync("/api/elections");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await resp.Content.ReadFromJsonAsync<List<ElectionDto>>();
        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(2);
        items.Should().BeInAscendingOrder(e => e.ScheduledAt);
        items.Select(e => e.Slug).Should().Contain("us-general-2026");
        items.Should().OnlyContain(e => e.ScheduledAt >= DateTime.UtcNow);
    }

    [Fact]
    public async Task List_ScopeFilter_AcceptsKebabAndSnakeAndCamel()
    {
        foreach (var raw in new[] { "national", "National", "NATIONAL" })
        {
            var resp = await _client.GetAsync($"/api/elections?scope={raw}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = await resp.Content.ReadFromJsonAsync<List<ElectionDto>>();
            items.Should().NotBeNull();
            items!.Should().OnlyContain(e => e.Scope == "National");
        }
    }

    [Fact]
    public async Task List_UnknownScope_Returns400()
    {
        var resp = await _client.GetAsync("/api/elections?scope=galactic");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Next_NoFilter_ReturnsSoonestUpcoming()
    {
        var resp = await _client.GetAsync("/api/elections/next");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<ElectionDto>();
        dto.Should().NotBeNull();
        dto!.ScheduledAt.Should().BeAfter(DateTime.UtcNow);
        dto.Name.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Next_NationalScope_ReturnsNationalElection()
    {
        var resp = await _client.GetAsync("/api/elections/next?scope=national");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<ElectionDto>();
        dto.Should().NotBeNull();
        dto!.Scope.Should().Be("National");
        dto.Region.Should().BeNull();
    }

    [Fact]
    public async Task Next_RegionWithoutMatch_Returns404()
    {
        var resp = await _client.GetAsync("/api/elections/next?scope=local&region=zzz-not-real");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
