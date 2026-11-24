param([string]$Mode, [int]$W, [int]$H, [string]$Tag)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$dir = Join-Path $here "data-$Mode-$Tag"
New-Item -ItemType Directory -Force $dir | Out-Null
$src = Join-Path $env:LOCALAPPDATA "EQL Pathfinder"
foreach ($f in "settings.json", "pathfinder.db", "pathfinder.db-wal", "pathfinder.db-shm") {
  if (Test-Path (Join-Path $src $f)) { Copy-Item (Join-Path $src $f) (Join-Path $dir $f) }
}
$s = Get-Content -Raw -Encoding UTF8 (Join-Path $dir "settings.json") | ConvertFrom-Json
$place = [pscustomobject]@{ Left = 0; Top = 0; Right = $W; Bottom = $H; Maximized = $false; ScreenLeft = $null; ScreenTop = $null; ScreenRight = $null; ScreenBottom = $null }
$s | Add-Member -Force -NotePropertyName MainWindowPlace -NotePropertyValue $place
$s | ConvertTo-Json -Depth 20 | Out-File -Encoding utf8 (Join-Path $dir "settings.json")
$script = "const MODE = `"$Mode`";`n" + (Get-Content -Raw -Encoding UTF8 (Join-Path $here "runs-mock.js"))
$sp = Join-Path $dir "mock.js"
[IO.File]::WriteAllText($sp, $script, [Text.UTF8Encoding]::new($false))
$env:EQLPATHFINDER_DATA = $dir
$env:EQLPATHFINDER_SELFTEST = "mock"
$env:EQLPATHFINDER_MOCK = $sp
$env:EQLPATHFINDER_MOCK_OUT = Join-Path $here "$Mode.png"
$env:EQLPATHFINDER_MOCK_WAIT = "30000"
$p = Start-Process -FilePath (Join-Path $here "..\..\..\src\EqlPathfinder.App\bin\Debug\net10.0-windows\EQLPathfinder.exe") -PassThru
if (-not $p.WaitForExit(240000)) { Stop-Process -Id $p.Id; "timed out" }
"exit $($p.ExitCode)"

