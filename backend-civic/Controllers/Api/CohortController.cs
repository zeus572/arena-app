using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Civic.API.Models.DTOs;
using Civic.API.Services;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/cohort")]
public class CohortController : ControllerBase
{
    private readonly ICohortService _cohort;
    private readonly ICurrentUserService _user;

    public CohortController(ICohortService cohort, ICurrentUserService user)
    {
        _cohort = cohort;
        _user = user;
    }

    /// <summary>The caller's weekly cohort (up to 50 people) and its leaderboard.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<CohortDto>> Mine(CancellationToken ct)
        => Ok(await _cohort.GetOrCreateForUserAsync(_user.GetCurrentUserId(), ct));
}
