using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Proxies premium-user-initiated debate creation from a civic Briefing into
/// the debate backend's <c>POST /api/debates</c> endpoint. The debate backend's
/// existing <c>[Authorize(Policy = "Premium")]</c> gate runs on the same JWT
/// (we just forward the Authorization header), so there is no debate-side
/// change needed for premium enforcement.
/// </summary>
[ApiController]
[Authorize]
[Route("api/briefings/{slug}/debate")]
public class DebateInitController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DebateInitController> _log;

    public DebateInitController(
        CivicDbContext db,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<DebateInitController> log)
    {
        _db = db;
        _httpFactory = httpFactory;
        _config = config;
        _log = log;
    }

    public record InitDebateRequest(
        string? Format,
        Guid? ProponentId,
        Guid? OpponentId,
        string? ArenaSlug);

    public record InitDebateResponse(
        Guid DebateId,
        string DebateUrl);

    [HttpPost]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<IActionResult> StartFromBriefing(
        string slug,
        [FromBody] InitDebateRequest? body,
        CancellationToken ct)
    {
        // Premium-only. The debate backend will reject Free tokens too, but
        // we enforce up-front so Free users see a clean 403 from civic.
        var plan = User.FindFirst("plan")?.Value;
        if (!string.Equals(plan, "Premium", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Starting a debate from a briefing requires a Premium account.",
            });
        }

        var briefing = await _db.Briefings.FirstOrDefaultAsync(b => b.Slug == slug, ct);
        if (briefing is null)
        {
            return NotFound(new { error = $"Briefing '{slug}' not found." });
        }

        var token = ExtractBearer(Request.Headers.Authorization.ToString());
        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized();
        }

        var payload = new
        {
            topic = briefing.ThinkDeeperQuestion,
            description = briefing.Summary30,
            format = body?.Format ?? "standard",
            proponentId = body?.ProponentId,
            opponentId = body?.OpponentId,
            arenaSlug = body?.ArenaSlug,
        };

        var http = _httpFactory.CreateClient("DebateApi");
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/debates")
        {
            Content = JsonContent.Create(payload),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, ct);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "Debate API unreachable while initiating from briefing {Slug}", slug);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Could not reach the debate service. Try again in a moment.",
            });
        }

        var rawBody = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("Debate API returned {Status}: {Body}", (int)resp.StatusCode, rawBody);
            // Surface 4xx from debate (e.g. 401, 403 if the token is somehow rejected)
            // but collapse 5xx to 502 so the civic client gets a consistent shape.
            var status = (int)resp.StatusCode >= 500
                ? StatusCodes.Status502BadGateway
                : (int)resp.StatusCode;
            return StatusCode(status, new { error = "Debate service rejected the request.", details = rawBody });
        }

        Guid debateId;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var idElement = doc.RootElement.TryGetProperty("id", out var found)
                ? found
                : throw new InvalidOperationException("Response missing 'id'.");
            debateId = idElement.GetGuid();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Debate API returned unparseable success body: {Body}", rawBody);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Debate service returned an unexpected response.",
            });
        }

        var debateUrl = $"{DebateWebBaseUrl().TrimEnd('/')}/debates/{debateId}";
        return Ok(new InitDebateResponse(debateId, debateUrl));
    }

    private string DebateWebBaseUrl() =>
        _config["Debate:WebBaseUrl"] ?? "http://localhost:5173";

    private static string? ExtractBearer(string? header)
    {
        if (string.IsNullOrEmpty(header)) return null;
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }
}
