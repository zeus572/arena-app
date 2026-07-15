namespace Civic.API.Services.Bills;

/// <summary>
/// Config for bill ingestion + value synthesis. Bound from the "Bills" section.
/// Live Congress.gov ingestion only runs when <see cref="Enabled"/> is true AND
/// an <see cref="ApiKey"/> is present; otherwise the seeded bills
/// (<c>Seed/bills.json</c>) already cover the whole experience offline.
/// </summary>
public class BillOptions
{
    public const string SectionName = "Bills";

    /// <summary>Master switch for live Congress.gov ingestion. Seeding is unaffected.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// api.data.gov / Congress.gov API key. Empty ⇒ the live source is skipped
    /// (seed bills still load). Never commit a real key — set via user-secrets / env.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Congress number to pull recent bills from (e.g. 118).</summary>
    public int Congress { get; set; } = 118;

    /// <summary>How often the ingestion loop ticks.</summary>
    public int IngestIntervalHours { get; set; } = 6;

    /// <summary>Max bills to pull from the live API per tick.</summary>
    public int MaxBillsPerRun { get; set; } = 20;

    /// <summary>How often the synthesis loop ticks.</summary>
    public int SynthesisIntervalMinutes { get; set; } = 10;

    /// <summary>Max bills to synthesize per synthesis tick.</summary>
    public int SynthesisBatchSize { get; set; } = 3;

    /// <summary>Give up on a bill after this many failed synthesis attempts.</summary>
    public int MaxSynthesisAttempts { get; set; } = 3;
}
