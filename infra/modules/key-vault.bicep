param location string
param name string
param tags object
param webPrincipalId string
param jobsPrincipalId string
@secure()
param applicationInsightsConnectionString string
@secure()
param loginCodePepper string
@secure()
param invitationTokenPepper string
@secure()
param smtpPassword string

var secretsUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6'
)
var cryptoUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '12338af0-0e69-4776-bea7-57ae8d297424'
)

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enablePurgeProtection: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 30
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: vault
  name: 'data-protection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [
      'unwrapKey'
      'wrapKey'
    ]
  }
}

resource loginPepperSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'login-code-pepper'
  properties: {
    value: loginCodePepper
  }
}

resource invitationPepperSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'invitation-token-pepper'
  properties: {
    value: invitationTokenPepper
  }
}

resource applicationInsightsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'application-insights-connection-string'
  properties: {
    value: applicationInsightsConnectionString
  }
}

resource smtpPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'smtp-password'
  properties: {
    value: smtpPassword
  }
}

resource webSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, webPrincipalId, secretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: secretsUserRoleId
    principalId: webPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource webCryptoRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, webPrincipalId, cryptoUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: cryptoUserRoleId
    principalId: webPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource jobsSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, jobsPrincipalId, secretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: secretsUserRoleId
    principalId: jobsPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output id string = vault.id
output name string = vault.name
output uri string = vault.properties.vaultUri
output loginCodePepperSecretUri string = loginPepperSecret.properties.secretUri
output invitationTokenPepperSecretUri string = invitationPepperSecret.properties.secretUri
output applicationInsightsSecretUri string = applicationInsightsSecret.properties.secretUri
output smtpCredentialUri string = smtpPasswordSecret.properties.secretUri
output dataProtectionKeyUri string = dataProtectionKey.properties.keyUriWithVersion
