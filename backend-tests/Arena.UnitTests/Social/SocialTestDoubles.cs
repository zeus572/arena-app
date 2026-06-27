using Arena.API.Social;
using Arena.API.Models.Social;

namespace Arena.UnitTests.Social;

/// <summary>
/// Pure, in-memory ranking-score provider. Returns ONLY pre-seeded scores — it makes no model
/// call and has no network. Used to assert the selection/scoring path is LLM-free (Gate 2).
/// </summary>
public sealed class FakeRankingScoreProvider : IRankingScoreProvider
{
    private readonly Dictionary<Guid, RankingScore> _scores = new();
    public int CallCount { get; private set; }

    public FakeRankingScoreProvider Add(Guid contentId, RankingScore score)
    {
        _scores[contentId] = score;
        return this;
    }

    public RankingScore? GetScore(SocialContentType type, Guid contentId)
    {
        CallCount++;
        return _scores.TryGetValue(contentId, out var s) ? s : null;
    }
}

public sealed class FakeFeaturePostProvider : IFeaturePostProvider
{
    private readonly List<FeaturePostSeed> _seeds = new();
    public FakeFeaturePostProvider Add(FeaturePostSeed seed) { _seeds.Add(seed); return this; }
    public IReadOnlyList<FeaturePostSeed> GetDueSeeds(DateTimeOffset now) => _seeds;
}

public sealed class TestClock : IClock
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public void Advance(TimeSpan by) => Now += by;
}

public sealed class FakeSelector : IHighlightSelector
{
    public List<PostCandidate> Candidates { get; set; } = new();
    public IReadOnlyList<PostCandidate> SelectCandidates(DateTimeOffset now) => Candidates;
}

/// <summary>Controllable platform client: records published texts, returns per-text canned results.</summary>
public sealed class FakePlatformClient : IPlatformClient
{
    public FakePlatformClient(string key = "bluesky") => PlatformKey = key;

    public string PlatformKey { get; }
    public List<string> PublishedTexts { get; } = new();
    public int CallCount { get; private set; }
    public RateLimitStatus RateLimit { get; set; } = RateLimitStatus.Available;

    /// <summary>If set, returns this result for the given text; otherwise success.</summary>
    public Func<SocialPostPayload, PublishResult>? Responder { get; set; }

    /// <summary>Optional artificial delay (for the time-box test).</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>If true, throws on every call (unexpected-exception path).</summary>
    public bool ThrowAlways { get; set; }

    public RateLimitStatus GetRateLimitStatus() => RateLimit;

    public async Task<PublishResult> PublishAsync(SocialPostPayload payload, CancellationToken ct)
    {
        CallCount++;
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
        if (ThrowAlways) throw new InvalidOperationException("boom");
        PublishedTexts.Add(payload.Text);
        return Responder?.Invoke(payload) ?? PublishResult.Ok($"at://posted/{CallCount}");
    }
}

/// <summary>An ISocialPublisher that always throws — for the heartbeat-swallow test (Gate 6.1).</summary>
public sealed class ThrowingPublisher : ISocialPublisher
{
    public int Calls { get; private set; }
    public Task RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        Calls++;
        throw new InvalidOperationException("publisher exploded");
    }
}

public sealed class FakePlatformRegistry : IPlatformClientRegistry
{
    private readonly Dictionary<string, IPlatformClient> _clients;
    public FakePlatformRegistry(params string[] keys)
        => _clients = keys.ToDictionary(k => k, k => (IPlatformClient)null!);
    public FakePlatformRegistry(IEnumerable<IPlatformClient> clients)
        => _clients = clients.ToDictionary(c => c.PlatformKey, c => c);

    public bool TryGet(string platformKey, out IPlatformClient client)
        => _clients.TryGetValue(platformKey, out client!) && client is not null;

    public IReadOnlyCollection<string> Keys => _clients.Keys.ToList();
}
