namespace Arena.API.Services.Email;

/// <summary>
/// Dev/fallback sender. Doesn't send anything — it logs the recipient, subject
/// and a plain-text rendering so a developer can copy the verification/reset link
/// out of the console without a configured email provider. Selected whenever
/// <c>Email:Provider</c> is not "acs".
/// </summary>
public class NoOpEmailSender : IEmailSender
{
    private readonly ILogger<NoOpEmailSender> _logger;

    public NoOpEmailSender(ILogger<NoOpEmailSender> logger) => _logger = logger;

    public Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NoOpEmailSender] Would send email\n  To: {To}\n  Subject: {Subject}\n  Body:\n{Body}",
            toAddress, subject, textBody);
        return Task.CompletedTask;
    }
}
