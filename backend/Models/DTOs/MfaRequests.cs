namespace Arena.API.Models.DTOs;

/// <summary>Confirm enrollment by proving the authenticator is set up correctly.</summary>
public record MfaEnableRequest(string Code);

/// <summary>Disabling 2FA requires re-proving the password (sensitive action).</summary>
public record MfaDisableRequest(string Password);

/// <summary>Regenerating backup codes requires re-proving the password.</summary>
public record MfaBackupCodesRequest(string Password);

/// <summary>
/// Second-factor step of login. <paramref name="MfaToken"/> is the short-lived token
/// returned by <c>/login</c> when 2FA is required; <paramref name="Code"/> is a TOTP or
/// backup code; <paramref name="RememberDevice"/> opts into a 90-day trusted-device bypass.
/// </summary>
public record MfaChallengeRequest(string MfaToken, string Code, bool RememberDevice = false);
