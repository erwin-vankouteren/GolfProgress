using 'main.bicep'

// docs/deployment.md → "API", step 1.
param environment = 'staging'
param webOrigin = 'https://staging.genics.nl'
param sqlAdminGroupName = 'golfprogress-sql-admins'
param sqlAdminGroupObjectId = 'cb9a578b-1382-4b3a-8cd5-fac0370085e4'
param emailSenderAddress = ''
