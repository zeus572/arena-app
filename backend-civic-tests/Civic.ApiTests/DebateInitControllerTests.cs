using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class DebateInitControllerTests
{
    private readonly DatabaseFixture _fx;

    public DebateInitControllerTests(DatabaseFixture fx) => _fx = fx;

    private (HttpClient Client, StubDebateApiHandler Stub) NewClient(StubDebateApiHandler handler)
    {
        // Spin up a new factory per test that swaps the DebateApi HttpClient's
        // primary handler for our stub. The new factory still points at the
        // shared civic_test DB so seeded briefings are visible.
        var factory = _fx.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("DebateApi")
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        });
        return (factory.CreateClient(), handler);
    }

    private static void Auth(HttpClient c, Guid userId, string plan = "Premium")
    {
        var token = JwtTestHelper.MintAccessToken(userId, $"u-{userId:N}@example.com", plan);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private const string SeededBriefingSlug = "congress-student-data-privacy-bill";

    [Fact]
    public async Task NoAuth_Returns401()
    {
        var (client, _) = NewClient(new StubDebateApiHandler(HttpStatusCode.OK, "{}"));
        var resp = await client.PostAsJsonAsync($"/api/briefings/{SeededBriefingSlug}/debate", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FreePlanUser_Returns403_WithoutCallingDebateApi()
    {
        var (client, stub) = NewClient(new StubDebateApiHandler(HttpStatusCode.OK, "{}"));
        Auth(client, Guid.NewGuid(), plan: "Free");

        var resp = await client.PostAsJsonAsync($"/api/briefings/{SeededBriefingSlug}/debate", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.Requests.Should().BeEmpty("free users should not hit the debate service");
    }

    [Fact]
    public async Task PremiumUser_UnknownBriefingSlug_Returns404()
    {
        var (client, stub) = NewClient(new StubDebateApiHandler(HttpStatusCode.OK, "{}"));
        Auth(client, Guid.NewGuid());

        var resp = await client.PostAsJsonAsync("/api/briefings/no-such-briefing/debate", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        stub.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PremiumUser_DebateApi200_ReturnsDebateIdAndUrl_AndForwardsBearer()
    {
        var newDebateId = Guid.NewGuid();
        var (client, stub) = NewClient(new StubDebateApiHandler(
            HttpStatusCode.OK,
            $"{{\"id\":\"{newDebateId}\",\"topic\":\"...\"}}"));
        Auth(client, Guid.NewGuid());

        var resp = await client.PostAsJsonAsync($"/api/briefings/{SeededBriefingSlug}/debate", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<DebateInitResponse>();
        body.Should().NotBeNull();
        body!.DebateId.Should().Be(newDebateId);
        body.DebateUrl.Should().Contain(newDebateId.ToString());

        stub.Requests.Should().HaveCount(1);
        stub.Requests[0].RequestUri!.AbsolutePath.Should().Be("/api/debates");
        stub.AuthorizationHeaders[0].Should().StartWith("Bearer ");
        stub.RequestBodies[0].Should().Contain("topic").And.Contain("standard");
    }

    [Fact]
    public async Task PremiumUser_DebateApi503_CollapsesTo502()
    {
        var (client, _) = NewClient(new StubDebateApiHandler(
            HttpStatusCode.ServiceUnavailable,
            "{\"error\":\"upstream down\"}"));
        Auth(client, Guid.NewGuid());

        var resp = await client.PostAsJsonAsync($"/api/briefings/{SeededBriefingSlug}/debate", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task PremiumUser_DebateApiUnreachable_Returns502()
    {
        var (client, _) = NewClient(StubDebateApiHandler.ThrowsTransport());
        Auth(client, Guid.NewGuid());

        var resp = await client.PostAsJsonAsync($"/api/briefings/{SeededBriefingSlug}/debate", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    private record DebateInitResponse(Guid DebateId, string DebateUrl);
}
