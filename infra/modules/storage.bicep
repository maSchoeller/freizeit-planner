param location string
param name string
param tags object
param webPrincipalId string
param jobsPrincipalId string

var blobContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
)

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 30
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 30
    }
    isVersioningEnabled: true
  }
}

resource filesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'files'
  properties: {
    publicAccess: 'None'
  }
}

resource dataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'data-protection'
  properties: {
    publicAccess: 'None'
  }
}

resource webBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, webPrincipalId, blobContributorRoleId)
  scope: account
  properties: {
    roleDefinitionId: blobContributorRoleId
    principalId: webPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource jobsBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, jobsPrincipalId, blobContributorRoleId)
  scope: account
  properties: {
    roleDefinitionId: blobContributorRoleId
    principalId: jobsPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output id string = account.id
output name string = account.name
output blobEndpoint string = account.properties.primaryEndpoints.blob
output filesContainerName string = filesContainer.name
output dataProtectionContainerName string = dataProtectionContainer.name
