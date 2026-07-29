using Azure;
using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace Arena.API.Services.Email;

/// <summary>
/// Sends mail through Azure Communication Services. Uses a connection string when
/// one is configured (dev / user-secrets) and falls back to managed identity
/// against <c>Email:Acs:Endpoint</c> in production — mirroring the
/// <see cref="DefaultAzureCredential"/> pattern used for Postgres in Program.cs.
/// </summary>
public class AcsEmailSender : IEmailSender
{
    private readonly EmailClient _client;
    private readonly EmailOptions _options;
    private readonly ILogger<AcsEmailSender> _logger;

    public AcsEmailSender(IOptions<EmailOptions> options, ILogger<AcsEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.AcsConnectionString))
        {
            _client = new EmailClient(_options.AcsConnectionString);
        }
        else if (!string.IsNullOrWhiteSpace(_options.AcsEndpoint))
        {
            _client = new EmailClient(new Uri(_options.AcsEndpoint), new DefaultAzureCredential());
        }
        else
        {
            throw new InvalidOperationException(
                "Email:Provider is 'acs' but neither Email:AcsConnectionString nor Email:AcsEndpoint is configured.");
        }
    }

    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken ct = default)
    {
        var message = BuildMessage(_options, toAddress, subject, htmlBody, textBody);

        try
        {
            // WaitUntil.Started returns once accepted for delivery; final delivery
            // status (bounce/complaint) arrives asynchronously via the Event Grid
            // webhook that feeds the suppression list.
            await _client.SendAsync(WaitUntil.Started, message, ct);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "ACS rejected email to {To} (status {Status})", toAddress, ex.Status);
            throw;
        }
    }

    /// <summary>
    /// Builds the ACS message. Pure and public so the sender-address contract is
    /// unit-testable without a live ACS client.
    /// </summary>
    /// <remarks>
    /// <c>senderAddress</c> MUST be the BARE address. ACS rejects the RFC 5322
    /// "Display Name &lt;address&gt;" form with a 400 — "Request body validation
    /// error. See property 'senderAddress'". Building it as
    /// <c>$"{SenderName} &lt;{SenderAddress}&gt;"</c> silently broke ALL account
    /// email from 2026-07-14 to 2026-07-28; do not reintroduce it.
    /// The friendly From name belongs on the ACS sender-username resource:
    /// <code>
    /// az communication email domain sender-username update \
    ///   --email-service-name &lt;svc&gt; --domain-name &lt;domain&gt; \
    ///   --sender-username &lt;name&gt; --display-name "&lt;SenderName&gt;"
    /// </code>
    /// </remarks>
    public static EmailMessage BuildMessage(
        EmailOptions options,
        string toAddress,
        string subject,
        string htmlBody,
        string textBody)
    {
        var content = new EmailContent(subject)
        {
            PlainText = textBody,
            Html = htmlBody,
        };
        return new EmailMessage(
            senderAddress: options.SenderAddress,
            recipientAddress: toAddress,
            content: content);
    }
}
