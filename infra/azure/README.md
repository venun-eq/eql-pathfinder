# Azure: code signing

Signs the installer, the app and its updater with Azure Artifact Signing, so Windows shows a verified publisher instead
of "Windows protected your PC". `signing.bicep` is the whole setup except the identity validation, which only the
person can do, in the Azure portal.

| What | Where |
|---|---|
| Subscription | Azure subscription 1 (its ID is the private setting `EQLP_AZ_SUBSCRIPTION_ID`), pay-as-you-go (trials can't use Artifact Signing) |
| Resource group | `eqlpathfinder-signing`, East US |
| Signing account | `eqlpathfinder`, Basic ($9.99 a month, 5,000 signatures), endpoint `https://eus.codesigning.azure.net/` |
| Roles | Owner: Identity Verifier and Certificate Profile Signer, on the account. Release signers: Certificate Profile Signer |
| Certificate profile | `eqlpathfinder-public`, Public Trust, created once `identityValidationId` is set |

Changing it: edit the Bicep or the parameters, preview, then deploy (Azure CLI, signed in with `az login`; PowerShell, from
the repository's folder):

```powershell
.\tools\private.ps1; az deployment group what-if -g eqlpathfinder-signing -f infra\azure\signing.bicep -p infra\azure\signing.bicepparam --subscription $env:EQLP_AZ_SUBSCRIPTION_ID
.\tools\private.ps1; az deployment group create  -g eqlpathfinder-signing -f infra\azure\signing.bicep -p infra\azure\signing.bicepparam --subscription $env:EQLP_AZ_SUBSCRIPTION_ID
```

The subscription, the owner's object ID and the identity validation's ID aren't in this repository: they're private
settings (`tools\private.example.env` has the names; docs/pipeline.md, "Private settings"). `tools\private.ps1` loads
them into the PowerShell that ran it, and nothing after the `;` runs when one is missing. `signing.bicepparam` reads its
two from the environment, and Bicep stops, naming the variable, when one isn't set. `--subscription` makes a deploy land
where the settings say, whatever the login's default subscription is.

## The parts that aren't in code

- **Identity validation (individual, Public).** Azure portal → the `eqlpathfinder` signing account → Identity
  validations → Individual → New identity → Public. It takes the name and address from the billing account's "Sold to",
  which must match the government ID exactly; the certificate shows the name, city, state and country. Then the AU10TIX
  check on a phone and a Verified ID in Microsoft Authenticator. It has to be renewed before it expires (reminders start
  60 days ahead).
- **Billing "Sold to"** (Cost Management + Billing → the billing account → Properties): name and address as on the ID.

When the validation shows Completed, put its ID in the private settings (`EQLP_AZ_IDENTITY_VALIDATION_ID`) and
deploy: that creates the certificate profile.

## Signing releases

`tools/release.ps1` signs whenever `tools/signing.json` exists: Velopack 1.2's own signtool and Azure plug-in sign
Setup.exe, the app, the updater and Pathfinder's DLLs (Microsoft's .NET files are already signed and skipped), each
timestamped, then the script checks Setup.exe's signature before anything is published. About 25 seconds and 11
signatures a release.

It signs as whoever is signed in to the Azure CLI (`az login`, the owner; a personal Microsoft account works through the
CLI). If the login has expired the script stops before building and says so. `-NoSign` makes an unsigned local build.
No service principal or stored secret is needed while releases are made from this PC.
