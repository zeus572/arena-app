using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class ZeitgeistControllerTests
{
    private readonly DatabaseFixture _fixture;

    public ZeitgeistControllerTests(DatabaseFixture fixture) => _fixture = fixture;

    private HttpClient ClientFor(string userId)
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task Get_ReturnsCompassAxesAndTotals()
    {
        var client = _fixture.Factory.CreateClient();
        var resp = await client.GetAsync("/api/zeitgeist");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var z = await resp.Content.ReadFromJsonAsync<ZeitgeistDto>();
        z.Should().NotBeNull();
        z!.Totals.Should().NotBeNull();
        z.Coalitions.Should().NotBeNull();

        // Every Civic Compass axis is represented, with its labels, ready to show a public lean.
        z.Axes.Should().HaveCountGreaterThanOrEqualTo(10);
        z.Axes.Select(a => a.AxisKey).Should().Contain("govt-role");
        z.Axes.Should().OnlyContain(a => a.LowLabel.Length > 0 && a.HighLabel.Length > 0);
        z.Axes.Should().OnlyContain(a => a.AverageScore >= -1 && a.AverageScore <= 1);
    }

    [Fact]
    public async Task Get_QuizSignalsReflectRecentAnswers()
    {
        await _fixture.ResetMutableAsync();
        var client = ClientFor(Guid.NewGuid().ToString());

        var items = await client.GetFromJsonAsync<List<QuizQuestionDto>>("/api/quiz/questions?count=1000");
        var q = items!.First(x => x.ExternalId == "q-001");
        var wrongIndex = (q.CorrectAnswerIndex + 1) % q.Options.Length;
        await client.PostAsJsonAsync($"/api/quiz/questions/{q.Id}/responses", new { selectedIndex = wrongIndex });

        var z = await client.GetFromJsonAsync<ZeitgeistDto>("/api/zeitgeist");
        z.Should().NotBeNull();
        z!.Totals.QuizResponseCount.Should().BeGreaterThanOrEqualTo(1);
        z.QuizSignals.Should().Contain(s => s.Topic == q.Topic);
        z.QuizSignals.First(s => s.Topic == q.Topic).CorrectRate.Should().Be(0);
    }
}
