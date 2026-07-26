using System.Text.Json.Nodes;
using Civic.API.Models.DTOs;
using Civic.API.Models.Daily;
using Civic.API.Services;
using Civic.API.Services.Daily;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Civic.API.Controllers.Api;

/// <summary>
/// The daily games (docs/civic_daily_games). Deliberately <see cref="AllowAnonymousAttribute"/>:
/// these exist to be the top of the funnel, so there is no sign-in wall on any of them.
/// Anonymous visitors play fully; they just don't accrue XP until they have a stable id.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/daily")]
public class DailyController : ControllerBase
{
    private readonly DailyPuzzleService _daily;
    private readonly ICurrentUserService _user;

    public DailyController(DailyPuzzleService daily, ICurrentUserService user)
    {
        _daily = daily;
        _user = user;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static bool TryParseKind(string kind, out DailyGameKind parsed) =>
        Enum.TryParse(kind.Replace("-", ""), ignoreCase: true, out parsed);

    /// <summary>Today's slate — every live puzzle plus the caller's state. One round-trip for the hub.</summary>
    [HttpGet]
    public async Task<ActionResult<DailySlateDto>> Slate([FromQuery] string? date, CancellationToken ct)
    {
        var day = ParseDate(date);
        return Ok(await _daily.GetSlateAsync(_user.GetCurrentUserId(), day, ct));
    }

    /// <summary>One puzzle. Answer-key fields are stripped — see <see cref="DailyRedaction"/>.</summary>
    [HttpGet("{kind}")]
    public async Task<ActionResult<DailyPuzzleDto>> Puzzle(string kind, [FromQuery] string? date, CancellationToken ct)
    {
        if (!TryParseKind(kind, out var parsed)) return NotFound();

        var dto = await _daily.GetPuzzleAsync(parsed, ParseDate(date), _user.GetCurrentUserId(), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Past editions with the caller's scores, for players who want to binge.</summary>
    [HttpGet("{kind}/archive")]
    public async Task<ActionResult<List<DailyArchiveRowDto>>> Archive(
        string kind, [FromQuery] int take = 14, CancellationToken ct = default)
    {
        if (!TryParseKind(kind, out var parsed)) return NotFound();
        return Ok(await _daily.ArchiveAsync(parsed, _user.GetCurrentUserId(), take, ct));
    }

    /// <summary>
    /// Single-shot submission (Fork, Crowd Call, Time Machine, Whose Value). Scoring happens
    /// here, server-side; the reveal comes back in the response and nowhere earlier.
    /// </summary>
    [HttpPost("{kind}/plays")]
    public async Task<ActionResult<DailyResultDto>> Play(
        string kind, [FromBody] JsonNode? body, [FromQuery] string? date, CancellationToken ct)
    {
        if (!TryParseKind(kind, out var parsed)) return NotFound();

        var (result, error, message) = await _daily.SubmitAsync(
            parsed, _user.GetCurrentUserId(), body, ParseDate(date), ct);

        return error switch
        {
            DailyPuzzleService.SubmitError.None => Ok(result),
            DailyPuzzleService.SubmitError.NoPuzzle => NotFound(message),
            DailyPuzzleService.SubmitError.AlreadyPlayed => Conflict(message),
            _ => BadRequest(message),
        };
    }

    /// <summary>One round of Place It: three bucket guesses in, per-axis hints back.</summary>
    [HttpPost("place-it/rounds")]
    public async Task<ActionResult<PlaceItRoundResultDto>> PlaceItRound(
        [FromBody] PlaceItRoundRequest request, [FromQuery] string? date, CancellationToken ct)
    {
        if (request?.Guesses is null) return BadRequest("Guesses are required.");

        var (result, error, message) = await _daily.PlaceItRoundAsync(
            _user.GetCurrentUserId(), request.Guesses, ParseDate(date), ct);

        return error switch
        {
            DailyPuzzleService.SubmitError.None => Ok(result),
            DailyPuzzleService.SubmitError.NoPuzzle => NotFound(message),
            DailyPuzzleService.SubmitError.AlreadyPlayed => Conflict(message),
            _ => BadRequest(message),
        };
    }

    /// <summary>
    /// One guess in the Priced In ladder. Higher/lower is computed server-side so the true
    /// value never crosses the wire before the reveal.
    /// </summary>
    [HttpPost("priced-in/guesses")]
    public async Task<ActionResult<PricedInGuessResultDto>> PricedInGuess(
        [FromBody] PricedInGuessRequest request, [FromQuery] string? date, CancellationToken ct)
    {
        if (request is null) return BadRequest("A guess is required.");

        var (result, error, message) = await _daily.PricedInGuessAsync(
            _user.GetCurrentUserId(), request, ParseDate(date), ct);

        return error switch
        {
            DailyPuzzleService.SubmitError.None => Ok(result),
            DailyPuzzleService.SubmitError.NoPuzzle => NotFound(message),
            DailyPuzzleService.SubmitError.AlreadyPlayed => Conflict(message),
            _ => BadRequest(message),
        };
    }

    private static DateOnly ParseDate(string? date) =>
        DateOnly.TryParse(date, out var parsed) ? parsed : Today;
}

public class PlaceItRoundRequest
{
    public List<int> Guesses { get; set; } = new();
}
