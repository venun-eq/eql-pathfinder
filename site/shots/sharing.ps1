# Renders the website's sharing shot (img/sharing.png) in the real window, in an isolated copy that never touches the
# player's own settings or database. site/README.md, "Screenshots", says when to run it.
#
#   .\site\shots\sharing.ps1 -Demo <demo game folder> -Out <png> [-Scope patch|all] [-Exe <EQLPathfinder.exe>]
#
# -Demo is the folder with the renamed logs in Logs\ and dbstr_us.txt / spells_us.txt beside it.
param(
    [Parameter(Mandatory)] [string] $Demo,
    [Parameter(Mandatory)] [string] $Out,
    [ValidateSet("patch", "all")] [string] $Scope = "patch",
    [string] $Exe = (Join-Path $PSScriptRoot "..\..\src\EqlPathfinder.App\bin\Debug\net10.0-windows\EQLPathfinder.exe"),
    [string] $Pool = (Join-Path $PSScriptRoot "sample-pool.json"),
    [int] $WaitMs = 150000
)
$ErrorActionPreference = "Stop"

# A new folder each run: the last run's WebView2 profile stays locked for a few seconds after it exits.
$data = Join-Path ([IO.Path]::GetTempPath()) ("eqlp-sharing-" + [DateTime]::Now.ToString("yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Force $data | Out-Null

# Settings for the demo character only: no share keys, no usage counts, sharing off (a visitor reads what players who
# share publish without sharing). A fresh database: the copy reads the demo logs first (about a minute).
@{
    LogsDir = (Join-Path $Demo "Logs"); Character = "Kestrel_oggok"; WelcomeSeen = 99; ShareIntroSeen = $true
    FragmentValue = 2; FragmentValueRecommended = $true; FarmRate = 50; FarmRateMode = "best"
    ShareEnabled = $false; UsageEnabled = $false; Beta = $false; StartWindow = "Open"
} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $data "settings.json")

# The sample pool as the app's saved copy. It is under 25 minutes old, so the app reads it rather than downloading,
# and the download address points nowhere in case it tries anyway.
Copy-Item $Pool (Join-Path $data "community-v1.json")

$script = Join-Path $data "mock.js"
"const SCOPE = `"$Scope`";`n" + (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot "sharing.js")) |
    Set-Content -Encoding utf8 $script

$env:EQLPATHFINDER_DATA = $data
$env:EQLPATHFINDER_LOGS = (Join-Path $Demo "Logs")
$env:EQLPATHFINDER_COMMUNITY = "http://127.0.0.1:9/community/v1.json"
$env:EQLPATHFINDER_SELFTEST = "mock"
$env:EQLPATHFINDER_MOCK = $script
$env:EQLPATHFINDER_MOCK_OUT = $Out
$env:EQLPATHFINDER_MOCK_WAIT = "$WaitMs"
try { Start-Process -FilePath $Exe -Wait }
finally {
    foreach ($v in "DATA", "LOGS", "COMMUNITY", "SELFTEST", "MOCK", "MOCK_OUT", "MOCK_WAIT") { Remove-Item "env:EQLPATHFINDER_$v" -ErrorAction SilentlyContinue }
}
if (Test-Path $Out) { "Saved $Out (isolated copy: $data)" } else { throw "No PNG: see $data\error.log" }

# The window is a little larger than the site's 1892 x 1116 frame: keep the top-left 1892 x 1116, which leaves out
# only the scroll bar and the bottom edge. Nothing is drawn or scaled.
Add-Type -AssemblyName System.Drawing
$full = [Drawing.Image]::FromFile((Resolve-Path $Out))
try {
    if ($full.Width -gt 1892 -or $full.Height -gt 1116) {
        $crop = ([Drawing.Bitmap]$full).Clone([Drawing.Rectangle]::new(0, 0, [Math]::Min(1892, $full.Width), [Math]::Min(1116, $full.Height)), $full.PixelFormat)
        $full.Dispose(); $full = $null
        $crop.Save((Resolve-Path $Out), [Drawing.Imaging.ImageFormat]::Png); $crop.Dispose()
    }
} finally { if ($full) { $full.Dispose() } }
