using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Civic.API.Models.DTOs;
using Civic.API.Services;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/zeitgeist")]
public class ZeitgeistController : ControllerBase
{
    private readonly IZeitgeistService _zeitgeist;

    public ZeitgeistController(IZeitgeistService zeitgeist) => _zeitgeist = zeitgeist;

    /// <summary>Aggregate discoveries from how people are governing themselves on Civersify.</summary>
    [HttpGet]
    public async Task<ActionResult<ZeitgeistDto>> Get(CancellationToken ct)
        => Ok(await _zeitgeist.BuildAsync(ct));
}
