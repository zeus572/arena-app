using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class ProfileControllerTests
{
    private readonly DatabaseFixture _fixture;

    public ProfileControllerTests(DatabaseFixture fixture)
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
    public async Task GetMine_NoAnswers_ReturnsEmptyProfileWithAxisChrome()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var dto = await (await client.GetAsync("/api/profile/me"))
            .Content.ReadFromJsonAsync<ProfileDto>();

        dto.Should().NotBeNull();
        dto!.ProfileVersion.Should().Be(0);
        dto.AnswerCount.Should().Be(0);
        dto.Axes.Should().HaveCount(15);
        dto.Axes.Should().OnlyContain(a => a.Score == 0 && a.SupportingAnswerCount == 0);
        dto.ArchetypeBlend.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMine_AfterAnswers_HasNonZeroAxesAndArchetypeBlend()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var questions = await (await client.GetAsync("/api/questions?type=simple_pairing&take=10"))
            .Content.ReadFromJsonAsync<List<QuestionDto>>();

        // Pick "B" on every question to push axes consistently in one direction
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

        var dto = await (await client.GetAsync("/api/profile/me"))
            .Content.ReadFromJsonAsync<ProfileDto>();

        dto!.AnswerCount.Should().Be(10);
        dto.ProfileVersion.Should().BeGreaterThan(0);
        dto.Axes.Any(a => Math.Abs(a.Score) > 0).Should().BeTrue();
        dto.ArchetypeBlend.Should().NotBeEmpty();
        dto.ArchetypeBlend.Sum(a => a.Percent).Should().BeApproximately(100.0, 0.5);
        dto.ArchetypeBlend.Should().BeInDescendingOrder(b => b.Percent);
    }

    [Fact]
    public async Task PostAnswer_RecomputesProfile()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var questions = await (await client.GetAsync("/api/questions?type=simple_pairing&take=10"))
            .Content.ReadFromJsonAsync<List<QuestionDto>>();
        var q = questions![0];

        var initial = await (await client.GetAsync("/api/profile/me"))
            .Content.ReadFromJsonAsync<ProfileDto>();
        initial!.ProfileVersion.Should().Be(0);

        await client.PostAsJsonAsync("/api/answers", new
        {
            questionId = q.Id,
            selectedChoiceKey = "A",
            confidence = "SomewhatSure",
            intensity = "Medium",
        });

        var after = await (await client.GetAsync("/api/profile/me"))
            .Content.ReadFromJsonAsync<ProfileDto>();
        after!.ProfileVersion.Should().BeGreaterThan(0);
        after.AnswerCount.Should().Be(1);
    }

    [Fact]
    public async Task PostAnswer_RecomputeIsIdempotent_ProfileVersionIncrementsPerAnswer()
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
            selectedChoiceKey = "A",
            confidence = "VerySure",
            intensity = "High",
        });
        var v1 = (await (await client.GetAsync("/api/profile/me"))
            .Content.ReadFromJsonAsync<ProfileDto>())!.ProfileVersion;

        // Re-answer the same question — upsert, but profile recomputes
        await client.PostAsJsonAsync("/api/answers", new
        {
            questionId = q.Id,
            selectedChoiceKey = "B",
            confidence = "VerySure",
            intensity = "High",
        });
        var v2 = (await (await client.GetAsync("/api/profile/me"))
            .Content.ReadFromJsonAsync<ProfileDto>())!.ProfileVersion;

        v2.Should().BeGreaterThan(v1);
    }

    [Fact]
    public async Task PostRecompute_WorksExplicitly()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var resp = await client.PostAsync("/api/profile/me/recompute", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<ProfileDto>();
        dto.Should().NotBeNull();
        dto!.Axes.Should().HaveCount(15);
    }
}
