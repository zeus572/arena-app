using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public class Briefing
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    [Required, MaxLength(300)]
    public string Headline { get; set; } = "";

    [Required, MaxLength(64)]
    public string Institution { get; set; } = "";

    [Required, MaxLength(32)]
    public string Branch { get; set; } = "";

    [Required, MaxLength(64)]
    public string Status { get; set; } = "";

    [Required, MaxLength(32)]
    public string AudienceLevel { get; set; } = "";

    [Required, MaxLength(160)]
    public string KeyConcept { get; set; } = "";

    public string[] Tags { get; set; } = Array.Empty<string>();

    [Required]
    public string Summary30 { get; set; } = "";

    [Required]
    public string Summary3Min { get; set; } = "";

    [Required]
    public string Summary10Min { get; set; } = "";

    [Required]
    public string WhoActed { get; set; } = "";

    [Required]
    public string WhatChanged { get; set; } = "";

    [Required]
    public string WhyItMatters { get; set; } = "";

    public List<BriefingWord> WordsToKnow { get; set; } = new();

    [Required]
    public string Disagreement { get; set; } = "";

    [Required]
    public string StrongestArgumentFor { get; set; } = "";

    [Required]
    public string StrongestArgumentAgainst { get; set; } = "";

    public string[] ValuesInConflict { get; set; } = Array.Empty<string>();

    [Required]
    public string ThinkDeeperQuestion { get; set; } = "";

    public string[] RelatedConcepts { get; set; } = Array.Empty<string>();

    public string[] WhereToGoNext { get; set; } = Array.Empty<string>();

    public int IssueOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Provenance: which pipeline produced this row. "seed" for hand-seeded
    // catalog rows, "news" when generated from a NewsItem, "manual" for
    // future admin-created rows.
    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public Guid? SourceNewsItemId { get; set; }
}

public class BriefingWord
{
    [Required, MaxLength(120)]
    public string Term { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Definition { get; set; } = "";
}
