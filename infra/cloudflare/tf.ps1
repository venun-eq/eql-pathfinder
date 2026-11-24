# Runs Terraform for infra/cloudflare as the person signed in to the `cf` CLI (`npx cf auth login` in server/).
# The login's token is refreshed, set for this run only and removed after; it is never printed or stored elsewhere.
# The account's IDs come from the private settings (tools\private.example.env): loaded here, handed to Terraform as
# TF_VAR_ variables for this run only, and removed after like the token.
#   .\infra\cloudflare\tf.ps1 plan
#   .\infra\cloudflare\tf.ps1 apply
param([Parameter(ValueFromRemainingArguments)][string[]]$TfArgs)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$env:PATH = "C:\Program Files\nodejs;$env:PATH"

# This PC's private file fills the environment; a CI service's secret variables are the same names with no file.
# Only Cloudflare's four are needed here, so a job with no Azure or NAS settings can still plan.
. (Join-Path $here "..\..\tools\pipeline-common.ps1")
$null = Import-PrivateSettings
$tfVars = [ordered]@{
    account_id = "EQLP_CF_ACCOUNT_ID"; zone_id = "EQLP_CF_ZONE_ID"
    cache_ruleset_id = "EQLP_CF_CACHE_RULESET_ID"; redirects_ruleset_id = "EQLP_CF_REDIRECTS_RULESET_ID"
}
$unset = @($tfVars.Values | Where-Object { -not "$([Environment]::GetEnvironmentVariable($_))".Trim() })
if ($unset) { throw "Missing private setting(s): $($unset -join ', '). Terraform wasn't started. They go in $(Get-PrivateSettingsPath) (tools\private.example.env has the names), or in the environment." }

# Any cf command refreshes an expired login token.
Push-Location (Join-Path $here "..\..\server")
try {
    npx cf auth whoami *> $null
    if ($LASTEXITCODE) { throw "The cf CLI isn't signed in: run 'npx cf auth login' in server\." }
}
finally { Pop-Location }
$cfg = Get-Content (Join-Path $env:APPDATA "xdg.config\cloudflare\config\default.json") -Raw | ConvertFrom-Json
if ([DateTimeOffset]::Parse($cfg.expiration_time) -lt [DateTimeOffset]::UtcNow.AddMinutes(5)) { throw "The cf login token has expired: run 'npx cf auth login' in server\." }

$tf = (Get-Command terraform -ErrorAction SilentlyContinue).Source
if (-not $tf) { $tf = (Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter terraform.exe | Select-Object -First 1).FullName }
if (-not $tf) { throw "Terraform isn't installed (winget install Hashicorp.Terraform)." }

$env:CLOUDFLARE_API_TOKEN = $cfg.oauth_token
try {
    foreach ($v in $tfVars.Keys) { [Environment]::SetEnvironmentVariable("TF_VAR_$v", "$([Environment]::GetEnvironmentVariable($tfVars[$v]))".Trim()) }
    Push-Location $here
    & $tf @TfArgs
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
    Remove-Item Env:CLOUDFLARE_API_TOKEN -ErrorAction SilentlyContinue
    foreach ($v in $tfVars.Keys) { Remove-Item "Env:TF_VAR_$v" -ErrorAction SilentlyContinue }
}
exit $code
