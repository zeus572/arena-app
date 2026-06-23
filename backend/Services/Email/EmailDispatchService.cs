using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services.Email;

public enum DispatchResult { Sent, Suppressed, RateLimited, Failed }

/// <summary>
/// The single entry point controllers use to send account email. Enforces the
/// suppression list and rate limits before handing off to <see cref="IEmailSender"/>,
/// builds the templated message (with sender-identity footer for CAN-SPAM), and
/// records each send for durable per-address rate limiting.
/// </summary>
public class EmailDispatchService
{
    private readonly ArenaDbContext _db;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EmailDispatchService> _logger;

    public EmailDispatchService(
        ArenaDbContext db,
        IEmailSender sender,
        IOptions<EmailOptions> options,
        IMemoryCache cache,
        ILogger<EmailDispatchService> logger)
    {
        _db = db;
        _sender = sender;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DispatchResult> SendAccountEmailAsync(
        User user,
        AccountTokenPurpose purpose,
        string rawToken,
        string app,
        string? ip,
        CancellationToken ct = default)
    {
        var email = user.Email;

        if (await IsSuppressedAsync(email, ct))
        {
            _logger.LogInformation("Skipping {Purpose} email to suppressed address {Email}", purpose, email);
            return DispatchResult.Suppressed;
        }

        if (!await WithinRateLimitAsync(email, ip, ct))
        {
            _logger.LogWarning("Rate limit hit for {Purpose} email to {Email} (ip {Ip})", purpose, email, ip);
            return DispatchResult.RateLimited;
        }

        var link = BuildLink(app, purpose, rawToken);
        var (subject, html, text) = purpose == AccountTokenPurpose.PasswordReset
            ? BuildReset(link)
            : BuildVerify(link);

        try
        {
            await _sender.SendAsync(email, subject, html, text, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {Purpose} email to {Email}", purpose, email);
            return DispatchResult.Failed;
        }

        _db.EmailSendLogs.Add(new EmailSendLog
        {
            Id = Guid.NewGuid(),
            Email = email,
            Purpose = purpose,
            IpAddress = ip,
        });
        await _db.SaveChangesAsync(ct);
        return DispatchResult.Sent;
    }

    private async Task<bool> IsSuppressedAsync(string email, CancellationToken ct) =>
        await _db.EmailSuppressions.AnyAsync(s => s.Email == email, ct);

    private async Task<bool> WithinRateLimitAsync(string email, string? ip, CancellationToken ct)
    {
        var window = TimeSpan.FromMinutes(_options.RateLimit.WindowMinutes);
        var since = DateTime.UtcNow - window;

        var perAddress = await _db.EmailSendLogs.CountAsync(l => l.Email == email && l.SentAt >= since, ct);
        if (perAddress >= _options.RateLimit.PerAddressPerHour) return false;

        if (!string.IsNullOrWhiteSpace(ip))
        {
            var key = $"emailrate:ip:{ip}";
            var count = _cache.GetOrCreate(key, e =>
            {
                e.AbsoluteExpirationRelativeToNow = window;
                return 0;
            });
            if (count >= _options.RateLimit.PerIpPerHour) return false;
            _cache.Set(key, count + 1, window);
        }

        return true;
    }

    /// <summary>Build the user-facing link from the allow-listed base for the app.
    /// Never trusts a client-supplied URL — prevents open-redirect/phishing.</summary>
    private string BuildLink(string app, AccountTokenPurpose purpose, string rawToken)
    {
        var key = string.IsNullOrWhiteSpace(app) ? "arena" : app.Trim().ToLowerInvariant();
        if (!_options.AppUrls.TryGetValue(key, out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = _options.AppUrls.GetValueOrDefault("arena") ?? "http://localhost:5173";

        var path = purpose == AccountTokenPurpose.PasswordReset ? "reset-password" : "verify-email";
        return $"{baseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(rawToken)}";
    }

    private (string subject, string html, string text) BuildVerify(string link)
    {
        const string subject = "Verify your Political Arena email";
        var html = Wrap(
            "Confirm your email",
            $"<p>Thanks for signing up. Please confirm your email address to finish setting up your account.</p>" +
            $"<p><a href=\"{link}\" style=\"display:inline-block;padding:10px 18px;background:#dc2626;color:#fff;text-decoration:none;border-radius:8px\">Verify email</a></p>" +
            $"<p style=\"color:#666;font-size:12px\">Or paste this link into your browser:<br>{link}</p>" +
            "<p style=\"color:#666;font-size:12px\">This link expires in 24 hours. If you didn't create an account, you can ignore this email.</p>");
        var text =
            $"Confirm your Political Arena email by opening this link (expires in 24 hours):\n\n{link}\n\n" +
            "If you didn't create an account, you can ignore this email.\n\n" + FooterText();
        return (subject, html, text);
    }

    private (string subject, string html, string text) BuildReset(string link)
    {
        const string subject = "Reset your Political Arena password";
        var html = Wrap(
            "Reset your password",
            $"<p>We received a request to reset your password. Click below to choose a new one.</p>" +
            $"<p><a href=\"{link}\" style=\"display:inline-block;padding:10px 18px;background:#dc2626;color:#fff;text-decoration:none;border-radius:8px\">Reset password</a></p>" +
            $"<p style=\"color:#666;font-size:12px\">Or paste this link into your browser:<br>{link}</p>" +
            "<p style=\"color:#666;font-size:12px\">This link expires in 1 hour. If you didn't request this, you can safely ignore this email — your password won't change.</p>");
        var text =
            $"Reset your Political Arena password by opening this link (expires in 1 hour):\n\n{link}\n\n" +
            "If you didn't request this, you can ignore this email.\n\n" + FooterText();
        return (subject, html, text);
    }

    private string Wrap(string heading, string body) =>
        $"<div style=\"font-family:system-ui,Arial,sans-serif;max-width:480px;margin:0 auto;color:#111\">" +
        $"<h2 style=\"color:#111\">{heading}</h2>{body}" +
        $"<hr style=\"border:none;border-top:1px solid #eee;margin:20px 0\">" +
        $"<p style=\"color:#999;font-size:11px\">{System.Net.WebUtility.HtmlEncode(_options.SenderIdentity)}. " +
        "This is a transactional message about your account.</p></div>";

    private string FooterText() =>
        $"{_options.SenderIdentity}. This is a transactional message about your account.";
}
