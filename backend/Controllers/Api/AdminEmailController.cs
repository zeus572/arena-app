using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services.Email;

namespace Arena.API.Controllers.Api;

public record AdminResendRequest(List<string> Emails, string? App);

public record AdminResendResult(string Email, string Outcome);

/// <summary>
/// Operator tooling for account email. Exists because the 2026-07-14 senderAddress
/// outage stranded users whose verification mail silently failed: the normal
/// <c>/auth/resend-verification</c> is <c>[Authorize]</c>, so re-sending on someone's
/// behalf otherwise means impersonating them with a minted JWT.
///
/// Guarded by the same shared secret as the smoke endpoint, and deliberately narrow:
/// it will only mail an address that is ALREADY a registered, non-anonymous,
/// UNVERIFIED user. It never creates users and never mails an unknown address, so it
/// cannot be used as a relay. Sending goes through the same
/// <see cref="EmailDispatchService"/> as a user-initiated resend, so suppression and
/// rate limits still apply.
/// </summary>
[ApiController]
[Route("api/admin/email")]
public class AdminEmailController : ControllerBase
{
    private readonly ArenaDbContext _db;
    private readonly AccountTokenService _accountTokens;
    private readonly EmailDispatchService _dispatch;
    private readonly EmailOptions _options;
    private readonly ILogger<AdminEmailController> _logger;

    public AdminEmailController(
        ArenaDbContext db,
        AccountTokenService accountTokens,
        EmailDispatchService dispatch,
        IOptions<EmailOptions> options,
        ILogger<AdminEmailController> logger)
    {
        _db = db;
        _accountTokens = accountTokens;
        _dispatch = dispatch;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(
        [FromHeader(Name = "X-Smoke-Secret")] string? secret,
        [FromBody] AdminResendRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SmokeSecret))
            return StatusCode(503, new { error = "Operator email endpoints are not configured." });

        if (!SharedSecret.Matches(secret, _options.SmokeSecret))
            return Unauthorized(new { error = "Invalid secret." });

        if (request?.Emails is null || request.Emails.Count == 0)
            return BadRequest(new { error = "Provide at least one email." });

        if (request.Emails.Count > 25)
            return BadRequest(new { error = "Refusing to send more than 25 at once." });

        // Only these two app keys build a link; anything else would silently fall back
        // to the arena base URL, which is how a Civic user ends up with a debatearena link.
        var app = (request.App ?? "arena").Trim().ToLowerInvariant();
        if (app is not ("arena" or "civic"))
            return BadRequest(new { error = "App must be 'arena' or 'civic'." });

        var results = new List<AdminResendResult>();

        foreach (var raw in request.Emails)
        {
            var email = EmailPolicyService.Normalize(raw);
            if (email is null)
            {
                results.Add(new AdminResendResult(raw, "malformed"));
                continue;
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == email && !u.IsAnonymous, ct);

            if (user is null)
            {
                results.Add(new AdminResendResult(email, "no_such_user"));
                continue;
            }

            if (user.EmailVerified)
            {
                results.Add(new AdminResendResult(email, "already_verified"));
                continue;
            }

            var token = await _accountTokens.IssueAsync(user, AccountTokenPurpose.EmailVerify, ct);
            var dispatch = await _dispatch.SendAccountEmailAsync(
                user, AccountTokenPurpose.EmailVerify, token, app, ip: null, ct);

            // Warning level so it lands in App Insights, which only collects >= Warning.
            _logger.LogWarning(
                "Operator-initiated verification resend to {Email} (app {App}): {Outcome}",
                email, app, dispatch);

            results.Add(new AdminResendResult(email, dispatch.ToString().ToLowerInvariant()));
        }

        var sent = results.Count(r => r.Outcome == "sent");
        return Ok(new { app, requested = request.Emails.Count, sent, results });
    }
}
