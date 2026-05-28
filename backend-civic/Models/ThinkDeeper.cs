using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public class ThinkDeeper
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    [Required, MaxLength(500)]
    public string Issue { get; set; } = "";

    [Required]
    public string FirstReactionPrompt { get; set; } = "";

    public string[] Values { get; set; } = Array.Empty<string>();

    [Required]
    public string StrongestArgumentA { get; set; } = "";

    [Required]
    public string StrongestArgumentB { get; set; } = "";

    [Required]
    public string WhatSideAMayMiss { get; set; } = "";

    [Required]
    public string WhatSideBMayMiss { get; set; } = "";

    public string[] WhatWouldChangeYourMind { get; set; } = Array.Empty<string>();

    [Required]
    public string CanBothBeTrue { get; set; } = "";

    [Required]
    public string BuildYourViewPrompt { get; set; } = "";

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public Guid? SourceNewsItemId { get; set; }
}
