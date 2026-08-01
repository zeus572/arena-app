namespace Arena.API.Services.Reporting;

/// <summary>
/// Strongly-typed view of the <c>DailyReport</c> configuration section. Non-secret values
/// live in appsettings; <see cref="Secret"/> and <see cref="CivicSecret"/> are secrets and
/// must come from user-secrets / Azure App settings only — never a committed file.
/// </summary>
public class DailyReportOptions
{
    public const string SectionName = "DailyReport";

    /// <summary>Switch for the SCHEDULED send. Off by default so the report can't start
    /// emailing from a dev box (or a freshly deployed environment) before anyone asked it to.
    /// The manual trigger still works when this is false — that's the point of it: pull a
    /// report on demand without committing to a daily one.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Mailbox the report goes to. Server-side only — no endpoint accepts a
    /// client-supplied recipient, so the trigger can't be turned into a relay.</summary>
    public string Recipient { get; set; } = "";

    /// <summary>UTC hour (0–23) after which the day's report may be sent. Default 14:00 UTC
    /// = 7am Pacific. The report covers the most recently COMPLETED UTC day, so a morning
    /// send always describes a full 24 hours rather than a partial one.</summary>
    public int HourUtc { get; set; } = 14;

    /// <summary>Base URL of the Civic backend, e.g. "https://civic-api-fexzo2.azurewebsites.net".
    /// Blank omits the Civic section (and says so in the email rather than silently dropping it).</summary>
    public string CivicBaseUrl { get; set; } = "";

    /// <summary>Shared secret sent as X-Report-Secret to Civic's daily-stats endpoint.
    /// Must match Civic's <c>Reporting:Secret</c>.</summary>
    public string CivicSecret { get; set; } = "";

    /// <summary>Shared secret guarding Arena's own operator endpoints
    /// (<c>/api/admin/daily-stats</c>, <c>/api/admin/daily-report/send</c>).
    /// Blank disables those endpoints entirely.</summary>
    public string Secret { get; set; } = "";

    /// <summary>Ask Claude for the "what happened today" opener. When false — or whenever the
    /// call fails or Anthropic is disabled/keyless — the report falls back to a deterministic
    /// summary sentence, so the email always goes out.</summary>
    public bool UseLlmNarrative { get; set; } = true;
}
