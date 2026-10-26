using 'main.bicep'

// docs/deployment.md → "API", step 1.
param environment = 'production'
param webOrigin = 'https://golfprogress.app'
param apiUrl = 'https://api.golfprogress.app'
param sqlAdminGroupName = 'golfprogress-sql-admins'
param sqlAdminGroupObjectId = 'cb9a578b-1382-4b3a-8cd5-fac0370085e4'
// Production needs password reset to send its emails (docs/deployment.md → "Email").
param emailDomain = 'golfprogress.app'
// Set once the domain is verified: it switches sending on.
param emailSenderAddress = 'DoNotReply@golfprogress.app'
param alertEmail = 'erwin.vankouteren@outlook.com'
// Raise these to make older apps show "update required".
param minimumVersions = { ios: '1.0.0', android: '1.0.0', web: '1.0.0' }
