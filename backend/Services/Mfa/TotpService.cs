using System.Web;
using OtpNet;

namespace Arena.API.Services.Mfa;

/// <summary>
/// RFC 6238 TOTP helpers (Google Authenticator / Authy / 1Password compatible):
/// generate a secret, build the <c>otpauth://</c> provisioning URI the client turns
/// into a QR code, and verify a submitted 6-digit code with a small clock-skew window.
/// </summary>
public class TotpService
{
    private const int Digits = 6;
    private const int PeriodSeconds = 30;

    private readonly string _issuer;

    public TotpService(IConfiguration config)
    {
        _issuer = config["Mfa:Issuer"] ?? "Political Arena";
    }

    /// <summary>Generate a fresh 160-bit secret, Base32-encoded for authenticator apps.</summary>
    public string GenerateSecretBase32()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    /// <summary>
    /// Build the otpauth provisioning URI. Account label is the user's email; both the
    /// label and issuer are URL-encoded so addresses with reserved characters work.
    /// </summary>
    public string BuildOtpauthUri(string accountEmail, string secretBase32)
    {
        var issuer = HttpUtility.UrlEncode(_issuer);
        var account = HttpUtility.UrlEncode(accountEmail);
        return $"otpauth://totp/{issuer}:{account}" +
               $"?secret={secretBase32}&issuer={issuer}" +
               $"&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";
    }

    /// <summary>
    /// Verify a submitted code against the secret, allowing ±1 time step (~30s either
    /// side) to tolerate clock drift between server and authenticator.
    /// </summary>
    public bool VerifyCode(string secretBase32, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;

        byte[] secret;
        try
        {
            secret = Base32Encoding.ToBytes(secretBase32);
        }
        catch (Exception)
        {
            return false;
        }

        var totp = new Totp(secret, step: PeriodSeconds, totpSize: Digits);
        return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
    }
}
