using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Campaign;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class CampaignGenerationApiTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;

    public CampaignGenerationApiTests(DatabaseFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetMutableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private CampaignPostGenerationService BuildSvc(StubLlmClient llm) =>
        new(
            _fx.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            llm,
            new TestOptionsMonitor<CampaignOptions>(new CampaignOptions
            {
                MaxPostsPerDay = 5,
                MaxPostsPerWindow = 2,
                CooldownWindowHours = 6,
                // Keep the intensity-5 daily cap out of the way so the window
                // cooldown is the only limit exercised by these tests.
                MaxIntensity5PerDay = 10,
                MaxCandidatesPerBriefing = 4,
            }),
            NullLogger<CampaignPostGenerationService>.Instance,
            _fx.Factory.Services.GetRequiredService<Civic.API.Services.StartupReadiness>());

    private async Task<Guid> CandidateIdAsync(string slug)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        return await db.VirtualCandidates.Where(c => c.Slug == slug).Select(c => c.Id).FirstAsync();
    }

    private async Task ClearPostsAsync(Guid candidateId)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var posts = await db.CampaignPosts.Where(p => p.CandidateId == candidateId).ToListAsync();
        db.CampaignPosts.RemoveRange(posts);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GenerateForCandidate_HappyPath_PersistsPostWithFragments()
    {
        var llm = new StubLlmClient().WithJson("GeneratedCampaignPostDto",
            "{\"body\":\"Test it before you ship it. Powerful AI needs guardrails, not guesswork.\",\"citedReference\":\"Safety Standards for Powerful AI\"}");
        var candidateId = await CandidateIdAsync("sofia-alvarez");

        var post = await BuildSvc(llm).GenerateForCandidateAsync(candidateId, null, PostTrigger.Platform, force: true);

        post.Should().NotBeNull();
        post!.Body.Length.Should().BeLessThanOrEqualTo(400);
        post.Fragments.Should().NotBeEmpty();
        post.CitedReference.Should().Be("Safety Standards for Powerful AI");
        post.Trigger.Should().Be(PostTrigger.Platform);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.CampaignPosts.Include(p => p.Fragments).SingleAsync(p => p.Id == post.Id))
            .Fragments.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateForCandidate_OverLimit_RepromptsThenTruncates()
    {
        var tooLong = new string('x', 50) + ". " + string.Join(" ", Enumerable.Repeat("policy", 90));
        var llm = new StubLlmClient().WithJson("GeneratedCampaignPostDto",
            $"{{\"body\":\"{tooLong}\",\"citedReference\":\"A Tax Code on a Postcard\"}}");
        var candidateId = await CandidateIdAsync("marcus-reed");

        var post = await BuildSvc(llm).GenerateForCandidateAsync(candidateId, null, PostTrigger.Platform, force: true);

        post.Should().NotBeNull();
        post!.Body.Length.Should().BeLessThanOrEqualTo(400);
        // Over-limit output triggers exactly one re-prompt before the hard truncate.
        llm.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateForCandidate_FallsBackToPlankTitleWhenModelOmitsCitation()
    {
        var llm = new StubLlmClient().WithJson("GeneratedCampaignPostDto",
            "{\"body\":\"Wire the last mile. A farm needs bandwidth like it needs rain.\"}");
        var candidateId = await CandidateIdAsync("hank-whitfield");

        var post = await BuildSvc(llm).GenerateForCandidateAsync(candidateId, null, PostTrigger.Platform, force: true);

        post.Should().NotBeNull();
        post!.CitedReference.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateForCandidate_RespectsCooldownWhenNotForced()
    {
        var llm = new StubLlmClient().WithJson("GeneratedCampaignPostDto",
            "{\"body\":\"A short, clear post.\",\"citedReference\":\"A Health Care Floor No One Falls Through\"}");
        var candidateId = await CandidateIdAsync("dana-okonkwo");
        await ClearPostsAsync(candidateId); // isolate from any prior test's posts
        var svc = BuildSvc(llm);

        // Two posts fill the per-window budget (MaxPostsPerWindow = 2)...
        (await svc.GenerateForCandidateAsync(candidateId, null, PostTrigger.Platform, force: false)).Should().NotBeNull();
        (await svc.GenerateForCandidateAsync(candidateId, null, PostTrigger.Platform, force: false)).Should().NotBeNull();
        // ...the third is suppressed by cooldown.
        (await svc.GenerateForCandidateAsync(candidateId, null, PostTrigger.Platform, force: false)).Should().BeNull();
    }
}
