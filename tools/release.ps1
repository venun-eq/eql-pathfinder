<#
.SYNOPSIS
  Builds a release into releases\ (installer, full package, small delta update) and publishes it to the update
  server, so friends get it within hours. -LocalOnly builds without publishing. Installed copies offer
  "Restart to update"; nobody has to run Setup again.

.EXAMPLE
  .\tools\release.ps1 0.3.1 -Notes "Route: the star goes to the best zone backed by two or more crawls."

.EXAMPLE
  .\tools\release.ps1 0.9.0-beta.1 -Beta -Notes "Ask: the fastest place to level any range."
  A beta release: only copies that joined the beta (Settings > Updates) get it. Beta versions carry a
  pre-release tag, so the regular release 0.9.0 counts as newer and moves everyone onto it.

.PARAMETER Feed
  Where installed copies look for updates, baked into the build. Default: this repo's releases folder,
  which only works on this PC. When the feed is hosted, pass its https address instead.
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Notes = "",
    [string]$Feed = "",
    [switch]$SkipTests,
    # Build into releases\ only; don't send it to friends yet.
    [switch]$LocalOnly,
    # Release to the beta channel (releases.beta.json) instead of the regular one.
    [switch]$Beta,
    # Don't code-sign (a local test build). Releases are signed whenever tools\signing.json exists.
    [switch]$NoSign
)
$ErrorActionPreference = "Stop"
$channel = if ($Beta) { "beta" } else { "win" }
if ($Beta -and $Version -notmatch '-') { throw "A beta version needs a pre-release tag, like 0.9.0-beta.1, so the regular release 0.9.0 comes after it." }
if (-not $Beta -and $Version -match '-') { throw "$Version has a pre-release tag: release it with -Beta, or drop the tag." }
$root = Split-Path $PSScriptRoot -Parent
$env:PATH = "C:\Program Files\Git\cmd;$env:USERPROFILE\.dotnet\tools;$env:PATH"
$releases = Join-Path $root "releases"
if (-not $Feed) {
    # Friends' copies update from the hosted feed once tools\feed.json names it; until then, this folder.
    $cfg = Get-Content (Join-Path $PSScriptRoot "feed.json") -Raw | ConvertFrom-Json
    $Feed = if ($cfg.publicUrl) { $cfg.publicUrl } else { $releases }
}
# publish.ps1 uploads to the Cloudflare account's R2 endpoint, a private setting (docs\specs\private-settings.md).
# Asked here, before anything is built or written, so a release by hand never stops half way for the lack of it.
if ($Feed -ne $releases -and -not $LocalOnly -and -not "$env:EQLP_CF_ACCOUNT_ID".Trim()) {
    throw "EQLP_CF_ACCOUNT_ID isn't set, so the release couldn't be published. Nothing was built. Load this PC's private settings first (.\tools\private.ps1), or pass -LocalOnly."
}
# This PC's own copy keeps updating from the releases folder, so you try each build before you publish it.
$devFeed = Join-Path $env:LOCALAPPDATA "EQL Pathfinder\update-feed.txt"
if (-not (Test-Path $devFeed)) {
    New-Item -ItemType Directory -Force (Split-Path $devFeed) | Out-Null
    [IO.File]::WriteAllText($devFeed, $releases)
}
$proj = Join-Path $root "src\EqlPathfinder.App\EqlPathfinder.App.csproj"

# Code signing (infra/azure): Azure Artifact Signing through Velopack's own signtool and Azure plug-in, as the user signed
# in with `az login` (a personal Microsoft account works through the Azure CLI). Checked before the long build, so a
# missing login stops it at once rather than after packing.
$signing = Join-Path $PSScriptRoot "signing.json"
$signArgs = @()
if ((Test-Path $signing) -and -not $NoSign) {
    $env:PATH = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin;$env:PATH"
    az account get-access-token --resource https://codesigning.azure.net -o none 2>$null
    if ($LASTEXITCODE) { throw "Signing needs the Azure CLI signed in: run az login (or -NoSign for an unsigned local build)." }
    $signArgs = @("--azureTrustedSignFile", $signing)
}

# Bump <Version>. .NET file APIs keep the encoding and line endings as they are.
$text = [IO.File]::ReadAllText($proj)
$text = [regex]::Replace($text, "<Version>[^<]*</Version>", "<Version>$Version</Version>")
[IO.File]::WriteAllText($proj, $text)

if (-not $SkipTests) {
    dotnet test (Join-Path $root "tests\EqlPathfinder.Engine.Tests") -c Release --nologo -v q --logger "console;verbosity=minimal"
    if ($LASTEXITCODE) { throw "Tests failed; nothing was released." }
}

$pub = Join-Path $root "artifacts\publish-$Version"
dotnet publish $proj -c Release -r win-x64 --self-contained -o $pub "-p:UpdateFeed=$Feed" --nologo -v q
if ($LASTEXITCODE) { throw "Publish failed." }

$notesFile = Join-Path $root "artifacts\notes-$Version.md"
if (-not $Notes) { $Notes = "EQL Pathfinder $Version" }
[IO.File]::WriteAllText($notesFile, $Notes)

# From here to the kept note, a failure puts releases\ back as it was: vpk rewrites the json, RELEASES and Setup.exe in
# place and adds this version's packages, and the next run would pack over a feed that already lists them. Keeps every
# name, and a copy of each file that isn't a *.nupkg (those are only added). A failure after the upload starts (publish.ps1)
# leaves releases\ alone: part of it may have gone out.
$before = Join-Path $root "artifacts\releases-before-$Version"
$keptNames = @()
if (Test-Path -LiteralPath $releases) {
    if (Test-Path -LiteralPath $before) { Remove-Item -LiteralPath $before -Recurse -Force }
    New-Item -ItemType Directory -Force $before | Out-Null
    foreach ($f in Get-ChildItem -LiteralPath $releases -Recurse -File) {
        $rel = $f.FullName.Substring($releases.Length).TrimStart('\')
        $keptNames += $rel
        if ($f.Extension -ne ".nupkg") {
            $to = Join-Path $before $rel
            New-Item -ItemType Directory -Force (Split-Path $to -Parent) | Out-Null
            Copy-Item -LiteralPath $f.FullName -Destination $to
        }
    }
}
try {
# Don't pipe vpk's output through Select-Object -First: closing the pipe kills it mid-pack.
vpk pack --packId EQLPathfinder --packVersion $Version --channel $channel --runtime win-x64 --packDir $pub --mainExe EQLPathfinder.exe `
    --packTitle "EQL Pathfinder" --packAuthors Venun --icon (Join-Path $root "src\EqlPathfinder.App\Assets\pathfinder.ico") `
    --splashImage (Join-Path $root "src\EqlPathfinder.App\Assets\splash.png") --splashProgressColor "#F0C860" `
    --releaseNotes $notesFile --outputDir (Join-Path $root "releases") @signArgs
if ($LASTEXITCODE) { throw "vpk pack failed." }
if ($signArgs) {
    # Nothing goes out unsigned: the installer friends download must carry a valid signature.
    $setup = Join-Path $releases "EQLPathfinder-$channel-Setup.exe"
    $sig = Get-AuthenticodeSignature $setup
    if ($sig.Status -ne "Valid") { throw "$setup isn't validly signed ($($sig.Status)): nothing was published." }
    Write-Host "Signed by $($sig.SignerCertificate.Subject)"
}

# The note, kept in the repo with its day and channel: the website's What's new is built from docs\releases (site\).
if ($Feed -ne $releases) {
    $kept = Join-Path $root "docs\releases\$Version.md"
    $front = "---`nversion: $Version`ndate: $((Get-Date).ToString('yyyy-MM-dd'))`nchannel: $(if ($Beta) { 'beta' } else { 'stable' })`n---`n"
    [IO.File]::WriteAllText($kept, $front + $Notes.Trim().Replace("`r`n", "`n") + "`n", (New-Object Text.UTF8Encoding $false))
}
}
catch {
    $failure = $_
    # One file at a time: a file that can't be replaced doesn't stop the rest.
    foreach ($f in @(Get-ChildItem -LiteralPath $releases -Recurse -File -ErrorAction SilentlyContinue)) {
        if ($keptNames -notcontains $f.FullName.Substring($releases.Length).TrimStart('\')) {
            try { Remove-Item -LiteralPath $f.FullName -Force } catch { Write-Host "Couldn't remove $($f.FullName): $($_.Exception.Message)" }
        }
    }
    foreach ($rel in $keptNames) {
        $copy = Join-Path $before $rel
        if (Test-Path -LiteralPath $copy) {
            try { Copy-Item -LiteralPath $copy -Destination (Join-Path $releases $rel) -Force } catch { Write-Host "Couldn't put back $($rel): $($_.Exception.Message)" }
        }
    }
    # Check it rather than trust it: a kept name that's gone, a file that isn't what it was, or a leftover from the pack.
    $notBack = @()
    foreach ($rel in $keptNames) {
        $now = Join-Path $releases $rel
        $copy = Join-Path $before $rel
        if (-not (Test-Path -LiteralPath $now)) { $notBack += $rel }
        elseif ((Test-Path -LiteralPath $copy) -and (Get-FileHash -LiteralPath $now).Hash -ne (Get-FileHash -LiteralPath $copy).Hash) { $notBack += $rel }
    }
    foreach ($f in @(Get-ChildItem -LiteralPath $releases -Recurse -File -ErrorAction SilentlyContinue)) {
        $rel = $f.FullName.Substring($releases.Length).TrimStart('\')
        if ($keptNames -notcontains $rel) { $notBack += $rel }
    }
    if ($notBack) {
        $msg = "releases\ is NOT as it was (it could not be restored); differing:$($notBack -join ', '). A copy of what it held is in $before (a *.nupkg is only added, never changed, so it isn't copied). To clear it, copy the files back: Copy-Item -Force -Path '$before\*' -Destination '$releases' -Recurse, and delete any file in releases\ that wasn't there before."
        Write-Host $msg
        throw "$($failure.Exception.Message)`n$msg"
    }
    Write-Host "releases\ was put back as it was (checked)."
    throw $failure
}
if (Test-Path -LiteralPath $before) { try { Remove-Item -LiteralPath $before -Recurse -Force } catch { Write-Host "The copy of releases\ in $before couldn't be removed ($($_.Exception.Message)); it can go." } }

Write-Host ""
Write-Host "Released $Version ($(if ($Beta) { 'beta channel' } else { 'regular channel' })) into releases\ (update feed baked in: $Feed)"
if ($Feed -ne $releases) { Write-Host "Commit docs\releases\$Version.md with the version bump, then redeploy the website (site\: npm run deploy)." }
if ($Feed -ne $releases -and -not $LocalOnly) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}
elseif ($Feed -ne $releases) { Write-Host "Not published (-LocalOnly). Send it to friends with: .\tools\publish.ps1" }
