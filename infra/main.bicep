// One environment of the API (#115, #123), in one resource group: staging or production.
// Deploy with
//   az deployment group create -g <resource-group> -f infra/main.bicep -p infra/<environment>.bicepparam
// Walkthrough, and the steps Bicep can't do: docs/deployment.md → "API".

targetScope = 'resourceGroup'

@description('Which environment this is. Production gets an always-on database, longer backups, purge protection and alerts.')
@allowed(['staging', 'production'])
param environment string

@description('Short name used in every resource name.')
param name string = 'golfprogress'

param location string = resourceGroup().location

@description('The web app\'s origin, e.g. https://app.golfprogress.app: allowed by CORS, and where reset links point.')
param webOrigin string

@description('The Microsoft Entra group that administers the SQL server: the people and the deploy identity that run migrations.')
param sqlAdminGroupName string

@description('That group\'s object id.')
param sqlAdminGroupObjectId string

@description('A verified Azure Communication Services sender, e.g. DoNotReply@golfprogress.app. Empty: reset emails are only logged.')
param emailSenderAddress string = ''

@description('The oldest app versions that may still sign in (GET /client-config); older ones are told to update.')
param minimumVersions object = { ios: '1.0.0', android: '1.0.0', web: '1.0.0' }

@description('The API\'s public address, as the availability test calls it, e.g. https://api.golfprogress.app. Empty: the *.azurewebsites.net address.')
param apiUrl string = ''

@description('Where alerts go. Empty: no alerts (fine for staging, not for production).')
param alertEmail string = ''

var isProduction = environment == 'production'
var suffix = take(uniqueString(resourceGroup().id), 6)
var apiName = '${name}-api-${environment}'
var emailConfigured = !empty(emailSenderAddress)
var alertsConfigured = !empty(alertEmail)

// ---------------------------------------------------------------- monitoring

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${name}-logs-${environment}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: isProduction ? 90 : 30
  }
}

// API logs and telemetry. IP masking stays on: addresses are not stored.
resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${name}-insights-${environment}'
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
// Vault names are at most 24 characters, so no environment here: the resource group says it.
// Soft-delete retention can't change once a vault exists, so staging keeps its 7 days.
resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${name}-${suffix}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: isProduction ? 90 : 7
    // Production's JWT key can't be purged by mistake, even by an owner. (Once on, it stays on.)
    enablePurgeProtection: isProduction ? true : null
  }
}

// ---------------------------------------------------------------- database

// Microsoft Entra authentication only (#24): no SQL password exists anywhere.
resource sql 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${name}-sql-${environment}-${suffix}'
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

// Staging: serverless, pausing after an hour idle, since it's idle most of the time; the
// first request after a quiet spell waits up to a minute for it to resume.
// Production: Standard S0 (10 DTUs), always on, so nobody waits. Scale up when it's busy.
var databaseSku = isProduction
  ? { name: 'S0', tier: 'Standard' }
  : { name: 'GP_S_Gen5', tier: 'GeneralPurpose', family: 'Gen5', capacity: 1 }

var databaseProperties = isProduction
  ? {
      // Backups copied to the paired region, so a regional outage can't lose them.
      requestedBackupStorageRedundancy: 'Geo'
    }
  : {
      autoPauseDelay: 60
      minCapacity: json('0.5')
    }

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sql
  name: 'GolfProgressDb'
  location: location
  sku: databaseSku
  properties: databaseProperties
}

// Point-in-time restore to any moment of the last 35 days (the most Azure allows),
// rather than the default 7.
resource shortTermBackups 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01-preview' = if (isProduction) {
  parent: database
  name: 'default'
  properties: {
    retentionDays: 35
    diffBackupIntervalInHours: 12
  }
}

// Weekly backups kept for 5 weeks, and the first of each month for a year: for the
// mistake nobody notices within 35 days.
resource longTermBackups 'Microsoft.Sql/servers/databases/backupLongTermRetentionPolicies@2023-08-01-preview' = if (isProduction) {
  parent: database
  name: 'default'
  properties: {
    weeklyRetention: 'P5W'
    monthlyRetention: 'P12M'
    weekOfYear: 1
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
  name: '${name}-plan-${environment}'
  location: location
  kind: 'linux'
  sku: { name: 'B1' }
  properties: { reserved: true }
}

var keyVaultRef = 'VaultName=${vault.name}'

var baseSettings = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: isProduction ? 'Production' : 'Staging' }
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
  { name: 'ClientConfig__MinimumVersions__Ios', value: minimumVersions.ios }
  { name: 'ClientConfig__MinimumVersions__Android', value: minimumVersions.android }
  { name: 'ClientConfig__MinimumVersions__Web', value: minimumVersions.web }
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

// ---------------------------------------------------------------- alerts

var publicApiUrl = empty(apiUrl) ? 'https://${api.properties.defaultHostName}' : apiUrl

resource alertGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (alertsConfigured) {
  name: '${name}-alerts-${environment}'
  location: 'global'
  properties: {
    groupShortName: take('gp-${environment}', 12)
    enabled: true
    emailReceivers: [
      { name: 'owner', emailAddress: alertEmail, useCommonAlertSchema: true }
    ]
  }
}

// Calls /client-config every 5 minutes from three places in Europe, and checks the
// certificate has at least a week left.
resource availabilityTest 'Microsoft.Insights/webtests@2022-06-15' = if (alertsConfigured) {
  name: '${name}-api-availability-${environment}'
  location: location
  // Application Insights only lists a test that carries this link to it.
  tags: { 'hidden-link:${insights.id}': 'Resource' }
  kind: 'standard'
  properties: {
    SyntheticMonitorId: '${name}-api-availability-${environment}'
    Name: 'API answers (${environment})'
    Kind: 'standard'
    Enabled: true
    Frequency: 300
    Timeout: 30
    RetryEnabled: true
    Locations: [
      { Id: 'emea-nl-ams-azr' }
      { Id: 'emea-gb-db3-azr' }
      { Id: 'emea-fr-pra-edge' }
    ]
    Request: {
      RequestUrl: '${publicApiUrl}/client-config'
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

// Down: the test failed from at least two of its three locations.
resource availabilityAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsConfigured) {
  name: '${name}-api-down-${environment}'
  location: 'global'
  properties: {
    description: 'The API stopped answering GET /client-config.'
    severity: 1
    enabled: true
    scopes: [availabilityTest.id, insights.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: availabilityTest.id
      componentId: insights.id
      failedLocationCount: 2
    }
    actions: [{ actionGroupId: alertGroup.id }]
  }
}

// Failing: more than 10 server errors in 5 minutes.
resource serverErrorAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsConfigured) {
  name: '${name}-api-errors-${environment}'
  location: 'global'
  properties: {
    description: 'The API answered more than 10 requests with a 5xx in 5 minutes.'
    severity: 2
    enabled: true
    scopes: [api.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'Http5xx'
          metricName: 'Http5xx'
          metricNamespace: 'Microsoft.Web/sites'
          operator: 'GreaterThan'
          threshold: 10
          timeAggregation: 'Total'
        }
      ]
    }
    actions: [{ actionGroupId: alertGroup.id }]
  }
}

output apiName string = api.name
output apiUrl string = 'https://${api.properties.defaultHostName}'
output sqlServer string = sql.properties.fullyQualifiedDomainName
output vaultName string = vault.name
