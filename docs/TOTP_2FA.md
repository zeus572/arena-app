# TOTP Two-Factor Authentication

The **Arena backend** (`backend/`, `Arena.API`) owns 2FA for **both** the Arena and
Civic frontends, the same way it owns auth and account email. Civic's frontend
authenticates against the Arena backend, so the TOTP implementation is single and
serves both apps. 2FA is **optional opt-in** per user.

## How it works today

- **Standard**: RFC 6238 TOTP (Google Authenticator / Authy / 1Password compatible),
  6 digits, 30s period, ±1 step verification window for clock skew.
- **Library**: `Otp.NET`, wrapped by `TotpService` (`backend/Services/Mfa/`).
- **Secret at rest**: the TOTP secret must be reversible (needed to verify codes), so
  it is **encrypted, not hashed** — AES-256-GCM via `MfaSecretProtector`, with the key
  derived (SHA-256) from `Mfa:EncryptionKey`. Stored on `User.TotpSecretEnc`.
- **Backup codes**: 10 one-time recovery codes generated at enrollment, stored
  SHA-256-hashed (`MfaBackupCode`), accepted in place of a TOTP code.
- **Trusted devices**: a "remember this computer for 90 days" option mints an opaque
  token (hashed in `TrustedDevice`) that lets a 2FA-enabled user skip the second factor
  on that device. Revoked on disable-2FA and on password reset.
- **Two-step login**: when 2FA is on, `POST /auth/login` returns a short-lived
  MFA-pending token (distinct `arena-mfa` audience, **not** accepted as an access
  token); the client completes `POST /auth/mfa/challenge` with a TOTP or backup code.
  Challenge attempts are throttled (5 / 15 min / user).
- **Endpoints**: `GET /auth/mfa/status`, `POST /auth/mfa/{setup,enable,disable,backup-codes,challenge}`.

## Configuration: `Mfa:EncryptionKey`

The value can be **any string** — `MfaSecretProtector` SHA-256-derives the actual
32-byte AES key from it. Use a strong random value.

| Environment | Where it lives | Stability |
| --- | --- | --- |
| Local dev | `dotnet user-secrets` (per machine) | throwaway |
| Prod | App Service setting on `arena-api-2af326` | **permanent** |
| Committed `appsettings.json` | dev-only placeholder fallback | never the real key |

Other `Mfa:*` settings (all have safe defaults, no need to set): `Issuer`
(`"Political Arena"`), `TrustedDeviceDays` (`90`), `PendingTokenMinutes` (`5`).

> **Only `arena-api-2af326` needs the key.** It owns every auth/MFA endpoint. The
> Civic backend (`civic-api-fexzo2`) calls Arena for auth and never runs the TOTP
> code, so it does **not** need this setting.

### Generate a key

From CMD (shells out to PowerShell's crypto RNG for a 32-byte base64 value):

```cmd
powershell -NoProfile -Command "$b=New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)"
```

**Generate a separate key per environment** — dev and prod must not share a value
(a leaked dev key should give nothing against prod; they protect different databases).

### Set it locally (dev)

```cmd
dotnet user-secrets set "Mfa:EncryptionKey" "PASTE_DEV_KEY" --project backend
```

Restart the backend (config is read at startup). User-secrets are machine-local and
load only in Development, so the key never touches committed files and overrides the
`appsettings.json` placeholder.

### Set it in prod

This project stores secrets as App Service settings (no Key Vault — same pattern as
`Jwt:Secret`). Note the **double underscore** mapping `Mfa:EncryptionKey` →
`Mfa__EncryptionKey`:

```cmd
az webapp config appsettings set --resource-group rg-arena --name arena-api-2af326 --settings Mfa__EncryptionKey="PASTE_PROD_KEY"
```

- That command **restarts the app on its own** — do **not** follow it with
  `az webapp restart` (queues a second cold start; see azure deployment gotcha #5).
- Expect a brief cold-start 5xx window (~230s warmup) before `/health` is 200 again.

## ⚠️ Operational caveats

1. **Set the prod key *before* the first deploy that includes the `AddMfa` migration.**
   If a user enrolls before the key exists, their secret can't be encrypted/decrypted.
2. **Never rotate `Mfa:EncryptionKey` casually.** Unlike `Jwt:Secret` (rotating just
   forces re-login), rotating this key makes **every** enrolled user's stored TOTP
   secret undecryptable at once — they'd all be locked out. If you ever truly must
   rotate, you need a re-encryption migration (decrypt-with-old → encrypt-with-new for
   every `User.TotpSecretEnc`) run as part of the cutover.
3. **Lost-device recovery**: a user without their authenticator uses a backup code at
   the challenge. With no backup codes left, recovery is manual — an operator clears
   `MfaEnabled` / `TotpSecretEnc` for that user in the `arena` DB.

## Migration

`AddMfa` adds `User.MfaEnabled/TotpSecretEnc/MfaEnrolledAt` and the `MfaBackupCodes` /
`TrustedDevices` tables. EF migrations apply on startup (`DatabaseInitializerService`),
so a normal deploy that recycles the worker applies it — no manual `database update`
needed in prod.
