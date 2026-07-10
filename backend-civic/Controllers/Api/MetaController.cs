using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/meta")]
public class MetaController : ControllerBase
{
    private readonly IConfiguration _config;

    public MetaController(IConfiguration config) => _config = config;

    /// <summary>
    /// Client/API contract info. The Android app checks this on launch and shows a
    /// blocking update prompt when its versionCode is below MinAndroidAppVersion.
    /// Bump Meta:MinAndroidAppVersion only when an API change genuinely breaks
    /// already-shipped app bundles (the API is otherwise additive-only, because
    /// Play Store review lag keeps old bundles alive for days).
    /// </summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        MinAndroidAppVersion = _config.GetValue("Meta:MinAndroidAppVersion", 0),
    });
}
