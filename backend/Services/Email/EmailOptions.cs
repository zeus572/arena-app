namespace Arena.API.Services.Email;

/// <summary>
/// Strongly-typed view of the <c>Email</c> configuration section. Non-secret
/// values live in appsettings; the ACS connection string is a secret and must
/// come from user-secrets / Azure App settings / managed identity only.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"acs" to send via Azure Communication Services, anything else
    /// (or unset) uses the dev no-op sender that just logs the link.</summary>
    public string Provider { get; set; } = "none";

    /// <summary>Verified ACS sender, e.g. "DoNotReply@your-domain.com".</summary>
    public string SenderAddress { get; set; } = "DoNotReply@localhost";

    /// <summary>Display name shown as the From name.</summary>
    public string SenderName { get; set; } = "Political Arena";

    /// <summary>Physical sender identity shown in the email footer (CAN-SPAM).</summary>
    public string SenderIdentity { get; set; } = "Political Arena";

    /// <summary>ACS connection string (secret). Null = use managed identity via
    /// <see cref="AcsEndpoint"/>.</summary>
    public string? AcsConnectionString { get; set; }

    /// <summary>ACS resource endpoint, used with managed identity in production.</summary>
    public string? AcsEndpoint { get; set; }

    /// <summary>DNS MX deliverability check at signup. Auto-skipped in Development.</summary>
    public bool CheckMx { get; set; } = true;

    /// <summary>Extra disposable domains to reject, on top of the bundled list.</summary>
    public List<string> DisposableDomains { get; set; } = new();

    /// <summary>Allow-listed frontend base URLs keyed by app ("arena", "civic").
    /// Verification/reset links are built only from these — never a client value.</summary>
    public Dictionary<string, string> AppUrls { get; set; } = new();

    public RateLimitOptions RateLimit { get; set; } = new();

    public class RateLimitOptions
    {
        /// <summary>Max account emails per address within the window.</summary>
        public int PerAddressPerHour { get; set; } = 5;
        /// <summary>Max account emails per client IP within the window.</summary>
        public int PerIpPerHour { get; set; } = 20;
        public int WindowMinutes { get; set; } = 60;
    }
}
