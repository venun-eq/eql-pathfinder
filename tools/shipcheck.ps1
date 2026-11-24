<#
.SYNOPSIS
  ship.ps1 and release.ps1 on a throwaway repository, with one step made to fail: what a failed release leaves behind
  and what it says. tools\check.ps1 runs it. Nothing is built for players, signed, published or sent: the real
  scripts run on a copy of themselves, and the copy of pipeline-common.ps1 swaps the logins, the build steps after
  the tests, the upload and the ntfy alert for stand-ins (Add-StandIns, below). The main checkout is never touched.

.EXAMPLE
  .\tools\shipcheck.ps1                      # every case
  .\tools\shipcheck.ps1 -Only lock,held      # the cases whose names start with these

.DESCRIPTION
  Each case makes a repository in %TEMP% shaped like this one (the app's .csproj with a <Version>, a waiting release
  note, tools\ with ship.ps1, release.ps1 and pipeline-common.ps1 copied from here), runs ship.ps1 the way the hourly
  task does (powershell.exe 5.1, -Scheduled, so its output goes to cmd.exe's redirected log) or from this console,
  and compares the checkout after the run with the checkout before it, file by file, by content.
  The tests' step is a real dotnet test of a one-test project that fails with a reason of its own, so the log can
  be read for that reason. Exits 1 when any case fails.
#>
param([string]$Only)
$names = @($Only -split "," | Where-Object { $_ })
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$reason = "the sample's own reason: 7 is not 8"
$proj = "src/EqlPathfinder.App/EqlPathfinder.App.csproj"

# The stand-ins, appended to the throwaway's copy of pipeline-common.ps1 (which ship.ps1 dot-sources, and release.ps1
# runs inside): functions come before programs in PowerShell, so these answer for az, npx, ssh, dotnet, vpk and
# Send-Ntfy. EQLP_SHIPCHECK_FAIL picks the step that fails.
$standIns = @'

# ---- tools\shipcheck.ps1's stand-ins: nothing leaves this PC ----
function az { $global:LASTEXITCODE = 0 }
function npx.cmd { $global:LASTEXITCODE = 0 }
function ssh.exe { $global:LASTEXITCODE = 0 }
function Send-Ntfy([string]$Topic, [string]$Title, [string]$Message, [string]$Tags = "") {
    [IO.File]::AppendAllText($env:EQLP_SHIPCHECK_ALERTS, "[$Topic] $Title`n$Message`n----`n"); $true
}
# vpk rewrites the json and Setup.exe in releases\ in place and adds the new version's package; a failing pack
# leaves a partial file behind. Get-AuthenticodeSignature answers NotSigned for "sign", Valid otherwise.
function vpk {
    $out = $args[[array]::IndexOf($args, "--outputDir") + 1]
    $ver = $args[[array]::IndexOf($args, "--packVersion") + 1]
    if ($env:EQLP_SHIPCHECK_FAIL -eq "pack") { [IO.File]::WriteAllText((Join-Path $out "EQLPathfinder-$ver-win-full.nupkg"), "partial"); $global:LASTEXITCODE = 1; return }
    [IO.File]::WriteAllText((Join-Path $out "EQLPathfinder-$ver-win-full.nupkg"), "package $ver")
    [IO.File]::WriteAllText((Join-Path $out "releases.win.json"), "{ lists $ver }")
    [IO.File]::WriteAllText((Join-Path $out "EQLPathfinder-win-Setup.exe"), "installer $ver")
    $global:LASTEXITCODE = 0
}
function Get-AuthenticodeSignature([string]$FilePath) {
    if ($env:EQLP_SHIPCHECK_FAIL -eq "signheld") {
        # A scanner or an open handle holds the installer as the signature fails: the copy-back can't replace it.
        $ready = Join-Path $env:EQLP_SHIPCHECK_BASE "busy.ready"
        Start-Process powershell.exe -WindowStyle Hidden -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", "`"$env:EQLP_SHIPCHECK_BASE\busy.ps1`"", "signheld", "`"$FilePath`""
        while (-not (Test-Path $ready)) { Start-Sleep -Milliseconds 50 }
    }
    [pscustomobject]@{ Status = $(if ($env:EQLP_SHIPCHECK_FAIL -in "sign", "signheld") { "NotSigned" } else { "Valid" }); SignerCertificate = [pscustomobject]@{ Subject = "CN=stand-in" } }
}
function dotnet {
    $fail = $env:EQLP_SHIPCHECK_FAIL
    if ($args[0] -eq "test" -and $fail -eq "test") {
        & (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source @args
        return
    }
    if ($args[0] -eq "test" -and $fail -in "lock", "held") {
        # Another process has the main checkout busy as the step fails: a git command holding .git\index.lock for
        # two seconds (a session's or an editor's git status), or a program holding the .csproj open for longer
        # than any restore waits.
        $ready = Join-Path $env:EQLP_SHIPCHECK_BASE "busy.ready"
        Start-Process powershell.exe -WindowStyle Hidden -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", "`"$env:EQLP_SHIPCHECK_BASE\busy.ps1`"", $fail
        while (-not (Test-Path $ready)) { Start-Sleep -Milliseconds 50 }
        Write-Host "Failed!  - Failed:     1, Passed:     0 (stand-in)"
        $global:LASTEXITCODE = 1
        return
    }
    $global:LASTEXITCODE = 0
}
'@

# Holds the main checkout busy for the "lock" and "held" cases. Writes its pid, then busy.ready once it holds it.
$busy = @'
param([string]$What, [string]$File)
$base = Split-Path $PSCommandPath -Parent
Set-Content (Join-Path $base "busy.pid") $PID
if ($What -eq "lock") {
    $lock = Join-Path $base "repo\.git\index.lock"
    Set-Content $lock ""
    Set-Content (Join-Path $base "busy.ready") 1
    Start-Sleep -Seconds 2
    Remove-Item $lock
}
elseif ($What -eq "signheld") {
    $fs = [IO.File]::Open($File, "Open", "Read", "Read")
    Set-Content (Join-Path $base "busy.ready") 1
    Start-Sleep -Seconds 120
    $fs.Dispose()
}
else {
    $fs = [IO.File]::Open((Join-Path $base "repo\src\EqlPathfinder.App\EqlPathfinder.App.csproj"), "Open", "Read", "Read")
    Set-Content (Join-Path $base "busy.ready") 1
    Start-Sleep -Seconds 120
    $fs.Dispose()
}
'@

# git.exe with its stderr dropped, without Windows PowerShell 5.1 turning a line-ending warning into a terminating error.
function Invoke-Git { $eap = $ErrorActionPreference; $ErrorActionPreference = "Continue"; try { & git.exe @args 2>$null } finally { $ErrorActionPreference = $eap } }

function Write-Text([string]$Path, [string]$Text) {
    New-Item -ItemType Directory -Force (Split-Path $Path -Parent) | Out-Null
    [IO.File]::WriteAllText($Path, $Text)
}

function New-Throwaway([string]$Case) {
    $base = Join-Path ([IO.Path]::GetTempPath()) "eqlp-shipcheck-$Case-$(Get-Random)"
    $repo = Join-Path $base "repo"
    New-Item -ItemType Directory -Force $repo, (Join-Path $base "appdata\EQLPathfinder-dev"), (Join-Path $base "localappdata") | Out-Null
    Set-Content (Join-Path $base "appdata\EQLPathfinder-dev\r2.xml") "stand-in"
    Write-Text (Join-Path $base "busy.ps1") $busy
    Copy-Item (Join-Path $here "..\.gitignore") (Join-Path $repo ".gitignore")
    foreach ($f in "ship.ps1", "release.ps1", "pipeline-common.ps1", "feed.json") { Write-Text (Join-Path $repo "tools\$f") ([IO.File]::ReadAllText((Join-Path $here $f))) }
    # This PC's private settings (docs\specs\private-settings.md): the example's names, each with a stand-in value.
    $example = Join-Path $here "private.example.env"
    if (Test-Path $example) {
        Write-Text (Join-Path $repo "tools\private.example.env") ([IO.File]::ReadAllText($example))
        $n = 0
        $settings = @(Get-Content $example | Where-Object { $_ -match '^[A-Z][A-Z0-9_]*=' } | ForEach-Object { $n++; "$(($_ -split '=')[0])=stand-in-$n" })
        Write-Text (Join-Path $base "appdata\EQLPathfinder-dev\private.env") (($settings -join "`r`n") + "`r`n")
    }
    [IO.File]::AppendAllText((Join-Path $repo "tools\pipeline-common.ps1"), $standIns)
    Write-Text (Join-Path $repo "tools\publish.ps1") "throw 'The upload failed (stand-in).'`r`n"
    Write-Text (Join-Path $repo "tools\signing.json") "{}`r`n"
    Write-Text (Join-Path $repo "releases\EQLPathfinder-0.1.0-win-full.nupkg") "package 0.1.0"
    Write-Text (Join-Path $repo "releases\releases.win.json") "{ lists 0.1.0 }"
    Write-Text (Join-Path $repo "releases\EQLPathfinder-win-Setup.exe") "installer 0.1.0"
    Write-Text (Join-Path $repo "site\README.md") "The website.`r`n"
    Write-Text (Join-Path $repo $proj) "<Project Sdk=`"Microsoft.NET.Sdk`">`r`n  <PropertyGroup>`r`n    <Version>0.1.0</Version>`r`n  </PropertyGroup>`r`n</Project>`r`n"
    Write-Text (Join-Path $repo "docs\releases\0.1.0.md") "---`nversion: 0.1.0`ndate: 2026-01-01`nchannel: stable`n---`nThe first release.`n"
    Write-Text (Join-Path $repo "docs\releases\next\sample.md") "---`ntitle: A sample change`nreread: false`njob: false`nbump: patch`n---`nA sample change, for players.`n"
    Write-Text (Join-Path $repo "tests\EqlPathfinder.Engine.Tests\EqlPathfinder.Engine.Tests.csproj") @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
</Project>
"@
    Write-Text (Join-Path $repo "tests\EqlPathfinder.Engine.Tests\SampleTests.cs") "public class SampleTests`r`n{`r`n    [Xunit.Fact] public void TheSampleFails() => Xunit.Assert.Fail(`"$reason`");`r`n    [Xunit.Fact] public void TheSamplePasses() { }`r`n}`r`n"
    Invoke-Git -C $repo init -q -b main
    Invoke-Git -C $repo config core.autocrlf true
    Invoke-Git -C $repo add -A
    Invoke-Git -C $repo -c user.name=shipcheck -c user.email=shipcheck@localhost commit -q -m "The throwaway's first commit"
    if ($LASTEXITCODE) { throw "Couldn't make the throwaway repository in $repo." }
    [pscustomobject]@{ Base = $base; Repo = $repo; Alerts = (Join-Path $base "alerts.txt") }
}

# Everything ship.ps1's own "uncommitted changes" check looks at, by content rather than by git's file times: HEAD,
# the index, and every file that isn't ignored.
function Get-Checkout([string]$Repo) {
    $files = @(Invoke-Git -C $Repo ls-files -co --exclude-standard) | Sort-Object -Unique
    $state = [ordered]@{ "(HEAD)" = (Invoke-Git -C $Repo rev-parse HEAD); "(index)" = ((Invoke-Git -C $Repo ls-files -s) -join "`n") }
    foreach ($f in $files) {
        $p = Join-Path $Repo $f
        $state[$f] = if (Test-Path -LiteralPath $p) { (Get-FileHash -LiteralPath $p).Hash } else { "(missing)" }
    }
    $state
}
# releases\ is ignored by git, so it's compared on its own: every name and every file's hash.
function Get-Releases([string]$Repo) {
    $state = [ordered]@{}
    $dir = Join-Path $Repo "releases"
    foreach ($f in Get-ChildItem $dir -Recurse -File | Sort-Object FullName) { $state[$f.FullName.Substring($dir.Length)] = (Get-FileHash -LiteralPath $f.FullName).Hash }
    $state
}
function Compare-Checkout($Before, $After) {
    @(@($Before.Keys) + @($After.Keys) | Sort-Object -Unique | Where-Object { $Before[$_] -ne $After[$_] })
}

function Invoke-Ship($T, [string]$Fail, [switch]$Scheduled) {
    $envs = @{ APPDATA = "$($T.Base)\appdata"; LOCALAPPDATA = "$($T.Base)\localappdata"; EQLP_SHIPCHECK_FAIL = $Fail
        EQLP_SHIPCHECK_ALERTS = $T.Alerts; EQLP_SHIPCHECK_BASE = $T.Base; EQLP_SHIP_CHILD = $null }
    # The throwaway's private.env is all the ship may know. With no file the environment counts, so a shell that had
    # loaded this PC's real settings (tools\private.ps1) would hand the "private-missing" case a login and let it pass
    # or fail for the wrong reason: every name of the example starts cleared, and comes back afterwards.
    $example = Join-Path $here "private.example.env"
    if (Test-Path $example) {
        foreach ($line in Get-Content $example) {
            $kv = [regex]::Match($line, '^\s*([A-Z][A-Z0-9_]*)\s*=')
            if ($kv.Success) { $envs[$kv.Groups[1].Value] = $null }
        }
    }
    $saved = @{}
    foreach ($k in $envs.Keys) { $saved[$k] = [Environment]::GetEnvironmentVariable($k); [Environment]::SetEnvironmentVariable($k, $envs[$k]) }
    try {
        $a = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$(Join-Path $T.Repo 'tools\ship.ps1')`"")
        if ($Scheduled) { $a += "-Scheduled" }
        $ship = Start-Process powershell.exe -ArgumentList $a -NoNewWindow -Wait -PassThru
    }
    finally { foreach ($k in $saved.Keys) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) } }
    $log = Get-ChildItem (Join-Path $T.Repo "artifacts\ship-logs") -Filter "ship-*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    [pscustomobject]@{
        Code = $ship.ExitCode
        Log = $(if ($log) { [IO.File]::ReadAllText($log.FullName) } else { "" })
        Alerts = $(if (Test-Path $T.Alerts) { [IO.File]::ReadAllText($T.Alerts) } else { "" })
    }
}

function Stop-Busy($T) {
    $pidFile = Join-Path $T.Base "busy.pid"
    if (Test-Path $pidFile) { Stop-Process -Id ([int](Get-Content $pidFile)) -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 300 }
    Remove-Item (Join-Path $T.Repo ".git\index.lock") -ErrorAction SilentlyContinue
}

# A failed release: the checkout afterwards is the checkout before, file by file.
function Test-LeavesMainAsItWas([string]$Fail, [switch]$Scheduled, [switch]$ReleasesBack, [switch]$ReleasesStay) {
    $t = New-Throwaway $Fail
    try {
        $before = Get-Checkout $t.Repo
        $relBefore = Get-Releases $t.Repo
        $run = Invoke-Ship $t $Fail -Scheduled:$Scheduled
        Stop-Busy $t
        if ($run.Alerts -notmatch "FAILED") { return "the release didn't fail as the case meant it to. Alerts:`n$($run.Alerts)`nLog:`n$($run.Log)" }
        $changed = Compare-Checkout $before (Get-Checkout $t.Repo)
        if ($changed) { return "main isn't as it was; changed: $($changed -join ', ')`n$(Invoke-Git -C $t.Repo status --porcelain)" }
        $relChanged = Compare-Checkout $relBefore (Get-Releases $t.Repo)
        if ($ReleasesBack -and $relChanged) { return "releases\ isn't as it was; changed: $($relChanged -join ', ')`nLog:`n$($run.Log)" }
        if ($ReleasesStay -and -not $relChanged) { return "releases\ was put back, though the upload may have started" }
    }
    finally { Stop-Busy $t; Remove-Item -Recurse -Force $t.Base -ErrorAction SilentlyContinue }
}

$cases = [ordered]@{
    "tests-fail-scheduled: a release whose tests fail leaves main as it was, run as the hourly task runs it" = {
        Test-LeavesMainAsItWas "test" -Scheduled }
    "tests-fail-console: a release whose tests fail leaves main as it was, run from a console" = {
        Test-LeavesMainAsItWas "test" }
    "pack-fails: a release whose packing fails leaves main as it was" = {
        Test-LeavesMainAsItWas "pack" -Scheduled -ReleasesBack }
    "sign-fails: a release whose installer isn't validly signed leaves main and releases\ as they were" = {
        Test-LeavesMainAsItWas "sign" -Scheduled -ReleasesBack }
    "sign-held: when releases\ can't be put back, the FAILED alert names the file and where the copy is" = {
        $t = New-Throwaway "signheld"
        try {
            $run = Invoke-Ship $t "signheld" -Scheduled
            Stop-Busy $t
            $failed = ($run.Alerts -split "----`n" | Where-Object { $_ -match "FAILED" }) -join ""
            if (-not $failed) { return "no FAILED alert. Alerts:`n$($run.Alerts)`nLog:`n$($run.Log)" }
            if ($failed -notmatch "EQLPathfinder-win-Setup\.exe") { return "the FAILED alert doesn't name the installer that wasn't put back:`n$failed" }
            if ($failed -notmatch "releases-before-") { return "the FAILED alert doesn't say where the copy is:`n$failed" }
            if ($run.Log -match "put back as it was") { return "the log says releases\ was put back, and it wasn't:`n$($run.Log)" }
        }
        finally { Stop-Busy $t; Remove-Item -Recurse -Force $t.Base -ErrorAction SilentlyContinue }
    }
    "upload-fails: a release whose upload fails leaves main as it was, its What's new page too" = {
        Test-LeavesMainAsItWas "upload" -Scheduled -ReleasesStay }
    "lock: a release that fails while another git command holds the index for two seconds leaves main as it was" = {
        Test-LeavesMainAsItWas "lock" -Scheduled }
    "held: when a file can't be put back, the FAILED alert names it and a git command that clears it" = {
        $t = New-Throwaway "held"
        try {
            $before = Get-Checkout $t.Repo
            $run = Invoke-Ship $t "held" -Scheduled
            Stop-Busy $t
            $failed = ($run.Alerts -split "----`n" | Where-Object { $_ -match "FAILED" }) -join ""
            if (-not $failed) { return "no FAILED alert. Alerts:`n$($run.Alerts)" }
            $left = Compare-Checkout $before (Get-Checkout $t.Repo)
            if (-not $left) { return "the case meant the .csproj to stay changed, and nothing did" }
            $named = $proj -replace '/', '[\\/]'
            if ($failed -notmatch $named) { return "the FAILED alert doesn't name $proj, which is still changed:`n$failed" }
            $commands = @($failed -split "`n" | Where-Object { $_ -match '^\s*git\s' })
            if (-not $commands) { return "the FAILED alert has no line that is a git command to clear it:`n$failed" }
            foreach ($c in $commands) { $eap = $ErrorActionPreference; $ErrorActionPreference = "Continue"; & cmd.exe /c "$($c.Trim()) 2>nul" | Out-Null; $ErrorActionPreference = $eap }
            $still = Compare-Checkout $before (Get-Checkout $t.Repo)
            if ($still) { return "the alert's commands didn't clear it; still changed: $($still -join ', ')`n$($commands -join "`n")" }
        }
        finally { Stop-Busy $t; Remove-Item -Recurse -Force $t.Base -ErrorAction SilentlyContinue }
    }
    "dirty: a main checkout a release left changed waits with an alert that names each file and a git command that clears it" = {
        $t = New-Throwaway "dirty"
        try {
            $before = Get-Checkout $t.Repo
            $p = Join-Path $t.Repo $proj
            [IO.File]::WriteAllText($p, [IO.File]::ReadAllText($p).Replace("0.1.0", "0.1.1"))
            $run = Invoke-Ship $t "" -Scheduled
            if ($run.Alerts -notmatch "waits") { return "no 'shipping waits' alert. Alerts:`n$($run.Alerts)" }
            if ($run.Alerts -notmatch ($proj -replace '/', '[\\/]')) { return "the alert doesn't name $($proj):`n$($run.Alerts)" }
            $commands = @($run.Alerts -split "`n" | Where-Object { $_ -match '^\s*git\s' })
            if (-not $commands) { return "the alert has no line that is a git command to clear it:`n$($run.Alerts)" }
            foreach ($c in $commands) { $eap = $ErrorActionPreference; $ErrorActionPreference = "Continue"; & cmd.exe /c "$($c.Trim()) 2>nul" | Out-Null; $ErrorActionPreference = $eap }
            $still = Compare-Checkout $before (Get-Checkout $t.Repo)
            if ($still) { return "the alert's commands didn't clear it; still changed: $($still -join ', ')`n$($commands -join "`n")" }
        }
        finally { Remove-Item -Recurse -Force $t.Base -ErrorAction SilentlyContinue }
    }
    "why: the ship log says why each red test failed, not only its name" = {
        $t = New-Throwaway "why"
        try {
            $run = Invoke-Ship $t "test" -Scheduled
            if ($run.Log -notmatch "TheSampleFails") { return "the log doesn't name the red test at all:`n$($run.Log)" }
            if (-not $run.Log.Contains($reason)) { return "the log names TheSampleFails but not why it failed ('$reason')" }
        }
        finally { Remove-Item -Recurse -Force $t.Base -ErrorAction SilentlyContinue }
    }
    "private-missing: with no private settings on this PC, the hourly ship releases nothing and its alert names the file" = {
        Test-StopsWithoutPrivate "private-missing" { param($file) Remove-Item $file -ErrorAction SilentlyContinue; "private.env" }
    }
    "private-blank: with one private setting left blank, the hourly ship releases nothing and its alert names that setting" = {
        Test-StopsWithoutPrivate "private-blank" -NeedsFile {
            param($file)
            $lines = @(Get-Content $file)
            $name = ($lines[0] -split "=")[0]
            Set-Content $file (@("$name=") + @($lines | Select-Object -Skip 1))
            $name
        }
    }
}

# This PC's private settings are missing or incomplete (docs\specs\private-settings.md): no release starts, main and
# releases\ are as they were, one alert and the kept log name what's missing, and the task ends with an error.
# $Break changes the throwaway's private.env and returns what the alert must name.
function Test-StopsWithoutPrivate([string]$Case, [scriptblock]$Break, [switch]$NeedsFile) {
    $t = New-Throwaway $Case
    try {
        $file = Join-Path $t.Base "appdata\EQLPathfinder-dev\private.env"
        if ($NeedsFile -and -not (Test-Path $file)) { return "tools\private.example.env isn't there yet, so the throwaway has no private settings to leave one blank in" }
        $named = "$(& $Break $file)"
        $before = Get-Checkout $t.Repo
        $relBefore = Get-Releases $t.Repo
        $run = Invoke-Ship $t "" -Scheduled
        if (-not $run.Alerts.Contains($named)) { return "no alert names $named. Alerts:`n$($run.Alerts)`nLog:`n$($run.Log)" }
        if ($run.Alerts -match "FAILED" -or $run.Log -match "Version 0\.1\.0 ->") { return "a release was started before the settings were checked. Alerts:`n$($run.Alerts)" }
        if (-not $run.Log.Contains($named)) { return "the ship log wasn't kept, or doesn't name $named (the alert travels over the NAS login, which is one of the settings):`n$($run.Log)" }
        if ($run.Code -eq 0) { return "ship.ps1 ended with 0, so the scheduled task's last result reads as a success" }
        $changed = Compare-Checkout $before (Get-Checkout $t.Repo)
        if ($changed) { return "main isn't as it was; changed: $($changed -join ', ')" }
        $relChanged = Compare-Checkout $relBefore (Get-Releases $t.Repo)
        if ($relChanged) { return "releases\ isn't as it was; changed: $($relChanged -join ', ')" }
    }
    finally { Remove-Item -Recurse -Force $t.Base -ErrorAction SilentlyContinue }
}

$failures = 0
foreach ($name in $cases.Keys) {
    if ($names -and -not ($names | Where-Object { $name.StartsWith("$_") })) { continue }
    $problem = & $cases[$name]
    if ($problem) { $failures++; Write-Host "FAIL $name`n     $($problem -replace "`n", "`n     ")" }
    else { Write-Host "ok   $name" }
}
if ($failures) { Write-Host "$failures ship case(s) failed."; exit 1 }
