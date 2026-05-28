using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class ConceptsControllerTests
{
    private readonly HttpClient _client;

    public ConceptsControllerTests(DatabaseFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task List_ReturnsSeededConcepts()
    {
        var resp = await _client.GetAsync("/api/concepts");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await resp.Content.ReadFromJsonAsync<List<ConceptDto>>();
        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(3);
        items.Select(c => c.Slug).Should().Contain("committee-hearing");
    }

    [Fact]
    public async Task GetBySlug_ReturnsFullConcept()
    {
        var resp = await _client.GetAsync("/api/concepts/committee-hearing");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await resp.Content.ReadFromJsonAsync<ConceptDto>();
        dto.Should().NotBeNull();
        dto!.Title.Should().Be("Committee Hearing");
        dto.WhereYouSeeIt.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetBySlug_UnknownSlug_Returns404()
    {
        var resp = await _client.GetAsync("/api/concepts/nope");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
