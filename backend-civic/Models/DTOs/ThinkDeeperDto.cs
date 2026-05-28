namespace Civic.API.Models.DTOs;

public class ThinkDeeperDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Issue { get; set; } = "";
    public string FirstReactionPrompt { get; set; } = "";
    public string[] Values { get; set; } = Array.Empty<string>();
    public string StrongestArgumentA { get; set; } = "";
    public string StrongestArgumentB { get; set; } = "";
    public string WhatSideAMayMiss { get; set; } = "";
    public string WhatSideBMayMiss { get; set; } = "";
    public string[] WhatWouldChangeYourMind { get; set; } = Array.Empty<string>();
    public string CanBothBeTrue { get; set; } = "";
    public string BuildYourViewPrompt { get; set; } = "";
}
