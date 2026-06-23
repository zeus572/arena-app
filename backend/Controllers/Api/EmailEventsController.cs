using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services.Email;

namespace Arena.API.Controllers.Api;

/// <summary>
/// Receives Azure Communication Services delivery reports via Azure Event Grid.
/// Hard bounces and complaints (spam reports) add the recipient to the suppression
/// list so we never mail them again. Handles the Event Grid subscription-validation
/// handshake on first wire-up.
/// </summary>
[ApiController]
[Route("api/email/events")]
public class EmailEventsController : ControllerBase
{
    private readonly ArenaDbContext _db;
    private readonly ILogger<EmailEventsController> _logger;

    public EmailEventsController(ArenaDbContext db, ILogger<EmailEventsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] JsonElement[] events, CancellationToken ct)
    {
        foreach (var evt in events)
        {
            var eventType = evt.TryGetProperty("eventType", out var et) ? et.GetString() : null;

            // Event Grid handshake: echo the validation code to confirm the endpoint.
            if (eventType == "Microsoft.EventGrid.SubscriptionValidationEvent")
            {
                if (evt.TryGetProperty("data", out var vdata)
                    && vdata.TryGetProperty("validationCode", out var code))
                    return Ok(new { validationResponse = code.GetString() });
                return Ok();
            }

            if (eventType == "Microsoft.Communication.EmailDeliveryReportReceived"
                && evt.TryGetProperty("data", out var data))
            {
                var status = data.TryGetProperty("status", out var s) ? s.GetString() : null;
                var recipient = data.TryGetProperty("recipient", out var r) ? r.GetString() : null;
                await HandleDeliveryReport(status, recipient, ct);
            }
        }

        return Ok();
    }

    private async Task HandleDeliveryReport(string? status, string? recipient, CancellationToken ct)
    {
        var reason = status switch
        {
            "Bounced" => (EmailSuppressionReason?)EmailSuppressionReason.Bounce,
            "Suppressed" => EmailSuppressionReason.Bounce,
            "Quarantined" => EmailSuppressionReason.Complaint,
            "FilteredSpam" => EmailSuppressionReason.Complaint,
            _ => null,
        };
        if (reason is null) return;

        var email = EmailPolicyService.Normalize(recipient);
        if (email is null) return;

        if (await _db.EmailSuppressions.AnyAsync(x => x.Email == email, ct)) return;

        _db.EmailSuppressions.Add(new EmailSuppression
        {
            Id = Guid.NewGuid(),
            Email = email,
            Reason = reason.Value,
            Detail = status,
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Suppressed {Email} due to delivery status {Status}", email, status);
    }
}
