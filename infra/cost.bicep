// The subscription's cost watch: a monthly budget that emails when the forecast goes over
// it, and a portal dashboard with this month's costs and forecast. Covers the whole
// subscription (staging, production and anything else in it). Deploy with
//   az deployment sub create -l westeurope -f infra/cost.bicep
// docs/deployment.md → "Costs".

targetScope = 'subscription'

@description('The monthly budget, in the billing currency (EUR).')
param monthlyBudget int = 50

@description('Who hears about the budget.')
param alertEmail string = 'erwin.vankouteren@outlook.com'

@description('Where the dashboard lives: dashboards belong to a resource group.')
param dashboardResourceGroup string = 'rg-golfprogress-production'

resource budget 'Microsoft.Consumption/budgets@2023-05-01' = {
  name: 'monthly'
  properties: {
    category: 'Cost'
    amount: monthlyBudget
    timeGrain: 'Monthly'
    // Must be the first of a month; budgets can't move their start later on.
    timePeriod: { startDate: '2026-10-01T00:00:00Z' }
    notifications: {
      // The early warning: this month is heading over budget.
      forecastOver: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [alertEmail]
        locale: 'en-us'
      }
      actual80: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [alertEmail]
        locale: 'en-us'
      }
      actualOver: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [alertEmail]
        locale: 'en-us'
      }
    }
  }
}

module dashboard 'cost-dashboard.bicep' = {
  name: 'cost-dashboard'
  scope: resourceGroup(dashboardResourceGroup)
  params: {
    budgetId: budget.id
  }
}
