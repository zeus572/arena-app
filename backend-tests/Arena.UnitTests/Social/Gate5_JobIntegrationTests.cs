using Arena.Shared.Social;
using Arena.Shared.Social;
using Arena.Shared.Social.Platforms;
using FluentAssertions;
using Xunit;
using static Arena.UnitTests.Social.SocialPublisherHarness;

namespace Arena.UnitTests.Social;

/// <summary>
/// Gate 5 (SocialPublisher_Spec §9, Phase 5): job integration + review queue against the REAL
/// SocialPublisher and a fake IPlatformClient, on a real EF (SQLite) context.
/// </summary>
public class Gate5_JobIntegrationTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    [Fact]
    public async Task Review_items_await_review_and_auto_items_publish_once()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var selector = new FakeSelector
        {
            Candidates =
            {
                Cand(SocialContentType.DebateHighlight, A, "AUTO", 0.9, review: false),
                Cand(SocialContentType.DebateHighlight, B, "REVIEW", 0.5, review: true),
            }
        };
        var client = new FakePlatformClient();
        var (pub, _, clock, _) = Build(db, selector, new SocialPublisherOptions(), client);

        await pub.RunOnceAsync(clock.Now, default);

        ByContent(db, A)!.Status.Should().Be(SocialPostStatus.Published);
        ByContent(db, A)!.PlatformPostId.Should().NotBeNull();
        ByContent(db, B)!.Status.Should().Be(SocialPostStatus.AwaitingReview);

        client.CallCount.Should().Be(1);
        client.PublishedTexts.Should().ContainSingle().Which.Should().Be("AUTO");
    }

    [Fact]
    public async Task Daily_cap_halts_further_publishes()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var options = new SocialPublisherOptions { BlueskyDailyCap = 1, MaxPostsPerTick = 5 };
        var selector = new FakeSelector
        {
            Candidates =
            {
                Cand(SocialContentType.DebateHighlight, A, "FIRST", 0.9, review: false),
                Cand(SocialContentType.DebateHighlight, B, "SECOND", 0.8, review: false),
            }
        };
        var client = new FakePlatformClient();
        var (pub, _, clock, _) = Build(db, selector, options, client);

        await pub.RunOnceAsync(clock.Now, default);

        client.CallCount.Should().Be(1, "daily cap of 1 stops the second publish");
        ByContent(db, A)!.Status.Should().Be(SocialPostStatus.Published);
        ByContent(db, B)!.Status.Should().Be(SocialPostStatus.Pending, "deferred, not failed");
    }

    [Fact]
    public async Task Forced_adapter_failure_marks_failed_and_does_not_block_others()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var selector = new FakeSelector
        {
            Candidates =
            {
                Cand(SocialContentType.DebateHighlight, A, "BAD", 0.9, review: false),
                Cand(SocialContentType.DebateHighlight, B, "GOOD", 0.8, review: false),
            }
        };
        var client = new FakePlatformClient
        {
            Responder = p => p.Text == "BAD"
                ? PublishResult.Fail(SocialErrorCodes.ContentRejected, "policy")
                : PublishResult.Ok("at://ok"),
        };
        var (pub, _, clock, _) = Build(db, selector, new SocialPublisherOptions(), client);

        await pub.RunOnceAsync(clock.Now, default);

        var bad = ByContent(db, A)!;
        bad.Status.Should().Be(SocialPostStatus.Failed);
        bad.ErrorCode.Should().Be(SocialErrorCodes.ContentRejected);
        ByContent(db, B)!.Status.Should().Be(SocialPostStatus.Published, "one failure must not block others");
        client.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Custom_card_body_drives_the_image_and_alt_text_not_the_post_copy()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var selector = new FakeSelector
        {
            Candidates =
            {
                new PostCandidate
                {
                    ContentType = SocialContentType.CivicOpenBill,
                    ContentId = A,
                    Platform = "bluesky",
                    Text = "Help bridge this one before it closes: Grid fee\nhttps://civersify.com/coalition/x",
                    PostScore = 0.9,
                    Priority = 3,
                    RequiresReview = false,
                    Card = new CardModel("Open Bill", "Grid fee", "Civersify"),
                    CardBody = "Which facilities are covered?\n\nAll facilities\n— or —\nOnly large ones",
                },
            }
        };
        SocialPostPayload? captured = null;
        var client = new FakePlatformClient { Responder = p => { captured = p; return PublishResult.Ok("at://ok"); } };
        var (pub, _, clock, _) = Build(db, selector, new SocialPublisherOptions(), client);

        await pub.RunOnceAsync(clock.Now, default);

        captured.Should().NotBeNull();
        captured!.Text.Should().StartWith("Help bridge", "the post copy is unchanged");
        captured.ImagePng.Should().NotBeNullOrEmpty("the card image is still rendered");
        captured.AltText.Should().Contain("Which facilities are covered",
            "the alt text describes the card body, not the post copy");
        captured.AltText.Should().NotContain("Help bridge");
        db.SocialPosts.Single().CardBody.Should().Contain("Which facilities are covered");
    }

    [Fact]
    public async Task Approve_endpoint_transitions_and_next_tick_publishes()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var selector = new FakeSelector
        {
            Candidates = { Cand(SocialContentType.DebateHighlight, A, "NEEDS-REVIEW", 0.5, review: true) }
        };
        var client = new FakePlatformClient();
        var (pub, _, clock, _) = Build(db, selector, new SocialPublisherOptions(), client);

        // Tick 1: lands in review, not published.
        await pub.RunOnceAsync(clock.Now, default);
        var post = ByContent(db, A)!;
        post.Status.Should().Be(SocialPostStatus.AwaitingReview);
        client.CallCount.Should().Be(0);

        // Approve via the review service (backs the POST /approve endpoint).
        var review = new SocialReviewService(new EfSocialPostStore(db), clock);
        (await review.ApproveAsync(post.Id, "sam", default)).Should().BeTrue();
        ByContent(db, A)!.Status.Should().Be(SocialPostStatus.Approved);

        // Tick 2: no new candidates; the approved post publishes.
        selector.Candidates.Clear();
        await pub.RunOnceAsync(clock.Now, default);

        ByContent(db, A)!.Status.Should().Be(SocialPostStatus.Published);
        client.CallCount.Should().Be(1);
    }
}
