using System.Net;
using System.Net.Http.Json;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        var pageResult = await resp.Content.ReadFromJsonAsync<BriefingPageDto>();
        pageResult.Should().NotBeNull();
        pageResult!.Total.Should().BeGreaterOrEqualTo(4);
        var items = pageResult.Items;
        items.Should().HaveCountGreaterOrEqualTo(4);
        items.Select(b => b.Slug).Should().Contain("congress-student-data-privacy-bill");
        items[0].Headline.Should().NotBeNullOrWhiteSpace();
        items[0].CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task List_RespectsPageSize_AndReportsTotal()
    {
        var resp = await _client.GetAsync("/api/briefings?page=1&pageSize=2");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var pageResult = await resp.Content.ReadFromJsonAsync<BriefingPageDto>();
        pageResult.Should().NotBeNull();
        pageResult!.Items.Should().HaveCount(2);
        pageResult.Page.Should().Be(1);
        pageResult.PageSize.Should().Be(2);
        pageResult.Total.Should().BeGreaterThan(2, "the seeded set has more than one page at pageSize=2");
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

    [Fact]
    public async Task GetBySlug_SurfacesCoalitionCallout_WhenProvisionBornFromBriefing()
    {
        const string slug = "congress-student-data-privacy-bill";
        var provisionId = Guid.NewGuid();

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var briefing = await db.Briefings.FirstAsync(b => b.Slug == slug);
            db.Provisions.Add(new Provision
            {
                Id = provisionId,
                Slug = "callout-test-provision",
                Title = "Callout test provision",
                NeutralText = "neutral",
                SourceBriefingId = briefing.Id,
                State = ProvisionState.Open,
                Locality = briefing.Locality,
            });
            await db.SaveChangesAsync();
        }

        try
        {
            var dto = await _client.GetFromJsonAsync<BriefingDto>($"/api/briefings/{slug}");
            dto.Should().NotBeNull();
            dto!.CoalitionProvisionId.Should().Be(provisionId, "the article links the coalition born from it");
            dto.CoalitionProvisionState.Should().Be("Open");
        }
        finally
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            await db.Provisions.Where(p => p.Id == provisionId).ExecuteDeleteAsync();
        }
    }
}
