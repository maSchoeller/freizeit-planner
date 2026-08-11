param registryName string
param webPrincipalId string
param jobsPrincipalId string
param deploymentPrincipalId string = ''
param configureDeploymentIdentity bool = false

var acrPullRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)
var acrPushRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '8311e382-0749-4cb8-b61a-304f252e45ec'
)
var contributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'b24988ac-6180-42a0-ab88-20f7382dd24c'
)
var rbacAdministratorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'f58310d9-a9f6-439a-9e8d-f62e7b41a168'
)

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}

resource webAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, webPrincipalId, acrPullRoleId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: webPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource jobsAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, jobsPrincipalId, acrPullRoleId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: jobsPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource deploymentContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (configureDeploymentIdentity) {
  name: guid(resourceGroup().id, deploymentPrincipalId, contributorRoleId)
  properties: {
    roleDefinitionId: contributorRoleId
    principalId: deploymentPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource deploymentRbacAdministrator 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (configureDeploymentIdentity) {
  name: guid(resourceGroup().id, deploymentPrincipalId, rbacAdministratorRoleId)
  properties: {
    roleDefinitionId: rbacAdministratorRoleId
    principalId: deploymentPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource deploymentAcrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (configureDeploymentIdentity) {
  name: guid(registry.id, deploymentPrincipalId, acrPushRoleId)
  scope: registry
  properties: {
    roleDefinitionId: acrPushRoleId
    principalId: deploymentPrincipalId
    principalType: 'ServicePrincipal'
  }
}
