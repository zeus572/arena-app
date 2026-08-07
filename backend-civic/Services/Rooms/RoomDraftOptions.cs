namespace Civic.API.Services.Rooms;

/// <summary>
/// Configuration for the room drafting pipeline (PRD 08 phase R7).
///
/// <see cref="Enabled"/> defaults to <b>false</b>. Drafting spends real money on every tick,
/// and the failure mode is not a crash — it is a quiet retry loop that bills for nothing.
/// That has already happened once on this codebase (the bill-synthesis poison-pill incident),
/// so the pipeline is opt-in per environment rather than on by default.
/// </summary>
public class RoomDraftOptions
{
    public const string SectionName = "RoomDrafting";

    /// <summary>Master switch. Off by default; the candidate pass runs regardless.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The candidate pass is deterministic keyword matching with no LLM call, so it costs
    /// nothing and is safe to leave on. Separate switch so it can be disabled independently.
    /// </summary>
    public bool CandidatesEnabled { get; set; } = true;

    public int CandidateIntervalMinutes { get; set; } = 60;
    public int DraftIntervalMinutes { get; set; } = 15;

    /// <summary>Drafts attempted per tick. Small on purpose — this is the spend dial.</summary>
    public int DraftBatchSize { get; set; } = 3;

    /// <summary>
    /// After this many failed attempts a candidate stops being retried.
    ///
    /// The bill-synthesis incident was an unbounded retry against items that could never
    /// succeed. A ceiling is the thing that turns that from an invoice into a log line.
    /// </summary>
    public int MaxDraftAttempts { get; set; } = 3;

    /// <summary>Candidates created per theme room per pass.</summary>
    public int MaxCandidatesPerRoom { get; set; } = 10;

    /// <summary>
    /// How many match terms a source must hit before it becomes a candidate.
    ///
    /// One is too loose: "spending" alone matches most of the news. Two independent terms is
    /// the cheapest filter that removes the bulk of the false positives without an LLM.
    /// </summary>
    public int MinTermHits { get; set; } = 2;
}
