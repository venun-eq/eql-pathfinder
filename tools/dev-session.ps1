<#
.SYNOPSIS
  Starts Claude (or any command) with dev limits, so every build, test and Edge it starts inherits them and the game
  keeps its cores (docs/developing-while-playing.md). Run it from a terminal in place of `claude`.
.EXAMPLE
  .\tools\dev-session.ps1                  # claude, pinned
  .\tools\dev-session.ps1 -Command dotnet -Arguments "test"
#>
param([string]$Command = "claude", [Parameter(ValueFromRemainingArguments)]$Arguments)
. (Join-Path $PSScriptRoot "dev-limits.ps1")
Set-DevLimits
Write-Host "dev session: $(([uint64][Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity.ToInt64()).ToString('X')) affinity, below normal. Game running: $(Test-GameRunning)"
& $Command @Arguments
exit $LASTEXITCODE
