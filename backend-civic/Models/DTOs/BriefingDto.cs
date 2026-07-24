namespace Civic.API.Models.DTOs;

public class BriefingWordDto
{
    public string Term { get; set; } = "";
    public string Definition { get; set; } = "";
}

public class BriefingSummaryDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Institution { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Status { get; set; } = "";
    public string AudienceLevel { get; set; } = "";
    public string KeyConcept { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string Summary30 { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string ThinkDeeperQuestion { get; set; } = "";

    /// <summary>Local-news region (state code) for this briefing, or null for national.</summary>
    public string? Locality { get; set; }

    /// <summary>
    /// Publisher of the upstream article this briefing was synthesized from (e.g. "NPR",
    /// "BBC", "Washington State Standard"), or null for hand-seeded briefings. Lets the
    /// feed show a small per-source moniker.
    /// </summary>
    public string? SourcePublisher { get; set; }
}

/// <summary>
/// A lightweight "is there anything new?" signal for the briefings feed. Returned by
/// GET /api/briefings/latest so the client can poll cheaply (no page payload) to notice
/// freshly published stories. Scoped to the caller's locality, exactly like the list.
/// </summary>
public class BriefingLatestDto
{
    /// <summary>Creation timestamp of the most recent visible briefing, or null if none.</summary>
    public DateTime? LatestCreatedAt { get; set; }

    /// <summary>Total number of briefings visible to the caller.</summary>
    public int Total { get; set; }
}

/// <summary>A single page of briefing summaries plus the total count for paging UI.</summary>
public class BriefingPageDto
{
    public List<BriefingSummaryDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class BriefingDto : BriefingSummaryDto
{
    public string Summary3Min { get; set; } = "";
    public string Summary10Min { get; set; } = "";
    public string WhoActed { get; set; } = "";
    public string WhatChanged { get; set; } = "";
    public string WhyItMatters { get; set; } = "";
    public List<BriefingWordDto> WordsToKnow { get; set; } = new();
    public string Disagreement { get; set; } = "";
    public string StrongestArgumentFor { get; set; } = "";
    public string StrongestArgumentAgainst { get; set; } = "";
    public string[] ValuesInConflict { get; set; } = Array.Empty<string>();
    public string[] RelatedConcepts { get; set; } = Array.Empty<string>();
    public string[] WhereToGoNext { get; set; } = Array.Empty<string>();

    // Original-source attribution, resolved from the NewsItem this briefing was synthesized from.
    // Null for hand-seeded briefings (no upstream article). SourcePublisher lives on the base
    // BriefingSummaryDto so the feed list can show it too.
    public string? SourceUrl { get; set; }
    public DateTime? SourcePublishedAt { get; set; }

    // Coalition that was born from this briefing, if any (the most recent non-dead one).
    // Lets the article surface a call-out linking readers to participate. Null when no
    // provision has been birthed from this briefing yet.
    public Guid? CoalitionProvisionId { get; set; }
    public string? CoalitionProvisionState { get; set; }
}
