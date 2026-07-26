using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Daily;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Daily.Generators;

/// <summary>
/// Crowd Call (02): guess what share of people got each question right.
///
/// Two content sources, and the reveal must always say which one a round came from —
/// attributing a published poll to "our users" (or vice versa) is a credibility problem,
/// not a cosmetic one.
///
///  - <c>civic-users</c>: the 60-day correct rate from <see cref="QuizPollStats"/>. Only
///    eligible above <see cref="MinResponsesInWindow"/>, because thin data makes the game
///    both wrong and boring.
///  - <c>national-poll</c>: the authored bank in Seed/crowd-call-polls.json, which works
///    at zero traffic. Unverified placeholder rows are DEVELOPMENT ONLY — see
///    <see cref="IncludeUnverified"/>.
/// </summary>
public class CrowdCallGenerator : IDailyPuzzleGenerator
{
    /// <summary>Below this a question's correct rate is noise. See 02_CROWD_CALL.md.</summary>
    public const int MinResponsesInWindow = 50;
    public const int RoundsPerPuzzle = 5;

    /// <summary>Don't reuse a question inside this window.</summary>
    private const int ReuseCooldownDays = 30;

    private readonly CivicDbContext _db;
    private readonly QuizPollStats _poll;
    private readonly IHostEnvironment _env;
    private readonly ILogger<CrowdCallGenerator> _logger;

    public CrowdCallGenerator(
        CivicDbContext db, QuizPollStats poll, IHostEnvironment env, ILogger<CrowdCallGenerator> logger)
    {
        _db = db;
        _poll = poll;
        _env = env;
        _logger = logger;
    }

    public DailyGameKind Kind => DailyGameKind.CrowdCall;
    public bool RequiresReview => false;

    /// <summary>
    /// Placeholder poll rows carry made-up rates so the game is playable on a dev box
    /// before anyone has authored a real bank. They must NEVER ship to real users — a
    /// fabricated "what the country thinks" figure is exactly the thing this app exists
    /// to argue against.
    /// </summary>
    private bool IncludeUnverified => _env.IsDevelopment();

    public async Task<DailyPuzzle?> GenerateAsync(DateOnly date, CancellationToken ct)
    {
        var recentlyUsed = await RecentlyUsedPromptsAsync(date, ct);
        var rounds = new List<CrowdCallRound>();

        rounds.AddRange(await FromQuizAsync(recentlyUsed, ct));
        rounds.AddRange(FromPollBank(recentlyUsed, rounds.Select(r => r.Prompt).ToHashSet()));

        if (rounds.Count < RoundsPerPuzzle)
        {
            _logger.LogInformation(
                "Crowd Call: only {Found}/{Needed} eligible rounds for {Date} " +
                "(quiz questions need {Min}+ responses in the {Window}-day window)",
                rounds.Count, RoundsPerPuzzle, date, MinResponsesInWindow, QuizPollStats.WindowDays);
            return null;
        }

        // Deterministic mix so a regenerated puzzle is byte-identical.
        var rng = new Random(date.DayNumber);
        var selected = rounds.OrderBy(_ => rng.Next()).Take(RoundsPerPuzzle).ToList();

        return new DailyPuzzle
        {
            Kind = Kind,
            PuzzleDate = date,
            PayloadJson = DailyJson.Serialize(new CrowdCallPayload(selected)),
            GenerationSource = CivicGenerationSource.Seed,
        };
    }

    private async Task<List<CrowdCallRound>> FromQuizAsync(HashSet<string> recentlyUsed, CancellationToken ct)
    {
        var questions = await _db.QuizQuestions.ToListAsync(ct);
        if (questions.Count == 0) return new();

        var stats = await _poll.ForQuestionsAsync(questions.Select(q => q.Id).ToList(), ct);
        var rounds = new List<CrowdCallRound>();

        foreach (var q in questions)
        {
            if (recentlyUsed.Contains(q.Question)) continue;
            if (!stats.TryGetValue(q.Id, out var s) || s.Total < MinResponsesInWindow) continue;

            rounds.Add(new CrowdCallRound(
                Prompt: q.Question,
                Answer: q.Options.ElementAtOrDefault(q.CorrectAnswerIndex) ?? "",
                Explanation: q.Explanation,
                CrowdSource: CrowdSource.CivicUsers,
                Attribution: $"Civersify players, last {QuizPollStats.WindowDays} days",
                SourceUrl: null,
                FieldedOn: null,
                SampleSize: s.Total,
                TrueRate: (double)s.Correct / s.Total));
        }

        return rounds;
    }

    private List<CrowdCallRound> FromPollBank(HashSet<string> recentlyUsed, HashSet<string> alreadyPicked)
    {
        var bank = SeedService.LoadJson<List<PollSeedItem>>("Seed.crowd-call-polls.json") ?? new();
        var rounds = new List<CrowdCallRound>();

        foreach (var item in bank)
        {
            if (!item.Verified && !IncludeUnverified) continue;
            if (recentlyUsed.Contains(item.Prompt) || alreadyPicked.Contains(item.Prompt)) continue;

            rounds.Add(new CrowdCallRound(
                Prompt: item.Prompt,
                Answer: item.Answer,
                Explanation: item.Explanation,
                CrowdSource: CrowdSource.NationalPoll,
                Attribution: item.Attribution,
                SourceUrl: item.SourceUrl,
                FieldedOn: item.FieldedOn,
                SampleSize: item.SampleSize,
                TrueRate: item.TrueRate));
        }

        if (rounds.Count > 0 && IncludeUnverified && rounds.Any(r => r.Attribution.StartsWith("PLACEHOLDER")))
        {
            _logger.LogWarning(
                "Crowd Call is using PLACEHOLDER poll rows. These are development-only and " +
                "must be replaced with cited polls before this game goes to real users.");
        }

        return rounds;
    }

    private async Task<HashSet<string>> RecentlyUsedPromptsAsync(DateOnly date, CancellationToken ct)
    {
        var since = date.AddDays(-ReuseCooldownDays);
        var payloads = await _db.DailyPuzzles
            .Where(p => p.Kind == DailyGameKind.CrowdCall && p.PuzzleDate >= since)
            .Select(p => p.PayloadJson)
            .ToListAsync(ct);

        var used = new HashSet<string>();
        foreach (var json in payloads)
        {
            try
            {
                foreach (var r in DailyJson.Deserialize<CrowdCallPayload>(json).Rounds) used.Add(r.Prompt);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Crowd Call: skipping unreadable historical payload");
            }
        }
        return used;
    }

    private sealed class PollSeedItem
    {
        public string Key { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string Answer { get; set; } = "";
        public string Explanation { get; set; } = "";
        public double TrueRate { get; set; }
        public int SampleSize { get; set; }
        public string Attribution { get; set; } = "";
        public string? SourceUrl { get; set; }
        public string? FieldedOn { get; set; }

        /// <summary>False = placeholder; excluded outside Development.</summary>
        public bool Verified { get; set; }
    }
}
