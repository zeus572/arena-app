# Bootstrap script for the Civic Arena production deployment.
#
# This is a one-shot provisioner. After it succeeds you have:
#   - civic-api-<suffix>.azurewebsites.net (Linux Web App, MI-auth to PG)
#   - civic-frontend-<suffix>.azurestaticapps.net (Static Web App)
#   - civic database on arena-pgserver
#   - civic Web App MI registered as a PG Entra admin
#   - GitHub secrets/vars printed at the end (paste into repo settings)
#
# Re-running is safe: Bicep is idempotent and the secret-harvesting steps just
# re-read values.
#
# Usage:
#   pwsh -File infra/deploy-civic.ps1 -JwtSecret "<paste>" -AnthropicApiKey "<paste>"

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $JwtSecret,

    [string] $AnthropicApiKey = "",

    [string] $ResourceGroup    = "rg-arena",
    [string] $PgServerName     = "arena-pgserver",
    [string] $TemplateFile     = "infra/civic.bicep",
    [string] $ParametersFile   = "infra/civic.parameters.json",
    [string] $DeploymentName   = "civic-$(Get-Date -Format yyyyMMdd-HHmmss)"
)

$ErrorActionPreference = "Stop"

Write-Host "==> Provisioning civic infra in resource group $ResourceGroup ..."
$result = az deployment group create `
    --resource-group $ResourceGroup `
    --name $DeploymentName `
    --template-file $TemplateFile `
    --parameters $ParametersFile `
    --parameters jwtSecret=$JwtSecret anthropicApiKey=$AnthropicApiKey `
    --query "{appName:properties.outputs.civicAppName.value,host:properties.outputs.civicHostname.value,principal:properties.outputs.civicAppPrincipalId.value,swaName:properties.outputs.civicSwaName.value,swaHost:properties.outputs.civicSwaHostname.value,db:properties.outputs.civicDatabaseName.value}" `
    -o json | ConvertFrom-Json

if (-not $result) { throw "Deployment failed." }

$civicAppName  = $result.appName
$civicHost     = $result.host
$civicPrincipal = $result.principal
$swaName       = $result.swaName
$swaHost       = $result.swaHost
$dbName        = $result.db

Write-Host ""
Write-Host "Provisioned:"
Write-Host "  Web App:        $civicAppName ($civicHost)"
Write-Host "  Static Web App: $swaName ($swaHost)"
Write-Host "  Database:       $dbName"
Write-Host "  MI principalId: $civicPrincipal"
Write-Host ""

Write-Host "==> Registering civic MI as PG Entra admin on $PgServerName ..."
$tenantId = az account show --query "tenantId" -o tsv
$adminUri = "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$ResourceGroup/providers/Microsoft.DBforPostgreSQL/flexibleServers/$PgServerName/administrators/$civicPrincipal" + "?api-version=2023-06-01-preview"
$adminBody = @{
    properties = @{
        principalType = "ServicePrincipal"
        principalName = $civicAppName
        tenantId      = $tenantId
    }
} | ConvertTo-Json -Compress
az rest --method PUT --url $adminUri --body $adminBody --output none

Write-Host "==> Patching civic backend Cors__Origins with the actual SWA hostname ..."
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $civicAppName `
    --settings "Cors__Origins__0=https://$swaHost" "Cors__Origins__1=https://$swaName.azurestaticapps.net" `
    --output none

Write-Host "==> Harvesting publish profile for GitHub Actions ..."
$publishProfile = az webapp deployment list-publishing-profiles `
    --resource-group $ResourceGroup `
    --name $civicAppName `
    --xml

Write-Host "==> Harvesting Static Web App deployment token ..."
$swaToken = az staticwebapp secrets list `
    --name $swaName `
    --resource-group $ResourceGroup `
    --query "properties.apiKey" `
    -o tsv

Write-Host ""
Write-Host "------------------------------------------------------------"
Write-Host "GitHub secrets/vars to set in your repository:"
Write-Host "------------------------------------------------------------"
Write-Host ""
Write-Host "VARIABLE  CIVIC_APP_NAME = $civicAppName"
Write-Host ""
Write-Host "SECRET    AZURE_WEBAPP_PUBLISH_PROFILE_CIVIC ="
Write-Host $publishProfile
Write-Host ""
Write-Host "SECRET    AZURE_STATIC_WEB_APPS_API_TOKEN_CIVIC ="
Write-Host $swaToken
Write-Host ""
Write-Host "SECRET    VITE_CIVIC_API_URL = https://$civicHost/api"
Write-Host "SECRET    VITE_ARENA_API_URL = https://arena-api-2af326.azurewebsites.net/api"
Write-Host ""
Write-Host "------------------------------------------------------------"
Write-Host "Next steps:"
Write-Host "  1. Paste the values above into the repo's Secrets and Variables."
Write-Host "  2. Grant the civic MI permission on the 'civic' DB (one-time SQL: see README)."
Write-Host "  3. Run EF migrations against the new civic DB (see README)."
Write-Host "  4. Add https://$swaHost to debate App Service's Cors__Origins."
Write-Host "  5. Push to release branch or run the deploy-civic workflow."
Write-Host "------------------------------------------------------------"
