using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Daily;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Daily.Generators;

/// <summary>
/// Time Machine (05): real headlines, wrong order.
///
/// EXPLICIT NON-GOAL: we never fabricate a headline. The obvious "real or fake — spot the
/// misinformation" version of this game requires generating plausible fake news, which
/// means an LLM cost, a moderation surface, and a table of synthetic misinformation in our
/// database with our name on it. Sorting REAL headlines delivers the same media-literacy
/// beat with none of that. Every item here carries a publisher, a date and a URL.
///
/// The intended experience needs the archival bank in Seed/archive-headlines.json — five
/// headlines spanning decades, so the reveal can land "the debate is older than it looks".
/// That bank ships EMPTY because its contents must be real and human-verified. Until it is
/// populated the generator degrades to sorting recent ingested news by publication date,
/// which is a weaker game but an honest one.
/// </summary>
public class TimeMachineGenerator : IDailyPuzzleGenerator
{
    public const int ItemsPerPuzzle = 5;

    /// <summary>Below this the archival bank can't supply a varied puzzle.</summary>
    public const int MinArchiveBankSize = 20;

    /// <summary>Archival sorting is only fair when the gaps are inferable from content.</summary>
    public const int MinYearsApart = 4;

    /// <summary>The degraded recent-news mode needs at least this much spread to be sortable.</summary>
    public const int MinDaysApartRecent = 3;

    private readonly CivicDbContext _db;
    private readonly ILogger<TimeMachineGenerator> _logger;

    public TimeMachineGenerator(CivicDbContext db, ILogger<TimeMachineGenerator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public DailyGameKind Kind => DailyGameKind.TimeMachine;

    /// <summary>
    /// Requires review: a juxtaposition of five real headlines can imply an argument that
    /// no individual headline makes. A human should see that before it ships.
    /// </summary>
    public bool RequiresReview => true;

    public async Task<DailyPuzzle?> GenerateAsync(DateOnly date, CancellationToken ct)
    {
        var bank = SeedService.LoadJson<List<ArchiveSeedItem>>("Seed.archive-headlines.json") ?? new();
        var recentlyUsed = await RecentlyUsedHeadlinesAsync(date, ct);

        if (bank.Count >= MinArchiveBankSize)
            return FromArchive(bank, recentlyUsed, date);

        _logger.LogInformation(
            "Time Machine: archival bank has {Count} items (need {Min}) — falling back to recent news sorting",
            bank.Count, MinArchiveBankSize);
        return await FromRecentNewsAsync(recentlyUsed, date, ct);
    }

    private DailyPuzzle? FromArchive(List<ArchiveSeedItem> bank, HashSet<string> recentlyUsed, DateOnly date)
    {
        var eligible = bank
            .Where(i => !recentlyUsed.Contains(i.Headline))
            .OrderBy(i => i.PublishedAt)
            .ToList();

        // Greedily take items at least MinYearsApart apart so the ordering is inferable
        // from content rather than a coin flip.
        var picked = new List<ArchiveSeedItem>();
        foreach (var item in eligible)
        {
            if (picked.Count == 0 ||
                item.PublishedAt.Year - picked[^1].PublishedAt.Year >= MinYearsApart)
            {
                picked.Add(item);
            }
            if (picked.Count == ItemsPerPuzzle) break;
        }

        if (picked.Count < ItemsPerPuzzle)
        {
            _logger.LogInformation("Time Machine: only {Count} archival items are far enough apart", picked.Count);
            return null;
        }

        var trueOrder = picked.Select(p => p.Key).ToList();
        var rng = new Random(date.DayNumber);
        var shuffled = picked.OrderBy(_ => rng.Next()).ToList();

        var payload = new TimeMachinePayload(
            Mode: TimeMachineMode.Sort,
            Items: shuffled.Select(p => new TimeMachineItem(p.Key, p.Headline, p.Publisher)).ToList(),
            TrueOrder: trueOrder,
            CurrentItemId: null,
            Dates: picked.ToDictionary(p => p.Key, p => p.PublishedAt.ToString("yyyy-MM-dd")),
            Urls: picked.ToDictionary(p => p.Key, p => p.Url),
            RevealLine: "Same argument, different decades — the debate is older than it looks.");

        return new DailyPuzzle
        {
            Kind = Kind,
            PuzzleDate = date,
            PayloadJson = DailyJson.Serialize(payload),
            GenerationSource = CivicGenerationSource.Seed,
        };
    }

    /// <summary>
    /// Degraded mode: sort this period's real ingested headlines by publication date.
    /// Still entirely real content — just a narrower time range than the game wants.
    /// </summary>
    private async Task<DailyPuzzle?> FromRecentNewsAsync(
        HashSet<string> recentlyUsed, DateOnly date, CancellationToken ct)
    {
        var news = await _db.NewsItems
            .Where(n => n.Status != NewsItemStatus.Skipped)
            .OrderByDescending(n => n.PublishedAt)
            .Take(120)
            .ToListAsync(ct);

        var picked = new List<NewsItem>();
        foreach (var item in news.Where(n => !recentlyUsed.Contains(n.Headline))
                                 .OrderBy(n => n.PublishedAt))
        {
            if (picked.Count == 0 ||
                (item.PublishedAt - picked[^1].PublishedAt).TotalDays >= MinDaysApartRecent)
            {
                picked.Add(item);
            }
            if (picked.Count == ItemsPerPuzzle) break;
        }

        if (picked.Count < ItemsPerPuzzle)
        {
            _logger.LogInformation(
                "Time Machine: only {Count}/{Needed} recent headlines are far enough apart",
                picked.Count, ItemsPerPuzzle);
            return null;
        }

        var ids = picked.Select(p => p.Id.ToString()).ToList();
        var rng = new Random(date.DayNumber);
        var order = Enumerable.Range(0, picked.Count).OrderBy(_ => rng.Next()).ToList();

        var payload = new TimeMachinePayload(
            Mode: TimeMachineMode.Sort,
            Items: order.Select(i => new TimeMachineItem(
                ids[i], picked[i].Headline, picked[i].Publisher ?? picked[i].Source)).ToList(),
            TrueOrder: ids,
            CurrentItemId: null,
            Dates: picked.ToDictionary(p => p.Id.ToString(), p => p.PublishedAt.ToString("yyyy-MM-dd")),
            Urls: picked.ToDictionary(p => p.Id.ToString(), p => p.Url),
            RevealLine: "Sorted by when each story actually ran.");

        return new DailyPuzzle
        {
            Kind = Kind,
            PuzzleDate = date,
            PayloadJson = DailyJson.Serialize(payload),
            SourceNewsItemId = picked[0].Id,
            GenerationSource = CivicGenerationSource.News,
        };
    }

    private async Task<HashSet<string>> RecentlyUsedHeadlinesAsync(DateOnly date, CancellationToken ct)
    {
        var since = date.AddDays(-90);
        var payloads = await _db.DailyPuzzles
            .Where(p => p.Kind == DailyGameKind.TimeMachine && p.PuzzleDate >= since)
            .Select(p => p.PayloadJson)
            .ToListAsync(ct);

        var used = new HashSet<string>();
        foreach (var json in payloads)
        {
            try
            {
                foreach (var i in DailyJson.Deserialize<TimeMachinePayload>(json).Items) used.Add(i.Headline);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Time Machine: skipping unreadable historical payload");
            }
        }
        return used;
    }

    private sealed class ArchiveSeedItem
    {
        public string Key { get; set; } = "";
        public string Headline { get; set; } = "";
        public string Publisher { get; set; } = "";
        public DateTime PublishedAt { get; set; }
        public string Url { get; set; } = "";
        public string Theme { get; set; } = "";
        public string Era { get; set; } = "";
    }
}
