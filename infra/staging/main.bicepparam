using 'main.bicep'

// Fill these in before deploying (docs/deployment.md → "API (staging)", step 1).
param webOrigin = 'https://staging.golfprogress.app'
param sqlAdminGroupName = 'golfprogress-sql-admins'
param sqlAdminGroupObjectId = '00000000-0000-0000-0000-000000000000'
param emailSenderAddress = ''
