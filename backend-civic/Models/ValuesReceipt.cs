using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public class ValuesReceipt
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int AnswerCountAtTime { get; set; }
    public int ProfileVersionAtTime { get; set; }

    /// <summary>Plain-English summary bullets from rule-based explanation.</summary>
    public List<string> LearnedInsights { get; set; } = new();

    /// <summary>Axis keys where the user's score changed meaningfully since last receipt.</summary>
    public List<string> ChangedAxes { get; set; } = new();

    /// <summary>Axis keys with near-zero scores — the system is unsure here.</summary>
    public List<string> UncertainAreas { get; set; } = new();

    /// <summary>Detected tensions to surface to the user.</summary>
    public List<ReceiptTension> Tensions { get; set; } = new();
}

public class ReceiptTension
{
    [Required, MaxLength(60)]
    public string AxisKey { get; set; } = "";

    [Required, MaxLength(80)]
    public string AxisName { get; set; } = "";

    [Required]
    public string Framing { get; set; } = "";

    public Guid[] AnswerIdsLow { get; set; } = Array.Empty<Guid>();
    public Guid[] AnswerIdsHigh { get; set; } = Array.Empty<Guid>();
}
