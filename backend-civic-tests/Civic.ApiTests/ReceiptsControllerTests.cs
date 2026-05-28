using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class ReceiptsControllerTests
{
    private readonly DatabaseFixture _fixture;

    public ReceiptsControllerTests(DatabaseFixture fixture) => _fixture = fixture;

    private HttpClient ClientFor(string userId)
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task BuildReceipt_NoAnswers_ProducesFallbackInsight()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var resp = await client.PostAsync("/api/receipts", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<ValuesReceiptDto>();
        dto!.LearnedInsights.Should().NotBeEmpty();
        dto.AnswerCountAtTime.Should().Be(0);
        dto.Tensions.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildReceipt_AfterStrongAnswers_HasInsightsAndProfileVersion()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var questions = await (await client.GetAsync("/api/questions?type=simple_pairing&take=10"))
            .Content.ReadFromJsonAsync<List<QuestionDto>>();
        foreach (var q in questions!)
        {
            await client.PostAsJsonAsync("/api/answers", new
            {
                questionId = q.Id,
                selectedChoiceKey = "B",
                confidence = "VerySure",
                intensity = "High",
            });
        }

        var receipt = await (await client.PostAsync("/api/receipts", null))
            .Content.ReadFromJsonAsync<ValuesReceiptDto>();
        receipt!.AnswerCountAtTime.Should().Be(10);
        receipt.ProfileVersionAtTime.Should().BeGreaterThan(0);
        receipt.LearnedInsights.Should().NotBeEmpty();
        receipt.LearnedInsights.Should().Contain(s => s.Contains("tendency", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildReceipt_OpposingAnswers_SurfaceTension()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var questions = (await (await client.GetAsync("/api/questions?take=20"))
            .Content.ReadFromJsonAsync<List<QuestionDto>>())!;

        // Find two questions that both affect "speech" — q-pairing-08-speech-harm and q-pressure-01-campus-speaker
        var speechHarm = questions.First(q => q.ExternalId == "q-pairing-08-speech-harm");
        var campusSpeaker = questions.First(q => q.ExternalId == "q-pressure-01-campus-speaker");

        // Choose A on speech-harm (speech -0.6) and B on campus-speaker (speech +0.6) — opposite
        await client.PostAsJsonAsync("/api/answers", new
        {
            questionId = speechHarm.Id,
            selectedChoiceKey = "A",
            confidence = "VerySure",
            intensity = "High",
        });
        await client.PostAsJsonAsync("/api/answers", new
        {
            questionId = campusSpeaker.Id,
            selectedChoiceKey = "B",
            confidence = "VerySure",
            intensity = "High",
        });

        var receipt = await (await client.PostAsync("/api/receipts", null))
            .Content.ReadFromJsonAsync<ValuesReceiptDto>();
        receipt!.Tensions.Should().Contain(t => t.AxisKey == "speech");
        var speechTension = receipt.Tensions.Single(t => t.AxisKey == "speech");
        speechTension.Framing.Should().NotBeNullOrWhiteSpace();
        speechTension.AxisName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetMine_ListsRecentReceipts()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        await client.PostAsync("/api/receipts", null);
        await client.PostAsync("/api/receipts", null);

        var list = await (await client.GetAsync("/api/receipts/me"))
            .Content.ReadFromJsonAsync<List<ValuesReceiptDto>>();
        list.Should().HaveCount(2);
        list.Should().BeInDescendingOrder(r => r.CreatedAt);
    }

    [Fact]
    public async Task GetById_OtherUserReceipt_Returns404()
    {
        await _fixture.ResetMutableAsync();
        var userA = Guid.NewGuid().ToString();
        var userB = Guid.NewGuid().ToString();

        var receiptA = await (await ClientFor(userA).PostAsync("/api/receipts", null))
            .Content.ReadFromJsonAsync<ValuesReceiptDto>();

        var resp = await ClientFor(userB).GetAsync($"/api/receipts/{receiptA!.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
