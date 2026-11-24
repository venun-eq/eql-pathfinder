<#
.SYNOPSIS
  Loads this PC's private settings (%APPDATA%\EQLPathfinder-dev\private.env: account IDs, the NAS login, the owner's
  names) into the PowerShell that ran it, so a deploy or a plan started next sees them. The hourly ship loads them
  itself; this is for work by hand.

.EXAMPLE
  ..\tools\private.ps1; npm run deploy       # in server\: nothing after the ; runs when a setting is missing

.DESCRIPTION
  Every tool reads its EQLP_* values from the environment and never opens the file (docs\specs\private-settings.md).
  The file wins over a variable already set. Throws, naming them, when a name in tools\private.example.env has no
  value: the names only, never a value.
#>
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "pipeline-common.ps1")
$settings = Import-PrivateSettings
if ($settings.Missing) { throw (Format-PrivateMissing $settings) }
Write-Host "Private settings loaded: $($settings.Names.Count) names from $($settings.Path)."
