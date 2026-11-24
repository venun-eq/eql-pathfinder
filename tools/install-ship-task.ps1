<#
.SYNOPSIS
  Registers (or updates) the Windows scheduled task "EQL Pathfinder ship": tools\ship.ps1 -Scheduled every hour at
  :25, as you, only while you're signed in (it uses your logins and the R2 key encrypted for your account), with no
  window (conhost --headless). Run it once from the main checkout.
#>
param([int]$Minute = 25, [switch]$Remove)
$ErrorActionPreference = "Stop"
$name = "EQL Pathfinder ship"
if ($Remove) { Unregister-ScheduledTask -TaskName $name -Confirm:$false; Write-Host "Removed '$name'."; return }

$ship = Join-Path $PSScriptRoot "ship.ps1"
# Through run-limited.ps1: the whole ship stays on the E-cores at below-normal CPU, so the game keeps the machine.
$runLimited = Join-Path $PSScriptRoot "run-limited.ps1"
$action = New-ScheduledTaskAction -Execute "$env:SystemRoot\System32\conhost.exe" `
    -Argument "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$runLimited`" -Script `"$ship`" -Scheduled" `
    -WorkingDirectory (Split-Path $PSScriptRoot -Parent)
$trigger = New-ScheduledTaskTrigger -Once -At ((Get-Date).Date.AddHours((Get-Date).Hour).AddMinutes($Minute)) `
    -RepetitionInterval (New-TimeSpan -Hours 1)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 1) -Priority 4   # 4 is normal disk and memory priority; 7 hung the page tests
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
    -Description "Releases EQL Pathfinder when finished features wait in docs\releases\next (tools\ship.ps1; CLAUDE.md)." -Force | Out-Null
Write-Host "'$name' runs tools\ship.ps1 every hour at :$('{0:D2}' -f $Minute). Logs: artifacts\ship-logs. Pause: .\tools\ship.ps1 -Pause"
