using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class BudgetControllerTests
{
    private readonly DatabaseFixture _fixture;

    public BudgetControllerTests(DatabaseFixture fixture) => _fixture = fixture;

    private HttpClient ClientFor(string userId)
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task Categories_ReturnsSeededList()
    {
        var client = _fixture.Factory.CreateClient();
        var resp = await client.GetAsync("/api/budget/categories");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await resp.Content.ReadFromJsonAsync<List<BudgetCategoryDto>>();
        items.Should().HaveCount(10);
        items!.Select(c => c.Key).Should().Contain(new[]
        {
            "defense", "healthcare", "safety-net", "education", "infrastructure",
            "climate", "debt-reduction", "tax-relief", "immigration", "science-innovation",
        });
    }

    [Fact]
    public async Task SessionLifecycle_StartSetCompleteUpdatesProfile()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var start = await (await client.PostAsync("/api/budget/sessions", null))
            .Content.ReadFromJsonAsync<BudgetSessionDto>();
        start.Should().NotBeNull();
        start!.IsComplete.Should().BeFalse();
        start.TotalPoints.Should().Be(0);

        var set = await client.PutAsJsonAsync(
            $"/api/budget/sessions/{start.Id}/allocations",
            new
            {
                allocations = new[]
                {
                    new { categoryKey = "healthcare", points = 30 },
                    new { categoryKey = "education", points = 25 },
                    new { categoryKey = "infrastructure", points = 25 },
                    new { categoryKey = "climate", points = 20 },
                },
            });
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterSet = await set.Content.ReadFromJsonAsync<BudgetSessionDto>();
        afterSet!.TotalPoints.Should().Be(100);
        afterSet.IsComplete.Should().BeFalse();

        var complete = await client.PostAsync($"/api/budget/sessions/{start.Id}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var done = await complete.Content.ReadFromJsonAsync<BudgetSessionDto>();
        done!.IsComplete.Should().BeTrue();
        done.CompletedAt.Should().NotBeNull();

        // Profile picked up the allocation signal
        var profile = await (await client.GetAsync("/api/profile/me"))
            .Content.ReadFromJsonAsync<ProfileDto>();
        profile!.ProfileVersion.Should().BeGreaterThan(0);
        profile.Axes.Any(a => Math.Abs(a.Score) > 0).Should().BeTrue();
    }

    [Fact]
    public async Task Complete_TotalNot100_Returns400()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var start = await (await client.PostAsync("/api/budget/sessions", null))
            .Content.ReadFromJsonAsync<BudgetSessionDto>();
        await client.PutAsJsonAsync(
            $"/api/budget/sessions/{start!.Id}/allocations",
            new { allocations = new[] { new { categoryKey = "healthcare", points = 50 } } });

        var complete = await client.PostAsync($"/api/budget/sessions/{start.Id}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetAllocations_UnknownCategory_Returns400()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var start = await (await client.PostAsync("/api/budget/sessions", null))
            .Content.ReadFromJsonAsync<BudgetSessionDto>();

        var resp = await client.PutAsJsonAsync(
            $"/api/budget/sessions/{start!.Id}/allocations",
            new { allocations = new[] { new { categoryKey = "not-a-category", points = 50 } } });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Current_NoSession_ReturnsNullBody()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var resp = await client.GetAsync("/api/budget/sessions/me/current");
        // ASP.NET serializes Ok(null) as 204 No Content; either response means
        // "no active session yet."
        ((int)resp.StatusCode).Should().BeOneOf(200, 204);
        var body = await resp.Content.ReadAsStringAsync();
        (body == "" || body == "null").Should().BeTrue($"expected empty or 'null' but was '{body}'");
    }

    [Fact]
    public async Task Current_ActiveSession_ReturnsIt()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var start = await (await client.PostAsync("/api/budget/sessions", null))
            .Content.ReadFromJsonAsync<BudgetSessionDto>();

        var current = await (await client.GetAsync("/api/budget/sessions/me/current"))
            .Content.ReadFromJsonAsync<BudgetSessionDto>();
        current.Should().NotBeNull();
        current!.Id.Should().Be(start!.Id);
    }
}
