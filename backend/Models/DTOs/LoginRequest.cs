namespace Arena.API.Models.DTOs;

/// <summary>
/// Login credentials. <paramref name="TrustedDeviceToken"/> is the opaque token a
/// client stored after choosing "remember this computer" on a prior MFA challenge;
/// when present and valid it lets a 2FA-enabled user skip the second factor.
/// </summary>
public record LoginRequest(string Email, string Password, string? TrustedDeviceToken = null);
