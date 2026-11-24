// Code signing for EQL Pathfinder's installer and app: Azure Artifact Signing (infra/azure/README.md).
// Everything here can be rebuilt from this file except the identity validation, which is tied to a person and only
// runs in the Azure portal (license photo and selfie).
//
//   az deployment group create -g eqlpathfinder-signing -f infra/azure/signing.bicep -p infra/azure/signing.bicepparam
targetScope = 'resourceGroup'

@description('The person who verifies their identity in the portal (needs Identity Verifier) and may sign by hand.')
param ownerObjectId string

@description('Service principals that sign releases (tools/release.ps1): Certificate Profile Signer only.')
param signerObjectIds array = []

param location string = resourceGroup().location
param accountName string = 'eqlpathfinder'

@description('The completed identity validation ID from the portal. Empty until the ID check is done; then the certificate profile is created.')
param identityValidationId string = ''
param certificateProfileName string = 'eqlpathfinder-public'

// Built-in roles (az role definition list --query "[?contains(roleName, 'Artifact Signing')]").
var identityVerifier = '4339b7cf-9826-4e41-b4ed-c7f4505dac08'
var profileSigner = '2837e146-70d7-4cfd-ad55-7efa6464f958'

// Basic: $9.99 a month for up to 5,000 signatures, billed as a full month.
resource account 'Microsoft.CodeSigning/codeSigningAccounts@2025-10-13' = {
  name: accountName
  location: location
  properties: {
    sku: { name: 'Basic' }
  }
}

resource ownerVerifier 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, ownerObjectId, identityVerifier)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', identityVerifier)
    principalId: ownerObjectId
    principalType: 'User'
  }
}

resource ownerSigner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, ownerObjectId, profileSigner)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', profileSigner)
    principalId: ownerObjectId
    principalType: 'User'
  }
}

resource releaseSigners 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for id in signerObjectIds: {
  name: guid(account.id, id, profileSigner)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', profileSigner)
    principalId: id
    principalType: 'ServicePrincipal'
  }
}]

// Public Trust: the certificate shows the validated legal name, city, state and country; no street address or ZIP.
resource profile 'Microsoft.CodeSigning/codeSigningAccounts/certificateProfiles@2025-10-13' = if (!empty(identityValidationId)) {
  parent: account
  name: certificateProfileName
  properties: {
    profileType: 'PublicTrust'
    identityValidationId: identityValidationId
    includeStreetAddress: false
    includePostalCode: false
  }
}

@description('The signing endpoint for tools/signing.json.')
output endpoint string = account.properties.accountUri
output account string = account.name
output certificateProfile string = empty(identityValidationId) ? '' : certificateProfileName
