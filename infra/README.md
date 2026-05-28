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
