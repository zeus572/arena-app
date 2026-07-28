# Transactional Email (verification + password reset)

The **Arena backend** (`backend/`, `Arena.API`) owns all account email for **both**
the Arena and Civic frontends. Civic's frontend authenticates against the same
backend, so verification and password-reset email is a single implementation that
serves both apps.

## How it works today

- **Delivery** is behind `IEmailSender` (`backend/Services/Email/`):
  - `AcsEmailSender` — Azure Communication Services (production).
  - `NoOpEmailSender` — logs the link (dev / when `Email:Provider` != `acs`).
- **Tokens** are one-time, SHA-256-hashed, expiring (`AccountToken` + `AccountTokenService`).
- **Safeguards** (`EmailPolicyService`, `EmailDispatchService`): format/normalize,
  disposable-domain blocklist, MX check, suppression list + rate limiting.
- **Bounce/complaint suppression**: ACS → Event Grid → `EmailEventsController`
  (`/api/email/events`).

### Per-app links (done)

Both apps share the backend, so each request carries an `app` field
(`"arena"` | `"civic"`). `EmailDispatchService.BuildLink` builds the verification /
reset link **only** from the allow-listed `Email:AppUrls` map for that app
(`arena` → debatearena.fun, `civic` → civersify.com). Callers that pass `app`:
`register` (both frontends), `resend-verification`, `forgot-password`.

### Provisioned infrastructure (prod)

- Email Communication Service `arena-acs-email`, Communication Service `arena-acs`
  (holds the connection string), all in resource group `rg-arena`.
- **Single sender domain** `notify.civersify.com` (verified: Domain/SPF/DKIM),
  sender `DoNotReply@notify.civersify.com`. Set via `Email:SenderAddress` /
  `Email__SenderAddress` on `arena-api-2af326`.

---

## TODO / Follow-up: per-app **sender** domain

**Problem.** Per-app *links* are routed, but the *sender address* is a single fixed
value (`notify.civersify.com`). So an **Arena** signup gets an email **from
`notify.civersify.com`** with a `debatearena.fun` link — the sender domain doesn't
match the product, which is confusing. (Civic is already consistent: civersify
sender + civersify link.)

**Goal.** Route the sender address by `app` the same way links already are:
- `arena` → `DoNotReply@notify.debatearena.fun`
- `civic` → `DoNotReply@notify.civersify.com`

### Steps

1. **Infra** — add a second ACS custom domain `notify.debatearena.fun` on
   `arena-acs-email`:
   ```bash
   az communication email domain create --domain-name notify.debatearena.fun \
     --email-service-name arena-acs-email --resource-group rg-arena \
     --domain-management CustomerManaged
   az communication email domain sender-username create --sender-username DoNotReply \
     --username DoNotReply --domain-name notify.debatearena.fun \
     --email-service-name arena-acs-email --resource-group rg-arena
   ```
   Then add the returned DNS records at **Namecheap** for `debatearena.fun`
   (host relative to the apex, so DKIM hosts get `.notify`):
   - TXT `notify` `ms-domain-verification=…`
   - TXT `notify` `v=spf1 include:spf.protection.outlook.com -all`
   - CNAME `selector1-azurecomm-prod-net._domainkey.notify` → `…_domainkey.azurecomm.net`
   - CNAME `selector2-…notify` likewise

   After propagation: `az communication email domain initiate-verification` for
   Domain/SPF/DKIM/DKIM2, then link both domains:
   `az communication update -n arena-acs -g rg-arena --linked-domains <civersify id> <debatearena id>`.

2. **Config** — add an app→sender map alongside `Email:AppUrls` in
   `EmailOptions` (`backend/Services/Email/EmailOptions.cs`), e.g.
   `Email:AppSenders` (`arena` / `civic`). Keep `SenderAddress` as the fallback.
   In prod set `Email__AppSenders__arena` / `Email__AppSenders__civic` on
   `arena-api-2af326`.

3. **Code** —
   - `EmailDispatchService.SendAccountEmailAsync`: resolve the sender by `app`
     (mirror `BuildLink`'s allow-list lookup) and pass it through.
   - `IEmailSender.SendAsync` / `AcsEmailSender`: accept a `fromAddress` parameter
     instead of always using `EmailOptions.SenderAddress`. `NoOpEmailSender`
     unaffected.

4. **Verify** — register on Arena → email **from `notify.debatearena.fun`** with a
   debatearena link; register on Civic → from `notify.civersify.com` with a
   civersify link. Confirm SPF/DKIM pass for the new domain (Gmail "Show original").

### Notes
- Reuses the existing `arena-acs` Communication Service and Event Grid suppression
  subscription — only a new *sender domain* + sender routing is added.
- Consider a DMARC record (`_dmarc.notify` TXT, start `p=none`) for each sender
  subdomain once SPF/DKIM verify.

---

## Incident 2026-07-14 → 2026-07-28: total send outage (ACS `senderAddress`)

**Every** verification and password-reset email failed for 13 days. Root cause: PR #72
(`2abadaa`, CAN-SPAM footer) started building the sender as the RFC 5322 display-name
form:

```csharp
$"{_options.SenderName} <{_options.SenderAddress}>"   // WRONG — ACS 400s on this
```

ACS requires `senderAddress` to be the **bare** address and rejected all 12 attempts with:

```
Azure.RequestFailedException: Request body validation error. See property 'senderAddress'
Status: 400 (Bad Request)
```

The friendly From name is **not** a message property — it lives on the sender-username
resource:

```bash
az communication email domain sender-username update \
  --email-service-name arena-acs-email --domain-name notify.civersify.com \
  --sender-username DoNotReply --display-name "Political Arena"
```

### Why it went unnoticed for 13 days

- `POST /auth/resend-verification` returned `200 {"status":"verification email sent"}`
  even when the dispatch returned `Failed`. It now returns **502** (Failed) / **422**
  (Suppressed). `Register` still can't fail signup over email, but the error is logged.
- `ILogger.LogError(ex, ...)` lands in App Insights **`AppExceptions`**, not `AppTraces`,
  so searching traces for the message found nothing. Both apps also only collect
  `SeverityLevel >= 2`, making every `LogInformation` invisible in prod.
- Nothing sent a real email after a deploy — see below.

### Guardrails now in place

1. **Unit:** `AcsEmailSenderTests` pins the bare-address contract via the pure
   `AcsEmailSender.BuildMessage`.
2. **Post-deploy E2E:** the `smoke-email` job in `.github/workflows/deploy.yml` calls
   `POST /api/admin/email-smoke` on the deployed app after every backend deploy and
   fails the run unless ACS accepts the message. It goes through the real
   `IEmailSender` + real prod `EmailOptions` on purpose — a test that built its own ACS
   client would have missed this bug entirely.

### One-time setup required for the smoke job

| Where | Name | Value |
|---|---|---|
| Azure app settings (`arena-api-2af326`) | `Email__SmokeSecret` | a long random string |
| Azure app settings (`arena-api-2af326`) | `Email__SmokeRecipient` | a mailbox you monitor |
| GitHub repo secret | `EMAIL_SMOKE_SECRET` | same value as `Email__SmokeSecret` |

Blank secret or recipient ⇒ the endpoint returns 503 and the CI job fails, so a
half-configured smoke test can't masquerade as a passing one. The recipient is
server-side only; the endpoint never accepts a client-supplied address.
