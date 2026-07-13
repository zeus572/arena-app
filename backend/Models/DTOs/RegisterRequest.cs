namespace Arena.API.Models.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName,
    string? InviteCode,
    string? App,
    // Full date of birth, collected at signup for the COPPA under-13 gate (and
    // future age-based features). Required — the server rejects signups without a
    // plausible DOB. Serialized as "yyyy-MM-dd".
    DateOnly? DateOfBirth);
