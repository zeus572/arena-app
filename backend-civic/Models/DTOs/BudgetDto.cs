using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.DTOs;

public class BudgetCategoryDto
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Order { get; set; }
}

public class BudgetAllocationDto
{
    [Required, MaxLength(60)]
    public string CategoryKey { get; set; } = "";

    [Range(0, 100)]
    public int Points { get; set; }
}

public class BudgetSessionDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalPoints { get; set; }
    public bool IsComplete { get; set; }
    public List<BudgetAllocationDto> Allocations { get; set; } = new();
}

public class SetAllocationsRequest
{
    public List<BudgetAllocationDto> Allocations { get; set; } = new();
}
