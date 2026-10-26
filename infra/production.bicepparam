using 'main.bicep'

// docs/deployment.md → "API", step 1.
param environment = 'production'
param webOrigin = 'https://golfprogress.app'
param apiUrl = 'https://api.golfprogress.app'
param sqlAdminGroupName = 'golfprogress-sql-admins'
param sqlAdminGroupObjectId = 'cb9a578b-1382-4b3a-8cd5-fac0370085e4'
// Set once the sender domain is verified (docs/deployment.md → "Email"): production
// needs password reset to send its emails.
param emailSenderAddress = ''
param alertEmail = 'erwin.vankouteren@outlook.com'
// Raise these to make older apps show "update required".
param minimumVersions = { ios: '1.0.0', android: '1.0.0', web: '1.0.0' }
