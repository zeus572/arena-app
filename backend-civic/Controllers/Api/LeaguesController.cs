using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using Civic.API.Services.Leagues;

namespace Civic.API.Controllers.Api;

/// <summary>
/// The social layer: leagues (friend-group competitions), shareable invites, membership, and shared
/// rounds. Every endpoint requires a signed-in user (a JWT minted by the debate backend) — there is
/// no anonymous league play, since membership needs a stable identity. League-scoped reads treat a
/// non-member like a missing league (404) so existence never leaks.
///
/// Write/participation actions additionally require a verified email
/// (<c>[Authorize(Policy = "VerifiedEmail")]</c>) so unverified throwaway accounts can't spam
/// leagues. Reads stay accessible to any signed-in user so they can still browse.
/// </summary>
[ApiController]
[Authorize]
[Route("api/leagues")]
public class LeaguesController : ControllerBase
{
    private readonly ICurrentUserService _user;
    private readonly LeagueService _leagues;
    private readonly LeagueRoundService _rounds;

    public LeaguesController(ICurrentUserService user, LeagueService leagues, LeagueRoundService rounds)
    {
        _user = user;
        _leagues = leagues;
        _rounds = rounds;
    }

    // ---------------------------------------------------------------- Leagues

    [HttpPost]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> Create([FromBody] CreateLeagueRequest req, CancellationToken ct)
        => Execute(() => _leagues.CreateAsync(RequireUserId(), req, ct));

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
        => Execute(() => _leagues.ListAsync(RequireUserId(), ct));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct)
        => Execute(() => _leagues.GetDetailAsync(RequireUserId(), id, ct));

    [HttpPost("{id:guid}/link-campaign")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> LinkCampaign(Guid id, [FromBody] LinkCampaignRequest req, CancellationToken ct)
        => Execute(() => _leagues.LinkCampaignAsync(RequireUserId(), id, req, ct));

    [HttpPost("{id:guid}/refresh-identity")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> RefreshIdentity(Guid id, [FromBody] RefreshIdentityRequest req, CancellationToken ct)
        => Execute(() => _leagues.RefreshIdentityAsync(RequireUserId(), id, req, ct));

    [HttpPost("{id:guid}/leave")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> Leave(Guid id, CancellationToken ct)
        => Execute(() => _leagues.LeaveAsync(RequireUserId(), id, ct));

    // ---------------------------------------------------------------- Invites

    [HttpPost("{id:guid}/invites")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> CreateInvite(Guid id, [FromBody] CreateInviteRequest req, CancellationToken ct)
        => Execute(() => _leagues.CreateInviteAsync(RequireUserId(), id, req, ct));

    [HttpGet("{id:guid}/invites")]
    public Task<IActionResult> ListInvites(Guid id, CancellationToken ct)
        => Execute(() => _leagues.ListInvitesAsync(RequireUserId(), id, ct));

    [HttpPost("{id:guid}/invites/email")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> InviteByEmail(Guid id, [FromBody] InviteByEmailRequest req, CancellationToken ct)
        => Execute(() => _leagues.CreateEmailInvitesAsync(RequireUserId(), id, req ?? new InviteByEmailRequest(), ct));

    [HttpDelete("{id:guid}/invites/{inviteId:guid}")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> RevokeInvite(Guid id, Guid inviteId, CancellationToken ct)
        => Execute(() => _leagues.RevokeInviteAsync(RequireUserId(), id, inviteId, ct));

    [HttpGet("join/{code}")]
    public Task<IActionResult> PreviewInvite(string code, CancellationToken ct)
        => Execute(() => _leagues.PreviewInviteAsync(RequireUserId(), code, ct));

    /// <summary>
    /// Anonymous-friendly invite preview for the signed-out join page. Returns only league name,
    /// headcount, and organizer (no caller-specific or sensitive fields) so a visitor without an
    /// account still sees what they'd be joining and is nudged to sign in.
    /// </summary>
    [HttpGet("join/{code}/public")]
    [AllowAnonymous]
    public Task<IActionResult> PublicPreviewInvite(string code, CancellationToken ct)
        => Execute(() => _leagues.PublicPreviewInviteAsync(code, ct));

    [HttpPost("join/{code}")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> Join(string code, [FromBody] JoinLeagueRequest req, CancellationToken ct)
        => Execute(() => _leagues.JoinAsync(RequireUserId(), code, req ?? new JoinLeagueRequest(), ct));

    // ---------------------------------------------------------------- Rounds

    [HttpGet("{id:guid}/rounds")]
    public Task<IActionResult> ListRounds(Guid id, CancellationToken ct)
        => Execute(() => _rounds.ListRoundsAsync(RequireUserId(), id, ct));

    [HttpPost("{id:guid}/rounds")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> OpenRound(Guid id, [FromBody] OpenRoundRequest req, CancellationToken ct)
        => Execute(() => _rounds.OpenRoundAsync(RequireUserId(), id, req, ct));

    [HttpGet("{id:guid}/rounds/{roundId:guid}")]
    public Task<IActionResult> RoundDetail(Guid id, Guid roundId, CancellationToken ct)
        => Execute(() => _rounds.GetRoundAsync(RequireUserId(), id, roundId, ct));

    [HttpPost("{id:guid}/rounds/{roundId:guid}/entries")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> SubmitEntry(Guid id, Guid roundId, [FromBody] SubmitRoundEntryRequest req, CancellationToken ct)
        => Execute(() => _rounds.SubmitEntryAsync(RequireUserId(), id, roundId, req, ct));

    [HttpPost("{id:guid}/rounds/{roundId:guid}/entries/{entryId:guid}/vote")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> VoteEntry(Guid id, Guid roundId, Guid entryId, [FromBody] ReactionRequestDto req, CancellationToken ct)
        => Execute(() => _rounds.VoteEntryAsync(RequireUserId(), id, roundId, entryId, req, ct));

    [HttpDelete("{id:guid}/rounds/{roundId:guid}/entries/{entryId:guid}/vote")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> UnvoteEntry(Guid id, Guid roundId, Guid entryId, CancellationToken ct)
        => Execute(() => _rounds.UnvoteEntryAsync(RequireUserId(), id, roundId, entryId, ct));

    [HttpPost("{id:guid}/rounds/{roundId:guid}/start-voting")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> StartVoting(Guid id, Guid roundId, CancellationToken ct)
        => Execute(() => _rounds.StartVotingAsync(RequireUserId(), id, roundId, ct));

    [HttpPost("{id:guid}/rounds/{roundId:guid}/close")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> CloseRound(Guid id, Guid roundId, CancellationToken ct)
        => Execute(() => _rounds.CloseRoundAsync(RequireUserId(), id, roundId, ct));

    [HttpGet("{id:guid}/rounds/{roundId:guid}/results")]
    public Task<IActionResult> RoundResults(Guid id, Guid roundId, CancellationToken ct)
        => Execute(() => _rounds.GetResultsAsync(RequireUserId(), id, roundId, ct));

    // ---------------------------------------------------------------- Plumbing

    /// <summary>
    /// Resolve the signed-in user's id. The [Authorize] attribute guarantees a valid JWT, but we
    /// assert a non-anonymous id here too (defense-in-depth) so league state can never be written
    /// under the anonymous fallback.
    /// </summary>
    private string RequireUserId()
    {
        var id = _user.GetCurrentUserId();
        if (!_user.IsAuthenticated || string.IsNullOrWhiteSpace(id) || id == "anonymous")
            throw new LeagueAuthRequiredException();
        return id;
    }

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (Exception ex) { return Map(ex); }
    }

    private async Task<IActionResult> Execute(Func<Task> action)
    {
        try { await action(); return NoContent(); }
        catch (Exception ex) { return Map(ex); }
    }

    private IActionResult Map(Exception ex) => ex switch
    {
        LeagueAuthRequiredException => Unauthorized(new { error = ex.Message }),
        LeagueForbiddenException => StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message }),
        LeagueNotFoundException => NotFound(new { error = ex.Message }),
        LeagueValidationException => BadRequest(new { error = ex.Message }),
        LeagueInviteGoneException => StatusCode(StatusCodes.Status410Gone, new { error = ex.Message }),
        LeagueConflictException => Conflict(new { error = ex.Message }),
        _ => throw ex,
    };
}
