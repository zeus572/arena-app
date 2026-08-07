using Civic.API.Models.Rooms;

namespace Civic.API.Services.Rooms;

/// <summary>
/// The six-hour rule: an object flagged for review and left unreviewed disappears from
/// NEW sessions after six hours.
///
/// Enforced at read time, in this one pure function, for a reason worth writing down. A
/// background job that hid things would (a) yank content out from under someone mid-read,
/// and (b) race the reviewer who is at that moment fixing it. Hiding at read time, keyed on
/// whether the session predates the flag, does neither.
///
/// Nothing is ever deleted. The sweep service only alerts.
/// </summary>
public static class RoomVisibility
{
    /// <summary>How long an unreviewed flag may sit before new readers stop seeing the object.</summary>
    public static readonly TimeSpan UnreviewedGrace = TimeSpan.FromHours(6);

    /// <summary>
    /// A reader counts as mid-session if they were already in this room within this window.
    /// Anything longer and someone who wandered off for lunch would keep seeing content the
    /// grace period has expired on.
    /// </summary>
    public static readonly TimeSpan SessionWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Whether an object carrying <paramref name="flag"/> should be hidden from this reader.
    /// </summary>
    /// <param name="flag">The unresolved flag on the object.</param>
    /// <param name="now">Current time, injected so the boundary is testable.</param>
    /// <param name="sessionStartedAt">
    /// When this reader's session in the room began — their last visit, if it was recent
    /// enough to count as the same session. Null for anonymous or first-time readers, who
    /// are always "new" and therefore always get the protection.
    /// </param>
    public static bool IsHiddenForReader(ReviewFlag flag, DateTime now, DateTime? sessionStartedAt)
    {
        if (flag.ResolvedAt is not null) return false;

        // Inside the grace period the object is visible to everyone — six hours is the
        // window a reviewer is given before readers are protected from it.
        var hideableFrom = flag.CreatedAt + UnreviewedGrace;
        if (now < hideableFrom) return false;

        // The exemption is keyed on when the content became HIDEABLE, not on when the flag
        // was raised. A session that began at T+5h55m is still running at T+6h05m, and that
        // is the reader the rule protects — pulling a section out from under someone
        // mid-read is its own kind of failure, and they have already read it.
        //
        // Keying on flag.CreatedAt instead would make this branch dead code: a session
        // window is half an hour, so no live session can predate a six-hour-old flag.
        if (sessionStartedAt is { } started && started < hideableFrom) return false;

        return true;
    }

    /// <summary>
    /// Whether a reader's last visit is recent enough to be the same session.
    /// Null last-visit (anonymous, or never been here) is always a new session.
    /// </summary>
    public static DateTime? SessionStart(DateTime? lastVisitedAt, DateTime now)
        => lastVisitedAt is { } v && now - v <= SessionWindow ? v : null;

    /// <summary>Past this, the room is marked CorrectionRequired for the admin queue.</summary>
    public static readonly TimeSpan EscalationAfter = TimeSpan.FromHours(24);

    public static bool NeedsEscalation(ReviewFlag flag, DateTime now)
        => flag.ResolvedAt is null && now - flag.CreatedAt >= EscalationAfter;
}
