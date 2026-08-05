using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>How a prediction ended. Cancelled is excluded from calibration entirely.</summary>
public enum PredictionOutcome
{
    Unresolved,
    Yes,
    No,
    /// <summary>The cancellation condition fired. Scored as nothing, not as a miss.</summary>
    Cancelled,
}

/// <summary>
/// A measurable question with a deadline and an objective resolution rule (PRD 06 §7.1).
///
/// Binary only for MVP. Multi-outcome questions need a different scoring rule and a
/// different UI, and shipping a half version would make the calibration numbers wrong.
///
/// Nothing here auto-resolves. There is no objective resolution feed to read, so
/// PredictionResolutionService flags overdue questions for an editor and stops. A
/// prediction that resolved itself from a guess would be worse than one that resolved late.
/// </summary>
public class Prediction
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    public Guid? RoomId { get; set; }
    public Room? Room { get; set; }

    [Required, MaxLength(500)]
    public string Proposition { get; set; } = "";

    /// <summary>Stated BEFORE anyone answers (design 1v). Required.</summary>
    [Required, MaxLength(1000)]
    public string ResolutionCriteria { get; set; } = "";

    /// <summary>What will be checked to resolve it. Required — a question with no named
    /// resolution source is not measurable, it is rhetorical.</summary>
    [Required, MaxLength(500)]
    public string ResolutionSourceDescription { get; set; } = "";

    public Guid? ResolutionSourceRefId { get; set; }

    /// <summary>When the question stops applying (e.g. "cancelled if the bill is withdrawn").
    /// Required, and shown up front — an unstated cancellation rule is an escape hatch.</summary>
    [Required, MaxLength(500)]
    public string CancellationPolicy { get; set; } = "";

    public DateTime OpensAt { get; set; } = DateTime.UtcNow;

    /// <summary>Forecasts are frozen after this.</summary>
    public DateTime ClosesAt { get; set; }

    public DateTime? ResolvesByAt { get; set; }

    public PredictionOutcome Outcome { get; set; } = PredictionOutcome.Unresolved;

    public DateTime? ResolvedAt { get; set; }

    [MaxLength(1000)]
    public string? ResolutionEvidence { get; set; }

    [MaxLength(120)]
    public string? ResolvedBy { get; set; }

    [MaxLength(120)]
    public string? EditorialOwner { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Draft;

    // Cached aggregates, recomputed on each submit. Cheap, and it avoids a group-by on
    // every read of a room that shows the crowd bar.
    public int ForecastCount { get; set; }
    public double MeanProbability { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One person's forecast.
///
/// There is no IsPrivate flag because there is no endpoint that can return another user's
/// row. Privacy is enforced by absence, which cannot be misconfigured.
/// </summary>
public class UserPrediction
{
    public Guid Id { get; set; }

    public Guid PredictionId { get; set; }
    public Prediction? Prediction { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    /// <summary>0..100. An integer because that is what the slider produces, and because
    /// float equality in calibration bucketing is a needless source of pain.</summary>
    public int Probability { get; set; }

    /// <summary>How many times they moved it before close. Changing your mind is fine.</summary>
    public int UpdateCount { get; set; }

    /// <summary>Written at resolution. Null while unresolved or if cancelled.</summary>
    public double? BrierScore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
