using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// The anti-spam gate: account-bound write/participation endpoints require a verified
/// email (the JWT's email_verified claim). Unverified-but-signed-in callers get a 403
/// carrying a machine-readable code so the frontend can prompt them; reads stay open,
/// and anonymous callers still get the normal 401 challenge.
/// </summary>
[Collection("Database")]
public class EmailVerificationGateTests
{
    private readonly DatabaseFixture _fixture;

    public EmailVerificationGateTests(DatabaseFixture fixture) => _fixture = fixture;

    private HttpClient ClientFor(Guid userId, bool emailVerified)
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestHelper.MintAccessToken(userId, $"u-{userId:N}@example.com", emailVerified: emailVerified));
        return client;
    }

    [Fact]
    public async Task CreateLeague_WithUnverifiedEmail_Returns403_WithCode()
    {
        await _fixture.ResetMutableAsync();
        var client = ClientFor(Guid.NewGuid(), emailVerified: false);

        var res = await client.PostAsJsonAsync("/api/leagues",
            new CreateLeagueRequest { Name = "Spammers United", DisplayName = "Nope", Email = "nope@example.com" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await res.Content.ReadFromJsonAsync<GateError>();
        body!.Code.Should().Be("email_unverified");
    }

    [Fact]
    public async Task ListLeagues_WithUnverifiedEmail_StillAllowed()
    {
        await _fixture.ResetMutableAsync();
        var client = ClientFor(Guid.NewGuid(), emailVerified: false);

        // Reading is not gated — an unverified user can still browse.
        var res = await client.GetAsync("/api/leagues");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateLeague_WithVerifiedEmail_Succeeds()
    {
        await _fixture.ResetMutableAsync();
        var client = ClientFor(Guid.NewGuid(), emailVerified: true);

        var res = await client.PostAsJsonAsync("/api/leagues",
            new CreateLeagueRequest { Name = "Verified Crew", DisplayName = "Owner", Email = "owner@example.com" });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateLeague_WhenAnonymous_Returns401_NotTheVerifiedEmail403()
    {
        var anon = _fixture.Factory.CreateClient();

        var res = await anon.PostAsJsonAsync("/api/leagues",
            new CreateLeagueRequest { Name = "Anon", DisplayName = "Anon", Email = "anon@example.com" });

        // No identity at all → standard challenge, not the email_unverified 403.
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePetition_WithUnverifiedEmail_Returns403_WithCode()
    {
        await _fixture.ResetMutableAsync();
        var client = ClientFor(Guid.NewGuid(), emailVerified: false);

        var res = await client.PostAsJsonAsync("/api/petitions",
            new CreatePetitionRequest { Title = "Free pizza for all", Description = "A noble cause." });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await res.Content.ReadFromJsonAsync<GateError>();
        body!.Code.Should().Be("email_unverified");
    }

    [Fact]
    public async Task CreatePetition_WithVerifiedEmail_Succeeds()
    {
        await _fixture.ResetMutableAsync();
        var client = ClientFor(Guid.NewGuid(), emailVerified: true);

        var res = await client.PostAsJsonAsync("/api/petitions",
            new CreatePetitionRequest { Title = "Better bike lanes", Description = "Safer streets." });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private record GateError(string? Error, string? Code);
}
