param location string
param prefix string
param environmentName string
param tags object
param workspaceCustomerId string
@secure()
param workspaceSharedKey string
param registryLoginServer string
param webIdentityId string
param webIdentityClientId string
param webIdentityName string
param jobsIdentityId string
param jobsIdentityClientId string
param jobsIdentityName string
param postgresHost string
param postgresDatabase string
param storageAccountName string
param storageBlobEndpoint string
param filesContainerName string
param dataProtectionContainerName string
param dataProtectionKeyUri string
param loginCodePepperSecretUri string
param invitationTokenPepperSecretUri string
param applicationInsightsSecretUri string
param smtpCredentialUri string
param publicBaseUrl string = ''
param imprintUrl string = ''
param privacyUrl string = ''
param smtpHost string = ''
param smtpPort int = 587
param smtpUsername string = ''
param bibleApiBaseUrl string = 'https://api.scripture.api.bible'
param customDomainName string = ''
param webImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param migratorImage string = 'mcr.microsoft.com/k8se/quickstart-jobs:latest'
param cleanupImage string = 'mcr.microsoft.com/k8se/quickstart-jobs:latest'
param webMinReplicas int = 0
param webMaxReplicas int = 3
param cleanupSchedule string = '0 3 * * *'

var commonWebEnvironment = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'AZURE_CLIENT_ID'
    value: webIdentityClientId
  }
  {
    name: 'Database__Authentication'
    value: 'ManagedIdentity'
  }
  {
    name: 'ConnectionStrings__freizeit'
    value: 'Host=${postgresHost};Port=5432;Database=${postgresDatabase};Username=${webIdentityName};SSL Mode=Require;Trust Server Certificate=false'
  }
  {
    name: 'Storage__AccountName'
    value: storageAccountName
  }
  {
    name: 'Storage__BlobServiceUri'
    value: storageBlobEndpoint
  }
  {
    name: 'Storage__FilesContainer'
    value: filesContainerName
  }
  {
    name: 'DataProtection__BlobContainer'
    value: dataProtectionContainerName
  }
  {
    name: 'DataProtection__KeyIdentifier'
    value: dataProtectionKeyUri
  }
  {
    name: 'PublicBaseUrl'
    value: publicBaseUrl
  }
  {
    name: 'ImprintUrl'
    value: imprintUrl
  }
  {
    name: 'PrivacyUrl'
    value: privacyUrl
  }
  {
    name: 'Bible__BaseUrl'
    value: bibleApiBaseUrl
  }
  {
    name: 'Smtp__Host'
    value: smtpHost
  }
  {
    name: 'Smtp__Port'
    value: string(smtpPort)
  }
  {
    name: 'Smtp__Username'
    value: smtpUsername
  }
  {
    name: 'Authentication__LoginCodePepper'
    secretRef: 'login-code-pepper'
  }
  {
    name: 'Authentication__InvitationTokenPepper'
    secretRef: 'invitation-token-pepper'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    secretRef: 'application-insights'
  }
]
var webEnvironment = concat(commonWebEnvironment, [
  {
    name: 'Smtp__Password'
    secretRef: 'smtp-password'
  }
])
var commonJobEnvironment = [
  {
    name: 'DOTNET_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'AZURE_CLIENT_ID'
    value: jobsIdentityClientId
  }
  {
    name: 'Database__Authentication'
    value: 'ManagedIdentity'
  }
  {
    name: 'ConnectionStrings__freizeit'
    value: 'Host=${postgresHost};Port=5432;Database=${postgresDatabase};Username=${jobsIdentityName};SSL Mode=Require;Trust Server Certificate=false'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    secretRef: 'application-insights'
  }
]

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-env'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspaceCustomerId
        sharedKey: workspaceSharedKey
      }
    }
    zoneRedundant: false
  }
}

resource managedCertificate 'Microsoft.App/managedEnvironments/managedCertificates@2024-03-01' = if (!empty(customDomainName)) {
  parent: managedEnvironment
  name: 'custom-domain'
  location: location
  properties: {
    domainControlValidation: 'CNAME'
    subjectName: customDomainName
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-web'
  location: location
  tags: union(tags, {
    'azd-env-name': environmentName
    'azd-service-name': 'web'
  })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${webIdentityId}': {}
    }
  }
  properties: {
    environmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        allowInsecure: false
        external: true
        targetPort: 8080
        transport: 'auto'
        customDomains: !empty(customDomainName)
          ? [
              {
                name: customDomainName
                bindingType: 'SniEnabled'
                certificateId: managedCertificate.id
              }
            ]
          : []
      }
      registries: [
        {
          server: registryLoginServer
          identity: webIdentityId
        }
      ]
      secrets: [
        {
          name: 'login-code-pepper'
          keyVaultUrl: loginCodePepperSecretUri
          identity: webIdentityId
        }
        {
          name: 'invitation-token-pepper'
          keyVaultUrl: invitationTokenPepperSecretUri
          identity: webIdentityId
        }
        {
          name: 'application-insights'
          keyVaultUrl: applicationInsightsSecretUri
          identity: webIdentityId
        }
        {
          name: 'smtp-password'
          keyVaultUrl: smtpCredentialUri
          identity: webIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          env: webEnvironment
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: 8080
              }
              periodSeconds: 10
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/ready'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: webMinReplicas
        maxReplicas: webMaxReplicas
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

resource migrator 'Microsoft.App/jobs@2025-02-02-preview' = {
  name: '${prefix}-migrator'
  location: location
  tags: union(tags, {
    'azd-env-name': environmentName
    'azd-service-name': 'migrator'
  })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${jobsIdentityId}': {}
    }
  }
  properties: {
    environmentId: managedEnvironment.id
    configuration: {
      replicaRetryLimit: 1
      replicaTimeout: 900
      triggerType: 'Manual'
      registries: [
        {
          server: registryLoginServer
          identity: jobsIdentityId
        }
      ]
      secrets: [
        {
          name: 'application-insights'
          keyVaultUrl: applicationInsightsSecretUri
          identity: jobsIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'migrator'
          image: migratorImage
          env: commonJobEnvironment
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
    }
  }
}

resource cleanup 'Microsoft.App/jobs@2025-02-02-preview' = {
  name: '${prefix}-cleanup'
  location: location
  tags: union(tags, {
    'azd-env-name': environmentName
    'azd-service-name': 'cleanup'
  })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${jobsIdentityId}': {}
    }
  }
  properties: {
    environmentId: managedEnvironment.id
    configuration: {
      replicaRetryLimit: 2
      replicaTimeout: 1800
      scheduleTriggerConfig: {
        cronExpression: cleanupSchedule
        parallelism: 1
        replicaCompletionCount: 1
      }
      triggerType: 'Schedule'
      registries: [
        {
          server: registryLoginServer
          identity: jobsIdentityId
        }
      ]
      secrets: [
        {
          name: 'application-insights'
          keyVaultUrl: applicationInsightsSecretUri
          identity: jobsIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'cleanup'
          image: cleanupImage
          env: concat(commonJobEnvironment, [
            {
              name: 'Storage__AccountName'
              value: storageAccountName
            }
            {
              name: 'Storage__BlobServiceUri'
              value: storageBlobEndpoint
            }
          ])
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
    }
  }
}

output environmentId string = managedEnvironment.id
output webId string = web.id
output webName string = web.name
output webFqdn string = web.properties.configuration.ingress.fqdn
output migratorId string = migrator.id
output cleanupId string = cleanup.id
