param location string
param prefix string
param tags object
param webResourceId string
param webFqdn string
param applicationInsightsId string
param postgresResourceId string
param alertEmail string = ''
param latencyThresholdMs int = 2000

var actionGroupId = resourceId('Microsoft.Insights/actionGroups', '${prefix}-alerts')
var action = [
  {
    actionGroupId: actionGroupId
  }
]

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${prefix}-alerts'
  location: 'global'
  tags: tags
  properties: {
    enabled: true
    groupShortName: take(replace(prefix, '-', ''), 12)
    emailReceivers: !empty(alertEmail)
      ? [
          {
            name: 'operations'
            emailAddress: alertEmail
            useCommonAlertSchema: true
          }
        ]
      : []
  }
}

resource healthWebTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: '${prefix}-health'
  location: location
  tags: union(tags, {
    'hidden-link:${applicationInsightsId}': 'Resource'
  })
  kind: 'ping'
  properties: {
    Name: '${prefix}-health'
    Description: 'Freizeit-Cockpit health endpoint'
    Enabled: true
    Frequency: 300
    Kind: 'ping'
    Locations: [
      {
        Id: 'emea-nl-ams-azr'
      }
      {
        Id: 'emea-gb-db3-azr'
      }
    ]
    RetryEnabled: true
    SyntheticMonitorId: '${prefix}-health'
    Timeout: 30
    Request: {
      RequestUrl: 'https://${webFqdn}/health'
      FollowRedirects: true
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      IgnoreHttpStatusCode: false
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 14
    }
  }
}

resource healthAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-health-failed'
  location: 'global'
  tags: tags
  properties: {
    description: 'Health endpoint failed from both test locations.'
    severity: 1
    enabled: true
    scopes: [
      healthWebTest.id
      applicationInsightsId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      componentId: applicationInsightsId
      failedLocationCount: 2
      webTestId: healthWebTest.id
    }
    actions: action
  }
}

resource serverErrorAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-web-5xx'
  location: 'global'
  tags: tags
  properties: {
    description: 'The web app returned elevated HTTP 5xx responses.'
    severity: 1
    enabled: true
    scopes: [
      webResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Http5xx'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'Requests'
          metricNamespace: 'Microsoft.App/containerApps'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
          dimensions: [
            {
              name: 'StatusCodeCategory'
              operator: 'Include'
              values: [
                '5xx'
              ]
            }
          ]
        }
      ]
    }
    actions: action
  }
}

resource latencyAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-web-latency'
  location: 'global'
  tags: tags
  properties: {
    description: 'The average web response time is elevated.'
    severity: 2
    enabled: true
    scopes: [
      webResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ResponseTime'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'ResponseTime'
          metricNamespace: 'Microsoft.App/containerApps'
          operator: 'GreaterThan'
          threshold: latencyThresholdMs
          timeAggregation: 'Average'
          dimensions: []
        }
      ]
    }
    actions: action
  }
}

resource databaseAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-postgres-failures'
  location: 'global'
  tags: tags
  properties: {
    description: 'PostgreSQL is rejecting connections.'
    severity: 1
    enabled: true
    scopes: [
      postgresResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'FailedConnections'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'connections_failed'
          metricNamespace: 'Microsoft.DBforPostgreSQL/flexibleServers'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
          dimensions: []
        }
      ]
    }
    actions: action
  }
}

output actionGroupId string = actionGroup.id
