namespace Civic.API.Models.DTOs;

public class AxisScoreDto
{
    public string AxisKey { get; set; } = "";
    public string AxisName { get; set; } = "";
    public string LowLabel { get; set; } = "";
    public string HighLabel { get; set; } = "";
    public int Order { get; set; }
    public double Score { get; set; }
    public double Confidence { get; set; }
    public double Intensity { get; set; }
    public int SupportingAnswerCount { get; set; }
}

public class ArchetypeBlendItemDto
{
    public string ArchetypeKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double Percent { get; set; }
}

public class ProfileDto
{
    public string UserId { get; set; } = "";
    public int ProfileVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int AnswerCount { get; set; }
    public List<AxisScoreDto> Axes { get; set; } = new();
    public List<ArchetypeBlendItemDto> ArchetypeBlend { get; set; } = new();
}
