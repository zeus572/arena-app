using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Arena.API.Services.Email;

namespace Arena.API.Controllers.Api;

/// <summary>
/// Post-deploy send smoke test. CI calls this after every backend deploy so a
/// broken mail path fails the pipeline loudly instead of silently dropping every
/// verification email — which is exactly what happened between 2026-07-14 and
/// 2026-07-28 (a malformed ACS senderAddress 400'd on every send for 13 days
/// while the API kept returning 200).
///
/// It deliberately goes through the real <see cref="IEmailSender"/> singleton with
/// the real production <see cref="EmailOptions"/>, so it exercises the same code and
/// config that account email uses. A test that reconstructed its own ACS client
/// would have sailed straight past that outage.
///
/// Guards: requires a shared secret, and the recipient comes only from server
/// config — never the request — so this can't be turned into an open relay.
/// </summary>
[ApiController]
[Route("api/admin/email-smoke")]
public class EmailSmokeController : ControllerBase
{
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailSmokeController> _logger;

    public EmailSmokeController(
        IEmailSender sender,
        IOptions<EmailOptions> options,
        ILogger<EmailSmokeController> logger)
    {
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromHeader(Name = "X-Smoke-Secret")] string? secret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SmokeSecret) || string.IsNullOrWhiteSpace(_options.SmokeRecipient))
            return StatusCode(503, new { error = "Email smoke test is not configured." });

        if (!ConstantTimeEquals(secret, _options.SmokeSecret))
            return Unauthorized(new { error = "Invalid smoke secret." });

        // A fixed marker in the subject makes these trivially filterable in the
        // destination mailbox and unmistakable if one ever escapes to a real user.
        var stamp = DateTime.UtcNow.ToString("u");
        var subject = $"[smoke] Arena deploy send check {stamp}";
        var text =
            $"Automated post-deploy send check.\n\nSent: {stamp}\n" +
            $"Sender: {_options.SenderAddress}\nProvider: {_options.Provider}\n\n" +
            "If this arrived, the account-email path is working end to end.";
        var html = $"<p>Automated post-deploy send check.</p><p>Sent: {stamp}<br>" +
                   $"Sender: {_options.SenderAddress}<br>Provider: {_options.Provider}</p>";

        try
        {
            await _sender.SendAsync(_options.SmokeRecipient, subject, html, text, ct);
        }
        catch (Exception ex)
        {
            // The whole point: surface the provider's own words to the CI log.
            _logger.LogError(ex, "Email smoke test FAILED sending to {To}", _options.SmokeRecipient);
            return StatusCode(502, new
            {
                error = "Smoke send failed.",
                provider = _options.Provider,
                sender = _options.SenderAddress,
                detail = ex.Message,
            });
        }

        _logger.LogWarning("Email smoke test passed: accepted for delivery to {To}", _options.SmokeRecipient);
        return Ok(new
        {
            status = "accepted for delivery",
            provider = _options.Provider,
            sender = _options.SenderAddress,
            sentAt = stamp,
        });
    }

    private static bool ConstantTimeEquals(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided)) return false;
        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(a), SHA256.HashData(b));
    }
}
