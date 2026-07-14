namespace Civic.API.Models.DTOs;

/// <summary>List-row shape for GET /api/bills.</summary>
public class BillSummaryDto
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? ShortTitle { get; set; }

    /// <summary>Display identifier, e.g. "HR 1 · 118th Congress".</summary>
    public string Identifier { get; set; } = "";

    public string Sponsor { get; set; } = "";
    public string? Party { get; set; }
    public string Status { get; set; } = "";
    public string Jurisdiction { get; set; } = "";
    public string? JurisdictionRegion { get; set; }
    public DateTime IntroducedDate { get; set; }
    public DateTime? LatestActionDate { get; set; }

    /// <summary>Short teaser — the LLM synthesis summary when present, else the source summary.</summary>
    public string Teaser { get; set; } = "";

    /// <summary>Number of value axes this bill has been positioned on.</summary>
    public int AxisCount { get; set; }
}

/// <summary>
/// One axis on the bill-detail radial: where the bill pushes, and — when the
/// caller has a compass — where the user sits and how the two relate.
/// </summary>
public class BillAxisAlignmentDto
{
    public string AxisKey { get; set; } = "";
    public string AxisName { get; set; } = "";
    public string LowLabel { get; set; } = "";
    public string HighLabel { get; set; } = "";
    public int Order { get; set; }

    /// <summary>Where the bill pushes on this axis (-1..+1).</summary>
    public double BillScore { get; set; }
    public double BillConfidence { get; set; }
    public string Rationale { get; set; } = "";
    public string? Evidence { get; set; }

    /// <summary>The user's own score on this axis (-1..+1), or null when they have no profile.</summary>
    public double? UserScore { get; set; }

    /// <summary>
    /// Relationship between the user and the bill on this axis when both are known:
    /// "aligned", "mixed", or "tension". Null when the user has no profile.
    /// </summary>
    public string? Alignment { get; set; }
}

/// <summary>Detail shape for GET /api/bills/{id}.</summary>
public class BillDetailDto
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = "";
    public int Congress { get; set; }
    public string BillType { get; set; } = "";
    public int Number { get; set; }
    public string Identifier { get; set; } = "";
    public string Title { get; set; } = "";
    public string? ShortTitle { get; set; }
    public string Summary { get; set; } = "";
    public string? SynthesisSummary { get; set; }
    public string Sponsor { get; set; } = "";
    public string? Party { get; set; }
    public string Status { get; set; } = "";
    public string Jurisdiction { get; set; } = "";
    public string? JurisdictionRegion { get; set; }
    public DateTime IntroducedDate { get; set; }
    public DateTime? LatestActionDate { get; set; }
    public string? FullTextUrl { get; set; }
    public string? SourceUrl { get; set; }

    /// <summary>True when the caller has a scored compass (signed-in or anon-with-answers).</summary>
    public bool HasUserCompass { get; set; }

    /// <summary>
    /// Overall alignment percentage (0..100) between the user's compass and the
    /// bill across the shared axes, or null when the user has no profile.
    /// </summary>
    public int? OverallAlignmentPercent { get; set; }

    public List<BillAxisAlignmentDto> Axes { get; set; } = new();
}
