targetScope = 'subscription'

@minLength(1)
@maxLength(32)
param environmentName string
param location string = 'germanywestcentral'
param resourceGroupName string = 'rg-freizeit-${environmentName}'
param postgresAdministratorPrincipalName string
param postgresAdministratorPrincipalId string
@allowed([
  'Group'
  'ServicePrincipal'
  'User'
])
param postgresAdministratorPrincipalType string = 'Group'
@secure()
param loginCodePepper string
@secure()
param invitationTokenPepper string
@secure()
param smtpPassword string
param smtpHost string = ''
param smtpPort int = 587
param smtpUsername string = ''
param publicBaseUrl string = ''
param imprintUrl string = ''
param privacyUrl string = ''
param bibleApiBaseUrl string = 'https://api.scripture.api.bible'
param customDomainName string = ''
param githubRepository string = ''
param githubEnvironment string = 'production'
param alertEmail string = ''
@minValue(30)
@maxValue(730)
param logRetentionInDays int = 30
@minValue(1)
param logDailyQuotaGb int = 1
@minValue(0)
@maxValue(3)
param webMinReplicas int = 0
@minValue(1)
@maxValue(10)
param webMaxReplicas int = 3
param cleanupSchedule string = '0 3 * * *'
param tags object = {}

var normalizedEnvironment = toLower(take(replace(replace(environmentName, '-', ''), '_', ''), 10))
var suffix = take(uniqueString(subscription().id, environmentName), 6)
var prefix = 'fc-${normalizedEnvironment}-${suffix}'
var commonTags = union(tags, {
  application: 'freizeit-cockpit'
  environment: environmentName
  managedBy: 'azd-bicep'
})
var acrName = 'fc${normalizedEnvironment}${suffix}'
var storageName = 'st${normalizedEnvironment}${suffix}'
var keyVaultName = 'kv-${normalizedEnvironment}-${suffix}'
var postgresName = 'pg-${normalizedEnvironment}-${suffix}'
resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: commonTags
}

module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: resourceGroup
  params: {
    location: location
    prefix: prefix
    tags: commonTags
    retentionInDays: logRetentionInDays
    dailyQuotaGb: logDailyQuotaGb
  }
}

module identities './modules/identities.bicep' = {
  name: 'identities'
  scope: resourceGroup
  params: {
    location: location
    prefix: prefix
    tags: commonTags
    githubRepository: githubRepository
    githubEnvironment: githubEnvironment
  }
}

module registry './modules/registry.bicep' = {
  name: 'registry'
  scope: resourceGroup
  params: {
    location: location
    name: acrName
    tags: commonTags
  }
}

module storage './modules/storage.bicep' = {
  name: 'storage'
  scope: resourceGroup
  params: {
    location: location
    name: storageName
    tags: commonTags
    webPrincipalId: identities.outputs.webPrincipalId
    jobsPrincipalId: identities.outputs.jobsPrincipalId
  }
}

module keyVault './modules/key-vault.bicep' = {
  name: 'keyVault'
  scope: resourceGroup
  params: {
    location: location
    name: keyVaultName
    tags: commonTags
    webPrincipalId: identities.outputs.webPrincipalId
    jobsPrincipalId: identities.outputs.jobsPrincipalId
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    loginCodePepper: loginCodePepper
    invitationTokenPepper: invitationTokenPepper
    smtpPassword: smtpPassword
  }
}

module postgresql './modules/postgresql.bicep' = {
  name: 'postgresql'
  scope: resourceGroup
  params: {
    location: location
    name: postgresName
    tags: commonTags
    administratorPrincipalName: postgresAdministratorPrincipalName
    administratorPrincipalId: postgresAdministratorPrincipalId
    administratorPrincipalType: postgresAdministratorPrincipalType
    postgresIdentityId: identities.outputs.postgresId
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
  }
}

module containerApps './modules/container-apps.bicep' = {
  name: 'containerApps'
  scope: resourceGroup
  params: {
    location: location
    prefix: prefix
    environmentName: environmentName
    tags: commonTags
    workspaceCustomerId: monitoring.outputs.workspaceCustomerId
    workspaceSharedKey: monitoring.outputs.workspaceSharedKey
    registryLoginServer: registry.outputs.loginServer
    webIdentityId: identities.outputs.webId
    webIdentityClientId: identities.outputs.webClientId
    webIdentityName: identities.outputs.webName
    jobsIdentityId: identities.outputs.jobsId
    jobsIdentityClientId: identities.outputs.jobsClientId
    jobsIdentityName: identities.outputs.jobsName
    postgresHost: postgresql.outputs.fqdn
    postgresDatabase: postgresql.outputs.databaseName
    storageAccountName: storage.outputs.name
    storageBlobEndpoint: storage.outputs.blobEndpoint
    filesContainerName: storage.outputs.filesContainerName
    dataProtectionContainerName: storage.outputs.dataProtectionContainerName
    dataProtectionKeyUri: keyVault.outputs.dataProtectionKeyUri
    loginCodePepperSecretUri: keyVault.outputs.loginCodePepperSecretUri
    invitationTokenPepperSecretUri: keyVault.outputs.invitationTokenPepperSecretUri
    applicationInsightsSecretUri: keyVault.outputs.applicationInsightsSecretUri
    smtpCredentialUri: keyVault.outputs.smtpCredentialUri
    publicBaseUrl: publicBaseUrl
    imprintUrl: imprintUrl
    privacyUrl: privacyUrl
    smtpHost: smtpHost
    smtpPort: smtpPort
    smtpUsername: smtpUsername
    bibleApiBaseUrl: bibleApiBaseUrl
    customDomainName: customDomainName
    webMinReplicas: webMinReplicas
    webMaxReplicas: webMaxReplicas
    cleanupSchedule: cleanupSchedule
  }
}

module rbac './modules/rbac.bicep' = {
  name: 'rbac'
  scope: resourceGroup
  params: {
    registryName: registry.outputs.name
    webPrincipalId: identities.outputs.webPrincipalId
    jobsPrincipalId: identities.outputs.jobsPrincipalId
    deploymentPrincipalId: identities.outputs.deploymentPrincipalId
    configureDeploymentIdentity: !empty(githubRepository)
  }
}

module alerts './modules/alerts.bicep' = {
  name: 'alerts'
  scope: resourceGroup
  params: {
    location: location
    prefix: prefix
    tags: commonTags
    webResourceId: containerApps.outputs.webId
    webFqdn: containerApps.outputs.webFqdn
    applicationInsightsId: monitoring.outputs.applicationInsightsId
    postgresResourceId: postgresql.outputs.id
    alertEmail: alertEmail
  }
}

output AZURE_RESOURCE_GROUP string = resourceGroup.name
output AZURE_LOCATION string = location
output AZURE_CONTAINER_REGISTRY_NAME string = registry.outputs.name
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = registry.outputs.loginServer
output AZURE_WEB_APP_NAME string = containerApps.outputs.webName
output AZURE_WEB_APP_URI string = 'https://${containerApps.outputs.webFqdn}'
output AZURE_MIGRATOR_JOB_ID string = containerApps.outputs.migratorId
output AZURE_CLEANUP_JOB_ID string = containerApps.outputs.cleanupId
output AZURE_POSTGRES_HOST string = postgresql.outputs.fqdn
output AZURE_STORAGE_ACCOUNT_NAME string = storage.outputs.name
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output AZURE_WEB_IDENTITY_CLIENT_ID string = identities.outputs.webClientId
output AZURE_JOBS_IDENTITY_CLIENT_ID string = identities.outputs.jobsClientId
output AZURE_DEPLOYMENT_IDENTITY_CLIENT_ID string = identities.outputs.deploymentClientId
