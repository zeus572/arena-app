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

    /// <summary>Display name shown as the From name. NOT sent per-message — ACS
    /// takes the display name from the sender-username resource, and rejects a
    /// "Display Name &lt;address&gt;" senderAddress with a 400. Kept here so the
    /// intended name is versioned alongside the rest of the email config; apply
    /// it with <c>az communication email domain sender-username update
    /// --display-name</c>.</summary>
    public string SenderName { get; set; } = "Political Arena";

    /// <summary>Legal-entity identity shown in the email footer (CAN-SPAM). Set
    /// this to the real registered business name in production.</summary>
    public string SenderIdentity { get; set; } = "Political Arena";

    /// <summary>Physical mailing address shown in the email footer (CAN-SPAM
    /// requires a valid postal address on commercial mail; we include it on
    /// transactional mail too as the compliance-safest posture). Blank in dev —
    /// set the real address as an app setting in production.</summary>
    public string SenderPostalAddress { get; set; } = "";

    /// <summary>Base URL for the public legal pages (Privacy Policy / Terms),
    /// e.g. "https://civersify.com". Footer links are built from this; blank
    /// omits the links.</summary>
    public string LegalBaseUrl { get; set; } = "";

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

    /// <summary>Shared secret for the post-deploy send smoke test
    /// (<c>POST /api/admin/email-smoke</c>). Blank disables the endpoint entirely.
    /// Set as a prod app setting + matching GitHub secret; never commit it.</summary>
    public string SmokeSecret { get; set; } = "";

    /// <summary>Mailbox the smoke test sends to. Server-side only — the endpoint
    /// never accepts a client-supplied recipient, so it can't be used as a relay.</summary>
    public string SmokeRecipient { get; set; } = "";

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
