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
        var content = new EmailContent(subject)
        {
            PlainText = textBody,
            Html = htmlBody,
        };
        var message = new EmailMessage(
            senderAddress: _options.SenderAddress,
            recipientAddress: toAddress,
            content: content);

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
}
