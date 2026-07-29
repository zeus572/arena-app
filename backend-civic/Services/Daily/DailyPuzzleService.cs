using System.Text.Json;
using System.Text.Json.Nodes;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Models.Daily;
using Civic.API.Services.Coalition.Product;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Daily;

/// <summary>
/// Serving and scoring for the daily games. Owns three responsibilities the games
/// must never each re-implement:
///
///  1. <b>Answer-key hygiene</b> — every read path goes through
///     <see cref="DailyRedaction"/>; raw payloads never reach a client.
///  2. <b>Idempotent XP</b> — the unique (PuzzleId, UserId) index means one play per
///     person per puzzle, and XP is awarded exactly once, on completion.
///  3. <b>The anonymous guard</b> — <see cref="CurrentUserService"/> falls back to the
///     literal "anonymous" when there's no sub claim and no X-User-Id header. Writing
///     ledger rows for that id would pool every such visitor into one XP bucket and
///     corrupt both the diminishing-returns curve and the cohort board.
/// </summary>
public class DailyPuzzleService
{
    /// <summary>The literal id <see cref="CurrentUserService"/> falls back to.</summary>
    public const string AnonymousUserId = "anonymous";

    /// <summary>Below this many plays a crowd bar is noise — suppress it and say so.</summary>
    public const int MinCrowdBarPlays = 20;

    private readonly CivicDbContext _db;
    private readonly ReasoningLedger _ledger;
    private readonly ILogger<DailyPuzzleService> _logger;

    public DailyPuzzleService(CivicDbContext db, ReasoningLedger ledger, ILogger<DailyPuzzleService> logger)
    {
        _db = db;
        _ledger = ledger;
        _logger = logger;
    }

    public static bool IsAnonymous(string userId) =>
        string.IsNullOrWhiteSpace(userId) || userId == AnonymousUserId;

    // ------------------------------------------------------------- Reads

    /// <summary>Today's slate: every live puzzle, the caller's play state, and the ring.</summary>
    public async Task<DailySlateDto> GetSlateAsync(string userId, DateOnly date, CancellationToken ct = default)
    {
        var puzzles = await _db.DailyPuzzles
            .Where(p => p.PuzzleDate == date && p.Status == DailyPuzzleStatus.Live && p.Locality == null)
            .OrderBy(p => p.Kind)
            .ToListAsync(ct);

        var plays = await PlaysForAsync(userId, puzzles.Select(p => p.Id).ToList(), ct);

        return new DailySlateDto
        {
            Date = date.ToString("yyyy-MM-dd"),
            // A kind with no approved puzzle simply doesn't appear — the hub degrades to
            // however many games are live and must never error.
            Puzzles = puzzles.Select(p => ToDto(p, plays.GetValueOrDefault(p.Id))).ToList(),
            Cadence = await CadenceAsync(userId, ct),
            Anonymous = IsAnonymous(userId),
        };
    }

    public async Task<DailyPuzzleDto?> GetPuzzleAsync(
        DailyGameKind kind, DateOnly date, string userId, CancellationToken ct = default)
    {
        var puzzle = await LivePuzzleAsync(kind, date, ct);
        if (puzzle is null) return null;

        var plays = await PlaysForAsync(userId, new List<Guid> { puzzle.Id }, ct);
        return ToDto(puzzle, plays.GetValueOrDefault(puzzle.Id));
    }

    public async Task<List<DailyArchiveRowDto>> ArchiveAsync(
        DailyGameKind kind, string userId, int take, CancellationToken ct = default)
    {
        var puzzles = await _db.DailyPuzzles
            .Where(p => p.Kind == kind && p.Status == DailyPuzzleStatus.Live && p.Locality == null)
            .OrderByDescending(p => p.PuzzleDate)
            .Take(Math.Clamp(take, 1, 60))
            .ToListAsync(ct);

        var plays = await PlaysForAsync(userId, puzzles.Select(p => p.Id).ToList(), ct);

        return puzzles.Select(p =>
        {
            var play = plays.GetValueOrDefault(p.Id);
            return new DailyArchiveRowDto
            {
                PuzzleId = p.Id,
                Edition = p.Edition,
                PuzzleDate = p.PuzzleDate.ToString("yyyy-MM-dd"),
                Played = play?.Completed == true,
                Score = play?.Score ?? 0,
            };
        }).ToList();
    }

    private Task<DailyPuzzle?> LivePuzzleAsync(DailyGameKind kind, DateOnly date, CancellationToken ct) =>
        _db.DailyPuzzles.FirstOrDefaultAsync(
            p => p.Kind == kind && p.PuzzleDate == date && p.Status == DailyPuzzleStatus.Live && p.Locality == null,
            ct);

    private async Task<Dictionary<Guid, DailyPuzzlePlay>> PlaysForAsync(
        string userId, List<Guid> puzzleIds, CancellationToken ct)
    {
        if (IsAnonymous(userId) || puzzleIds.Count == 0) return new();
        return await _db.DailyPuzzlePlays
            .Where(x => x.UserId == userId && puzzleIds.Contains(x.PuzzleId))
            .ToDictionaryAsync(x => x.PuzzleId, ct);
    }

    private static DailyPuzzleDto ToDto(DailyPuzzle p, DailyPuzzlePlay? play) => new()
    {
        Id = p.Id,
        Kind = p.Kind.ToString(),
        PuzzleDate = p.PuzzleDate.ToString("yyyy-MM-dd"),
        Edition = p.Edition,
        PayloadVersion = p.PayloadVersion,
        Locality = p.Locality,
        Payload = DailyRedaction.Redact(p.Kind, p.PayloadJson),
        Play = play is null ? null : new DailyPlayStateDto
        {
            Completed = play.Completed,
            Score = play.Score,
            AttemptsUsed = play.AttemptsUsed,
            Response = string.IsNullOrEmpty(play.ResponseJson) ? null : JsonNode.Parse(play.ResponseJson),
        },
    };

    /// <summary>The weekly ring, read off the shared activity-day table.</summary>
    private async Task<DailyCadenceDto> CadenceAsync(string userId, CancellationToken ct)
    {
        if (IsAnonymous(userId)) return new DailyCadenceDto();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since = today.AddDays(-6);
        var active = (await _db.CoalitionActivityDays
            .Where(a => a.UserId == userId && a.Day >= since)
            .Select(a => a.Day)
            .ToListAsync(ct)).ToHashSet();

        var days = new bool[7];
        for (var i = 0; i < 7; i++) days[i] = active.Contains(since.AddDays(i));
        return new DailyCadenceDto { Last7Days = days, ActiveDays = days.Count(d => d) };
    }

    // ------------------------------------------------------------ Submit

    public enum SubmitError { None, NoPuzzle, AlreadyPlayed, BadRequest, WrongEndpoint }

    /// <summary>
    /// Single-shot submission for Fork, Crowd Call, Time Machine and Whose Value.
    /// Place It and Priced In are multi-round and use their own ladder endpoints.
    /// </summary>
    public async Task<(DailyResultDto? Result, SubmitError Error, string? Message)> SubmitAsync(
        DailyGameKind kind, string userId, JsonNode? body, DateOnly date, CancellationToken ct = default)
    {
        if (kind is DailyGameKind.PlaceIt or DailyGameKind.PricedIn)
            return (null, SubmitError.WrongEndpoint,
                $"{kind} is played in rounds — use the {(kind == DailyGameKind.PlaceIt ? "place-it/rounds" : "priced-in/guesses")} endpoint.");

        var puzzle = await LivePuzzleAsync(kind, date, ct);
        if (puzzle is null) return (null, SubmitError.NoPuzzle, "No live puzzle for that day.");

        var existing = await FindPlayAsync(puzzle.Id, userId, ct);
        if (existing?.Completed == true)
            return (null, SubmitError.AlreadyPlayed, "Already played.");

        DailyResultDto result;
        try
        {
            result = kind switch
            {
                DailyGameKind.Fork => await ScoreForkAsync(puzzle, body, ct),
                DailyGameKind.CrowdCall => ScoreCrowdCall(puzzle, body),
                DailyGameKind.TimeMachine => ScoreTimeMachine(puzzle, body),
                DailyGameKind.WhoseValue => ScoreWhoseValue(puzzle, body),
                DailyGameKind.WhichIsTrue => ScoreWhichIsTrue(puzzle, body),
                _ => throw new InvalidOperationException($"No scorer for {kind}."),
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NullReferenceException)
        {
            _logger.LogWarning(ex, "Malformed {Kind} submission", kind);
            return (null, SubmitError.BadRequest, "Malformed response for this game.");
        }

        var play = await UpsertPlayAsync(existing, puzzle.Id, userId, body?.ToJsonString() ?? "{}",
            result.Score, attempts: 1, completed: true, ct);

        result.PointsAwarded = await AwardAsync(userId, puzzle, play, ct);
        // Crowd stats are read AFTER this play is saved so the caller is included in the
        // split they're shown — otherwise the first player of the day sees "0%".
        result.Crowd = await CrowdForAsync(puzzle, userId, ct);
        if (kind == DailyGameKind.Fork) result.ShareGrid = ForkShareGrid(puzzle, body, result.Crowd);

        return (result, SubmitError.None, null);
    }

    // -------------------------------------------------- Place It ladder

    public async Task<(PlaceItRoundResultDto? Result, SubmitError Error, string? Message)> PlaceItRoundAsync(
        string userId, IReadOnlyList<int> guesses, DateOnly date, CancellationToken ct = default)
    {
        var puzzle = await LivePuzzleAsync(DailyGameKind.PlaceIt, date, ct);
        if (puzzle is null) return (null, SubmitError.NoPuzzle, "No live puzzle for that day.");

        var payload = DailyJson.Deserialize<PlaceItPayload>(puzzle.PayloadJson);
        if (guesses.Count != payload.Axes.Count)
            return (null, SubmitError.BadRequest, $"Expected {payload.Axes.Count} guesses.");
        if (guesses.Any(g => g < 0 || g > 4))
            return (null, SubmitError.BadRequest, "Guesses must be buckets 0-4.");

        var existing = await FindPlayAsync(puzzle.Id, userId, ct);
        if (existing?.Completed == true) return (null, SubmitError.AlreadyPlayed, "Already played.");

        var prior = existing is null || string.IsNullOrEmpty(existing.ResponseJson)
            ? new PlaceItResponse(new List<List<int>>())
            : DailyJson.Deserialize<PlaceItResponse>(existing.ResponseJson);
        prior.Rounds.Add(guesses.ToList());

        var roundsUsed = prior.Rounds.Count;
        var hints = DailyScoring.PlaceItHints(payload, guesses);
        var allExact = hints.All(h => h == "exact");
        var completed = allExact || roundsUsed >= payload.MaxRounds;

        var (total, axes) = DailyScoring.ScorePlaceIt(payload, guesses, roundsUsed);
        var play = await UpsertPlayAsync(existing, puzzle.Id, userId, DailyJson.Serialize(prior),
            completed ? total : 0, roundsUsed, completed, ct);

        var dto = new PlaceItRoundResultDto
        {
            Completed = completed,
            RoundsUsed = roundsUsed,
            RoundsRemaining = Math.Max(0, payload.MaxRounds - roundsUsed),
            Hints = hints,
        };

        if (!completed) return (dto, SubmitError.None, null);

        var rows = prior.Rounds
            .Select(r => DailyScoring.ScorePlaceIt(payload, r, 1).Axes.Select(a => a).ToList())
            .ToList();

        dto.Result = new DailyResultDto
        {
            PuzzleId = puzzle.Id,
            Kind = puzzle.Kind.ToString(),
            Edition = puzzle.Edition,
            Completed = true,
            Score = total,
            AttemptsUsed = roundsUsed,
            Rounds = axes.Select(ToRoundDto).ToList(),
            // Framed as a comparison, never a verdict: the "right answer" is our LLM
            // synthesis of the bill, not ground truth. See 04_PLACE_IT.md.
            Reveal = JsonSerializer.SerializeToNode(new
            {
                billId = payload.BillId,
                axes = payload.Axes.Select(a => new
                {
                    a.AxisKey, a.Name, a.LowLabel, a.HighLabel,
                    trueBucket = a.TrueBucket, a.Rationale, a.Evidence,
                }),
            }, DailyJson.Options),
            ShareGrid = DailyShareGrid.PlaceIt(puzzle.Edition, rows),
        };
        dto.Result.PointsAwarded = await AwardAsync(userId, puzzle, play, ct);
        dto.Result.Crowd = await CrowdForAsync(puzzle, userId, ct);

        return (dto, SubmitError.None, null);
    }

    // ------------------------------------------------- Priced In ladder

    public async Task<(PricedInGuessResultDto? Result, SubmitError Error, string? Message)> PricedInGuessAsync(
        string userId, PricedInGuessRequest request, DateOnly date, CancellationToken ct = default)
    {
        var puzzle = await LivePuzzleAsync(DailyGameKind.PricedIn, date, ct);
        if (puzzle is null) return (null, SubmitError.NoPuzzle, "No live puzzle for that day.");
        if (request.Guess <= 0) return (null, SubmitError.BadRequest, "Guess must be positive.");

        var payload = DailyJson.Deserialize<PricedInPayload>(puzzle.PayloadJson);
        var existing = await FindPlayAsync(puzzle.Id, userId, ct);
        if (existing?.Completed == true) return (null, SubmitError.AlreadyPlayed, "Already played.");

        var prior = existing is null || string.IsNullOrEmpty(existing.ResponseJson)
            ? new PricedInResponse(new List<double>())
            : DailyJson.Deserialize<PricedInResponse>(existing.ResponseJson);
        prior.Guesses.Add(request.Guess);

        var guessesUsed = prior.Guesses.Count;
        var exact = Math.Abs(request.Guess - payload.TrueValue) < double.Epsilon;
        var completed = request.Final || exact || guessesUsed >= payload.MaxGuesses;

        var score = completed ? DailyScoring.ScorePricedIn(payload.TrueValue, request.Guess, guessesUsed) : 0;
        var play = await UpsertPlayAsync(existing, puzzle.Id, userId, DailyJson.Serialize(prior),
            score, guessesUsed, completed, ct);

        var dto = new PricedInGuessResultDto
        {
            Completed = completed,
            GuessesUsed = guessesUsed,
            GuessesRemaining = Math.Max(0, payload.MaxGuesses - guessesUsed),
            // Direction only — the true value never crosses the wire before the reveal.
            Direction = exact ? "exact" : payload.TrueValue > request.Guess ? "higher" : "lower",
        };

        if (!completed) return (dto, SubmitError.None, null);

        var closeness = DailyScoring.Closeness(payload.TrueValue, request.Guess);
        dto.Result = new DailyResultDto
        {
            PuzzleId = puzzle.Id,
            Kind = puzzle.Kind.ToString(),
            Edition = puzzle.Edition,
            Completed = true,
            Score = score,
            AttemptsUsed = guessesUsed,
            Reveal = JsonSerializer.SerializeToNode(new
            {
                trueValue = payload.TrueValue,
                payload.Anchor,
                payload.Source,
                payload.SourceUrl,
                payload.AsOf,
                closeness,
            }, DailyJson.Options),
            ShareGrid = DailyShareGrid.PricedIn(puzzle.Edition, guessesUsed, closeness),
        };
        dto.Result.PointsAwarded = await AwardAsync(userId, puzzle, play, ct);
        dto.Result.Crowd = await CrowdForAsync(puzzle, userId, ct);

        return (dto, SubmitError.None, null);
    }

    // ------------------------------------------------------- Per-game scoring

    private async Task<DailyResultDto> ScoreForkAsync(DailyPuzzle puzzle, JsonNode? body, CancellationToken ct)
    {
        var payload = DailyJson.Deserialize<ForkPayload>(puzzle.PayloadJson);
        var response = DailyJson.Deserialize<ForkResponse>(body?.ToJsonString() ?? "{}");
        if (response.Choice is not ("A" or "B"))
            throw new InvalidOperationException("Choice must be \"A\" or \"B\".");

        await Task.CompletedTask;
        return new DailyResultDto
        {
            PuzzleId = puzzle.Id,
            Kind = puzzle.Kind.ToString(),
            Edition = puzzle.Edition,
            Completed = true,
            Score = 0, // No right answer — the payoff is the reveal, not a score.
            AttemptsUsed = 1,
            Reveal = JsonSerializer.SerializeToNode(new
            {
                payload.AxisKey,
                payload.Tradeoff,
                payload.ProvisionSlug,
            }, DailyJson.Options),
        };
    }

    private static DailyResultDto ScoreCrowdCall(DailyPuzzle puzzle, JsonNode? body)
    {
        var payload = DailyJson.Deserialize<CrowdCallPayload>(puzzle.PayloadJson);
        var response = DailyJson.Deserialize<CrowdCallResponse>(body?.ToJsonString() ?? "{}");

        var (total, rounds) = DailyScoring.ScoreCrowdCall(payload, response);
        var over = DailyScoring.CountOverestimatedDivision(payload, response);

        return new DailyResultDto
        {
            PuzzleId = puzzle.Id,
            Kind = puzzle.Kind.ToString(),
            Edition = puzzle.Edition,
            Completed = true,
            Score = total,
            AttemptsUsed = 1,
            Rounds = rounds.Select(ToRoundDto).ToList(),
            Reveal = JsonSerializer.SerializeToNode(new
            {
                rounds = payload.Rounds.Select(r => new
                {
                    trueRate = r.TrueRate,
                    r.SampleSize,
                    r.Attribution,
                    r.CrowdSource,
                    r.SourceUrl,
                    r.FieldedOn,
                    r.Explanation,
                }),
                overestimatedDivision = over,
            }, DailyJson.Options),
            ShareGrid = DailyShareGrid.CrowdCall(puzzle.Edition, total, rounds, over, payload.Rounds.Count),
        };
    }

    private static DailyResultDto ScoreTimeMachine(DailyPuzzle puzzle, JsonNode? body)
    {
        var payload = DailyJson.Deserialize<TimeMachinePayload>(puzzle.PayloadJson);
        var response = DailyJson.Deserialize<TimeMachineResponse>(body?.ToJsonString() ?? "{}");

        int score;
        string grid;
        var rounds = new List<RoundResult>();

        if (payload.Mode == TimeMachineMode.Sort)
        {
            var order = response.Order ?? throw new InvalidOperationException("Sort mode needs an order.");
            var (s, concordant, pairs) = DailyScoring.ScoreTimeMachineSort(payload.TrueOrder, order);
            score = s;
            rounds = DailyScoring.TimeMachineSlots(payload.TrueOrder, order);
            grid = DailyShareGrid.TimeMachineSort(puzzle.Edition, concordant, pairs, rounds);
        }
        else
        {
            score = DailyScoring.ScoreTimeMachineOddOneOut(payload.CurrentItemId, response.Pick);
            grid = DailyShareGrid.TimeMachineOddOneOut(puzzle.Edition, score == 100);
        }

        return new DailyResultDto
        {
            PuzzleId = puzzle.Id,
            Kind = puzzle.Kind.ToString(),
            Edition = puzzle.Edition,
            Completed = true,
            Score = score,
            AttemptsUsed = 1,
            Rounds = rounds.Select(ToRoundDto).ToList(),
            Reveal = JsonSerializer.SerializeToNode(new
            {
                payload.TrueOrder,
                payload.CurrentItemId,
                payload.Dates,
                payload.Urls,
                payload.RevealLine,
            }, DailyJson.Options),
            ShareGrid = grid,
        };
    }

    private static DailyResultDto ScoreWhoseValue(DailyPuzzle puzzle, JsonNode? body)
    {
        var payload = DailyJson.Deserialize<WhoseValuePayload>(puzzle.PayloadJson);
        var response = DailyJson.Deserialize<WhoseValueResponse>(body?.ToJsonString() ?? "{}");

        var (total, rounds) = DailyScoring.ScoreWhoseValue(payload, response);
        var correct = rounds.Count(r => r.Score == 100);

        // "Sharpest" is a READING-COMPREHENSION result, not a compass position. The copy
        // must never conflate the two — see 06_WHOSE_VALUE.md.
        var sharpest = payload.Rounds
            .Where((r, i) => i < rounds.Count && rounds[i].Score == 100)
            .Select(r => r.Choices.FirstOrDefault(c => c.AxisKey == r.CorrectAxisKey)?.Name)
            .FirstOrDefault(n => n is not null);

        return new DailyResultDto
        {
            PuzzleId = puzzle.Id,
            Kind = puzzle.Kind.ToString(),
            Edition = puzzle.Edition,
            Completed = true,
            Score = total,
            AttemptsUsed = 1,
            Rounds = rounds.Select(ToRoundDto).ToList(),
            Reveal = JsonSerializer.SerializeToNode(new
            {
                rounds = payload.Rounds.Select(r => new
                {
                    correctAxisKey = r.CorrectAxisKey,
                    r.BillTitle,
                    r.BillId,
                }),
                sharpestAxisName = sharpest,
            }, DailyJson.Options),
            ShareGrid = DailyShareGrid.WhoseValue(puzzle.Edition, correct, payload.Rounds.Count, sharpest, rounds),
        };
    }

    private static DailyResultDto ScoreWhichIsTrue(DailyPuzzle puzzle, JsonNode? body)
    {
        var payload = DailyJson.Deserialize<WhichIsTruePayload>(puzzle.PayloadJson);
        var response = DailyJson.Deserialize<WhichIsTrueResponse>(body?.ToJsonString() ?? "{}");

        if (response.Picks.Any(p => p is not ("A" or "B")))
            throw new InvalidOperationException("Each pick must be \"A\" or \"B\".");

        var (total, rounds) = DailyScoring.ScoreWhichIsTrue(payload, response);
        var correct = rounds.Count(r => r.Score == 100);

        return new DailyResultDto
        {
            PuzzleId = puzzle.Id,
            Kind = puzzle.Kind.ToString(),
            Edition = puzzle.Edition,
            Completed = true,
            Score = total,
            AttemptsUsed = 1,
            Rounds = rounds.Select(ToRoundDto).ToList(),
            // The whole point of the reveal is the SECOND number: both are real, and saying
            // what the loser actually is turns a wrong guess into a fact worth keeping.
            Reveal = JsonSerializer.SerializeToNode(new
            {
                rounds = payload.Rounds.Select(r => new
                {
                    correct = r.Correct,
                    r.Explanation,
                    r.DecoyTruth,
                    r.Source,
                    r.SourceUrl,
                    r.AsOf,
                    r.BillId,
                }),
                correctCount = correct,
            }, DailyJson.Options),
            ShareGrid = DailyShareGrid.WhichIsTrue(puzzle.Edition, correct, payload.Rounds.Count, rounds),
        };
    }

    // --------------------------------------------------------- Crowd stats

    private async Task<JsonNode?> CrowdForAsync(DailyPuzzle puzzle, string userId, CancellationToken ct)
    {
        var plays = await _db.DailyPuzzlePlays
            .Where(p => p.PuzzleId == puzzle.Id && p.Completed)
            .Select(p => new { p.UserId, p.ResponseJson, p.Score })
            .ToListAsync(ct);

        if (puzzle.Kind != DailyGameKind.Fork)
        {
            return JsonSerializer.SerializeToNode(new
            {
                plays = plays.Count,
                averageScore = plays.Count == 0 ? 0 : (int)Math.Round(plays.Average(p => p.Score)),
            }, DailyJson.Options);
        }

        // Fork: the split IS the payoff. National always; locality and age band only
        // once there are enough plays to be worth showing.
        var choices = new List<(string UserId, string Choice)>();
        foreach (var p in plays)
        {
            try
            {
                var choice = DailyJson.Deserialize<ForkResponse>(p.ResponseJson).Choice;
                if (choice is "A" or "B") choices.Add((p.UserId, choice));
            }
            catch (JsonException) { /* a malformed historical row shouldn't break the reveal */ }
        }

        var profiles = await ProfilesForAsync(choices.Select(c => c.UserId).ToList(), ct);
        var me = profiles.GetValueOrDefault(userId);

        return JsonSerializer.SerializeToNode(new
        {
            national = Split(choices.Select(c => c.Choice)),
            locality = me?.LocalityState is null ? null : Split(
                choices.Where(c => profiles.GetValueOrDefault(c.UserId)?.LocalityState == me.LocalityState)
                       .Select(c => c.Choice), me.LocalityState),
            ageBand = me?.AgeRange is null ? null : Split(
                choices.Where(c => profiles.GetValueOrDefault(c.UserId)?.AgeRange == me.AgeRange)
                       .Select(c => c.Choice), me.AgeRange),
        }, DailyJson.Options);
    }

    private async Task<Dictionary<string, UserProfile>> ProfilesForAsync(List<string> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new();
        return await _db.UserProfiles
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, ct);
    }

    /// <summary>A choice split, or a suppression marker when the sample is too thin to mean anything.</summary>
    private static object Split(IEnumerable<string> choices, string? label = null)
    {
        var list = choices.ToList();
        if (list.Count < MinCrowdBarPlays)
            return new { label, total = list.Count, suppressed = true, aPercent = 0, bPercent = 0 };

        var a = list.Count(c => c == "A");
        var aPct = (int)Math.Round(100.0 * a / list.Count);
        return new { label, total = list.Count, suppressed = false, aPercent = aPct, bPercent = 100 - aPct };
    }

    private static string ForkShareGrid(DailyPuzzle puzzle, JsonNode? body, JsonNode? crowd)
    {
        var choice = DailyJson.Deserialize<ForkResponse>(body?.ToJsonString() ?? "{}").Choice;
        var national = crowd?["national"];
        var suppressed = national?["suppressed"]?.GetValue<bool>() ?? true;
        var otherPct = suppressed
            ? 50
            : choice == "A"
                ? national?["bPercent"]?.GetValue<int>() ?? 50
                : national?["aPercent"]?.GetValue<int>() ?? 50;
        return DailyShareGrid.Fork(puzzle.Edition, choice, otherPct);
    }

    // ------------------------------------------------------------ Plumbing

    private Task<DailyPuzzlePlay?> FindPlayAsync(Guid puzzleId, string userId, CancellationToken ct) =>
        IsAnonymous(userId)
            ? Task.FromResult<DailyPuzzlePlay?>(null)
            : _db.DailyPuzzlePlays.FirstOrDefaultAsync(p => p.PuzzleId == puzzleId && p.UserId == userId, ct);

    /// <summary>
    /// Persist the play. Anonymous callers play fully but nothing is stored — there is no
    /// stable id to key on, so a row would be indistinguishable from every other visitor.
    /// </summary>
    private async Task<DailyPuzzlePlay?> UpsertPlayAsync(
        DailyPuzzlePlay? existing, Guid puzzleId, string userId, string responseJson,
        int score, int attempts, bool completed, CancellationToken ct)
    {
        if (IsAnonymous(userId)) return null;

        if (existing is null)
        {
            existing = new DailyPuzzlePlay { Id = Guid.NewGuid(), PuzzleId = puzzleId, UserId = userId };
            _db.DailyPuzzlePlays.Add(existing);
        }

        existing.ResponseJson = responseJson;
        existing.Score = score;
        existing.AttemptsUsed = attempts;
        existing.Completed = completed;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    /// <summary>
    /// Award reasoning XP once, on completion. The daily cap and the 0.8x within-day
    /// decay in <see cref="CoalitionPoints"/> are what stop six games from out-earning
    /// real coalition work — do not add a bonus large enough to defeat them.
    /// </summary>
    private async Task<int> AwardAsync(string userId, DailyPuzzle puzzle, DailyPuzzlePlay? play, CancellationToken ct)
    {
        if (play is null || !play.Completed || IsAnonymous(userId)) return 0;

        var marker = $"{puzzle.Kind}:{puzzle.PuzzleDate:yyyy-MM-dd}";
        var already = await _db.CoalitionActs.AnyAsync(
            a => a.UserId == userId && a.Type == CoalitionActType.DailyPuzzle && a.Payload == marker, ct);
        if (already) return 0;

        var (points, _) = await _ledger.RecordAsync(
            userId, CoalitionActType.DailyPuzzle,
            payload: marker,
            bonus: play.Score >= 80 ? 2 : 0,
            ct: ct);
        return points;
    }

    private static DailyRoundResultDto ToRoundDto(RoundResult r) => new() { Score = r.Score, Band = r.Band };
}
