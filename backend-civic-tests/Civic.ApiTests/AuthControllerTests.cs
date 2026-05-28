using System.Net;
using System.Net.Http.Headers;
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
public class AuthControllerTests
{
    private readonly DatabaseFixture _fixture;

    public AuthControllerTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient Client() => _fixture.Factory.CreateClient();

    private static void Auth(HttpClient c, Guid userId, string email = "user@example.com")
    {
        var token = JwtTestHelper.MintAccessToken(userId, email);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task DebateIssuedJwt_IsAcceptedByCivic_AndMeReturnsSubClaim()
    {
        var userId = Guid.NewGuid();
        var c = Client();
        Auth(c, userId);

        var resp = await c.GetAsync("/api/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await resp.Content.ReadFromJsonAsync<MeResponse>();
        payload.Should().NotBeNull();
        payload!.IsAuthenticated.Should().BeTrue();
        payload.UserId.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task MeWithoutToken_Returns401()
    {
        var c = Client();
        var resp = await c.GetAsync("/api/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LinkAnonymous_WithoutAuth_Returns401()
    {
        await _fixture.ResetMutableAsync();
        var c = Client();
        var resp = await c.PostAsJsonAsync(
            "/api/auth/link-anonymous",
            new { anonymousUserId = "some-anon" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LinkAnonymous_RejectsMissingId()
    {
        await _fixture.ResetMutableAsync();
        var c = Client();
        Auth(c, Guid.NewGuid());
        var resp = await c.PostAsJsonAsync(
            "/api/auth/link-anonymous",
            new { anonymousUserId = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LinkAnonymous_RejectsLiteralAnonymousId()
    {
        await _fixture.ResetMutableAsync();
        var c = Client();
        Auth(c, Guid.NewGuid());
        var resp = await c.PostAsJsonAsync(
            "/api/auth/link-anonymous",
            new { anonymousUserId = "anonymous" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LinkAnonymous_RekeyAnswersFromAnonToAuthedUser()
    {
        await _fixture.ResetMutableAsync();

        // Pick a real seeded question id so the FK to CivicQuestions holds.
        Guid questionId;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            questionId = await db.CivicQuestions
                .Where(q => q.Type == CivicQuestionType.SimplePairing)
                .Select(q => q.Id)
                .FirstAsync();
        }

        var anonId = $"anon-{Guid.NewGuid()}";

        // Submit one answer as the anonymous user.
        var anonClient = Client();
        anonClient.DefaultRequestHeaders.Add("X-User-Id", anonId);
        var submit = await anonClient.PostAsJsonAsync("/api/answers", new
        {
            questionId,
            selectedChoiceKey = "A",
            confidence = "VerySure",
            intensity = "High",
        });
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        // Now sign in as a real user and link.
        var authedId = Guid.NewGuid();
        var authedClient = Client();
        Auth(authedClient, authedId);

        var linkResp = await authedClient.PostAsJsonAsync(
            "/api/auth/link-anonymous",
            new { anonymousUserId = anonId });
        linkResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // The authed user's /api/answers/me should now include the migrated answer.
        var mine = await authedClient.GetFromJsonAsync<List<AnswerDto>>("/api/answers/me");
        mine.Should().NotBeNull();
        mine!.Should().ContainSingle(a => a.QuestionId == questionId);

        // And the anonymous bucket should be empty.
        var stillAnon = Client();
        stillAnon.DefaultRequestHeaders.Add("X-User-Id", anonId);
        var anonAfter = await stillAnon.GetFromJsonAsync<List<AnswerDto>>("/api/answers/me");
        anonAfter.Should().NotBeNull();
        anonAfter!.Should().BeEmpty();
    }

    [Fact]
    public async Task LinkAnonymous_IsIdempotent_WhenCalledTwice()
    {
        await _fixture.ResetMutableAsync();

        Guid questionId;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            questionId = await db.CivicQuestions
                .Where(q => q.Type == CivicQuestionType.SimplePairing)
                .Select(q => q.Id)
                .FirstAsync();
        }

        var anonId = $"anon-{Guid.NewGuid()}";
        var authedId = Guid.NewGuid();

        var anonClient = Client();
        anonClient.DefaultRequestHeaders.Add("X-User-Id", anonId);
        await anonClient.PostAsJsonAsync("/api/answers", new
        {
            questionId,
            selectedChoiceKey = "A",
            confidence = "VerySure",
            intensity = "High",
        });

        var authedClient = Client();
        Auth(authedClient, authedId);

        var first = await authedClient.PostAsJsonAsync(
            "/api/auth/link-anonymous",
            new { anonymousUserId = anonId });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await authedClient.PostAsJsonAsync(
            "/api/auth/link-anonymous",
            new { anonymousUserId = anonId });
        // Anon now has nothing left; second call is a no-op but still 200.
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record MeResponse(string UserId, bool IsAuthenticated);
}
