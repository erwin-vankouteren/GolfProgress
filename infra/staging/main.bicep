// The staging environment for the API (#115), in one resource group. Deploy with
//   az deployment group create -g <resource-group> -f infra/staging/main.bicep -p infra/staging/main.bicepparam
// Walkthrough, and the steps Bicep can't do: docs/deployment.md → "API (staging)".

targetScope = 'resourceGroup'

@description('Short name used in every resource name.')
param name string = 'golfprogress'

param location string = resourceGroup().location

@description('The staging web app\'s origin, e.g. https://staging.golfprogress.app: allowed by CORS, and where reset links point.')
param webOrigin string

@description('The Microsoft Entra group that administers the SQL server: the people and the deploy identity that run migrations.')
param sqlAdminGroupName string

@description('That group\'s object id.')
param sqlAdminGroupObjectId string

@description('A verified Azure Communication Services sender, e.g. DoNotReply@golfprogress.app. Empty: reset emails are only logged.')
param emailSenderAddress string = ''

var suffix = take(uniqueString(resourceGroup().id), 6)
var apiName = '${name}-api-staging'
var emailConfigured = !empty(emailSenderAddress)

// ---------------------------------------------------------------- monitoring

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${name}-logs-staging'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// API logs and telemetry. IP masking stays on: addresses are not stored.
resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${name}-insights-staging'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
    DisableIpMasking: false
  }
}

// ---------------------------------------------------------------- secrets

// Jwt__Key (and the email connection string) live here, read by the API's identity.
// The secrets themselves are added by hand: see the deployment guide.
// Vault names are at most 24 characters, so no "-staging" here: the resource group says it.
resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${name}-${suffix}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// ---------------------------------------------------------------- database

// Microsoft Entra authentication only (#24): no SQL password exists anywhere.
resource sql 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${name}-sql-staging-${suffix}'
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: sqlAdminGroupName
      sid: sqlAdminGroupObjectId
      tenantId: subscription().tenantId
      principalType: 'Group'
    }
  }
}

// Serverless, pausing after an hour idle: staging is idle most of the time.
resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sql
  name: 'GolfProgressDb'
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
  }
}

// Lets Azure services, including the App Service and GitHub's hosted runners'
// migration step, reach the server; every connection still needs an Entra identity.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sql
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ---------------------------------------------------------------- the API

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${name}-plan-staging'
  location: location
  kind: 'linux'
  sku: { name: 'B1' }
  properties: { reserved: true }
}

var keyVaultRef = 'VaultName=${vault.name}'

var baseSettings = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Staging' }
  // App Service terminates TLS: without this the app sees plain HTTP from the proxy.
  { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
  { name: 'Jwt__Key', value: '@Microsoft.KeyVault(${keyVaultRef};SecretName=jwt-key)' }
  // The API signs in to the database as itself (#24): nothing secret in here.
  {
    name: 'ConnectionStrings__DefaultConnection'
    value: 'Server=tcp:${sql.properties.fullyQualifiedDomainName},1433;Initial Catalog=${database.name};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
  { name: 'Cors__AllowedOrigins__0', value: webOrigin }
  { name: 'PasswordReset__LinkBaseUrl', value: '${webOrigin}/reset-password' }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
]

var emailSettings = emailConfigured
  ? [
      { name: 'Email__ConnectionString', value: '@Microsoft.KeyVault(${keyVaultRef};SecretName=email-connection-string)' }
      { name: 'Email__SenderAddress', value: emailSenderAddress }
    ]
  : []

resource api 'Microsoft.Web/sites@2023-12-01' = {
  name: apiName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      alwaysOn: true
      healthCheckPath: '/client-config'
      appSettings: concat(baseSettings, emailSettings)
    }
  }
}

// The API's identity may read secrets, and nothing else in the vault.
resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, api.id, 'key-vault-secrets-user')
  properties: {
    // Key Vault Secrets User
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output apiName string = api.name
output apiUrl string = 'https://${api.properties.defaultHostName}'
output sqlServer string = sql.properties.fullyQualifiedDomainName
output vaultName string = vault.name
