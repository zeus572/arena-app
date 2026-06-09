using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class QuizControllerTests
{
    private readonly DatabaseFixture _fixture;

    public QuizControllerTests(DatabaseFixture fixture) => _fixture = fixture;

    private HttpClient ClientFor(string userId)
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task List_ReturnsDynamicValidSubset()
    {
        var client = _fixture.Factory.CreateClient();
        var resp = await client.GetAsync("/api/quiz/questions");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await resp.Content.ReadFromJsonAsync<List<QuizQuestionDto>>();
        items.Should().NotBeNull();
        // Default serves a bounded, shuffled subset — not the whole bank, not empty.
        items!.Should().HaveCountGreaterThanOrEqualTo(1);
        items.Should().HaveCountLessThanOrEqualTo(6);
        items.Should().OnlyContain(q => q.Options.Length >= 2);
        items.Should().OnlyContain(q => q.CorrectAnswerIndex >= 0 && q.CorrectAnswerIndex < q.Options.Length);
        items.Should().OnlyContain(q => q.CorrectRate >= 0 && q.CorrectRate <= 1);
        items.Should().OnlyContain(q => q.ResponseCount >= 0);
    }

    [Fact]
    public async Task List_RespectsCountParam()
    {
        var client = _fixture.Factory.CreateClient();
        var items = await client.GetFromJsonAsync<List<QuizQuestionDto>>("/api/quiz/questions?count=3");
        items.Should().NotBeNull();
        items!.Should().HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task List_LargeCount_ReturnsWholeSeededBank()
    {
        var client = _fixture.Factory.CreateClient();
        var items = await client.GetFromJsonAsync<List<QuizQuestionDto>>("/api/quiz/questions?count=1000");
        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterThanOrEqualTo(4);
        items.Should().Contain(q => q.ExternalId == "q-001");
        // The bank was enlarged so the shuffle has real variety.
        items.Select(q => q.ExternalId).Distinct().Should().HaveCount(items.Count);
    }

    [Fact]
    public async Task Respond_RecordsAndReturnsSixtyDayMovingAverage()
    {
        await _fixture.ResetMutableAsync();
        var client = ClientFor(Guid.NewGuid().ToString());

        var items = await client.GetFromJsonAsync<List<QuizQuestionDto>>("/api/quiz/questions?count=1000");
        var q = items!.First(x => x.ExternalId == "q-001");

        // First answer: correct → 100% so far.
        var first = await (await client.PostAsJsonAsync(
            $"/api/quiz/questions/{q.Id}/responses",
            new { selectedIndex = q.CorrectAnswerIndex }))
            .Content.ReadFromJsonAsync<QuizPollResultDto>();
        first.Should().NotBeNull();
        first!.IsCorrect.Should().BeTrue();
        first.ResponseCount.Should().Be(1);
        first.CorrectCount.Should().Be(1);
        first.CorrectRate.Should().Be(1.0);
        first.WindowDays.Should().Be(60);

        // Second answer: wrong → moving average drops to 50%.
        var wrongIndex = (q.CorrectAnswerIndex + 1) % q.Options.Length;
        var second = await (await client.PostAsJsonAsync(
            $"/api/quiz/questions/{q.Id}/responses",
            new { selectedIndex = wrongIndex }))
            .Content.ReadFromJsonAsync<QuizPollResultDto>();
        second!.IsCorrect.Should().BeFalse();
        second.ResponseCount.Should().Be(2);
        second.CorrectCount.Should().Be(1);
        second.CorrectRate.Should().Be(0.5);
    }

    [Fact]
    public async Task Respond_OutOfRangeIndex_ReturnsBadRequest()
    {
        var client = ClientFor(Guid.NewGuid().ToString());
        var items = await client.GetFromJsonAsync<List<QuizQuestionDto>>("/api/quiz/questions?count=1000");
        var q = items!.First();

        var resp = await client.PostAsJsonAsync(
            $"/api/quiz/questions/{q.Id}/responses", new { selectedIndex = 999 });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Respond_UnknownQuestion_ReturnsNotFound()
    {
        var client = ClientFor(Guid.NewGuid().ToString());
        var resp = await client.PostAsJsonAsync(
            $"/api/quiz/questions/{Guid.NewGuid()}/responses", new { selectedIndex = 0 });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
