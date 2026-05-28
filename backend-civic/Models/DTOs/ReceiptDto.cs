namespace Civic.API.Models.DTOs;

public class ReceiptTensionDto
{
    public string AxisKey { get; set; } = "";
    public string AxisName { get; set; } = "";
    public string Framing { get; set; } = "";
}

public class ValuesReceiptDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AnswerCountAtTime { get; set; }
    public int ProfileVersionAtTime { get; set; }
    public List<string> LearnedInsights { get; set; } = new();
    public List<string> ChangedAxes { get; set; } = new();
    public List<string> UncertainAreas { get; set; } = new();
    public List<ReceiptTensionDto> Tensions { get; set; } = new();
}
