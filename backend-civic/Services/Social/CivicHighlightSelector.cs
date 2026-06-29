using Arena.Shared.Social;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Social;

/// <summary>
/// Civic Arena's content source for the shared SocialPublisher engine. LLM-free and side-effect-free
/// (only reads civic content + the SocialPosts dedup table). Three sources, in priority order:
///   1. Bill outcomes  — a coalition Provision that recently Passed/Forked ("Common Ground").
///   2. Zeitgeist      — a provision with strong cross-spectrum co-signing (uses the prebuilt Signal).
///   3. Open bills     — the soonest-closing open bill, rate-limited to ~1/day, to drive participation.
/// Each (ContentType, ContentId, platform) posts at most once (dedup via SocialPosts).
/// </summary>
public sealed class CivicHighlightSelector : IHighlightSelector
{
    private readonly CivicDbContext _db;
    private readonly IZeitgeistService _zeitgeist;
    private readonly IPlatformClientRegistry _platforms;
    private readonly CivicSocialOptions _options;

    public CivicHighlightSelector(
        CivicDbContext db, IZeitgeistService zeitgeist, IPlatformClientRegistry platforms,
        CivicSocialOptions options)
    {
        _db = db;
        _zeitgeist = zeitgeist;
        _platforms = platforms;
        _options = options;
    }

    private static readonly ProvisionState[] ResolvedStates = { ProvisionState.Passed, ProvisionState.Forked };
    private static readonly ProvisionState[] OpenStates =
        { ProvisionState.Open, ProvisionState.Contested, ProvisionState.NearCoalition };

    public IReadOnlyList<PostCandidate> SelectCandidates(DateTimeOffset now)
    {
        var platformKeys = _platforms.Keys.Count > 0 ? _platforms.Keys.ToArray() : new[] { "bluesky" };
        var raw = new List<PostCandidate>();

        AddBillOutcomes(raw, platformKeys, now);
        AddZeitgeist(raw, platformKeys);
        AddOpenBill(raw, platformKeys, now);

        return ExcludeAlreadyPosted(raw);
    }

    // ---- Source 1: bill outcomes (priority 1) ----
    private void AddBillOutcomes(List<PostCandidate> sink, string[] platforms, DateTimeOffset now)
    {
        // Only recently-resolved bills (by deadline) so a first run doesn't replay the whole archive.
        var since = now.UtcDateTime.AddDays(-_options.OutcomeLookbackDays);
        var resolved = _db.Provisions.AsNoTracking()
            .Where(p => ResolvedStates.Contains(p.State) && p.Deadline != null && p.Deadline >= since)
            .OrderByDescending(p => p.Deadline)
            .Take(_options.MaxOutcomesPerTick)
            .ToList();

        foreach (var p in resolved)
        {
            var passed = p.State == ProvisionState.Passed;
            var verb = passed ? "Common ground found" : "A workable split emerged";
            var text = $"{verb}: {p.Title}\n{Url(p.Id)}";
            AddPerPlatform(sink, platforms, SocialContentType.CivicBillOutcome, p.Id, text,
                score: passed ? 1.0 : 0.8,
                card: new CardModel("Common Ground", p.Title, "Civersify"));
        }
    }

    // ---- Source 2: zeitgeist convergence (priority 2) ----
    private void AddZeitgeist(List<PostCandidate> sink, string[] platforms)
    {
        ZeitgeistDto z;
        try { z = _zeitgeist.BuildAsync().GetAwaiter().GetResult(); }
        catch { return; } // never let a zeitgeist hiccup break selection

        var strong = z.Coalitions
            .Where(c => c.Accepts >= _options.ZeitgeistMinAccepts && c.Accepts > c.Declines)
            .OrderByDescending(c => c.Accepts - c.Declines)
            .Take(_options.MaxZeitgeistPerTick);

        foreach (var c in strong)
        {
            var signal = string.IsNullOrWhiteSpace(c.Signal)
                ? $"{c.Accepts} co-signs across the spectrum on {c.Title}."
                : c.Signal;
            var text = $"{signal}\n{Url(c.ProvisionId)}";
            AddPerPlatform(sink, platforms, SocialContentType.CivicZeitgeist, c.ProvisionId, text,
                score: 0.9,
                card: new CardModel("The Zeitgeist", c.Title, "Civersify"));
        }
    }

    // ---- Source 3: open bill engagement (priority 3, ~1/day) ----
    private void AddOpenBill(List<PostCandidate> sink, string[] platforms, DateTimeOffset now)
    {
        // Rate-limit: at most one open-bill post per UTC day across all bills.
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var postedTodayCount = _db.SocialPosts.AsNoTracking()
            .Where(s => s.ContentType == SocialContentType.CivicOpenBill && s.Status == SocialPostStatus.Published)
            .AsEnumerable()
            .Count(s => s.PublishedAt != null && s.PublishedAt >= dayStart);
        if (postedTodayCount >= _options.MaxOpenBillsPerDay) return;

        var soonest = _db.Provisions.AsNoTracking()
            .Where(p => OpenStates.Contains(p.State) && p.Deadline != null && p.Deadline > now.UtcDateTime)
            .OrderBy(p => p.Deadline)
            .FirstOrDefault();
        if (soonest is null) return;

        var text = $"Help bridge this one before it closes: {soonest.Title}\n{Url(soonest.Id)}";
        AddPerPlatform(sink, platforms, SocialContentType.CivicOpenBill, soonest.Id, text,
            score: 0.5,
            card: new CardModel("Open Bill", soonest.Title, "Civersify"));
    }

    private void AddPerPlatform(List<PostCandidate> sink, string[] platforms, SocialContentType type,
        Guid contentId, string text, double score, CardModel card)
    {
        foreach (var platform in platforms)
        {
            sink.Add(new PostCandidate
            {
                ContentType = type,
                ContentId = contentId,
                Platform = platform,
                Text = text,
                PostScore = score,
                Priority = (int)type,
                // Civic content is system-generated and pre-neutralized; auto-publish above the floor.
                RequiresReview = score < _options.AutoPublishMin,
                Card = card,
                Links = new[] { Url(contentId) },
            });
        }
    }

    private string Url(Guid provisionId) => $"{_options.PublicSiteUrl.TrimEnd('/')}/coalition/{provisionId}";

    /// <summary>Dedup (§2 step 3): drop any (ContentType, ContentId, Platform) already in SocialPosts.</summary>
    private List<PostCandidate> ExcludeAlreadyPosted(List<PostCandidate> candidates)
    {
        var ids = candidates.Where(c => c.ContentId.HasValue).Select(c => c.ContentId!.Value).Distinct().ToList();
        if (ids.Count == 0) return candidates;

        var posted = _db.SocialPosts.AsNoTracking()
            .Where(p => p.ContentId != null && ids.Contains(p.ContentId.Value))
            .Select(p => new { p.ContentType, p.ContentId, p.Platform })
            .ToList()
            .Select(p => (p.ContentType, p.ContentId!.Value, p.Platform))
            .ToHashSet();

        return candidates
            .Where(c => !c.ContentId.HasValue ||
                        !posted.Contains((c.ContentType, c.ContentId.Value, c.Platform)))
            .ToList();
    }
}
