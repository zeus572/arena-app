using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public class Petition
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [MaxLength(4000)]
    public string Description { get; set; } = "";

    [MaxLength(64)]
    public string CreatedBy { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public int SignatureCount { get; set; }
}
