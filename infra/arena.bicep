// ----------------------------------------------------------------------------
// Arena (debate) backend infrastructure  —  arena-api-2af326
// ----------------------------------------------------------------------------
// The Arena App Service was originally created manually (unlike civic, which has
// always been Bicep). This template back-fills that gap so the platform config
// that governs cold-start/warmup — alwaysOn, healthCheckPath, run-from-package —
// lives in source control instead of only in the portal.
//
// ⚠️  RECONCILE BEFORE YOU APPLY. ARM replaces the FULL appSettings collection on
//     deploy: any setting present on the live app but MISSING here will be DELETED.
//     The live app's settings were set by hand and are not fully reflected in this
//     repo. Before the first `az deployment group create`, dump the live settings
//     and reconcile this list against them:
//
//       az webapp config appsettings list -g rg-arena -n arena-api-2af326 -o table
//
//     Treat the appSettings below as a STARTING POINT, not ground truth.
//
// Deploy (only after reconciling):
//   az deployment group create \
//     --resource-group rg-arena \
//     --template-file infra/arena.bicep \
//     --parameters jwtSecret=$JWT_SECRET anthropicApiKey=$ANTHROPIC_KEY \
//                  mfaEncryptionKey=$MFA_KEY
// ----------------------------------------------------------------------------

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('The EXISTING Arena App Service name. This template updates it in place.')
param arenaAppName string = 'arena-api-2af326'

@description('Name of the EXISTING shared App Service Plan (B1 Linux). NOT modified here — SKU stays as-is.')
param appServicePlanName string = 'plan-arena'

@description('Name of the EXISTING PostgreSQL Flexible Server hosting the arena database.')
param postgresServerName string = 'arena-pgserver'

@description('Arena database name.')
param arenaDatabaseName string = 'arena'

@description('''Whether the app runs from the deployed zip package (WEBSITE_RUN_FROM_PACKAGE=1).
Civic uses 1 and pairs it with a post-deploy `az webapp restart` to load the new package.
Arena''s deploy.yml currently has NO restart step — so if this is 1, CI zip deploys may keep
serving the OLD package until something restarts the worker. Verify the live value and EITHER
(a) keep 1 and add a restart step to deploy.yml, OR (b) set 0 if the live app is not RFP.''')
param runFromPackage string = '1'

@description('Shared JWT signing secret. Must match the civic backend so cross-app tokens validate.')
@secure()
param jwtSecret string

@description('Anthropic API key for debate generation + topic moderation.')
@secure()
param anthropicApiKey string = ''

@description('MFA secret-protector key. NEVER rotate once set — rotating locks every 2FA user out.')
@secure()
param mfaEncryptionKey string = ''

// ---------------------------------------------------------------------------
// Existing resources we hang off
// ---------------------------------------------------------------------------

resource plan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: appServicePlanName
}

resource pgServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' existing = {
  name: postgresServerName
}

// Passwordless Entra-auth connection string (matches civic). The Arena backend
// wires an Npgsql token provider in Production (see backend/Program.cs), so no
// password is supplied. Username matches the App Service MI display name, which
// is registered as a PG Entra admin out-of-band.
var arenaConnString = 'Host=${postgresServerName}.postgres.database.azure.com;Port=5432;Database=${arenaDatabaseName};Username=${arenaAppName};Ssl Mode=Require'

// ---------------------------------------------------------------------------
// The Arena Linux Web App (updated in place — existing resource, fixed name)
// ---------------------------------------------------------------------------

resource arenaWeb 'Microsoft.Web/sites@2023-12-01' = {
  name: arenaAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      // Platform health probe → the init-aware /health endpoint. While migrations
      // run the app returns 200 "starting" (no DB), so warmup passes immediately;
      // it flips to "healthy" once StartupReadiness is Ready.
      healthCheckPath: '/health'
      http20Enabled: true
      // CORS owned entirely by ASP.NET UseCors() — NO platform `cors` block (see civic.bicep note).
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        // See the runFromPackage param doc above + the restart-step caveat.
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: runFromPackage }
        { name: 'ConnectionStrings__DefaultConnection', value: arenaConnString }
        { name: 'Jwt__Issuer', value: 'arena-api' }
        { name: 'Jwt__Audience', value: 'arena-app' }
        { name: 'Jwt__Secret', value: jwtSecret }
        { name: 'Anthropic__ApiKey', value: anthropicApiKey }
        { name: 'Mfa__EncryptionKey', value: mfaEncryptionKey }
        // NOTE: additional live settings (Auth__AdminEmails__*, Email__*/ACS, Cors__Origins__*,
        // BotHeartbeat__*, Ranking__*, News__*, Llm__Provider, SocialPublisher__*/Bluesky__*)
        // are set on the live app and MUST be reconciled into this list before applying — see
        // the header warning. They are intentionally omitted here rather than guessed.
      ]
    }
  }
}

output arenaAppName string = arenaWeb.name
output arenaHostname string = arenaWeb.properties.defaultHostName
output arenaAppPrincipalId string = arenaWeb.identity.principalId
