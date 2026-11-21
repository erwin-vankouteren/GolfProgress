using 'main.bicep'

// docs/deployment.md → "API", step 1.
param environment = 'staging'
param webOrigin = 'https://staging.genics.nl'
param sqlAdminGroupName = 'golfprogress-sql-admins'
param sqlAdminGroupObjectId = 'cb9a578b-1382-4b3a-8cd5-fac0370085e4'
param emailSenderAddress = ''
// Stripe sandbox prices (docs/payments.md → "Setup outside the code"). The secret key and
// webhook secret are in the staging Key Vault as stripe-secret-key and stripe-webhook-secret.
param stripePrices = {
  premiumMonth: 'price_1UeC7n0mE1am7zBubCpzszOY'
  premiumYear: 'price_1UeC8W0mE1am7zBu2GuIdVjI'
  coachingMonth: 'price_1UeC840mE1am7zBupDhDdcV0'
  coachingYear: 'price_1UeC8r0mE1am7zBu3w2H4X3j'
}
