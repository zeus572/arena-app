# Civic Arena — Production Infrastructure

This folder holds the Bicep template and bootstrap script for the civic
production deployment. The civic stack piggybacks on the existing arena
resources (`rg-arena` / `plan-arena` / `arena-pgserver`) so net new cost is
roughly zero — one extra Web App on a shared B1 plan, one extra database on
the existing PostgreSQL Flex Server, one Free-tier Static Web App.

## What gets created

| Resource | Name | SKU |
|---|---|---|
| Linux Web App | `civic-api-<6 hex>` | shared `plan-arena` (B1 Linux) |
| Static Web App | `civic-frontend-<6 hex>` | Free |
| PostgreSQL database | `civic` on `arena-pgserver` | (no SKU — db only) |

Both Azure-generated hostnames are used as the public site names — no custom
domain wiring in this iteration.

## Provision

```pwsh
# 1. Make sure you're authenticated and on the right subscription:
az login
az account set --subscription "Visual Studio Enterprise Subscription"

# 2. Provision. The script harvests the publish profile + SWA token at the end
#    and prints the GitHub secret values you need to paste.
pwsh -File infra/deploy-civic.ps1 `
    -JwtSecret      "<same value as debate Jwt:Secret>" `
    -AnthropicApiKey "<your Anthropic key>" `
    -PgAdminPassword "<the arena-pgserver admin password>"
```

The script also patches the civic Web App's `Cors__Origins__*` to the actual
Static Web App hostname (the SWA hostname isn't known until after creation).

## Database migrations

The Bicep template creates the empty `civic` database. EF migrations run from
your local machine against a one-shot client-IP firewall rule:

```pwsh
# Add your IP to the flex server's firewall (replace with your IP).
az postgres flexible-server firewall-rule create `
    --resource-group rg-arena `
    --name arena-pgserver `
    --rule-name dev-$(whoami) `
    --start-ip-address $(curl -s ifconfig.me) `
    --end-ip-address   $(curl -s ifconfig.me)

# Point EF at the prod civic DB just for this command.
$env:ConnectionStrings__DefaultConnection = "Host=arena-pgserver.postgres.database.azure.com;Port=5432;Database=civic;Username=arenaadmin;Password=<password>;SslMode=Require;Trust Server Certificate=true"

dotnet ef database update --project backend-civic

# Cleanup the firewall rule.
az postgres flexible-server firewall-rule delete `
    --resource-group rg-arena `
    --name arena-pgserver `
    --rule-name dev-$(whoami) `
    --yes
```

The above is the **manual / fallback** path (the `firewall-rule` flags shown are
the older-az form; newer az wants `--server-name`/`--name` — see the gotcha in
the CI section). In CI, migrations are pre-applied automatically — see the next
section.

## CI database migrations (both apps) — one-time setup

Both deploy workflows (`deploy.yml`, `deploy-civic.yml`) now have a **`migrate`
job that runs before the backend deploy**: it builds an EF migration bundle
(`dotnet ef migrations bundle`), opens a just-in-time firewall rule for the
runner IP, applies the bundle against the prod DB over a passwordless Entra
token, then removes the firewall rule. The deploy job `needs:` the migrate job,
so a failed migration **blocks the deploy** rather than shipping code against an
un-migrated schema. (The in-app background `MigrateAsync` remains as a safety
net, so it's a fast no-op once CI has applied everything.)

> **Migrations must be expand/contract (backward-compatible).** CI migrates the
> DB moments before the new code loads, so the *old* code briefly runs against
> the *new* schema. Expand (add columns/tables) in one release; contract (drop)
> only in a later release once no running code needs the old shape.

> **Status: provisioned 2026-06-30, validated in prod 2026-07-01.** The grants
> below are done against the existing SP `github-actions-civic-restart`
> (appId `039e8937-55fd-417c-8ba5-54c771351a79`, object id
> `b615e9a1-c05d-4d6d-8629-57692774dd2f`): a least-privilege custom role
> **"PG Firewall Rule Manager (CI)"** is assigned at the `arena-pgserver` scope,
> the SP is a PG Entra admin named **`ci-migrations`**, and the repo secret
> **`CI_PG_AAD_USER=ci-migrations`** is set. A full `release` deploy on
> 2026-07-01 ran both migrate jobs + backend deploys green — the `ci-migrations`
> token auth works. The steps are kept here for reproducibility / disaster
> recovery.
>
> ⚠️ **az-CLI gotchas** (both hit during this work — the migrate job failed twice
> on the second one before it was right):
> - `az role assignment create/list --scope <id>` throws `MissingSubscription`
>   even with the right default subscription. Create the assignment through ARM
>   instead: `az rest --method put --url "https://management.azure.com<scope>/providers/Microsoft.Authorization/roleAssignments/<new-guid>?api-version=2022-04-01" --body '{"properties":{"roleDefinitionId":"<roleDefId>","principalId":"<spObjectId>","principalType":"ServicePrincipal"}}'`.
> - `az postgres flexible-server firewall-rule` **flag names differ by az version**:
>   the GitHub-hosted runner needs `--server-name <server> --name <rule>` (it
>   rejects `--rule-name`), while older/local az uses `--name <server>
>   --rule-name <rule>`. The workflows try the runner form and fall back. Do
>   **not** swap in an ARM `az rest` PUT for firewall rules — that call is async
>   and races the migration apply; the native command waits for the rule to
>   actually become active.

This needs a **one-time** Azure setup before the first `release` deploy that
includes these workflow changes (until it's done, the migrate job — and thus the
deploy — will fail):

1. **Reuse the existing OIDC service principal** (the one civic's
   `restart-backend-civic` job already uses: `AZURE_CLIENT_ID` / `AZURE_TENANT_ID`
   / `AZURE_SUBSCRIPTION_ID`). Its federated credential must cover the `release`
   branch (it already does for civic).

2. **Grant that SP rights to manage firewall rules** on the PG flex server:
   ```pwsh
   $spId = az ad sp show --id $env:AZURE_CLIENT_ID --query id -o tsv
   $pg   = az postgres flexible-server show -g rg-arena -n arena-pgserver --query id -o tsv
   az role assignment create --assignee $spId --role "Contributor" --scope $pg
   ```
   (Or a narrower custom role limited to `.../firewallRules` write/delete.)

3. **Make the SP a Postgres Entra role with DDL** on both databases. Add it as an
   Entra admin (simplest) or create a mapped PG role and grant schema privileges
   on `arena` and `civic`. Do **NOT** reuse a personal admin account (see
   `docs/PENDING_prod_entra_admin_cleanup.md`).
   ```pwsh
   # NOTE: the subcommand is `microsoft-entra-admin` in current az
   # (the older `ad-admin` alias is gone).
   az postgres flexible-server microsoft-entra-admin create `
       -g rg-arena -s arena-pgserver `
       --object-id $spId --display-name "ci-migrations" --type ServicePrincipal
   ```

4. **Add the `CI_PG_AAD_USER` repo secret** = the Postgres role name the SP
   authenticates as (the Entra principal/display name registered above). The
   workflows put this in the connection string's `Username`.

Once these exist, every `release` deploy applies migrations first, then deploys.

## Deploy warmup & cold start

Both backends (Arena `backend/` and Civic `backend-civic/`) run DB migration +
seeding **off the Kestrel critical path** so the container answers its warmup
probe immediately instead of blocking on the slow managed-identity → Postgres
handshake. (Inline-before-`app.Run()` migration historically pushed cold starts
past the platform's ~230s kill threshold and caused flapping deploy outages;
Civic was migrated to this pattern on the `civic-warmup-readiness` branch.)

- **`DatabaseInitializerService`** (background hosted service) runs
  `MigrateAsync` + seeding after Kestrel is listening; 3 attempts, 300s/attempt
  timeout, and it stops the app on hard failure so the platform recycles it.
- **`StartupReadiness`** gate: until init finishes, non-`/health` requests get
  `503 + Retry-After`; `/health` returns `200 {status:"starting"}` **without
  touching the DB** so the platform warmup probe passes fast, then
  `{status:"healthy","ready":true}` once ready. Background workers also park on
  readiness. *Verified in prod 2026-07-01: Civic logs show `Now listening` ~2s
  after init starts — Kestrel is no longer blocked.*
- **ReadyToRun:** both APIs publish `<PublishReadyToRun>` with `-r linux-x64`
  (see the deploy workflows) to pre-JIT and cut first-request warmup.

**B1 ceiling.** The remaining cold-start time is the *container* start itself
(oryx script-gen + base-image CA-cert rehash + dotnet load), ~3 min on the
shared B1 and outside app control. Driving deploy downtime to ~zero would need a
Standard (S1) tier + a staging slot with swap-on-warmup — deliberately out of
scope; we chose to shrink the window, not eliminate it.

**Run-from-package / restart — asymmetric (open item).** Civic runs
`WEBSITE_RUN_FROM_PACKAGE=1` and its workflow has a post-deploy
`restart-backend-civic` (`az webapp restart`) so the new package loads.
**Arena's `deploy.yml` has no restart step.** If Arena is also RFP=1, a *code*
deploy may keep serving the old package until something restarts it. (Arena's
warmup-branch change was build-only — ReadyToRun — so this hasn't bitten yet.)
Verify before Arena ships an actual code change:
```pwsh
az webapp config appsettings list -g rg-arena -n arena-api-2af326 `
    --query "[?name=='WEBSITE_RUN_FROM_PACKAGE']" -o table
```
If it's `1`, add an `az webapp restart` step to `deploy.yml` mirroring Civic's.

## GitHub Actions

The `.github/workflows/deploy-civic.yml` workflow expects these repo settings:

**Variables**
- `CIVIC_APP_NAME` — the auto-generated app name printed by the bootstrap script (e.g. `civic-api-a1b2c3`)

**Secrets**
- `AZURE_WEBAPP_PUBLISH_PROFILE_CIVIC` — XML printed by the bootstrap script
- `AZURE_STATIC_WEB_APPS_API_TOKEN_CIVIC` — token printed by the bootstrap script
- `VITE_CIVIC_API_URL` — `https://<civic-api-host>/api`
- `VITE_ARENA_API_URL` — `https://arena-api-2af326.azurewebsites.net/api`

The workflow runs on pushes to `release` that touch `backend-civic/**`,
`frontend-civic/**`, `shared/**`, or this folder, and on `workflow_dispatch`.

## Cross-app trust checklist

For the merged login experience to work in production:

1. Civic and debate `Jwt:Secret` must be identical (Bicep sets civic; you set
   debate manually).
2. Debate's `Cors__Origins` must include `https://<civic-swa-host>`.
   The bootstrap script reminds you; the actual command:
   ```pwsh
   az webapp config appsettings set `
       --resource-group rg-arena `
       --name arena-api-2af326 `
       --settings "Cors__Origins__3=https://<civic-swa-host>"
   ```

## CORS — one layer only (important)

CORS is owned **entirely** by the ASP.NET `UseCors()` middleware in
`backend-civic/Program.cs`, which reads the `Cors__Origins__*` app settings.

Do **NOT** also configure Azure App Service *platform* CORS (the `cors` block in
`siteConfig`, or `az webapp cors add`). When both layers are active, the platform
intercepts the OPTIONS preflight and returns **400** before the app's policy runs —
the browser then reports a CORS failure. The Bicep template intentionally omits the
platform `cors` block for this reason.

If you ever see 400s on preflight, check for a stray platform CORS object and null it:
```pwsh
$siteId = az webapp show -g rg-arena -n civic-api-fexzo2 --query id -o tsv
az resource update --ids "$siteId/config/web" --set properties.cors=null
az webapp restart -g rg-arena -n civic-api-fexzo2   # required — the platform CORS module is sticky until restart
```

The real SWA origin is the random-word hostname (e.g.
`jolly-pebble-0e9d50810.7.azurestaticapps.net`), NOT the resource-name form. Make
sure that exact origin is in `Cors__Origins__*`.

## Tear down

```pwsh
az webapp delete --resource-group rg-arena --name civic-api-<suffix>
az staticwebapp delete --resource-group rg-arena --name civic-frontend-<suffix> --yes
# Database is dropped manually if you want — leave it for safety.
```

---

# Arena (debate) backend — `arena-api-2af326`

`arena.bicep` back-fills the Arena App Service into source control. The Arena app
was created manually, so unlike civic its platform config (Always On,
`healthCheckPath=/health`, run-from-package) only ever lived in the portal. The
template captures it so the warmup-relevant settings are reproducible.

> ⚠️ **Reconcile before applying.** ARM replaces the *entire* `appSettings`
> collection on deploy — any live setting not listed in `arena.bicep` is **deleted**.
> Arena's live settings were set by hand and are not fully reflected in this repo.
> Dump and reconcile first:
> ```pwsh
> az webapp config appsettings list -g rg-arena -n arena-api-2af326 -o table
> ```
> The `appSettings` block in `arena.bicep` is a starting point, not ground truth —
> secrets are `@secure()` params, and several known settings (`Auth__AdminEmails__*`,
> `Email__*`, `Cors__Origins__*`, `BotHeartbeat__*`, `Ranking__*`, `News__*`,
> `Llm__Provider`, `SocialPublisher__*`) are intentionally omitted rather than guessed.

### Run-from-package + restart (open risk)

Civic runs `WEBSITE_RUN_FROM_PACKAGE=1` and **restarts** after each deploy (the
`restart-backend-civic` job) so the new package is actually loaded. **Arena's
`deploy.yml` has no restart step.** If the live Arena app is also RFP=1, CI zip
deploys may keep serving the *old* package until the worker restarts. Verify the
live value:

```pwsh
az webapp config appsettings list -g rg-arena -n arena-api-2af326 `
    --query "[?name=='WEBSITE_RUN_FROM_PACKAGE']" -o table
```

Then either keep `1` and add an `az webapp restart` step to `deploy.yml` (mirroring
civic), or set `0` if the app is not run-from-package.
