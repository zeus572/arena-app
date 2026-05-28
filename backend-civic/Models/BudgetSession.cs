using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public class BudgetSession
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public List<BudgetAllocation> Allocations { get; set; } = new();

    public int TotalPoints => Allocations.Sum(a => a.Points);
}

public class BudgetAllocation
{
    public Guid Id { get; set; }

    public Guid BudgetSessionId { get; set; }
    public BudgetSession? BudgetSession { get; set; }

    [Required, MaxLength(60)]
    public string CategoryKey { get; set; } = "";

    public int Points { get; set; }
}
