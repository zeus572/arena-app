using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>How precisely we know when something happened.</summary>
public enum DatePrecision
{
    Day,
    Month,
    Year,
}

/// <summary>The marker vocabulary of the design 1h timeline.</summary>
public enum TimelineMarker
{
    /// <summary>Hollow square. Nobody disputes this happened.</summary>
    Agreed,
    /// <summary>Solid federal. A contested decision.</summary>
    Contested,
    /// <summary>State-coloured. The triggering event.</summary>
    Trigger,
    /// <summary>Accent. The "Now" cap on the right-hand end.</summary>
    Now,
}

/// <summary>
/// One turning point on a room's timeline.
///
/// Distinct from <see cref="Development"/>: a timeline event is durable history that
/// explains how we got here, while a development is a dated change to the room's current
/// state. The same real-world occurrence can be both, linked through the graph.
/// </summary>
public class TimelineEvent
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }
    public Room? Room { get; set; }

    public DateOnly OccurredOn { get; set; }

    /// <summary>Historical events are often only known to the month or year; rendering a
    /// false day would be a small lie repeated on every visit.</summary>
    public DatePrecision OccurredPrecision { get; set; } = DatePrecision.Day;

    [Required, MaxLength(200)]
    public string Label { get; set; } = "";

    [MaxLength(1000)]
    public string Description { get; set; } = "";

    public TimelineMarker Marker { get; set; } = TimelineMarker.Agreed;

    /// <summary>
    /// What was KNOWN on this date — not what is known now.
    ///
    /// This is the whole payoff of the Timeline Builder interaction: the second pass
    /// annotates the same events with what was knowable at the time, showing that most
    /// confident takes predate the evidence that contradicted them. An event used in a
    /// Timeline Builder must have this filled, and the publish gate checks it.
    /// </summary>
    [MaxLength(1000)]
    public string? WhatWasKnownThen { get; set; }

    /// <summary>
    /// Required text alternative for the visual timeline. An accessibility publish gate,
    /// not a nice-to-have — a horizontal track of squares is meaningless to a screen reader.
    /// </summary>
    [MaxLength(1000)]
    public string? TextAlternative { get; set; }

    public int Ordinal { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
