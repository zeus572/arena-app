using System.Net;
using System.Net.Http.Json;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services.Campaign;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class CampaignReactionApiTests
{
    private readonly DatabaseFixture _fixture;

    public CampaignReactionApiTests(DatabaseFixture fixture) => _fixture = fixture;

    private HttpClient ClientFor(string userId)
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    private async Task<(Guid PostId, Guid FirstFragmentId)> InsertPostAsync(
        string candidateSlug, string body, int up = 0, int down = 0, string? tone = null)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var candidate = await db.VirtualCandidates.FirstAsync(c => c.Slug == candidateSlug);

        var post = new CampaignPost
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            Body = body,
            Tone = tone is not null ? Enum.Parse<CampaignTone>(tone) : CampaignTone.Casual,
            Intensity = 3,
            IssueTags = new[] { "privacy" },
            Trigger = PostTrigger.Platform,
            UpCount = up,
            DownCount = down,
            CreatedAt = DateTime.UtcNow,
        };
        var frags = FragmentSplitter.Split(body);
        foreach (var f in frags) f.PostId = post.Id;
        post.Fragments = frags;

        db.CampaignPosts.Add(post);
        await db.SaveChangesAsync();
        return (post.Id, frags[0].Id);
    }

    [Fact]
    public async Task WholePostReaction_IsIdempotentAndFlippable()
    {
        await _fixture.ResetMutableAsync();
        var (postId, _) = await InsertPostAsync("dana-okonkwo", "We can build a fairer future. Let's start now.");
        var client = ClientFor(Guid.NewGuid().ToString());

        var r1 = await (await client.PostAsJsonAsync($"/api/posts/{postId}/reactions", new { type = "up" }))
            .Content.ReadFromJsonAsync<ReactionResultDto>();
        r1!.PostUp.Should().Be(1);
        r1.PostDown.Should().Be(0);

        // Same user reacting up again: still 1 (idempotent).
        var r2 = await (await client.PostAsJsonAsync($"/api/posts/{postId}/reactions", new { type = "up" }))
            .Content.ReadFromJsonAsync<ReactionResultDto>();
        r2!.PostUp.Should().Be(1);

        // Flip to down.
        var r3 = await (await client.PostAsJsonAsync($"/api/posts/{postId}/reactions", new { type = "down" }))
            .Content.ReadFromJsonAsync<ReactionResultDto>();
        r3!.PostUp.Should().Be(0);
        r3.PostDown.Should().Be(1);

        // Remove.
        var r4 = await (await client.DeleteAsync($"/api/posts/{postId}/reactions"))
            .Content.ReadFromJsonAsync<ReactionResultDto>();
        r4!.PostUp.Should().Be(0);
        r4.PostDown.Should().Be(0);
    }

    [Fact]
    public async Task TwoUsers_AccumulateOnSamePost()
    {
        await _fixture.ResetMutableAsync();
        var (postId, _) = await InsertPostAsync("marcus-reed", "Your data is yours. Demand a warrant.");

        await ClientFor("user-a").PostAsJsonAsync($"/api/posts/{postId}/reactions", new { type = "up" });
        var last = await (await ClientFor("user-b").PostAsJsonAsync($"/api/posts/{postId}/reactions", new { type = "up" }))
            .Content.ReadFromJsonAsync<ReactionResultDto>();

        last!.PostUp.Should().Be(2);
    }

    [Fact]
    public async Task FragmentReaction_UpdatesFragmentNotWholePost()
    {
        await _fixture.ResetMutableAsync();
        var (postId, fragmentId) = await InsertPostAsync("sofia-alvarez", "Test it before you ship it. Innovation needs guardrails.");
        var client = ClientFor(Guid.NewGuid().ToString());

        var r = await (await client.PostAsJsonAsync($"/api/posts/{postId}/fragments/{fragmentId}/reactions", new { type = "up" }))
            .Content.ReadFromJsonAsync<ReactionResultDto>();

        r!.FragmentUp.Should().Be(1);
        r.PostUp.Should().Be(0); // whole-post counter untouched
    }

    [Fact]
    public async Task Heatmap_ReflectsFragmentNetSentiment()
    {
        await _fixture.ResetMutableAsync();
        var (postId, fragmentId) = await InsertPostAsync("hank-whitfield", "Finish the broadband job. Wire the last mile.");
        await ClientFor("u1").PostAsJsonAsync($"/api/posts/{postId}/fragments/{fragmentId}/reactions", new { type = "up" });

        var heat = await ClientFor("u2").GetFromJsonAsync<PostHeatmapDto>($"/api/posts/{postId}/heatmap");
        heat!.Fragments.Should().NotBeEmpty();
        var f = heat.Fragments.Single(x => x.Id == fragmentId);
        f.Up.Should().Be(1);
        f.Net.Should().Be(1.0);
    }

    [Fact]
    public async Task PostDetail_IncludesFragmentsAndCandidate()
    {
        await _fixture.ResetMutableAsync();
        var (postId, _) = await InsertPostAsync("patricia-vance", "Safety and fairness are not a trade. Defend both.");

        var dto = await ClientFor("u").GetFromJsonAsync<CampaignPostDto>($"/api/posts/{postId}");
        dto!.Fragments.Should().NotBeEmpty();
        dto.Candidate.Should().NotBeNull();
        dto.Candidate!.IsFictional.Should().BeTrue();
        dto.Body.Length.Should().BeLessThanOrEqualTo(160);
    }

    [Fact]
    public async Task React_UnknownPost_Returns404()
    {
        await _fixture.ResetMutableAsync();
        var resp = await ClientFor("u").PostAsJsonAsync($"/api/posts/{Guid.NewGuid()}/reactions", new { type = "up" });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task React_InvalidType_Returns400()
    {
        await _fixture.ResetMutableAsync();
        var (postId, _) = await InsertPostAsync("dana-okonkwo", "A clear, simple message.");
        var resp = await ClientFor("u").PostAsJsonAsync($"/api/posts/{postId}/reactions", new { type = "meh" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Feed_ReturnsPostsWithCandidateAndDisclaimer()
    {
        await _fixture.ResetMutableAsync();
        await InsertPostAsync("dana-okonkwo", "We build the future together.");
        await InsertPostAsync("marcus-reed", "Smaller government, bigger freedom.");

        var feed = await ClientFor("u").GetFromJsonAsync<CampaignFeedDto>("/api/campaign/feed");
        feed!.Items.Should().HaveCountGreaterOrEqualTo(2);
        feed.Items.Should().OnlyContain(p => p.Candidate != null && p.Candidate!.IsFictional);
    }

    [Fact]
    public async Task Feed_SortTop_OrdersByUpvotes()
    {
        await _fixture.ResetMutableAsync();
        await InsertPostAsync("dana-okonkwo", "Low scoring post here.", up: 1);
        await InsertPostAsync("marcus-reed", "High scoring post here.", up: 50);

        var feed = await ClientFor("u").GetFromJsonAsync<CampaignFeedDto>("/api/campaign/feed?sort=top");
        feed!.Items.First().Up.Should().Be(50);
    }

    [Fact]
    public async Task Feed_FilterByTone()
    {
        await _fixture.ResetMutableAsync();
        await InsertPostAsync("dana-okonkwo", "A hopeful message of unity.", tone: "Hopeful");
        await InsertPostAsync("marcus-reed", "A stern warning about power.", tone: "Stern");

        var feed = await ClientFor("u").GetFromJsonAsync<CampaignFeedDto>("/api/campaign/feed?tone=Hopeful");
        feed!.Items.Should().OnlyContain(p => p.Tone == "Hopeful");
        feed.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BriefingCandidateReactions_ReturnsPostsForBriefing()
    {
        await _fixture.ResetMutableAsync();

        // Attach a post to a seeded briefing slug.
        string briefingSlug;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var briefing = await db.Briefings.FirstAsync();
            briefingSlug = briefing.Slug;
            var candidate = await db.VirtualCandidates.FirstAsync(c => c.Slug == "sofia-alvarez");
            db.CampaignPosts.Add(new CampaignPost
            {
                Id = Guid.NewGuid(),
                CandidateId = candidate.Id,
                Body = "This story is exactly why we need student-data guardrails.",
                Tone = CampaignTone.Stern,
                Intensity = 3,
                Trigger = PostTrigger.Briefing,
                TriggerBriefingSlug = briefingSlug,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var posts = await ClientFor("u")
            .GetFromJsonAsync<List<CampaignPostDto>>($"/api/briefings/{briefingSlug}/candidate-reactions");
        posts!.Should().ContainSingle();
        posts[0].TriggerBriefingSlug.Should().Be(briefingSlug);
    }
}
