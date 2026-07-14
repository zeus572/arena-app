namespace Civic.API.Services.Bills;

/// <summary>
/// JSON-mode response shape the bill value-synthesis Claude call deserializes
/// into. Kept separate from the EF entities so the model never has to guess at
/// DB-only fields (Id, timestamps, provenance).
/// </summary>
public class BillSynthesisResult
{
    /// <summary>One-paragraph neutral synthesis: what the bill does and the core tradeoff.</summary>
    public string Summary { get; set; } = "";

    public List<SynthesizedAxisPosition> Positions { get; set; } = new();
}

public class SynthesizedAxisPosition
{
    public string AxisKey { get; set; } = "";

    /// <summary>-1.0 (pushes toward the axis low end) .. +1.0 (toward the high end).</summary>
    public double Score { get; set; }

    /// <summary>0..1 confidence in this positioning.</summary>
    public double Confidence { get; set; }

    public string Rationale { get; set; } = "";

    public string? Evidence { get; set; }
}
