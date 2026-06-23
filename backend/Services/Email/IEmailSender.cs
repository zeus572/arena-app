namespace Arena.API.Services.Email;

/// <summary>
/// Low-level transport for a single email. Swapping providers (ACS, SendGrid,
/// SMTP, ...) is a one-class change behind this interface.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken ct = default);
}
