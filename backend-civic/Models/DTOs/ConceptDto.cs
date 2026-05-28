namespace Civic.API.Models.DTOs;

public class ConceptDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string PlainDefinition { get; set; } = "";
    public string WhyItMatters { get; set; } = "";
    public string[] WhereYouSeeIt { get; set; } = Array.Empty<string>();
    public string CurrentExample { get; set; } = "";
    public string CommonMisunderstanding { get; set; } = "";
    public string[] RelatedConcepts { get; set; } = Array.Empty<string>();
    public string TryItQuestion { get; set; } = "";
}
