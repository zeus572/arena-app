// ----------------------------------------------------------------------------
// Civic Arena production infrastructure
// ----------------------------------------------------------------------------
// Provisions the civic backend + frontend on top of the existing arena
// resources (rg-arena / plan-arena / arena-pgserver). Net new cost is
// effectively zero: the App Service Plan is shared, the Postgres flex server
// gains one extra database, and the Static Web App is the Free SKU.
//
// Deploy with:
//   az deployment group create \
//     --resource-group rg-arena \
//     --template-file infra/civic.bicep \
//     --parameters infra/civic.parameters.json \
//     --parameters anthropicApiKey=$ANTHROPIC_KEY jwtSecret=$JWT_SECRET
//
// The civic web app deliberately keeps the generic *.azurewebsites.net
// hostname per the spec — no custom domain yet.
// ----------------------------------------------------------------------------

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Suffix appended to the civic App Service name. Six lowercase hex chars by default.')
param nameSuffix string = substring(uniqueString(resourceGroup().id, 'civic'), 0, 6)

@description('Name of the EXISTING App Service Plan to host the civic web app on. Reuses arena plan by default for cost.')
param appServicePlanName string = 'plan-arena'

@description('Name of the EXISTING PostgreSQL Flexible Server to host the civic database on.')
param postgresServerName string = 'arena-pgserver'

@description('Civic database name. Lives alongside the arena DB on the same server.')
param civicDatabaseName string = 'civic'

@description('Shared JWT signing secret. Must match the value used by the debate backend so cross-app tokens validate.')
@secure()
param jwtSecret string

@description('Anthropic API key for the civic content generation hosted services.')
@secure()
param anthropicApiKey string = ''

@description('Debate API base URL (used by civic to proxy premium-initiated debates).')
param debateApiBaseUrl string = 'https://arena-api-2af326.azurewebsites.net'

@description('Debate web app base URL (used to build the share/redirect URL after a civic-initiated debate).')
param debateWebBaseUrl string = 'https://debatearena.fun'

@description('Shared Log Analytics workspace backing Application Insights. Modern (workspace-based) App Insights requires one.')
param logAnalyticsName string = 'log-arena'

@description('Shared Application Insights component (workspace-based). Consumed by BOTH backends and the civic frontend.')
param appInsightsName string = 'appi-arena'

// ---------------------------------------------------------------------------
// Existing resources we hang off
// ---------------------------------------------------------------------------

resource plan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: appServicePlanName
}

resource pgServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' existing = {
  name: postgresServerName
}

// ---------------------------------------------------------------------------
// New: civic database on the existing PG flex server
// ---------------------------------------------------------------------------

resource civicDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: pgServer
  name: civicDatabaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// ---------------------------------------------------------------------------
// New: shared Application Insights (workspace-based) for BOTH backends + the
// civic frontend. This is deliberately NOT a civic-only resource — it lives in
// this template only because civic is the Bicep-native stack, giving the shared
// component one reproducible home. The debate app and the frontend consume the
// SAME component through their own connection-string plumbing set out-of-band:
//   - debate backend (arena-api-2af326): APPLICATIONINSIGHTS_CONNECTION_STRING
//     App Service app setting (arena.bicep references this component; also set
//     live via `az` since arena.bicep is not routinely applied).
//   - civic frontend SWA: VITE_APPINSIGHTS_CONNECTION_STRING GitHub Actions
//     build secret (deploy-civic.yml), baked in at build time.
// One shared component => one Logs/Live-Metrics query across all three surfaces,
// which is the entire reason this exists (a civic-only sink would have again
// forced raw-log spelunking during the 2026-07-15 prod bills investigation).
// cloud_RoleName distinguishes the sources: the backends auto-set it from
// WEBSITE_SITE_NAME; the browser SDK reports its own.
// ---------------------------------------------------------------------------

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    // Workspace-based ingestion (classic AI is retired). Points at the LA workspace above.
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
  }
}

// ---------------------------------------------------------------------------
// New: civic Linux Web App on the existing plan
// ---------------------------------------------------------------------------

var civicAppName = 'civic-api-${nameSuffix}'
var civicSwaName = 'civic-frontend-${nameSuffix}'

// Passwordless Entra-auth connection string. The civic backend already wires
// up an Npgsql token provider in Production (see backend-civic/Program.cs),
// so no password is supplied. Username matches the App Service's MI display
// name, which we register as a PG Entra admin below.
var civicConnString = 'Host=${postgresServerName}.postgres.database.azure.com;Port=5432;Database=${civicDatabaseName};Username=${civicAppName};Ssl Mode=Require'

resource civicWeb 'Microsoft.Web/sites@2023-12-01' = {
  name: civicAppName
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
      healthCheckPath: '/health'
      http20Enabled: true
      // NOTE: deliberately NO platform `cors` block here. CORS is owned entirely
      // by the ASP.NET `UseCors()` middleware (reads Cors:Origins below). Setting
      // App Service platform CORS *and* app CORS makes the platform intercept the
      // OPTIONS preflight and return 400 before the app's policy runs. One layer only.
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        // Application Insights: the code (AddApplicationInsightsTelemetry) no-ops
        // until this is present. Bicep injects the component's connection string
        // directly so no secret is hand-managed here.
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        // KEEP THIS. We tried removing it (PR #6) to make CI deploys auto-restart, but on THIS app
        // removing it makes the worker run from the wwwroot filesystem — which only ever held the
        // first manual deploy. The CI publish-profile zipdeploy does NOT repopulate that filesystem,
        // so every restart serves an OLD build missing the newer controllers (candidates, leagues,
        // cohort). Verified + rolled back 2026-06-10. Trade-off we accept: a backend deploy needs a
        // one-off `az webapp restart -n civic-api-fexzo2 -g rg-arena` to load the new package.
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
        { name: 'ConnectionStrings__DefaultConnection', value: civicConnString }
        { name: 'Jwt__Issuer', value: 'arena-api' }
        { name: 'Jwt__Audience', value: 'arena-app' }
        { name: 'Jwt__Secret', value: jwtSecret }
        { name: 'Anthropic__ApiKey', value: anthropicApiKey }
        { name: 'Anthropic__SonnetModel', value: 'claude-sonnet-4-6' }
        { name: 'Anthropic__HaikuModel', value: 'claude-haiku-4-5-20251001' }
        { name: 'News__IngestIntervalHours', value: '2' }
        { name: 'News__GenerationIntervalMinutes', value: '120' }
        { name: 'News__BatchSize', value: '5' }
        { name: 'News__MaxItemsPerDay', value: '10' }
        // News__Sources__* env vars were removed when sources became typed
        // descriptors (name → { Kind, ... }) — a flat string here would make
        // options binding throw at startup. Source lists live in the committed
        // backend-civic/appsettings.json. If the live app still has the old
        // News__Sources__NPR/BBC settings, DELETE them before deploying code
        // built after this change.
        { name: 'Debate__ApiBaseUrl', value: debateApiBaseUrl }
        { name: 'Debate__WebBaseUrl', value: debateWebBaseUrl }
        // Placeholder origin (the resource-name SWA host). The real SWA hostname
        // is the random-word form (e.g. jolly-pebble-*.azurestaticapps.net) and
        // isn't known until the SWA exists, so deploy-civic.ps1 overwrites
        // Cors__Origins__0/1 with the actual hostname post-provision.
        { name: 'Cors__Origins__0', value: 'https://${civicSwaName}.azurestaticapps.net' }
      ]
    }
  }
}

// Allow Azure-services traffic into the PG flex server. This is the same
// 0.0.0.0/0.0.0.0 rule debate uses — Azure-internal traffic only, not the
// public internet.
resource pgFirewallAllowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = {
  parent: pgServer
  name: 'allow-all-azure-services'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// NOTE: the civic Web App's system-assigned MI is registered as a PG Entra
// admin in a post-provision step (see deploy-civic.ps1). Bicep can't bind the
// admin resource's name to `civicWeb.identity.principalId` because that value
// isn't known at template-start time.

// ---------------------------------------------------------------------------
// New: Static Web App for the civic frontend (Free SKU)
// ---------------------------------------------------------------------------

resource civicSwa 'Microsoft.Web/staticSites@2023-12-01' = {
  name: civicSwaName
  location: 'centralus'
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    buildProperties: {
      appLocation: 'frontend-civic'
      outputLocation: 'dist'
      apiLocation: ''
    }
    repositoryUrl: ''
    branch: ''
  }
}

// ---------------------------------------------------------------------------
// Outputs — consumed by the bootstrap script and the GitHub workflow.
// ---------------------------------------------------------------------------

output civicAppName string = civicWeb.name
output civicHostname string = civicWeb.properties.defaultHostName
output civicAppPrincipalId string = civicWeb.identity.principalId
output civicSwaName string = civicSwa.name
output civicSwaHostname string = civicSwa.properties.defaultHostname
output civicDatabaseName string = civicDb.name
// Name only — the connection string is a secret, so deploy-civic.ps1 harvests it
// via `az monitor app-insights component show` rather than surfacing it in the
// deployment's output history.
output appInsightsName string = appInsights.name
