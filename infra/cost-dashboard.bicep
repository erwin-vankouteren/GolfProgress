// The "Azure costs" portal dashboard, deployed by cost.bicep. Each tile is a pinned Cost
// analysis view of the whole subscription; open one to explore it further.

param budgetId string
param location string = resourceGroup().location

var scope = subscription().id

// A Cost analysis view as the portal pins it.
func costView(scope string, title string, dateRange string, granularity string, chart string, accumulated bool, groupBy string, kpis array) object => {
  currency: 'EUR'
  dateRange: dateRange
  query: {
    type: 'ActualCost'
    dataSet: {
      granularity: granularity
      aggregation: { totalCost: { name: 'Cost', function: 'Sum' } }
      grouping: empty(groupBy) ? [] : [{ type: 'Dimension', name: groupBy }]
      sorting: granularity == 'None' ? [{ direction: 'descending', name: 'Cost' }] : [{ direction: 'ascending', name: 'UsageDate' }]
    }
    timeframe: 'None'
  }
  chart: chart
  accumulated: string(accumulated)
  pivots: [
    { type: 'Dimension', name: 'ResourceGroupName' }
    { type: 'Dimension', name: 'ServiceName' }
    { type: 'Dimension', name: 'ResourceId' }
  ]
  scope: substring(scope, 1)
  kpis: kpis
  displayName: title
}

func costTile(scope string, x int, y int, view object) object => {
  position: { x: x, y: y, colSpan: 6, rowSpan: 4 }
  metadata: {
    type: 'Extension/Microsoft_Azure_CostManagement/PartType/CostAnalysisPinPart'
    inputs: [
      { name: 'scope', value: scope }
      { name: 'scopeName', value: subscription().displayName }
      { name: 'view', value: view, isOptional: true }
      { name: 'externalState', isOptional: true }
    ]
    deepLink: '#blade/Microsoft_Azure_CostManagement/Menu/costanalysis'
  }
}

var forecastAndBudget = [
  { type: 'Forecast', enabled: true }
  { type: 'Budget', id: budgetId, enabled: true }
]

resource dashboard 'Microsoft.Portal/dashboards@2020-09-01-preview' = {
  name: 'azure-costs'
  location: location
  tags: { 'hidden-title': 'Azure costs' }
  properties: {
    lenses: [
      {
        order: 0
        parts: [
          // The line to watch: where this month is heading, against the budget.
          costTile(scope, 0, 0, costView(scope, 'This month: cost and forecast', 'ThisMonth', 'Daily', 'Area', true, '', forecastAndBudget))
          costTile(scope, 6, 0, costView(scope, 'This month by resource group', 'ThisMonth', 'Daily', 'StackedColumn', false, 'ResourceGroupName', []))
          costTile(scope, 0, 4, costView(scope, 'This month by service', 'ThisMonth', 'None', 'Table', false, 'ServiceName', []))
          costTile(scope, 6, 4, costView(scope, 'Last month by resource group', 'LastMonth', 'None', 'Table', false, 'ResourceGroupName', []))
        ]
      }
    ]
    metadata: {}
  }
}

output portalUrl string = 'https://portal.azure.com/#@${tenant().tenantId}/dashboard/arm${dashboard.id}'
