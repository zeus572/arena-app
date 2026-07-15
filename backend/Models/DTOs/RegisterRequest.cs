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
    DateOnly? DateOfBirth,
    // The Terms of Service version the user explicitly agreed to at signup
    // (clickwrap). Required and must match the server's current version, so we
    // have a durable record of which Terms each account accepted — and can tell
    // who predates a later revision.
    string? AcceptedTermsVersion = null);
