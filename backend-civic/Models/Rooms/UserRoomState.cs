using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// The reading density dial (design 1c).
///
/// Stored on <see cref="UserProfile"/> rather than here, because the handoff is explicit
/// that density is remembered PER USER, not per room.
/// </summary>
public enum RoomDensity
{
    /// <summary>Prose carries the meaning. One idea per screen height. Default first visit.</summary>
    Read,
    /// <summary>Prose collapses to labelled rows; status marks appear.</summary>
    Brief,
    /// <summary>Full object tables, sortable and filterable.</summary>
    Board,
}

/// <summary>
/// One person's relationship to one room.
///
/// Rows exist for anonymous users too — <see cref="UserId"/> carries the literal
/// "anonymous" or the client's X-User-Id, same as everywhere else in Civic.
/// </summary>
public class UserRoomState
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public Guid RoomId { get; set; }
    public Room? Room { get; set; }

    /// <summary>
    /// The revision this reader has actually seen. Everything after it is their delta —
    /// this single integer is what makes "since your last visit" cheap.
    /// </summary>
    public int LastSeenRevision { get; set; }

    public DateTime LastVisitedAt { get; set; } = DateTime.UtcNow;

    public bool Following { get; set; }
    public DateTime? FollowedAt { get; set; }

    /// <summary>
    /// The ambient path (design 1a): a thin bar per section that only remembers. Nothing is
    /// required and nothing gates on it — "the bars just remember where you have been."
    /// </summary>
    public List<SectionProgress> SectionProgress { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>How far through one room section a reader has got.</summary>
public class SectionProgress
{
    [Required, MaxLength(40)]
    public string SectionKey { get; set; } = "";

    public bool Opened { get; set; }

    public int ItemsSeen { get; set; }
    public int ItemsTotal { get; set; }

    public DateTime? LastOpenedAt { get; set; }
}
