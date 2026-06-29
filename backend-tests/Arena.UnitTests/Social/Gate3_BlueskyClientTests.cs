using System.Net;
using Arena.Shared.Social;
using Arena.Shared.Social.Platforms;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Arena.UnitTests.Social;

/// <summary>
/// Gate 3 (SocialPublisher_Spec §9, Phase 3): Bluesky adapter, no live network.
/// NOTE: live publish remains UNVERIFIED until the Phase 7 manual smoke test with real credentials.
/// </summary>
public class Gate3_BlueskyClientTests
{
    private const string Session = "com.atproto.server.createSession";
    private const string Refresh = "com.atproto.server.refreshSession";
    private const string CreateRecord = "com.atproto.repo.createRecord";

    private static BlueskyClient Build(StubHttpHandler handler, out SocialPublisherOptions opts,
        string? handle = "bot.bsky.social", string? appPassword = "app-pass-1234")
    {
        opts = new SocialPublisherOptions();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local") };
        var bsky = Options.Create(new BlueskyOptions { Service = "https://stub.local", Handle = handle, AppPassword = appPassword });
        return new BlueskyClient(http, bsky, opts, NullLogger<BlueskyClient>.Instance);
    }

    // ---- Length validation (grapheme-aware) ----

    [Fact]
    public void CountGraphemes_treats_zwj_emoji_as_one_grapheme()
    {
        const string family = "\U0001F468‍\U0001F469‍\U0001F467"; // 👨‍👩‍👧
        family.Length.Should().BeGreaterThan(2, "UTF-16 length over-counts");
        BlueskyText.CountGraphemes(family).Should().Be(1, "it is a single grapheme cluster");
    }

    [Fact]
    public async Task PublishAsync_rejects_over_limit_text_with_LENGTH_EXCEEDED()
    {
        var handler = new StubHttpHandler((_, _) => (HttpStatusCode.OK, "{}"));
        var client = Build(handler, out var opts);
        var text = new string('a', opts.BlueskyMaxGraphemes + 1);

        var result = await client.PublishAsync(new SocialPostPayload(text, null, null, Array.Empty<string>()), default);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(SocialErrorCodes.LengthExceeded);
        handler.Calls.Should().BeEmpty("length is rejected before any network call");
    }

    [Fact]
    public async Task PublishAsync_accepts_300_grapheme_emoji_string_despite_huge_UTF16_length()
    {
        // 300 ZWJ-emoji graphemes: grapheme count == limit (ok) but .Length is far larger.
        var family = "\U0001F468‍\U0001F469‍\U0001F467";
        var text = string.Concat(Enumerable.Repeat(family, 300));
        BlueskyText.CountGraphemes(text).Should().Be(300);

        // No credentials → proves length PASSED (we reach the auth check, not LENGTH_EXCEEDED).
        var handler = new StubHttpHandler((_, _) => (HttpStatusCode.OK, "{}"));
        var client = Build(handler, out _, handle: null, appPassword: null);

        var result = await client.PublishAsync(new SocialPostPayload(text, null, null, Array.Empty<string>()), default);
        result.ErrorCode.Should().Be(SocialErrorCodes.AuthMissing);
    }

    // ---- Facet byte ranges (golden) ----

    [Fact]
    public void ComputeFacets_uses_utf8_byte_offsets_not_char_index()
    {
        // "héllo " — the é is 2 UTF-8 bytes, so the link starts at byte 7 though char index is 6.
        const string text = "héllo https://x.com end";
        var facets = BlueskyText.ComputeFacets(text, new[] { "https://x.com" });

        facets.Should().HaveCount(1);
        facets[0].ByteStart.Should().Be(7);
        facets[0].ByteEnd.Should().Be(20); // 7 + 13 bytes
        facets[0].Uri.Should().Be("https://x.com");
    }

    // ---- Session / refresh against stubbed HTTP ----

    [Fact]
    public async Task PublishAsync_creates_session_then_posts()
    {
        var handler = new StubHttpHandler((method, _) => method switch
        {
            Session => (HttpStatusCode.OK, """{"accessJwt":"acc","refreshJwt":"ref"}"""),
            CreateRecord => (HttpStatusCode.OK, """{"uri":"at://did:plc:abc/app.bsky.feed.post/xyz","cid":"c1"}"""),
            _ => (HttpStatusCode.OK, "{}"),
        });
        var client = Build(handler, out _);

        var result = await client.PublishAsync(new SocialPostPayload("hello world", null, null, Array.Empty<string>()), default);

        result.Success.Should().BeTrue();
        result.PlatformPostId.Should().Be("at://did:plc:abc/app.bsky.feed.post/xyz");
        handler.CountFor(Session).Should().Be(1);
        handler.CountFor(CreateRecord).Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_refreshes_session_on_401_then_retries()
    {
        var handler = new StubHttpHandler((method, callIndex) => method switch
        {
            Session => (HttpStatusCode.OK, """{"accessJwt":"acc1","refreshJwt":"ref1"}"""),
            // First createRecord → 401 (expired); after refresh, second → 200.
            CreateRecord when callIndex == 0 => (HttpStatusCode.Unauthorized, """{"error":"ExpiredToken"}"""),
            CreateRecord => (HttpStatusCode.OK, """{"uri":"at://did/app.bsky.feed.post/ok"}"""),
            Refresh => (HttpStatusCode.OK, """{"accessJwt":"acc2","refreshJwt":"ref2"}"""),
            _ => (HttpStatusCode.OK, "{}"),
        });
        var client = Build(handler, out _);

        var result = await client.PublishAsync(new SocialPostPayload("hi", null, null, Array.Empty<string>()), default);

        result.Success.Should().BeTrue();
        result.PlatformPostId.Should().Be("at://did/app.bsky.feed.post/ok");
        handler.CountFor(Refresh).Should().Be(1);
        handler.CountFor(CreateRecord).Should().Be(2, "one failed (401) then one succeeded after refresh");
    }

    [Fact]
    public async Task PublishAsync_returns_AUTH_INVALID_when_credentials_rejected()
    {
        var handler = new StubHttpHandler((method, _) => method switch
        {
            Session => (HttpStatusCode.Unauthorized, """{"error":"AuthenticationRequired"}"""),
            _ => (HttpStatusCode.OK, "{}"),
        });
        var client = Build(handler, out _);

        var result = await client.PublishAsync(new SocialPostPayload("hi", null, null, Array.Empty<string>()), default);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(SocialErrorCodes.AuthInvalid);
        handler.CountFor(CreateRecord).Should().Be(0);
    }

    [Fact]
    public async Task PublishAsync_maps_429_to_RATE_LIMITED()
    {
        var handler = new StubHttpHandler((method, _) => method switch
        {
            Session => (HttpStatusCode.OK, """{"accessJwt":"a","refreshJwt":"r"}"""),
            CreateRecord => (HttpStatusCode.TooManyRequests, """{"error":"RateLimitExceeded"}"""),
            _ => (HttpStatusCode.OK, "{}"),
        });
        var client = Build(handler, out _);

        var result = await client.PublishAsync(new SocialPostPayload("hi", null, null, Array.Empty<string>()), default);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(SocialErrorCodes.RateLimited);
        SocialErrorCodes.IsRetryable(result.ErrorCode).Should().BeTrue();
    }
}
