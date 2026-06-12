namespace Arena.API.Models;

public class BudgetFact
{
    public Guid Id { get; set; }
    public DateOnly FactDate { get; set; }
    public string Category { get; set; } = "";
    public string TensionLabel { get; set; } = "";
    public string PerspectiveA { get; set; } = "";
    public string SourceA { get; set; } = "";
    public string SourceUrlA { get; set; } = "";
    public string PerspectiveB { get; set; } = "";
    public string SourceB { get; set; } = "";
    public string SourceUrlB { get; set; } = "";
    public string Explanation { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
