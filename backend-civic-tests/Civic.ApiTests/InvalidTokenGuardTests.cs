using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// The explicit-bad-token guard (Program.cs): an AllowAnonymous endpoint must
/// still serve genuine anonymous callers (no token, or X-User-Id), but must
/// return 401 — not silently fall back to the shared "anonymous" identity — when
/// a caller presents a Bearer token that fails validation. `/api/profile/me` is
/// the probe: it's AllowAnonymous and returns 200 for an anonymous caller.
/// </summary>
[Collection("Database")]
public class InvalidTokenGuardTests
{
    private readonly DatabaseFixture _fixture;

    public InvalidTokenGuardTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient Client() => _fixture.Factory.CreateClient();

    private const string AnonEndpoint = "/api/profile/me";

    [Fact]
    public async Task NoToken_AnonymousEndpoint_StillReturns200()
    {
        var c = Client();
        var resp = await c.GetAsync(AnonEndpoint);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task XUserIdOnly_AnonymousEndpoint_StillReturns200()
    {
        var c = Client();
        c.DefaultRequestHeaders.Add("X-User-Id", $"anon-{Guid.NewGuid()}");
        var resp = await c.GetAsync(AnonEndpoint);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ValidToken_AnonymousEndpoint_Returns200()
    {
        var c = Client();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.MintAccessToken(Guid.NewGuid()));
        var resp = await c.GetAsync(AnonEndpoint);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MalformedToken_AnonymousEndpoint_Returns401()
    {
        var c = Client();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-jwt");
        var resp = await c.GetAsync(AnonEndpoint);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WronglySignedToken_AnonymousEndpoint_Returns401()
    {
        var c = Client();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.MintWronglySignedToken(Guid.NewGuid()));
        var resp = await c.GetAsync(AnonEndpoint);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredToken_AnonymousEndpoint_Returns401()
    {
        var c = Client();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.MintExpiredToken(Guid.NewGuid()));
        var resp = await c.GetAsync(AnonEndpoint);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
