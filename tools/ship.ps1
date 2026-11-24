<#
.SYNOPSIS
  Releases main when finished features are waiting (docs\releases\next\*.md): version, notes, build, test, sign,
  publish, website, NAS job, checks, a notification. No Claude session needed: the scheduled task
  "EQL Pathfinder ship" runs it every hour, and you can run it yourself.

.EXAMPLE
  .\tools\ship.ps1             # release now if anything is waiting
  .\tools\ship.ps1 -DryRun     # show the version and notes it would release; change nothing
  .\tools\ship.ps1 -Pause      # stop the hourly releases (until -Resume)

.DESCRIPTION
  Runs only in the main checkout, on main, with nothing uncommitted (sessions work in worktrees: CLAUDE.md).
  0. This PC's private settings (tools\private.example.env has the names): with one missing it alerts, releases
     nothing and ends with an error.
  1. Logins: Azure (code signing), Cloudflare (website), the R2 key, ssh to the NAS. Each hourly run refreshes them;
     one that has lapsed sends an alert (at most once a day) and nothing is released until it's fixed.
  2. Version: patch + 1, or minor + 1 when a note says bump: minor.
  3. Notes: the waiting notes in order; with several, their titles make the headline; a last line says whether the
     update re-reads players' logs.
  4. tools\release.ps1 builds, tests, signs and publishes; the notes go to docs\releases\<version>.md and the waiting
     ones are removed, in one commit "v<version>".
  5. The website (What's new), and the NAS community job when a note says job: true.
  6. Checks the live feed, the installer's signature and What's new, then ntfy homelab-updates (or homelab-alerts on
     a failure, with the log). Logs: artifacts\ship-logs (runs that did nothing leave none).
#>
param([switch]$Scheduled, [switch]$DryRun, [switch]$Pause, [switch]$Resume)
$ErrorActionPreference = "Stop"
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Git\cmd;C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin;$env:USERPROFILE\.dotnet\tools;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
. (Join-Path $PSScriptRoot "pipeline-common.ps1")
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$common = Get-CommonDir
$logDir = Join-Path $root "artifacts\ship-logs"

# The scheduled task runs this without a console: run again with everything (native tools too) going to a log file,
# keep the log only when something happened, and drop logs older than 30 days.
if ($Scheduled -and -not $env:EQLP_SHIP_CHILD) {
    New-Item -ItemType Directory -Force $logDir | Out-Null
    $log = Join-Path $logDir "ship-$(Get-Date -Format yyyyMMdd-HHmmss).log"
    $env:EQLP_SHIP_CHILD = "1"
    cmd.exe /c "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Scheduled > `"$log`" 2>&1"
    $code = $LASTEXITCODE
    if ((Get-Content $log -Raw -ErrorAction SilentlyContinue) -cnotmatch 'SHIPPED|ALERT|FAILED|Exception|At .*\.ps1:\d+') { Remove-Item $log -ErrorAction SilentlyContinue }
    Get-ChildItem $logDir -Filter "ship-*.log" | Where-Object LastWriteTime -lt (Get-Date).AddDays(-30) | Remove-Item -ErrorAction SilentlyContinue
    exit $code
}
$logPath = Get-ChildItem $logDir -Filter "ship-*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1

if ($root -ne (Get-MainCheckout)) { throw "Run ship.ps1 from the main checkout ($(Get-MainCheckout)), not a worktree." }
$pauseFile = Join-Path $common "eqlp-ship-paused"
if ($Pause) { Set-Content $pauseFile (Get-Date -Format o); Write-Host "Shipping paused. .\tools\ship.ps1 -Resume starts it again."; return }
if ($Resume) { Remove-Item $pauseFile -ErrorAction SilentlyContinue; Write-Host "Shipping resumed." }
Write-Host "== ship $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')$(if ($DryRun) { ' (dry run)' })"
if (Test-Path $pauseFile) { Write-Host "Paused since $(Get-Content $pauseFile): .\tools\ship.ps1 -Resume"; return }

# At most one alert of a kind a day from the hourly runs, so a lapsed login doesn't page every hour.
$stateFile = Join-Path $common "eqlp-ship-state.json"
function Send-Alert([string]$Kind, [string]$Title, [string]$Message) {
    Write-Host "ALERT: $Title`n$Message"
    $state = @{}
    if (Test-Path $stateFile) { (Get-Content $stateFile -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $state[$_.Name] = $_.Value } }
    if ($Scheduled -and $state[$Kind] -and ((Get-Date) - [datetime]$state[$Kind]).TotalHours -lt 20) { return }
    $state[$Kind] = (Get-Date -Format o)
    $state | ConvertTo-Json | Set-Content $stateFile
    $null = Send-Ntfy "homelab-alerts" $Title ($Message + $(if ($logPath) { "`nLog: $($logPath.FullName)" })) "warning"
}

# One entry per changed file: the file, and a git command that clears it. $Z is `git status --porcelain=v1 -z` output.
function Format-FileCommands([string]$Root, [string]$Z) {
    $parts = @($Z -split "`0" | Where-Object { $_ })
    for ($i = 0; $i -lt $parts.Count; $i++) {
        $xy = $parts[$i].Substring(0, 2); $path = $parts[$i].Substring(3)
        if ($xy[0] -in "R", "C") { $i++ }
        $cmd = if ($xy -eq "??") { "git -C `"$Root`" clean -f -- `"$path`"" }
               elseif ($xy[0] -eq "A") { "git -C `"$Root`" rm -f -- `"$path`"" }
               else { "git -C `"$Root`" checkout HEAD -- `"$path`"" }
        "$path ($($xy.Trim()))`n  $cmd"
    }
}

# The main checkout's changes. Ok is false when git couldn't say: reads git's exit code and its messages.
function Get-MainChanges([string]$Root) {
    $eap = $ErrorActionPreference; $ErrorActionPreference = "Continue"
    try { $out = @(& git.exe -C $Root status --porcelain=v1 -z --untracked-files=all 2>&1); $code = $LASTEXITCODE } finally { $ErrorActionPreference = $eap }
    $text = ($out | Where-Object { $_ -isnot [Management.Automation.ErrorRecord] }) -join ""
    $err = ($out | Where-Object { $_ -is [Management.Automation.ErrorRecord] } | ForEach-Object { "$_" }) -join "; "
    [pscustomobject]@{ Ok = ($code -eq 0); Z = $text; Error = $err }
}

# Puts back what release.ps1 may have written, with .NET file calls: no git, so no index lock to wait for.
# $Kept maps a path to its bytes, or $null when the file wasn't there. Returns what the alert says.
function Restore-Release([string]$Root, $Kept) {
    foreach ($path in @($Kept.Keys)) {
        $seen = @{}
        $deadline = (Get-Date).AddSeconds(30)
        while ($true) {
            try {
                if ($null -eq $Kept[$path]) { [IO.File]::Delete($path) } else { [IO.File]::WriteAllBytes($path, $Kept[$path]) }
                break
            }
            catch [IO.IOException] {
                if (-not $seen[$_.Exception.Message]) { $seen[$_.Exception.Message] = 1; Write-Host "Restore of $path failed, retrying: $($_.Exception.Message)" }
                if ((Get-Date) -gt $deadline) { Write-Host "Gave up on $path after 30 s."; break }
                Start-Sleep -Seconds 1
            }
            catch { Write-Host "Restore of $path failed: $($_.Exception.Message)"; break }
        }
    }
    $back = $true
    foreach ($path in @($Kept.Keys)) {
        $now = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllBytes($path) } else { $null }
        if ($null -eq $Kept[$path]) { if ($null -ne $now) { $back = $false } }
        elseif ($null -eq $now -or -not [Linq.Enumerable]::SequenceEqual([byte[]]$now, [byte[]]$Kept[$path])) { $back = $false }
    }
    $st = Get-MainChanges $Root
    if (-not $st.Ok) { return "The main checkout couldn't be checked ($($st.Error)): run 'git status' in $Root." }
    if ($back -and -not $st.Z) { return "The main checkout is as it was." }
    $lines = @(Format-FileCommands $Root $st.Z)
    if (-not $lines) { return "A file couldn't be put back: run 'git status' in $Root." }
    "These files are still changed. Each command discards the change:`n" + ($lines -join "`n")
}

# This PC's private settings (docs\specs\private-settings.md), before anything else uses one: step 1 already needs the
# NAS login. With any missing nothing is released, the alert names them (names, never values), and the task ends
# with an error. The alert itself travels over the NAS login: when that's the one missing, the kept log and the
# task's last result are the notice.
$privateSettings = Import-PrivateSettings
if ($privateSettings.Missing) {
    Send-Alert "private" "EQL Pathfinder: shipping needs its private settings" (Format-PrivateMissing $privateSettings)
    exit 1
}

# 1. Logins (refreshed by using them).
$lapsed = @()
& az account get-access-token --resource https://codesigning.azure.net --query expiresOn -o tsv > $null
if ($LASTEXITCODE) { $lapsed += "Azure (code signing): run 'az login'." }
Push-Location (Join-Path $root "site")
try { & npx.cmd cf auth whoami > $null; if ($LASTEXITCODE) { $lapsed += "Cloudflare (website): run 'npx cf auth login' in site\." } } finally { Pop-Location }
if (-not (Test-Path (Join-Path $env:APPDATA "EQLPathfinder-dev\r2.xml"))) { $lapsed += "R2 upload key: run '.\tools\publish.ps1 -ResetKey' once." }
& ssh.exe -o BatchMode=yes -o ConnectTimeout=10 $env:EQLP_NAS_LOGIN true
if ($LASTEXITCODE) { $lapsed += "ssh to the NAS (EQLP_NAS_LOGIN) without a password." }
if ($lapsed) { Send-Alert "logins" "EQL Pathfinder: shipping needs a login" ($lapsed -join "`n") }

# 2. Anything to ship?
$lock = Enter-PipelineLock "ship" -NoWait:$Scheduled
if (-not $lock) { Write-Host "A finish or another ship is running; next hour."; return }
try {
    if ((git -C $root branch --show-current) -ne "main") { Send-Alert "branch" "EQL Pathfinder: shipping waits" "The main checkout isn't on main."; return }
    $st = Get-MainChanges $root
    if (-not $st.Ok) {
        Send-Alert "dirty" "EQL Pathfinder: shipping waits" "Couldn't check the main checkout ($($st.Error)): run 'git status' in $root."
        return
    }
    if ($st.Z) {
        Send-Alert "dirty" "EQL Pathfinder: shipping waits" ("The main checkout has uncommitted changes (someone edited there instead of a worktree, or a release stopped half way). Each command discards the change:`n" + ((Format-FileCommands $root $st.Z) -join "`n"))
        return
    }
    $notes = @(Get-PendingNotes $root)
    if (-not $notes) { Write-Host "Nothing waiting in docs\releases\next."; return }
    if ($lapsed -and -not $DryRun) { Write-Host "Not shipping until the logins above are fixed."; return }

    $proj = Join-Path $root "src\EqlPathfinder.App\EqlPathfinder.App.csproj"
    $current = [regex]::Match([IO.File]::ReadAllText($proj), "<Version>([^<]*)</Version>").Groups[1].Value
    $v = [version]($current -replace '-.*', '')
    $version = if ($notes.Minor -contains $true) { "$($v.Major).$($v.Minor + 1).0" } else { "$($v.Major).$($v.Minor).$($v.Build + 1)" }

    $titles = ($notes | ForEach-Object Title) -join "; "
    $text = if ($notes.Count -eq 1) { $notes[0].Body } else { "$titles.`n`n" + (($notes | ForEach-Object Body) -join "`n`n") }
    # The re-read line joins the notes' last list when they end with one (one list on What's new, not two).
    $sep = if (($text -split "`n")[-1] -match '^- ') { "`n" } else { "`n`n" }
    $text += "$sep- " + $(if ($notes.Reread -contains $true) { "The first start after this update re-reads your logs once (a few minutes)." } else { "No re-read." })
    Write-Host "Version $current -> $version ($($notes.Count) note(s): $titles)$(if ($notes.Job -contains $true) { '; NAS job too' })"
    Write-Host "---- notes ----`n$text`n---------------"
    if ($DryRun) { Write-Host "Dry run: nothing changed."; return }

    # 3. Build, test, sign, publish.
    $noteFile = Join-Path $root "docs\releases\$version.md"
    $kept = [ordered]@{}
    foreach ($f in $proj, $noteFile) { $kept[$f] = $(if (Test-Path -LiteralPath $f) { [IO.File]::ReadAllBytes($f) } else { $null }) }
    try { & (Join-Path $PSScriptRoot "release.ps1") $version -Notes $text }
    catch {
        $failure = $_
        $outcome = try { Restore-Release $root $kept } catch { "The main checkout couldn't be put back or checked ($($_.Exception.Message)): run 'git status' in $root." }
        Send-Alert "release" "EQL Pathfinder $version FAILED" "Nothing was published. $($failure.Exception.Message)`n$outcome"
        throw $failure
    }
    git -C $root rm -q -- @($notes | ForEach-Object { $_.File })
    git -C $root add -- $proj "docs/releases/$version.md"
    git -C $root commit -q -m "v$version ($titles)" -m "Released by tools\ship.ps1."
    if ($LASTEXITCODE) { Send-Alert "commit" "EQL Pathfinder ${version}: released, not committed" "Published, but the release commit failed: commit the version and notes by hand." }
    Write-Host "SHIPPED $version"

    # 4. Website (What's new) and, when asked, the NAS community job.
    $problems = @()
    Push-Location (Join-Path $root "site")
    try {
        if (-not (Test-Path node_modules)) { & npm.cmd ci --no-audit --no-fund --loglevel=error }
        & npm.cmd run deploy
        if ($LASTEXITCODE) { $problems += "The website didn't deploy: run 'npm run deploy' in site\." }
    }
    finally { Pop-Location }
    if ($notes.Job -contains $true) {
        $nas = $env:EQLP_NAS_LOGIN; $dir = "/share/CACHEDEV1_DATA/AppData/scheduler"
        $out = Join-Path $root "out\nas-cli"
        & dotnet publish (Join-Path $root "src\EqlPathfinder.Cli") -c Release -r linux-musl-x64 --self-contained -p:PublishSingleFile=true `
            -p:InvariantGlobalization=true -p:DebugType=none -o $out --nologo -v q
        if (-not $LASTEXITCODE) { & ssh.exe -o BatchMode=yes $nas "rm -rf $dir/eqlp-prev && cp -a $dir/eqlp $dir/eqlp-prev" }
        if (-not $LASTEXITCODE) { & scp.exe -o BatchMode=yes -q (Get-ChildItem $out -File | ForEach-Object FullName) "${nas}:$dir/eqlp/" }
        if (-not $LASTEXITCODE) { & ssh.exe -o BatchMode=yes $nas "/share/CACHEDEV1_DATA/.qpkg/container-station/bin/docker exec scheduler sh /scripts/eqlp-community.sh" }
        if ($LASTEXITCODE) { $problems += "The NAS community job wasn't updated (the previous build is in $dir/eqlp-prev)." }
    }

    # 5. Check what players get.
    $feed = Invoke-RestMethod "https://updates.eqlpathfinder.com/releases.win.json"
    $live = ($feed.Assets | Where-Object Type -eq "Full" | Sort-Object { [version]($_.Version -replace '-.*', '') } | Select-Object -Last 1).Version
    if ($live -ne $version) { $problems += "The update feed offers $live, not $version." }
    $setup = Join-Path ([IO.Path]::GetTempPath()) "eqlp-ship-setup-$version.exe"
    try {
        Invoke-WebRequest "https://updates.eqlpathfinder.com/EQLPathfinder-win-Setup.exe" -OutFile $setup -UseBasicParsing
        if ((Get-AuthenticodeSignature $setup).Status -ne "Valid") { $problems += "The downloadable installer isn't validly signed." }
        if ((Get-FileHash $setup).Hash -ne (Get-FileHash (Join-Path $root "releases\EQLPathfinder-win-Setup.exe")).Hash) { $problems += "The downloadable installer isn't this build." }
    }
    finally { Remove-Item $setup -ErrorAction SilentlyContinue }
    if ((Invoke-WebRequest "https://eqlpathfinder.com/changelog" -UseBasicParsing).Content -notmatch "Latest version: $([regex]::Escape($version))\.") { $problems += "What's new doesn't show $version yet." }

    $headline = ($text -split "`n")[0]
    if ($problems) { Send-Alert "after-$version" "EQL Pathfinder $version released, with problems" ($headline + "`n`n" + ($problems -join "`n")) }
    else { $null = Send-Ntfy "homelab-updates" "EQL Pathfinder $version released" "$headline`nPlayers' copies install it on their own; What's new is updated." "rocket" }
    Write-Host "Done: $version$(if ($problems) { " with problems:`n" + ($problems -join "`n") })"
}
finally { Exit-PipelineLock $lock }
