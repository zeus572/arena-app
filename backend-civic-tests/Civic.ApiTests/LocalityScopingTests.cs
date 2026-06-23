using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services.Coalition.Product;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// The local-vs-national "hard wall": a reader's locality (state code on their
/// UserProfile) scopes which briefings and coalition provisions they can see.
/// National content (Locality null) is visible to everyone; local content only
/// to readers in the matching state.
/// </summary>
[Collection("Database")]
public class LocalityScopingTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;

    public LocalityScopingTests(DatabaseFixture fx) => _fx = fx;

    // Wipe mutable tables (UserProfiles + coalition tables) before each test.
    public async Task InitializeAsync() => await _fx.ResetMutableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Authenticate as the logical user id (JWT 'sub' = userId) so writes pass the
    // verified-email gate; identity/locality scoping is unchanged since the backend
    // keys users by the raw 'sub'. Pass null for a truly anonymous client.
    private HttpClient ClientFor(string? userId)
    {
        var client = _fx.Factory.CreateClient();
        if (userId is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", JwtTestHelper.MintAccessToken(userId));
        return client;
    }

    private async Task SetLocalityAsync(string userId, string? locality)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProfileVersion = 0,
            LocalityState = locality,
        });
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- briefings

    private static Briefing NewBriefing(string slug, string? locality) => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        Headline = $"Headline {slug}",
        Institution = "Congress",
        Branch = "Legislative",
        Status = "Proposed",
        AudienceLevel = "High School",
        KeyConcept = "Test",
        Summary30 = "x",
        Summary3Min = "x",
        Summary10Min = "x",
        WhoActed = "x",
        WhatChanged = "x",
        WhyItMatters = "x",
        Disagreement = "x",
        StrongestArgumentFor = "x",
        StrongestArgumentAgainst = "x",
        ThinkDeeperQuestion = "x?",
        Locality = locality,
        GenerationSource = CivicGenerationSource.News,
    };

    private async Task<(string nationalSlug, string waSlug)> SeedTwoBriefingsAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var nationalSlug = $"loc-national-{tag}";
        var waSlug = $"loc-wa-{tag}";
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        db.Briefings.Add(NewBriefing(nationalSlug, null));
        db.Briefings.Add(NewBriefing(waSlug, Localities.Washington));
        await db.SaveChangesAsync();
        return (nationalSlug, waSlug);
    }

    private static async Task<List<string>> ListSlugsAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<BriefingPageDto>("/api/briefings?page=1&pageSize=100");
        return page!.Items.Select(b => b.Slug).ToList();
    }

    [Fact]
    public async Task Briefings_LocalStory_VisibleOnlyToMatchingLocality()
    {
        var (nationalSlug, waSlug) = await SeedTwoBriefingsAsync();
        await SetLocalityAsync("wa-reader", Localities.Washington);
        await SetLocalityAsync("ca-reader", Localities.California);

        var waSlugs = await ListSlugsAsync(ClientFor("wa-reader"));
        waSlugs.Should().Contain(nationalSlug).And.Contain(waSlug);

        var caSlugs = await ListSlugsAsync(ClientFor("ca-reader"));
        caSlugs.Should().Contain(nationalSlug);
        caSlugs.Should().NotContain(waSlug, "a WA story is walled off from a CA reader");

        var anonSlugs = await ListSlugsAsync(ClientFor("anon-reader"));
        anonSlugs.Should().Contain(nationalSlug);
        anonSlugs.Should().NotContain(waSlug, "a reader with no locality sees national only");
    }

    [Fact]
    public async Task Briefings_GetBySlug_LocalStory_404sForOutOfAreaReader()
    {
        var (_, waSlug) = await SeedTwoBriefingsAsync();
        await SetLocalityAsync("wa-reader", Localities.Washington);

        var waResp = await ClientFor("wa-reader").GetAsync($"/api/briefings/{waSlug}");
        waResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var caResp = await ClientFor("ca-reader").GetAsync($"/api/briefings/{waSlug}");
        caResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- coalition

    /// <summary>
    /// Insert a minimal open provision with the given locality directly (no LLM
    /// birth call). A bare provision is enough to exercise list/detail scoping.
    /// </summary>
    private async Task<Guid> InsertProvisionAsync(string? locality)
    {
        var id = Guid.NewGuid();
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        db.Provisions.Add(new Provision
        {
            Id = id,
            Slug = $"loc-prov-{id:N}",
            Title = locality is null ? "National provision" : $"{locality} provision",
            NeutralText = "x",
            State = ProvisionState.Open,
            Deadline = DateTime.UtcNow.AddDays(7),
            Locality = locality,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<List<ProvisionSummaryDto>> ListProvisionsAsync(HttpClient client)
        => (await client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions"))!;

    [Fact]
    public async Task Coalition_LocalProvision_VisibleOnlyToMatchingLocality()
    {
        var nationalId = await InsertProvisionAsync(null);
        var waId = await InsertProvisionAsync(Localities.Washington);
        await SetLocalityAsync("wa-reader", Localities.Washington);
        await SetLocalityAsync("ca-reader", Localities.California);

        var waList = await ListProvisionsAsync(ClientFor("wa-reader"));
        waList.Select(p => p.Id).Should().Contain(nationalId).And.Contain(waId);

        var caList = await ListProvisionsAsync(ClientFor("ca-reader"));
        caList.Select(p => p.Id).Should().Contain(nationalId);
        caList.Select(p => p.Id).Should().NotContain(waId, "a WA provision is walled off from a CA reader");
    }

    [Fact]
    public async Task Coalition_LocalProvisionDetail_404sForOutOfAreaReader()
    {
        var waId = await InsertProvisionAsync(Localities.Washington);
        await SetLocalityAsync("wa-reader", Localities.Washington);

        var waResp = await ClientFor("wa-reader").GetAsync($"/api/coalition/provisions/{waId}");
        waResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var caResp = await ClientFor("ca-reader").GetAsync($"/api/coalition/provisions/{waId}");
        caResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Coalition_Act_OnOutOfLocalityProvision_EarnsNoPoints()
    {
        var waId = await InsertProvisionAsync(Localities.Washington);
        await SetLocalityAsync("ca-reader", Localities.California);

        var resp = await ClientFor("ca-reader").PostAsJsonAsync(
            $"/api/coalition/provisions/{waId}/acts",
            new { type = "ReactionWithReason", payload = "Trying to earn points out of area." });
        resp.EnsureSuccessStatusCode();
        var result = (await resp.Content.ReadFromJsonAsync<ActResultDto>())!;
        result.Points.Should().Be(0, "a forged act on an out-of-locality provision earns nothing");
    }
}
