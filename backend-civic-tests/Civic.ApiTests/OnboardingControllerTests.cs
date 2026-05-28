using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class OnboardingControllerTests
{
    private readonly DatabaseFixture _fixture;

    public OnboardingControllerTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient ClientFor(string userId)
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task Questions_List_FiltersByTypeAndPagesByOrder()
    {
        var client = _fixture.Factory.CreateClient();
        var resp = await client.GetAsync("/api/questions?type=simple_pairing&take=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await resp.Content.ReadFromJsonAsync<List<QuestionDto>>();
        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(10);
        items.Select(q => q.Order).Should().BeInAscendingOrder();
        items[0].Choices.Should().HaveCountGreaterOrEqualTo(2);
        items[0].Choices.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Label));
    }

    [Fact]
    public async Task Questions_List_UnknownType_Returns400()
    {
        var client = _fixture.Factory.CreateClient();
        var resp = await client.GetAsync("/api/questions?type=not_a_real_type");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAnswer_PersistsAndIsReturnedByMine()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var questions = await (await client.GetAsync("/api/questions?type=simple_pairing&take=10"))
            .Content.ReadFromJsonAsync<List<QuestionDto>>();
        questions.Should().NotBeNull();

        var q = questions![0];
        var postResp = await client.PostAsJsonAsync("/api/answers", new
        {
            questionId = q.Id,
            selectedChoiceKey = q.Choices[0].Key,
            confidence = "VerySure",
            intensity = "High",
        });
        postResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await postResp.Content.ReadFromJsonAsync<AnswerDto>();
        saved!.SelectedChoiceKey.Should().Be(q.Choices[0].Key);
        saved.Confidence.Should().Be("VerySure");

        var mineResp = await client.GetAsync("/api/answers/me");
        mineResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var mine = await mineResp.Content.ReadFromJsonAsync<List<AnswerDto>>();
        mine.Should().ContainSingle(a => a.QuestionId == q.Id && a.Confidence == "VerySure");
    }

    [Fact]
    public async Task PostAnswer_SecondPostSameQuestion_Replaces()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var questions = await (await client.GetAsync("/api/questions?type=simple_pairing&take=10"))
            .Content.ReadFromJsonAsync<List<QuestionDto>>();
        var q = questions![0];

        await client.PostAsJsonAsync("/api/answers", new
        {
            questionId = q.Id,
            selectedChoiceKey = q.Choices[0].Key,
            confidence = "NotSure",
            intensity = "Low",
        });

        await client.PostAsJsonAsync("/api/answers", new
        {
            questionId = q.Id,
            selectedChoiceKey = q.Choices[1].Key,
            confidence = "VerySure",
            intensity = "NonNegotiable",
        });

        var mine = await (await client.GetAsync("/api/answers/me"))
            .Content.ReadFromJsonAsync<List<AnswerDto>>();
        mine.Should().HaveCount(1);
        mine![0].SelectedChoiceKey.Should().Be(q.Choices[1].Key);
        mine[0].Confidence.Should().Be("VerySure");
        mine[0].Intensity.Should().Be("NonNegotiable");
    }

    [Fact]
    public async Task PostAnswer_InvalidChoiceKey_Returns400()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var questions = await (await client.GetAsync("/api/questions?type=simple_pairing&take=10"))
            .Content.ReadFromJsonAsync<List<QuestionDto>>();
        var q = questions![0];

        var resp = await client.PostAsJsonAsync("/api/answers", new
        {
            questionId = q.Id,
            selectedChoiceKey = "ZZ",
            confidence = "VerySure",
            intensity = "High",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMine_DifferentUsers_AreIsolated()
    {
        await _fixture.ResetMutableAsync();
        var userA = Guid.NewGuid().ToString();
        var userB = Guid.NewGuid().ToString();
        var clientA = ClientFor(userA);
        var clientB = ClientFor(userB);

        var questions = await (await clientA.GetAsync("/api/questions?type=simple_pairing&take=10"))
            .Content.ReadFromJsonAsync<List<QuestionDto>>();
        var q = questions![0];

        await clientA.PostAsJsonAsync("/api/answers", new
        {
            questionId = q.Id,
            selectedChoiceKey = q.Choices[0].Key,
            confidence = "VerySure",
            intensity = "High",
        });

        var aMine = await (await clientA.GetAsync("/api/answers/me"))
            .Content.ReadFromJsonAsync<List<AnswerDto>>();
        var bMine = await (await clientB.GetAsync("/api/answers/me"))
            .Content.ReadFromJsonAsync<List<AnswerDto>>();

        aMine!.Should().HaveCount(1);
        bMine!.Should().BeEmpty();
    }
}
