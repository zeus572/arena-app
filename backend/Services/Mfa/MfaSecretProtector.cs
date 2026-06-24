using System.Security.Cryptography;
using System.Text;

namespace Arena.API.Services.Mfa;

/// <summary>
/// Encrypts/decrypts the user's TOTP secret at rest with AES-256-GCM. Unlike the
/// password and opaque-token hashes (which are one-way), the TOTP secret must be
/// recoverable to verify codes, so it is encrypted rather than hashed.
///
/// The 256-bit key is derived (SHA-256) from <c>Mfa:EncryptionKey</c> in config, so
/// the configured value can be any string. This is deterministic across restarts —
/// deliberately avoiding the ASP.NET Data Protection key-ring, which regenerates on
/// Azure App Service's ephemeral filesystem and would make stored secrets unreadable.
///
/// Wire format (Base64): [12-byte nonce][16-byte GCM tag][ciphertext].
/// </summary>
public class MfaSecretProtector
{
    private const int NonceSize = 12; // AES-GCM standard nonce
    private const int TagSize = 16;   // AES-GCM standard tag

    private readonly byte[] _key;

    public MfaSecretProtector(IConfiguration config)
    {
        var configured = config["Mfa:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "Mfa:EncryptionKey is not configured. TOTP secrets cannot be encrypted.");

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
    }

    public string Protect(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string protectedValue)
    {
        var input = Convert.FromBase64String(protectedValue);
        if (input.Length < NonceSize + TagSize)
            throw new CryptographicException("MFA secret ciphertext is malformed.");

        var nonce = input.AsSpan(0, NonceSize);
        var tag = input.AsSpan(NonceSize, TagSize);
        var cipher = input.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
