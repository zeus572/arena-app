using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.DTOs;

public class CreatePetitionRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [MaxLength(4000)]
    public string Description { get; set; } = "";
}
