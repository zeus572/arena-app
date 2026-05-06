namespace Arena.API.Models.DTOs;

public class CreateArenaRequest
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Tone { get; set; } = "serious";
    public string Rules { get; set; } = string.Empty;
    public string DefaultFormat { get; set; } = "standard";
    public string IconEmoji { get; set; } = "🏛️";
    public string AccentColor { get; set; } = "#6366f1";
}
