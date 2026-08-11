param location string
param prefix string
param tags object
param githubRepository string = ''
param githubEnvironment string = 'production'

resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-web-id'
  location: location
  tags: tags
}

resource jobsIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-jobs-id'
  location: location
  tags: tags
}

resource postgresIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-postgres-id'
  location: location
  tags: tags
}

resource deploymentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = if (!empty(githubRepository)) {
  name: '${prefix}-deploy-id'
  location: location
  tags: tags
}

resource githubFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = if (!empty(githubRepository)) {
  parent: deploymentIdentity
  name: 'github-${githubEnvironment}'
  properties: {
    audiences: [
      'api://AzureADTokenExchange'
    ]
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepository}:environment:${githubEnvironment}'
  }
}

output webId string = webIdentity.id
output webName string = webIdentity.name
output webClientId string = webIdentity.properties.clientId
output webPrincipalId string = webIdentity.properties.principalId
output jobsId string = jobsIdentity.id
output jobsName string = jobsIdentity.name
output jobsClientId string = jobsIdentity.properties.clientId
output jobsPrincipalId string = jobsIdentity.properties.principalId
output postgresId string = postgresIdentity.id
output postgresPrincipalId string = postgresIdentity.properties.principalId
output deploymentClientId string = !empty(githubRepository) ? deploymentIdentity!.properties.clientId : ''
output deploymentPrincipalId string = !empty(githubRepository) ? deploymentIdentity!.properties.principalId : ''
