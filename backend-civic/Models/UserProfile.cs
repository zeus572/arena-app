using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public class UserProfile
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public int ProfileVersion { get; set; } = 1;

    /// <summary>
    /// The reader's chosen local-news region (2-letter state code, e.g. "WA"),
    /// or null for national-only. Drives which local briefings and coalition
    /// provisions this user can see. See <see cref="Localities"/>.
    /// </summary>
    [MaxLength(2)]
    public string? LocalityState { get; set; }

    /// <summary>
    /// The reader's 5-digit US ZIP code, collected at sign-up for personalization,
    /// or null if not provided. <see cref="LocalityState"/> is derived from this
    /// via <see cref="Localities.StateForZip"/>.
    /// </summary>
    [MaxLength(5)]
    public string? ZipCode { get; set; }

    /// <summary>
    /// The reader's self-reported age bracket (an <see cref="AgeRanges"/> key such
    /// as "25_34"), collected at sign-up for personalization, or null if not given.
    /// </summary>
    [MaxLength(20)]
    public string? AgeRange { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ArchetypePercent> ArchetypeBlend { get; set; } = new();
    public List<ProfileAxisScore> AxisScores { get; set; } = new();
}

public class ArchetypePercent
{
    [Required, MaxLength(60)]
    public string ArchetypeKey { get; set; } = "";

    public double Percent { get; set; }
}

public class ProfileAxisScore
{
    public Guid Id { get; set; }

    public Guid UserProfileId { get; set; }
    public UserProfile? UserProfile { get; set; }

    [Required, MaxLength(60)]
    public string AxisKey { get; set; } = "";

    /// <summary>-1.0 = low end of axis, +1.0 = high end. 0 = neutral or no data.</summary>
    public double Score { get; set; }

    /// <summary>0..1 weighted confidence of the supporting answers.</summary>
    public double Confidence { get; set; }

    /// <summary>0..1 weighted intensity of the supporting answers.</summary>
    public double Intensity { get; set; }

    public Guid[] SupportingAnswerIds { get; set; } = Array.Empty<Guid>();
}
