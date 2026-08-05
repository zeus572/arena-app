using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Rooms;
using Civic.API.Services;
using Civic.API.Services.Coalition.Product;
using Civic.API.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Calibrated predictions (PRD 06 §7, design 1v).
///
/// THERE IS NO LEADERBOARD ENDPOINT, AND NONE MAY BE ADDED.
///
/// This is not an oversight to be helpfully corrected later. PRD 06 §7.5 forbids ranking
/// forecasters, and the reason is structural: the moment accuracy is public and ranked, the
/// optimal play stops being "report your true belief" and becomes "pick questions you can
/// win". That destroys the only thing the feature measures. Forecasts are private, the
/// crowd figure is anonymous and suppressed below a threshold, and no endpoint anywhere
/// returns another user's number.
///
/// XP is paid for FORECASTING, never for being right — same reason.
/// </summary>
[ApiController]
[Route("api/predictions")]
public class PredictionsController : ControllerBase
{
    /// <summary>Below this many forecasts a crowd bar is noise. Matches the daily games.</summary>
    public const int MinCrowdForecasts = 20;

    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ReasoningLedger _ledger;

    public PredictionsController(
        CivicDbContext db, ICurrentUserService user, ReasoningLedger ledger)
    {
        _db = db;
        _user = user;
        _ledger = ledger;
    }

    /// <summary>
    /// One question. Resolution criteria and the cancellation policy are returned up front,
    /// before anyone answers — design 1v is explicit that they are stated first, and an API
    /// that withheld them would make the UI's promise unkeepable.
    /// </summary>
    [HttpGet("{slug}")]
    public async Task<ActionResult<PredictionDto>> Get(string slug, CancellationToken ct)
    {
        var p = await _db.Predictions.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (p is null || p.Status is RoomStatus.Draft or RoomStatus.Candidate) return NotFound();

        var userId = _user.GetCurrentUserId();
        var mine = await _db.UserPredictions.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PredictionId == p.Id && u.UserId == userId, ct);

        return Ok(ToDto(p, mine));
    }

    /// <summary>
    /// Submit or update a probability. Frozen after <see cref="Prediction.ClosesAt"/>.
    ///
    /// Requires an account: a forecast is a commitment we promise to score and show back
    /// months later, which needs a durable identity.
    /// </summary>
    [HttpPost("{slug}/forecast")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<ActionResult<PredictionDto>> Forecast(
        string slug, [FromBody] ForecastRequest body, CancellationToken ct)
    {
        if (body.Probability is < 0 or > 100)
        {
            return BadRequest(new { error = "Probability must be 0-100.", code = "bad_probability" });
        }

        var p = await _db.Predictions.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (p is null || p.Status is RoomStatus.Draft or RoomStatus.Candidate) return NotFound();

        if (p.Outcome != PredictionOutcome.Unresolved)
        {
            return Conflict(new { error = "This question has resolved.", code = "already_resolved" });
        }

        if (DateTime.UtcNow > p.ClosesAt)
        {
            return Conflict(new { error = "Forecasting has closed.", code = "closed" });
        }

        var userId = _user.GetCurrentUserId();
        var existing = await _db.UserPredictions
            .FirstOrDefaultAsync(u => u.PredictionId == p.Id && u.UserId == userId, ct);

        var isNew = existing is null;

        if (existing is null)
        {
            existing = new UserPrediction
            {
                Id = Guid.NewGuid(),
                PredictionId = p.Id,
                UserId = userId,
            };
            _db.UserPredictions.Add(existing);
        }
        else
        {
            existing.UpdateCount++;
        }

        existing.Probability = body.Probability;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await RecomputeAggregateAsync(p, ct);

        // XP once per question, on first forecast. Updating your number is free — charging
        // for it would discourage exactly the behaviour the product wants to reward.
        if (isNew)
        {
            await _ledger.RecordAsync(
                userId, CoalitionActType.CalibratedForecast,
                payload: $"forecast:{p.Slug}", ct: ct);
        }

        return Ok(ToDto(p, existing));
    }

    /// <summary>
    /// The caller's own calibration (design 1v card 2). Their data and nobody else's.
    /// </summary>
    [HttpGet("me/calibration")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<ActionResult<CalibrationDto>> MyCalibration(CancellationToken ct)
    {
        var userId = _user.GetCurrentUserId();

        var rows = await _db.UserPredictions.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Join(_db.Predictions.AsNoTracking(),
                u => u.PredictionId, p => p.Id,
                (u, p) => new { u.Probability, u.BrierScore, p.Outcome })
            .ToListAsync(ct);

        var bands = PredictionScoring.CalibrationBands(
            rows.Select(r => (r.Probability, r.Outcome)));

        return Ok(new CalibrationDto
        {
            TotalForecasts = rows.Count,
            ResolvedForecasts = rows.Count(r =>
                r.Outcome is PredictionOutcome.Yes or PredictionOutcome.No),
            MeanBrier = PredictionScoring.MeanBrier(rows.Select(r => r.BrierScore)),
            Summary = PredictionScoring.Summarize(bands),
            Bands = bands.Select(b => new CalibrationBandDto
            {
                LowerBound = b.LowerBound,
                UpperBound = b.UpperBound,
                Count = b.Count,
                MeanProbability = b.MeanProbability,
                ActualRate = b.ActualRate,
                Overconfident = b.Overconfident,
            }).ToList(),
        });
    }

    private async Task RecomputeAggregateAsync(Prediction p, CancellationToken ct)
    {
        var stats = await _db.UserPredictions.AsNoTracking()
            .Where(u => u.PredictionId == p.Id)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Mean = g.Average(x => (double)x.Probability) })
            .FirstOrDefaultAsync(ct);

        p.ForecastCount = stats?.Count ?? 0;
        p.MeanProbability = stats?.Mean ?? 0;
        await _db.SaveChangesAsync(ct);
    }

    private static PredictionDto ToDto(Prediction p, UserPrediction? mine) => new()
    {
        Slug = p.Slug,
        Proposition = p.Proposition,
        ResolutionCriteria = p.ResolutionCriteria,
        ResolutionSourceDescription = p.ResolutionSourceDescription,
        CancellationPolicy = p.CancellationPolicy,
        OpensAt = p.OpensAt,
        ClosesAt = p.ClosesAt,
        ResolvesByAt = p.ResolvesByAt,
        Outcome = p.Outcome.ToString(),
        ResolvedAt = p.ResolvedAt,
        ResolutionEvidence = p.ResolutionEvidence,
        IsOpen = p.Outcome == PredictionOutcome.Unresolved && DateTime.UtcNow <= p.ClosesAt,
        ForecastCount = p.ForecastCount,
        // Suppressed below the threshold, and the client is told WHY rather than being
        // handed a null it has to guess about.
        CrowdMeanProbability = p.ForecastCount >= MinCrowdForecasts ? p.MeanProbability : null,
        CrowdSuppressedBelow = MinCrowdForecasts,
        MyProbability = mine?.Probability,
        MyBrierScore = mine?.BrierScore,
    };
}

public class ForecastRequest
{
    public int Probability { get; set; }
}

public class PredictionDto
{
    public string Slug { get; set; } = "";
    public string Proposition { get; set; } = "";
    /// <summary>Stated before answering.</summary>
    public string ResolutionCriteria { get; set; } = "";
    public string ResolutionSourceDescription { get; set; } = "";
    public string CancellationPolicy { get; set; } = "";
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public DateTime? ResolvesByAt { get; set; }
    public string Outcome { get; set; } = "";
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionEvidence { get; set; }
    public bool IsOpen { get; set; }
    public int ForecastCount { get; set; }
    /// <summary>Null until enough people have answered for the number to mean anything.</summary>
    public double? CrowdMeanProbability { get; set; }
    public int CrowdSuppressedBelow { get; set; }
    /// <summary>The caller's own forecast. No endpoint returns anyone else's.</summary>
    public int? MyProbability { get; set; }
    public double? MyBrierScore { get; set; }
}

public class CalibrationBandDto
{
    public int LowerBound { get; set; }
    public int UpperBound { get; set; }
    public int Count { get; set; }
    public double MeanProbability { get; set; }
    public double ActualRate { get; set; }
    /// <summary>Rendered in --state: claimed more certainty than the outcomes justify.</summary>
    public bool Overconfident { get; set; }
}

public class CalibrationDto
{
    public int TotalForecasts { get; set; }
    public int ResolvedForecasts { get; set; }
    /// <summary>Lower is better. Null until something has resolved.</summary>
    public double? MeanBrier { get; set; }
    public string? Summary { get; set; }
    public List<CalibrationBandDto> Bands { get; set; } = new();
}
