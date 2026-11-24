<#
.SYNOPSIS
  Runs a script with dev limits (tools\dev-limits.ps1): the hourly ship's task starts ship.ps1 through this, so its
  whole process tree stays on the E-cores at below-normal CPU priority.
.EXAMPLE
  powershell -File tools\run-limited.ps1 -Script tools\ship.ps1 -Scheduled
#>
param([Parameter(Mandatory)][string]$Script, [Parameter(ValueFromRemainingArguments)]$Rest)
. (Join-Path $PSScriptRoot "dev-limits.ps1")
Set-DevLimits
# The rest of the line goes to the script as it was typed: "-Name value" and "-Switch" are named, anything else is positional.
$Rest = @($Rest | Where-Object { $null -ne $_ })
$named = @{}; $positional = @()
for ($i = 0; $i -lt @($Rest).Count; $i++) {
    if ($Rest[$i] -match '^-(\w+)$') {
        $n = $Matches[1]
        if ($i + 1 -lt @($Rest).Count -and $Rest[$i + 1] -notmatch '^-\w+$') { $named[$n] = $Rest[++$i] } else { $named[$n] = $true }
    } else { $positional += $Rest[$i] }
}
& $Script @positional @named
exit $LASTEXITCODE
