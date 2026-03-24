namespace Arena.API.Models.DTOs;

public record RegisterRequest(string Email, string Password, string? DisplayName, string? InviteCode);
