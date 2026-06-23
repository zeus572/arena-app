using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using Civic.API.Services.Campaign;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Campaign Manager game mode: the player manages an existing VirtualCandidate and tries to make
/// them finish first in their race by election day.
///
/// Every campaign mutates per-user state, so the whole controller requires a signed-in user (a JWT
/// minted by the debate backend) — except the read-only races teaser, which signed-out visitors may
/// browse to entice them to sign in. <see cref="ICurrentUserService.GetCurrentUserId"/> therefore
/// always resolves to a real authenticated user id here, never the anonymous fallback.
/// </summary>
[ApiController]
[Authorize]
[Route("api/campaign-manager")]
public class CampaignManagerController : ControllerBase
{
    private readonly ICurrentUserService _user;
    private readonly CivicCampaignService _campaigns;

    public CampaignManagerController(ICurrentUserService user, CivicCampaignService campaigns)
    {
        _user = user;
        _campaigns = campaigns;
    }

    /// <summary>
    /// Resolve the signed-in user's id. The [Authorize] attribute guarantees a valid JWT, but we
    /// assert a non-anonymous id here too so a campaign can never be written under the anonymous
    /// fallback even if the attribute were ever removed (defense-in-depth).
    /// </summary>
    private string RequireUserId()
    {
        var id = _user.GetCurrentUserId();
        if (!_user.IsAuthenticated || string.IsNullOrWhiteSpace(id) || id == "anonymous")
            throw new CivicCampaignAuthRequiredException();
        return id;
    }

    // GET /api/campaign-manager/races — current-cycle races grouped by seat, with candidates.
    // Anonymous-friendly: this is the teaser that shows signed-out visitors what they could manage.
    [AllowAnonymous]
    [HttpGet("races")]
    public async Task<ActionResult<IEnumerable<CivicRaceDto>>> Races(CancellationToken ct)
        => Ok(await _campaigns.GetRacesAsync(ct));

    // POST /api/campaign-manager/campaigns
    [HttpPost("campaigns")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<IActionResult> Create([FromBody] CreateCivicCampaignRequest request, CancellationToken ct)
    {
        try
        {
            var detail = await _campaigns.CreateAsync(RequireUserId(), request, ct);
            return CreatedAtAction(nameof(GetById), new { id = detail.Id }, detail);
        }
        catch (CivicCampaignAuthRequiredException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (CivicCampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/campaign-manager/campaigns
    [HttpGet("campaigns")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        try { return Ok(await _campaigns.ListAsync(RequireUserId(), ct)); }
        catch (CivicCampaignAuthRequiredException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    // GET /api/campaign-manager/campaigns/{id}
    [HttpGet("campaigns/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try { return Ok(await _campaigns.GetDetailAsync(RequireUserId(), id, ct)); }
        catch (CivicCampaignAuthRequiredException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (CivicCampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // GET /api/campaign-manager/campaigns/{id}/news/{briefingSlug} — response page data.
    [HttpGet("campaigns/{id:guid}/news/{briefingSlug}")]
    public async Task<IActionResult> NewsResponsePage(Guid id, string briefingSlug, CancellationToken ct)
    {
        try { return Ok(await _campaigns.GetNewsResponsePageAsync(RequireUserId(), id, briefingSlug, ct)); }
        catch (CivicCampaignAuthRequiredException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (CivicCampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CivicCampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // POST /api/campaign-manager/campaigns/{id}/actions
    [HttpPost("campaigns/{id:guid}/actions")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<IActionResult> TakeAction(Guid id, [FromBody] TakeActionRequest request, CancellationToken ct)
    {
        try { return Ok(await _campaigns.TakeActionAsync(RequireUserId(), id, request, ct)); }
        catch (CivicCampaignAuthRequiredException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (CivicCampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CivicCampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (CivicCampaignConflictException ex) { return Conflict(new { error = ex.Message }); }
    }

    // POST /api/campaign-manager/campaigns/{id}/advance — advance one campaign day.
    [HttpPost("campaigns/{id:guid}/advance")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<IActionResult> Advance(Guid id, CancellationToken ct)
    {
        try { return Ok(await _campaigns.AdvanceDayAsync(RequireUserId(), id, ct)); }
        catch (CivicCampaignAuthRequiredException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (CivicCampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CivicCampaignConflictException ex) { return Conflict(new { error = ex.Message }); }
    }

    // GET /api/campaign-manager/campaigns/{id}/results
    [HttpGet("campaigns/{id:guid}/results")]
    public async Task<IActionResult> Results(Guid id, CancellationToken ct)
    {
        try { return Ok(await _campaigns.GetResultsAsync(RequireUserId(), id, ct)); }
        catch (CivicCampaignAuthRequiredException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (CivicCampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CivicCampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
