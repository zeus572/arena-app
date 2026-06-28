using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Models.Social;
using Arena.API.Social;
using Arena.API.Social.Selection;
using FluentAssertions;
using Xunit;

namespace Arena.UnitTests.Social;

/// <summary>
/// Gate 2 (part B): golden-value selection tests. Fixed candidate fixtures with known ranking
/// scores and thresholds → exact ordering and exact requiresReview flags. Uses the REAL fallback
/// <see cref="CoalitionSignalProvider"/> and a pure <see cref="FakeRankingScoreProvider"/> (no LLM).
/// </summary>
public class Gate2_HighlightSelectorTests
{
    // Ids fixed so the test is fully deterministic.
    private static readonly Guid D1 = Guid.Parse("00000000-0000-0000-0000-0000000000d1"); // coalition (common_ground, celebrity)
    private static readonly Guid D2 = Guid.Parse("00000000-0000-0000-0000-0000000000d2"); // debate below engagement min
    private static readonly Guid D3 = Guid.Parse("00000000-0000-0000-0000-0000000000d3"); // already posted
    private static readonly Guid D4 = Guid.Parse("00000000-0000-0000-0000-0000000000d4"); // auto-publishable debate

    private static Agent Agent(string name, string type, double trait) => new()
    {
        Id = Guid.NewGuid(), Name = name, AgentType = type,
        Aggressiveness = trait, Eloquence = trait, FactReliance = trait, Empathy = trait, Wit = trait,
    };

    private static void Seed(ArenaDbContext db)
    {
        var cel1 = Agent("Cel One", "celebrity", 2); // → position 0.2 on every axis
        var cel2 = Agent("Cel Two", "celebrity", 8); // → position 0.8 on every axis  (breadth 0.6, bipartisan)
        var syn1 = Agent("Syn One", "original", 5);
        var syn2 = Agent("Syn Two", "original", 5);
        db.Agents.AddRange(cel1, cel2, syn1, syn2);

        Debate Deb(Guid id, string format, Agent p, Agent o) => new()
        {
            Id = id, Topic = $"Topic {id.ToString()[^2..]}", Format = format,
            Status = DebateStatus.Completed, ProponentId = p.Id, OpponentId = o.Id,
            CreatedAt = DateTime.UtcNow.AddHours(-2), UpdatedAt = DateTime.UtcNow.AddHours(-1),
        };

        db.Debates.AddRange(
            Deb(D1, "common_ground", cel1, cel2),
            Deb(D2, "standard", syn1, syn2),
            Deb(D3, "standard", syn1, syn2),
            Deb(D4, "standard", syn1, syn2));

        // D3 already posted → must be excluded by dedup.
        db.SocialPosts.Add(new SocialPost
        {
            ContentType = SocialContentType.DebateHighlight, ContentId = D3,
            Platform = "bluesky", Status = SocialPostStatus.Published, Text = "old",
        });
        db.SaveChanges();
    }

    private static (HighlightSelector selector, FakeRankingScoreProvider ranking) BuildSelector(ArenaDbContext db)
    {
        var options = new SocialPublisherOptions();
        var ranking = new FakeRankingScoreProvider()
            .Add(D1, new RankingScore(5, 8, 7, 5, 6, 5, 5, 0))   // coalition: PostScore = .4*.8+.3*.7+.2*.6+.1*.5 = 0.70
            .Add(D2, new RankingScore(5, 5, 3, 5, 5, 5, 5, 0))   // engagement 0.3 < 0.5 → excluded
            .Add(D3, new RankingScore(5, 5, 8, 5, 5, 5, 5, 0))   // would qualify, but dedup excludes
            .Add(D4, new RankingScore(5, 9, 8, 5, 7, 6, 5, 0));  // debate: PostScore = .4*.9+.3*.8+.2*.7+.1*.6 = 0.80

        var coalition = new CoalitionSignalProvider(db, options);
        var features = new FakeFeaturePostProvider().Add(
            new FeaturePostSeed(Guid.NewGuid(), "Civersify is live", Array.Empty<string>(), null));
        var registry = new FakePlatformRegistry("bluesky");

        return (new HighlightSelector(db, ranking, coalition, features, registry, options), ranking);
    }

    [Fact]
    public void Selects_exact_ordering_and_review_flags()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        Seed(db);
        var (selector, _) = BuildSelector(db);

        var result = selector.SelectCandidates(DateTimeOffset.UtcNow);

        // Exactly three survive: coalition D1, debate D4, the feature post. D2 (low engagement)
        // and D3 (already posted) are excluded.
        result.Should().HaveCount(3);

        result[0].ContentType.Should().Be(SocialContentType.CoalitionHighlight);
        result[0].ContentId.Should().Be(D1);
        result[0].Priority.Should().Be(1);
        result[0].PostScore.Should().BeApproximately(0.70, 1e-9);
        result[0].RequiresReview.Should().BeTrue("coalition involves real (celebrity) figures");

        result[1].ContentType.Should().Be(SocialContentType.DebateHighlight);
        result[1].ContentId.Should().Be(D4);
        result[1].Priority.Should().Be(3);
        result[1].PostScore.Should().BeApproximately(0.80, 1e-9);
        result[1].RequiresReview.Should().BeFalse("score 0.80 ≥ AutoPublishMin and agents are synthetic");

        result[2].ContentType.Should().Be(SocialContentType.FeaturePost);
        result[2].ContentId.Should().BeNull();
        result[2].Priority.Should().Be(4);
        result[2].PostScore.Should().BeApproximately(0.40, 1e-9, "FeaturePostBaseScore");
        result[2].RequiresReview.Should().BeTrue("0.40 < AutoPublishMin 0.65 → review queue");
    }

    [Fact]
    public void Debate_below_engagement_min_is_excluded()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        Seed(db);
        var (selector, _) = BuildSelector(db);

        var result = selector.SelectCandidates(DateTimeOffset.UtcNow);
        result.Should().NotContain(c => c.ContentId == D2);
    }

    [Fact]
    public void Already_posted_content_is_excluded_via_dedup()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        Seed(db);
        var (selector, _) = BuildSelector(db);

        var result = selector.SelectCandidates(DateTimeOffset.UtcNow);
        result.Should().NotContain(c => c.ContentId == D3);
    }

    [Fact]
    public void Selector_has_no_llm_dependency()
    {
        // Structural proof the scoring/selection path cannot make a model call: none of the
        // selector's constructor dependencies is an LLM service.
        var paramTypes = typeof(HighlightSelector)
            .GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType.Name);
        paramTypes.Should().NotContain(n => n.Contains("Llm", StringComparison.OrdinalIgnoreCase));
    }
}
