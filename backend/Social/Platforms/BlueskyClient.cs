using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Arena.API.Social.Platforms;

/// <summary>Bluesky credentials/config (§7 secrets). Never hardcoded, never logged.</summary>
public sealed class BlueskyOptions
{
    public const string SectionName = "Bluesky";
    public string Service { get; set; } = "https://bsky.social";
    public string? Handle { get; set; }
    public string? AppPassword { get; set; }
}

/// <summary>
/// Bluesky adapter over the AT Protocol (SocialPublisher_Spec §4.1). The ONLY platform adapter at
/// launch. Auth: app password → session JWT (createSession), refreshed on expiry. Post via
/// createRecord; image via uploadBlob embed. Grapheme-aware length; deterministic link facets.
///
/// Expected failures (length, auth, rate-limit, 5xx, network) return a <see cref="PublishResult"/>
/// with an ErrorCode — they never throw across the boundary (§4.3). Only truly unexpected
/// exceptions bubble up to the job's per-candidate try/catch.
///
/// // Deferred: XClient — see §4.2 (no stub, no config, no credentials this build).
/// </summary>
public sealed class BlueskyClient : IPlatformClient
{
    private readonly HttpClient _http;
    private readonly BlueskyOptions _options;
    private readonly SocialPublisherOptions _publisherOptions;
    private readonly ILogger<BlueskyClient> _logger;

    private string? _accessJwt;
    private string? _refreshJwt;
    private RateLimitStatus _lastRateLimit = RateLimitStatus.Available;

    public string PlatformKey => "bluesky";

    public BlueskyClient(
        HttpClient http,
        IOptions<BlueskyOptions> options,
        SocialPublisherOptions publisherOptions,
        ILogger<BlueskyClient> logger)
    {
        _http = http;
        _options = options.Value;
        _publisherOptions = publisherOptions;
        _logger = logger;
        if (_http.BaseAddress is null && !string.IsNullOrWhiteSpace(_options.Service))
            _http.BaseAddress = new Uri(_options.Service);
    }

    public RateLimitStatus GetRateLimitStatus() => _lastRateLimit;

    public async Task<PublishResult> PublishAsync(SocialPostPayload payload, CancellationToken ct)
    {
        // Re-validate length at the boundary (§4.3): reject, never silently truncate.
        if (BlueskyText.ExceedsGraphemeLimit(payload.Text, _publisherOptions.BlueskyMaxGraphemes))
            return PublishResult.Fail(SocialErrorCodes.LengthExceeded,
                $"Text exceeds {_publisherOptions.BlueskyMaxGraphemes} graphemes.");

        if (string.IsNullOrWhiteSpace(_options.Handle) || string.IsNullOrWhiteSpace(_options.AppPassword))
            return PublishResult.Fail(SocialErrorCodes.AuthMissing, "Bluesky credentials not configured.");

        try
        {
            if (_accessJwt is null)
            {
                var created = await CreateSessionAsync(ct);
                if (created is not null) return created; // auth failure
            }

            // Optional image: upload blob, attach as embed with alt text.
            JsonElement? blob = null;
            if (payload.ImagePng is { Length: > 0 })
            {
                var (uploaded, blobErr) = await UploadBlobAsync(payload.ImagePng, ct);
                if (blobErr is not null) return blobErr;
                blob = uploaded;
            }

            var record = BuildPostRecord(payload, blob);
            return await CreateRecordAsync(record, ct, allowRefresh: true);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return PublishResult.Fail(SocialErrorCodes.Timeout, "Bluesky request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return PublishResult.Fail(SocialErrorCodes.Network, ex.Message);
        }
    }

    // --- Session lifecycle -------------------------------------------------

    /// <summary>Returns a failed PublishResult on auth error, or null on success.</summary>
    private async Task<PublishResult?> CreateSessionAsync(CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync("/xrpc/com.atproto.server.createSession",
            new { identifier = _options.Handle, password = _options.AppPassword }, ct);
        CaptureRateLimit(resp);

        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            _logger.LogWarning("Bluesky credentials invalid — skipping Bluesky, other platforms unaffected.");
            return PublishResult.Fail(SocialErrorCodes.AuthInvalid, "createSession rejected credentials.");
        }
        if (!resp.IsSuccessStatusCode)
            return PublishResult.Fail(MapStatus(resp.StatusCode), $"createSession HTTP {(int)resp.StatusCode}");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        _accessJwt = doc.RootElement.GetProperty("accessJwt").GetString();
        _refreshJwt = doc.RootElement.GetProperty("refreshJwt").GetString();
        return null;
    }

    private async Task<bool> RefreshSessionAsync(CancellationToken ct)
    {
        if (_refreshJwt is null) return false;
        using var req = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.refreshSession");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _refreshJwt);
        using var resp = await _http.SendAsync(req, ct);
        CaptureRateLimit(resp);
        if (!resp.IsSuccessStatusCode) return false;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        _accessJwt = doc.RootElement.GetProperty("accessJwt").GetString();
        _refreshJwt = doc.RootElement.GetProperty("refreshJwt").GetString();
        return true;
    }

    // --- Posting -----------------------------------------------------------

    private object BuildPostRecord(SocialPostPayload payload, JsonElement? blob)
    {
        var facets = BlueskyText.ComputeFacets(payload.Text, payload.Links)
            .Select(f => new
            {
                index = new { byteStart = f.ByteStart, byteEnd = f.ByteEnd },
                features = new[] { new Dictionary<string, object>
                {
                    ["$type"] = "app.bsky.richtext.facet#link",
                    ["uri"] = f.Uri,
                } },
            })
            .ToArray();

        var record = new Dictionary<string, object>
        {
            ["$type"] = "app.bsky.feed.post",
            ["text"] = payload.Text,
            ["createdAt"] = DateTimeOffset.UtcNow.ToString("o"),
        };
        if (facets.Length > 0) record["facets"] = facets;

        if (blob is { } b)
        {
            record["embed"] = new Dictionary<string, object>
            {
                ["$type"] = "app.bsky.embed.images",
                ["images"] = new[] { new Dictionary<string, object>
                {
                    ["alt"] = payload.AltText ?? string.Empty,
                    ["image"] = JsonSerializer.Deserialize<object>(b.GetRawText())!,
                } },
            };
        }
        return record;
    }

    private async Task<(JsonElement? blob, PublishResult? error)> UploadBlobAsync(byte[] png, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.uploadBlob");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessJwt);
        req.Content = new ByteArrayContent(png);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var resp = await _http.SendAsync(req, ct);
        CaptureRateLimit(resp);
        if (!resp.IsSuccessStatusCode)
            return (null, PublishResult.Fail(MapStatus(resp.StatusCode), $"uploadBlob HTTP {(int)resp.StatusCode}"));

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return (doc.RootElement.GetProperty("blob").Clone(), null);
    }

    private async Task<PublishResult> CreateRecordAsync(object record, CancellationToken ct, bool allowRefresh)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessJwt);
        req.Content = JsonContent.Create(new
        {
            repo = _options.Handle,
            collection = "app.bsky.feed.post",
            record,
        });
        using var resp = await _http.SendAsync(req, ct);
        CaptureRateLimit(resp);

        if (resp.StatusCode == HttpStatusCode.Unauthorized && allowRefresh)
        {
            // Session likely expired — refresh once and retry.
            if (await RefreshSessionAsync(ct))
                return await CreateRecordAsync(record, ct, allowRefresh: false);
            _accessJwt = null;
            return PublishResult.Fail(SocialErrorCodes.AuthInvalid, "Session expired and refresh failed.");
        }

        if (!resp.IsSuccessStatusCode)
            return PublishResult.Fail(MapStatus(resp.StatusCode), $"createRecord HTTP {(int)resp.StatusCode}");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var uri = doc.RootElement.TryGetProperty("uri", out var u) ? u.GetString() : null;
        return uri is null
            ? PublishResult.Fail(SocialErrorCodes.Malformed, "createRecord returned no uri.")
            : PublishResult.Ok(uri);
    }

    // --- Rate limit + status mapping --------------------------------------

    private static string MapStatus(HttpStatusCode status) => (int)status switch
    {
        429 => SocialErrorCodes.RateLimited,
        >= 500 => SocialErrorCodes.Upstream5xx,
        401 or 403 => SocialErrorCodes.AuthInvalid,
        400 => SocialErrorCodes.ContentRejected,
        _ => SocialErrorCodes.Network,
    };

    private void CaptureRateLimit(HttpResponseMessage resp)
    {
        int? remaining = null;
        DateTimeOffset? reset = null;
        if (resp.Headers.TryGetValues("ratelimit-remaining", out var rem)
            && int.TryParse(rem.FirstOrDefault(), out var r)) remaining = r;
        if (resp.Headers.TryGetValues("ratelimit-reset", out var rst)
            && long.TryParse(rst.FirstOrDefault(), out var epoch))
            reset = DateTimeOffset.FromUnixTimeSeconds(epoch);

        var exhausted = resp.StatusCode == HttpStatusCode.TooManyRequests || remaining is <= 0;
        _lastRateLimit = new RateLimitStatus(exhausted, remaining, reset);
    }
}
