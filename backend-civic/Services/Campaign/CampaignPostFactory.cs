using System.Text.Json;
using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Shared pipeline for the two things both the Campaign Manager and league rounds need: producing a
/// candidate's strategic response options for a briefing (LLM-backed, lazily cached, with a
/// deterministic templated fallback), and turning a chosen body into a published
/// <see cref="CampaignPost"/>. Extracted from <see cref="CivicCampaignService"/> so leagues reuse the
/// exact same generation + post-creation behavior.
/// </summary>
public interface ICampaignPostFactory
{
    Task<List<NewsResponseOption>> GetOrCreateResponseOptionsAsync(
        VirtualCandidate candidate, Briefing briefing, CancellationToken ct = default);

    Task<CampaignPost> CreatePostFromBodyAsync(
        VirtualCandidate candidate, string body, CampaignTone tone, Briefing briefing,
        string ownerUserId, Guid? campaignId, CancellationToken ct = default);
}

public class CampaignPostFactory : ICampaignPostFactory
{
    private readonly CivicDbContext _db;
    private readonly ILlmClient _llm;
    private readonly CivicCampaignOptions _opts;
    private readonly ILogger<CampaignPostFactory> _log;

    private static readonly JsonSerializerOptions Json = new();

    public CampaignPostFactory(
        CivicDbContext db,
        ILlmClient llm,
        IOptions<CivicCampaignOptions> opts,
        ILogger<CampaignPostFactory> log)
    {
        _db = db;
        _llm = llm;
        _opts = opts.Value;
        _log = log;
    }

    // ---------------------------------------------------------------- News options (lazy + cached)

    public async Task<List<NewsResponseOption>> GetOrCreateResponseOptionsAsync(
        VirtualCandidate candidate, Briefing briefing, CancellationToken ct = default)
    {
        var existing = await _db.CandidateNewsResponses
            .FirstOrDefaultAsync(r => r.CandidateId == candidate.Id && r.BriefingSlug == briefing.Slug, ct);
        if (existing is not null)
        {
            var cached = SafeDeserializeOptions(existing.OptionsJson);
            // Reuse only if non-empty AND produced by the current prompt version — otherwise
            // regenerate so prompt improvements take effect without a manual cache wipe.
            if (cached.Count > 0 && existing.PromptVersion >= NewsResponsePrompts.Version)
                return cached;
        }

        var (options, llmGenerated) = await GenerateResponseOptionsAsync(candidate, briefing, ct);

        // Cache for reuse across views/players. Tolerate a race on the unique index.
        try
        {
            if (existing is null)
            {
                _db.CandidateNewsResponses.Add(new CandidateNewsResponse
                {
                    Id = Guid.NewGuid(),
                    CandidateId = candidate.Id,
                    BriefingSlug = briefing.Slug,
                    OptionsJson = JsonSerializer.Serialize(options, Json),
                    LlmGenerated = llmGenerated,
                    PromptVersion = NewsResponsePrompts.Version,
                });
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                existing.OptionsJson = JsonSerializer.Serialize(options, Json);
                existing.LlmGenerated = llmGenerated;
                existing.PromptVersion = NewsResponsePrompts.Version;
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (DbUpdateException)
        {
            // Another request cached it first; load theirs.
            _db.ChangeTracker.Clear();
            var winner = await _db.CandidateNewsResponses
                .FirstOrDefaultAsync(r => r.CandidateId == candidate.Id && r.BriefingSlug == briefing.Slug, ct);
            if (winner is not null) return SafeDeserializeOptions(winner.OptionsJson);
        }

        return options;
    }

    private async Task<(List<NewsResponseOption>, bool)> GenerateResponseOptionsAsync(
        VirtualCandidate candidate, Briefing briefing, CancellationToken ct)
    {
        var count = Math.Clamp(_opts.ResponseOptionsPerItem, 2, 3);
        var maxChars = _opts.ResponseMaxChars;
        try
        {
            var (sys, user) = NewsResponsePrompts.Build(candidate, briefing, count, maxChars);
            // Headroom for 3 multi-sentence bodies; too low a cap truncates the JSON
            // itself and forces the templated fallback.
            var dto = await _llm.GenerateStructuredAsync<GeneratedNewsResponsesDto>(sys, user, LlmModelTier.Sonnet, maxTokens: 2200, ct: ct);
            var options = dto.Options
                .Where(o => !string.IsNullOrWhiteSpace(o.Body))
                .Take(count)
                .Select((o, i) => new NewsResponseOption
                {
                    Id = $"opt{i + 1}",
                    Label = string.IsNullOrWhiteSpace(o.Label) ? $"Option {i + 1}" : o.Label.Trim(),
                    Angle = o.Angle?.Trim() ?? "",
                    Tone = ParseTone(o.Tone)?.ToString() ?? candidate.DefaultTone.ToString(),
                    Body = Truncate(o.Body.Trim(), maxChars),
                })
                .ToList();
            if (options.Count >= 2) return (options, true);
            _log.LogInformation("LLM returned too few response options; using templated fallback.");
        }
        catch (LlmException ex)
        {
            _log.LogInformation("LLM unavailable ({Message}); using templated news-response fallback.", ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "News-response generation failed; using templated fallback.");
        }

        return (TemplatedResponseOptions(candidate, briefing, count, maxChars), false);
    }

    /// <summary>
    /// Deterministic, offline response options derived from the candidate's planks + the briefing.
    /// Multi-sentence and pointed (used when no LLM key is configured); capped at maxChars.
    /// </summary>
    private static List<NewsResponseOption> TemplatedResponseOptions(
        VirtualCandidate candidate, Briefing briefing, int count, int maxChars)
    {
        var first = candidate.Name.Split(' ').FirstOrDefault() ?? candidate.Name;
        var plank = candidate.PlatformPlanks.FirstOrDefault();
        var plankTitle = plank?.Title ?? "real reform";
        var topic = briefing.Tags.FirstOrDefault()
            ?? briefing.ValuesInConflict.FirstOrDefault()
            ?? "this issue";
        var value = briefing.ValuesInConflict.FirstOrDefault() ?? topic;
        var headline = briefing.Headline;

        var templates = new List<NewsResponseOption>
        {
            new()
            {
                Id = "opt1",
                Label = "Go on offense",
                Angle = "Hit hard and draw the sharpest contrast on the candidate's terms.",
                Tone = CampaignTone.Angry.ToString(),
                Body = Truncate(
                    $"Let's be blunt about \"{headline}\": the people in charge have failed you on {topic}, and they're hoping you won't notice. {first} will. " +
                    $"That's exactly why I'm fighting for {plankTitle} — not someday, now. The status quo isn't neutral; it's a choice, and it's the wrong one.",
                    maxChars),
            },
            new()
            {
                Id = "opt2",
                Label = "Stay disciplined",
                Angle = "Acknowledge the story, then drive relentlessly back to the core message.",
                Tone = CampaignTone.Presidential.ToString(),
                Body = Truncate(
                    $"The headlines will keep changing. Our priorities won't. {first} is in this race to deliver {plankTitle} and protect {value} for the families who are counting on it. " +
                    $"I won't be distracted by the noise — I'll be judged by results. That's the promise, and I intend to keep it.",
                    maxChars),
            },
            new()
            {
                Id = "opt3",
                Label = "Find common ground",
                Angle = "Reframe the fight as a shared problem and offer a way forward.",
                Tone = CampaignTone.Hopeful.ToString(),
                Body = Truncate(
                    $"Here's what \"{headline}\" really shows: we agree on more than the shouting suggests. Almost everyone wants to protect {value} — we just argue about how. " +
                    $"{first}'s answer is {plankTitle}, and I'll work with anyone serious about getting it done. Let's stop scoring points and start solving this.",
                    maxChars),
            },
        };
        return templates.Take(Math.Clamp(count, 2, 3)).ToList();
    }

    // ---------------------------------------------------------------- Post creation

    public async Task<CampaignPost> CreatePostFromBodyAsync(
        VirtualCandidate candidate, string body, CampaignTone tone, Briefing briefing,
        string ownerUserId, Guid? campaignId, CancellationToken ct = default)
    {
        var clean = Truncate(string.IsNullOrWhiteSpace(body) ? $"{candidate.Name} responds." : body.Trim(), _opts.ResponseMaxChars);
        var post = new CampaignPost
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            Body = clean,
            Tone = tone,
            Intensity = candidate.DefaultIntensity,
            IssueTags = briefing.Tags.Take(3).ToArray(),
            Trigger = PostTrigger.Briefing,
            TriggerBriefingSlug = briefing.Slug,
            CitedReference = candidate.PlatformPlanks.FirstOrDefault()?.Title,
            // Attribute to the author so it shows only in THEIR tailored candidate feed (league round
            // feeds load posts explicitly by entry id, bypassing this tailoring).
            OwnerUserId = ownerUserId,
            CampaignId = campaignId,
            CreatedAt = DateTime.UtcNow,
        };
        // Split into clause-level fragments so the post supports fragment-level reactions.
        post.Fragments = FragmentSplitter.Split(clean)
            .Select(f => { f.PostId = post.Id; f.Id = Guid.NewGuid(); return f; })
            .ToList();

        _db.CampaignPosts.Add(post);
        await _db.SaveChangesAsync(ct);
        return post;
    }

    // ---------------------------------------------------------------- Helpers

    public static CampaignTone? ParseTone(string? s)
        => Enum.TryParse<CampaignTone>(s, ignoreCase: true, out var t) ? t : null;

    private static List<NewsResponseOption> SafeDeserializeOptions(string json)
    {
        try { return JsonSerializer.Deserialize<List<NewsResponseOption>>(json, Json) ?? new(); }
        catch { return new(); }
    }

    // Graceful truncation: never cut a response off mid-word or mid-sentence.
    // Prefer ending on a complete sentence within the limit; otherwise fall back
    // to the last whole word and add an ellipsis so it reads as a finished thought.
    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        var slice = s[..max];
        var lastSentence = slice.LastIndexOfAny(new[] { '.', '!', '?' });
        if (lastSentence >= max / 2)
            return slice[..(lastSentence + 1)].TrimEnd();
        var lastSpace = slice.LastIndexOf(' ');
        if (lastSpace >= max / 2)
            return slice[..lastSpace].TrimEnd() + "…";
        return slice.TrimEnd() + "…";
    }
}
