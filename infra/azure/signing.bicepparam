using 'signing.bicep'

// Resource group eqlpathfinder-signing, East US. The subscription and both IDs below come from the environment, never
// from this repository (docs/specs/private-settings.md): run tools\private.ps1 first, in the same PowerShell. Bicep
// stops, naming the variable, when one isn't set. IDs only: nothing here is secret.

// The owner's Microsoft Entra object ID in the Default Directory tenant (az ad signed-in-user show --query id).
param ownerObjectId = readEnvironmentVariable('EQLP_AZ_OWNER_OBJECT_ID')

// Release-signing service principals, added once one exists.
param signerObjectIds = []

// Set once the identity validation in the portal shows Completed (Identity validations > the request > Identity validation Id).
// Individual, Public; completed 11-17-2026, expires 11-17-2027.
param identityValidationId = readEnvironmentVariable('EQLP_AZ_IDENTITY_VALIDATION_ID')
