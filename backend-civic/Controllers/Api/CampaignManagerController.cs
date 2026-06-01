using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using Civic.API.Services.Campaign;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Campaign Manager game mode: the player manages an existing VirtualCandidate and tries to make
/// them finish first in their race by election day. Anonymous-friendly like the rest of Civic.
/// </summary>
[ApiController]
[AllowAnonymous]
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

    // GET /api/campaign-manager/races — current-cycle races grouped by seat, with candidates.
    [HttpGet("races")]
    public async Task<ActionResult<IEnumerable<CivicRaceDto>>> Races(CancellationToken ct)
        => Ok(await _campaigns.GetRacesAsync(ct));

    // POST /api/campaign-manager/campaigns
    [HttpPost("campaigns")]
    public async Task<IActionResult> Create([FromBody] CreateCivicCampaignRequest request, CancellationToken ct)
    {
        try
        {
            var detail = await _campaigns.CreateAsync(_user.GetCurrentUserId(), request, ct);
            return CreatedAtAction(nameof(GetById), new { id = detail.Id }, detail);
        }
        catch (CivicCampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/campaign-manager/campaigns
    [HttpGet("campaigns")]
    public async Task<ActionResult<IEnumerable<CivicCampaignSummaryDto>>> List(CancellationToken ct)
        => Ok(await _campaigns.ListAsync(_user.GetCurrentUserId(), ct));

    // GET /api/campaign-manager/campaigns/{id}
    [HttpGet("campaigns/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try { return Ok(await _campaigns.GetDetailAsync(_user.GetCurrentUserId(), id, ct)); }
        catch (CivicCampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // POST /api/campaign-manager/campaigns/{id}/actions
    [HttpPost("campaigns/{id:guid}/actions")]
    public async Task<IActionResult> TakeAction(Guid id, [FromBody] TakeActionRequest request, CancellationToken ct)
    {
        try { return Ok(await _campaigns.TakeActionAsync(_user.GetCurrentUserId(), id, request, ct)); }
        catch (CivicCampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CivicCampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (CivicCampaignConflictException ex) { return Conflict(new { error = ex.Message }); }
    }

    // POST /api/campaign-manager/campaigns/{id}/advance
    [HttpPost("campaigns/{id:guid}/advance")]
    public async Task<IActionResult> Advance(Guid id, CancellationToken ct)
    {
        try { return Ok(await _campaigns.AdvanceWeekAsync(_user.GetCurrentUserId(), id, ct)); }
        catch (CivicCampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CivicCampaignConflictException ex) { return Conflict(new { error = ex.Message }); }
    }

    // GET /api/campaign-manager/campaigns/{id}/results
    [HttpGet("campaigns/{id:guid}/results")]
    public async Task<IActionResult> Results(Guid id, CancellationToken ct)
    {
        try { return Ok(await _campaigns.GetResultsAsync(_user.GetCurrentUserId(), id, ct)); }
        catch (CivicCampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CivicCampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
