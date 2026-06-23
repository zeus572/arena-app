namespace Arena.API.Models.DTOs;

/// <summary>Request a password-reset email. <see cref="App"/> selects which
/// frontend ("arena" | "civic") the reset link points at.</summary>
public record ForgotPasswordRequest(string Email, string? App);

/// <summary>Complete a password reset with the token from the email link.</summary>
public record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>Body for resend-verification, so the link targets the right app.</summary>
public record ResendVerificationRequest(string? App);
