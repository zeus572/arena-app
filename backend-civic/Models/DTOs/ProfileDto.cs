namespace Civic.API.Models.DTOs;

public class AxisScoreDto
{
    public string AxisKey { get; set; } = "";
    public string AxisName { get; set; } = "";
    public string LowLabel { get; set; } = "";
    public string HighLabel { get; set; } = "";
    public int Order { get; set; }
    public double Score { get; set; }
    public double Confidence { get; set; }
    public double Intensity { get; set; }
    public int SupportingAnswerCount { get; set; }
}

public class ArchetypeBlendItemDto
{
    public string ArchetypeKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double Percent { get; set; }
}

public class ProfileDto
{
    public string UserId { get; set; } = "";
    public int ProfileVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int AnswerCount { get; set; }

    /// <summary>The reader's chosen local-news region (state code), or null for national.</summary>
    public string? LocalityState { get; set; }

    /// <summary>The reader's 5-digit ZIP code, or null if not provided.</summary>
    public string? ZipCode { get; set; }

    /// <summary>The reader's age bracket key (e.g. "25_34"), or null if not provided.</summary>
    public string? AgeRange { get; set; }

    public List<AxisScoreDto> Axes { get; set; } = new();
    public List<ArchetypeBlendItemDto> ArchetypeBlend { get; set; } = new();
}

/// <summary>Request body for PUT /api/profile/me/locality. Null/empty ⇒ national.</summary>
public class UpdateLocalityRequest
{
    public string? LocalityState { get; set; }
}

/// <summary>
/// Request body for PUT /api/profile/me/demographics — the personalization fields
/// collected at sign-up. Both are optional; the local-news region is derived from
/// the ZIP server-side.
/// </summary>
public class UpdateDemographicsRequest
{
    public string? ZipCode { get; set; }
    public string? AgeRange { get; set; }
}
