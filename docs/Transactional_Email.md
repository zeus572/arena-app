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
