<#
.SYNOPSIS
  Sends the releases already built in releases\ to the R2 bucket friends update from (tools\feed.json).
  Run tools\release.ps1 first, try the build on your own copy, then publish it.

.DESCRIPTION
  The R2 key is asked for once (a Windows sign-in box: user name = Access Key ID, password = Secret Access Key)
  and kept in %APPDATA%\EQLPathfinder-dev\r2.xml, encrypted for your Windows account only. -ResetKey asks again.
#>
param([switch]$ResetKey)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
$cfg = Get-Content (Join-Path $PSScriptRoot "feed.json") -Raw | ConvertFrom-Json
if (-not $cfg.publicUrl) { throw "tools\feed.json has no publicUrl (the update bucket's address, https://updates.eqlpathfinder.com)." }
# The bucket's endpoint is the Cloudflare account's, a private setting read from the environment
# (docs\specs\private-settings.md). Checked before the key is asked for and before anything is uploaded.
$account = "$env:EQLP_CF_ACCOUNT_ID".Trim()
if (-not $account) { throw "EQLP_CF_ACCOUNT_ID isn't set, so there's no R2 endpoint to upload to. Nothing was published. Load this PC's private settings first: .\tools\private.ps1; .\tools\publish.ps1" }
$endpoint = "https://$account.r2.cloudflarestorage.com"

$keyDir = Join-Path $env:APPDATA "EQLPathfinder-dev"
$keyFile = Join-Path $keyDir "r2.xml"
if ($ResetKey -or -not (Test-Path $keyFile)) {
    $cred = Get-Credential -Message "R2 API token for $($cfg.bucket): user name = Access Key ID, password = Secret Access Key"
    if (-not $cred) { throw "No key entered." }
    New-Item -ItemType Directory -Force $keyDir | Out-Null
    $cred | Export-Clixml $keyFile   # DPAPI: only this Windows account on this PC can read it
}
$cred = Import-Clixml $keyFile

# One upload per channel built here: "win" (regular releases) and "beta" once a beta exists.
$channels = Get-ChildItem (Join-Path $root "releases") -Filter "releases.*.json" |
    ForEach-Object { $_.Name -replace '^releases\.(.+)\.json$', '$1' } | Sort-Object -Unique
foreach ($channel in $channels) {
    vpk upload s3 --outputDir (Join-Path $root "releases") --channel $channel --bucket $cfg.bucket --endpoint $endpoint --region auto `
        --keyId $cred.UserName --secret $cred.GetNetworkCredential().Password --keepMaxReleases $cfg.keepReleases
    if ($LASTEXITCODE) { throw "Upload of the $channel channel failed." }
}

Write-Host ""
foreach ($channel in $channels) {
    $feed = Invoke-RestMethod "$($cfg.publicUrl.TrimEnd('/'))/releases.$channel.json"
    $latest = ($feed.Assets | Where-Object Type -eq "Full" | Sort-Object { [version]($_.Version -replace '-.*', '') }, { $_.Version } | Select-Object -Last 1).Version
    $who = if ($channel -eq "win") { "Friends' copies" } else { "Copies in the $channel" }
    Write-Host "Published. $who now see $latest."
}
Write-Host "Installer link for new friends: $($cfg.publicUrl.TrimEnd('/'))/EQLPathfinder-win-Setup.exe"
