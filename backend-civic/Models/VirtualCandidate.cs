using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public enum CandidateOffice
{
    President,
    Senate,
    House,
}

/// <summary>The eight register/tone categories a candidate can speak in.</summary>
public enum CampaignTone
{
    Stern,
    Angry,
    Casual,
    Hopeful,
    Sarcastic,
    Presidential,
    Folksy,
    Wonkish,
}

/// <summary>
/// An AI-driven fictional candidate. Mirrors the Celebrity Agent pattern:
/// a persona (bio/background), a values profile (axis scores), a platform
/// (planks), and a source library the LLM cites from when posting.
/// All candidates are fictional — never a real person.
/// </summary>
public class VirtualCandidate
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    public CandidateOffice Office { get; set; }

    /// <summary>US state code for Senate/House candidates. Null for President.</summary>
    [MaxLength(2)]
    public string? State { get; set; }

    /// <summary>District number for House candidates. Null otherwise.</summary>
    public int? District { get; set; }

    [Required, MaxLength(80)]
    public string Party { get; set; } = "";

    public bool IsIncumbent { get; set; }

    [Required, MaxLength(600)]
    public string Bio { get; set; } = "";

    [Required]
    public string Background { get; set; } = "";

    /// <summary>Values Profile archetype this candidate anchors (catalog key).</summary>
    [MaxLength(60)]
    public string ArchetypeKey { get; set; } = "";

    public CampaignTone DefaultTone { get; set; } = CampaignTone.Casual;

    /// <summary>1 (measured) .. 5 (fired up).</summary>
    public int DefaultIntensity { get; set; } = 2;

    /// <summary>Stylized, non-photorealistic avatar reference (e.g. a seed/url).</summary>
    [MaxLength(300)]
    public string AvatarBaseUrl { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<CandidateAxisScore> AxisScores { get; set; } = new();
    public List<CandidateIssueTone> IssueTones { get; set; } = new();
    public List<PlatformPlank> PlatformPlanks { get; set; } = new();
    public List<CandidateSource> Sources { get; set; } = new();
}

/// <summary>Candidate's position on a single Civic Arena axis (same shape as the user profile).</summary>
public class CandidateAxisScore
{
    public Guid Id { get; set; }

    public Guid CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    [Required, MaxLength(60)]
    public string AxisKey { get; set; } = "";

    /// <summary>-1.0 = low end of axis, +1.0 = high end.</summary>
    public double Score { get; set; }
}

/// <summary>Per-issue override of (tone, intensity) for a candidate.</summary>
public class CandidateIssueTone
{
    public Guid Id { get; set; }

    public Guid CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    [Required, MaxLength(60)]
    public string Issue { get; set; } = "";

    public CampaignTone Tone { get; set; }

    public int Intensity { get; set; } = 2;
}

/// <summary>A platform plank — a full position that posts can cite.</summary>
public class PlatformPlank
{
    public Guid Id { get; set; }

    public Guid CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = "";

    [Required]
    public string Body { get; set; } = "";

    public string[] IssueTags { get; set; } = Array.Empty<string>();
}

public enum SourceKind
{
    Speech,
    OpEd,
    PolicyDoc,
    Interview,
    Ad,
    TownHall,
}

/// <summary>
/// A source-library artifact (speech, op-ed, policy doc...). Shared shape with
/// the Celebrity Agents source library. Posts must cite a plank or a source.
/// </summary>
public class CandidateSource
{
    public Guid Id { get; set; }

    public Guid CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    public SourceKind Kind { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [Required]
    public string Excerpt { get; set; } = "";

    public string[] IssueTags { get; set; } = Array.Empty<string>();

    /// <summary>1 = core (used in most prompts), 3 = situational.</summary>
    public int Priority { get; set; } = 2;
}
