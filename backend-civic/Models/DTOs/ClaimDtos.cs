using Civic.API.Models.Rooms;

namespace Civic.API.Models.DTOs;

/// <summary>A cited document as rendered in an evidence trail (design 1n).</summary>
public class SourceRefDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public string? Organization { get; set; }
    /// <summary>What KIND of source this is — never a trust score (PRD 07 §4).</summary>
    public string SourceType { get; set; } = "";
    public bool IsPrimary { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime RetrievedAt { get; set; }
    public string Availability { get; set; } = "";
    public bool HasInterest { get; set; }
    public string? InterestNote { get; set; }
}

/// <summary>One row of "history of this label" in the expanded claim (design 1n).</summary>
public class ClaimStatusHistoryDto
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public string ChangeKind { get; set; } = "";
    public string Rationale { get; set; } = "";
    public DateTime ChangedAt { get; set; }
    /// <summary>When the original source corrected itself — the basis for the published
    /// service-level metric. Null when the change was not correction-driven.</summary>
    public DateTime? SourceCorrectedAt { get; set; }
}

/// <summary>A place this claim appears. Powers the "Appears in" cell of the ledger.</summary>
public class ClaimAppearanceDto
{
    public string ObjectType { get; set; } = "";
    public Guid ObjectId { get; set; }
    public string Slug { get; set; } = "";
    public string Label { get; set; } = "";
    public string Relation { get; set; } = "";
}

/// <summary>Compact claim, for ledger rows and inline evidence marks.</summary>
public class ClaimSummaryDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>One of the eight evidence statuses. The mark renders from this.</summary>
    public string Status { get; set; } = "";
    /// <summary>Fact / Interpretation / Opinion / Prediction.</summary>
    public string Kind { get; set; } = "";
    public string? EvidenceSummary { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public DateTime? StaleAsOf { get; set; }
    public int SupportingCount { get; set; }
    public int ContradictingCount { get; set; }
}

/// <summary>The expanded claim: both sides of the evidence, the history, and where it appears.</summary>
public class ClaimDetailDto : ClaimSummaryDto
{
    /// <summary>Required field — a claim nobody can say how to settle is not a claim.</summary>
    public string WhatWouldSettleIt { get; set; } = "";
    public string? GeographyScope { get; set; }
    public DateTime? TimeScopeStart { get; set; }
    public DateTime? TimeScopeEnd { get; set; }
    public double Confidence { get; set; }
    public DateTime FirstSeenAt { get; set; }

    public List<SourceRefDto> EvidenceFor { get; set; } = new();
    public List<SourceRefDto> EvidenceAgainst { get; set; } = new();
    /// <summary>Actors who assert it. Empty until actors land (R2).</summary>
    public List<ClaimAppearanceDto> AssertedBy { get; set; } = new();
    public List<ClaimAppearanceDto> AppearsIn { get; set; } = new();
    public List<ClaimStatusHistoryDto> StatusHistory { get; set; } = new();
}
