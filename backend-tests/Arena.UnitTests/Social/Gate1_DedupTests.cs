using Arena.Shared.Social;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Arena.UnitTests.Social;

/// <summary>
/// Gate 1 (SocialPublisher_Spec §9, Phase 1): the (ContentType, ContentId, Platform)
/// dedup index. Assertions run against a real EF context (SQLite) so the constraint
/// is genuinely enforced, not mocked.
/// </summary>
public class Gate1_DedupTests
{
    private static SocialPost Post(SocialContentType type, Guid? contentId, string platform) => new()
    {
        ContentType = type,
        ContentId = contentId,
        Platform = platform,
        Status = SocialPostStatus.Pending,
        Text = "hello",
    };

    [Fact]
    public async Task Duplicate_ContentType_ContentId_Platform_violates_unique_index()
    {
        using var db = new SqliteTestDb();
        var contentId = Guid.NewGuid();

        await using (var ctx = db.NewContext())
        {
            ctx.SocialPosts.Add(Post(SocialContentType.DebateHighlight, contentId, "bluesky"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            ctx.SocialPosts.Add(Post(SocialContentType.DebateHighlight, contentId, "bluesky"));
            var act = async () => await ctx.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>(
                "the same content cannot be posted twice to the same platform");
        }
    }

    [Fact]
    public async Task Same_content_different_platform_succeeds()
    {
        using var db = new SqliteTestDb();
        var contentId = Guid.NewGuid();

        await using var ctx = db.NewContext();
        ctx.SocialPosts.Add(Post(SocialContentType.DebateHighlight, contentId, "bluesky"));
        ctx.SocialPosts.Add(Post(SocialContentType.DebateHighlight, contentId, "fake-2"));

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("differing platform is a distinct dedup key");

        (await ctx.SocialPosts.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Null_ContentId_rows_are_exempt_from_uniqueness()
    {
        using var db = new SqliteTestDb();

        await using var ctx = db.NewContext();
        // Two FeaturePost seeds, both ContentId == null, same platform — must coexist.
        ctx.SocialPosts.Add(Post(SocialContentType.FeaturePost, null, "bluesky"));
        ctx.SocialPosts.Add(Post(SocialContentType.FeaturePost, null, "bluesky"));

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("the dedup index is filtered to ContentId IS NOT NULL");

        (await ctx.SocialPosts.CountAsync()).Should().Be(2);
    }
}
