using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class QuizControllerTests
{
    private readonly HttpClient _client;

    public QuizControllerTests(DatabaseFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task List_ReturnsSeededQuestionsInOrder()
    {
        var resp = await _client.GetAsync("/api/quiz/questions");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await resp.Content.ReadFromJsonAsync<List<QuizQuestionDto>>();
        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(4);
        items.Should().BeInAscendingOrder(q => q.Order);
        items.Should().Contain(q => q.ExternalId == "q-001");
        items.Should().OnlyContain(q => q.Options.Length >= 2);
        items.Should().OnlyContain(q => q.CorrectAnswerIndex >= 0 && q.CorrectAnswerIndex < q.Options.Length);
    }
}
