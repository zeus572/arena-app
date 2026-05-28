using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class BriefingsControllerTests
{
    private readonly DatabaseFixture _fixture;
    private readonly HttpClient _client;

    public BriefingsControllerTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task List_ReturnsSeededBriefings()
    {
        var resp = await _client.GetAsync("/api/briefings");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await resp.Content.ReadFromJsonAsync<List<BriefingSummaryDto>>();
        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(4);
        items.Select(b => b.Slug).Should().Contain("congress-student-data-privacy-bill");
        items[0].Headline.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetBySlug_ReturnsFullBriefing_WithWordsToKnow()
    {
        var resp = await _client.GetAsync("/api/briefings/congress-student-data-privacy-bill");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await resp.Content.ReadFromJsonAsync<BriefingDto>();
        dto.Should().NotBeNull();
        dto!.Headline.Should().Contain("Student Data Privacy");
        dto.WordsToKnow.Should().HaveCountGreaterOrEqualTo(3);
        dto.WordsToKnow.Should().Contain(w => w.Term == "Bill");
        dto.ValuesInConflict.Should().Contain("Privacy");
        dto.ThinkDeeperQuestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetBySlug_UnknownSlug_Returns404()
    {
        var resp = await _client.GetAsync("/api/briefings/does-not-exist");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
