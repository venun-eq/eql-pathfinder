<#
.SYNOPSIS
  The public repository's snapshot of main: one commit per release, made by the pseudonym in tools\github.json, with no
  history of this PC's (docs\specs\github-launch.md). Run by hand from the user's own terminal.
.DESCRIPTION
  Without -Push it checks everything and shows the version, the remote and the commit it would push. With -Push it
  pushes that commit to the remote's main and tags it v<version>; only then does the local refs/public/main move.
  Every check runs before the commit is made, and any problem exits non-zero with nothing pushed. Never forced.
.PARAMETER Repo             The main checkout (default: this repository's).
.PARAMETER PrivateSettings  This PC's private.env (default: %APPDATA%\EQLPathfinder-dev\private.env).
.PARAMETER ExamplePath      The names the settings must all fill (default: tools\private.example.env).
.PARAMETER Config           github.json (default: main's own tools\github.json, so a worktree's edit can't redirect a push).
.PARAMETER Remote           The URL to push to (default: from the config's account and repository).
.PARAMETER Gitleaks         gitleaks.exe (default: the one on the PATH). Required for -Push.
#>
param(
    [string]$Repo = "",
    [string]$PrivateSettings = "",
    [string]$ExamplePath = "",
    [string]$Config = "",
    [string]$Remote = "",
    [string]$Gitleaks = "",
    [switch]$Push
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "pipeline-common.ps1")

$publicRef = "refs/public/main"
$tmp = $null
$gitEnv = "GIT_AUTHOR_NAME", "GIT_AUTHOR_EMAIL", "GIT_AUTHOR_DATE", "GIT_COMMITTER_NAME", "GIT_COMMITTER_EMAIL", "GIT_COMMITTER_DATE"
$savedEnv = @{}; foreach ($n in $gitEnv) { $savedEnv[$n] = [Environment]::GetEnvironmentVariable($n) }

function Get-One($Lines) { "$(@($Lines)[0])".Trim() }

try {
    # Only the user's own terminal pushes: a session of Claude Code has CLAUDECODE set in every shell it starts.
    if ($Push -and $env:CLAUDECODE) { throw "Refusing -Push from a Claude Code session (CLAUDECODE is set). Run it in your own terminal." }

    if (-not $Repo) { $Repo = Get-MainCheckout }
    if (-not $PrivateSettings) { $PrivateSettings = Get-PrivateSettingsPath }
    if (-not $ExamplePath) { $ExamplePath = Join-Path $PSScriptRoot "private.example.env" }

    # A push is the one place a stranger's empty settings must not pass: every name filled (and below, a deny word).
    $settings = Import-PrivateSettings $PrivateSettings $ExamplePath
    if ($settings.Missing) { throw (Format-PrivateMissing $settings) }

    # A signature would carry the owner's key.
    foreach ($key in "commit.gpgsign", "tag.gpgsign") {
        if ((Get-One (git -C $Repo config --get $key)) -eq "true") { throw "git is set to sign ($key is true): a signature would carry your key. Turn it off for this repository (git config $key false)." }
    }

    # Main, read once, as a commit: never HEAD, never the folder.
    $main = Get-One (git -C $Repo rev-parse --verify "refs/heads/main^{commit}")
    if ($main -notmatch '^[0-9a-f]{40}$') { throw "No main branch in $Repo." }

    $configText = if ($Config) { [IO.File]::ReadAllText($Config) } else { (@(git -C $Repo show "${main}:tools/github.json")) -join "`n" }
    if (-not $configText.Trim()) { throw "No tools\github.json in main." }
    $cfg = $configText | ConvertFrom-Json
    $empty = @("account", "repository", "name", "email" | Where-Object { -not "$($cfg.$_)".Trim() })
    if ($empty) { throw "github.json leaves these empty: $($empty -join ', ')." }

    $csproj = (@(git -C $Repo show "${main}:src/EqlPathfinder.App/EqlPathfinder.App.csproj")) -join "`n"
    $vm = [regex]::Match($csproj, '<Version>\s*([^<\s]+)\s*</Version>')
    if (-not $vm.Success) { throw "No <Version> in main's EqlPathfinder.App.csproj." }
    $version = $vm.Groups[1].Value
    $tag = "refs/tags/v$version"
    if (-not $Remote) { $Remote = "https://$($cfg.account)@github.com/$($cfg.account)/$($cfg.repository).git" }

    # Everything that's read comes from one detached checkout of that one commit.
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "eqlp-public-$(Get-Random)"
    git -C $Repo worktree add --detach $tmp $main | Out-Null
    if ($LASTEXITCODE) { throw "Couldn't check out main's commit $main." }

    # What's published is main's tree without the paths github.json leaves out; every check below reads what's published.
    $leftOut = @()
    foreach ($prefix in @($cfg.leave_out)) {
        $pre = "$prefix".Trim().Replace("\", "/").TrimEnd("/")
        if (-not $pre) { continue }
        if ($pre -match '(^|/)\.\.?(/|$)|^[/:]') { throw "leave_out entry '$prefix' must be a path or glob inside the repository." }
        $n = @(git -C $tmp -c core.quotepath=off ls-files -- ":(glob)$pre").Count
        if ($n -eq 0 -and $pre -notmatch '[*?\[]') { throw "leave_out entry '$prefix' matches no file in main (paths are case-sensitive): it would leave nothing out." }
        git -C $tmp rm -r -q -f --ignore-unmatch -- ":(glob)$pre" | Out-Null
        if ($LASTEXITCODE) { throw "Couldn't leave out $pre." }
        $leftOut += "$pre ($n files)"
    }
    $publicTree = Get-One (git -C $tmp write-tree)
    if ($publicTree -notmatch '^[0-9a-f]{40}$') { throw "Couldn't make the public tree." }

    $problems = @()
    $priv =Test-NothingPrivate $tmp $PrivateSettings $ExamplePath
    $problems += @($priv.Problems | ForEach-Object { "private: $_" })
    if ($priv.Denied -lt 1) { $problems += "private: no deny words are filled, so the guard looks for nothing." }
    $problems += @(Get-PendingNotes $tmp | ForEach-Object { "a release note is waiting: docs\releases\next\$($_.Name).md: the next release must go first." })

    # Files the guard can't read (a NUL in them: images, fonts, UTF-16) need the user's eyes once each.
    $looked = @{}
    if ($cfg.looked) { foreach ($p in $cfg.looked.PSObject.Properties) { $looked[$p.Name] = "$($p.Value)" } }
    $unread = @()
    foreach ($line in @(git -C $tmp -c core.quotepath=off ls-files -s)) {
        $m = [regex]::Match("$line", '^\d+ ([0-9a-f]+) 0\t(.*)$')
        if (-not $m.Success) { continue }
        $bytes = $null
        try { $bytes = [IO.File]::ReadAllBytes((Join-Path $tmp $m.Groups[2].Value)) } catch { }
        if ($null -ne $bytes -and [Array]::IndexOf($bytes, [byte]0) -lt 0) { continue }
        if ($looked[$m.Groups[2].Value] -ne $m.Groups[1].Value) { $unread += "$($m.Groups[2].Value) $($m.Groups[1].Value)" }
    }
    if ($unread) {
        Write-Host "Unread files that need a look (path, blob hash): $($unread.Count)"
        $unread | ForEach-Object { Write-Host "  $_" }
        $problems += "$($unread.Count) unread file(s) haven't been looked at: add each path with its hash to `"looked`" in tools\github.json once you have."
    }

    # gitleaks, the second look: keys and tokens of any shape.
    $leaks = $null
    if ($Gitleaks) { if (Test-Path -LiteralPath $Gitleaks) { $leaks = $Gitleaks } }
    else { $c = Get-Command gitleaks -ErrorAction SilentlyContinue; if ($c) { $leaks = $c.Source } }
    if ($leaks) {
        $eap = $ErrorActionPreference; $ErrorActionPreference = "Continue"
        try { $found = & $leaks dir --redact --no-banner $tmp 2>&1 | Out-String; $code = $LASTEXITCODE } finally { $ErrorActionPreference = $eap }
        if ($code) { $problems += "gitleaks reported a finding (exit $code):`n$found" } else { Write-Host "gitleaks: nothing found." }
    }
    elseif ($Push) { $problems += "gitleaks isn't installed, and -Push needs it as the second look for keys and tokens." }
    else { Write-Host "gitleaks: not run (not installed). -Push requires it." }

    if ($problems) { throw "Refusing:`n  - $($problems -join "`n  - ")" }

    # The remote is the truth, read before anything is made.
    $public = Get-One (git -C $Repo rev-parse -q --verify "$publicRef^{commit}")
    if ($LASTEXITCODE) { $public = "" }
    if ($public) {
        git -C $Repo merge-base $public $main | Out-Null
        if (-not $LASTEXITCODE) { throw "$publicRef shares history with main: it's no public snapshot. Remove it (git update-ref -d $publicRef) and fetch the remote's main into it." }
    }
    $ls = @(git -C $Repo ls-remote $Remote refs/heads/main $tag)
    if ($LASTEXITCODE) { throw "Couldn't read the remote $Remote." }
    $remoteMain = ""; $remoteTag = ""
    foreach ($l in $ls) {
        $p = "$l" -split "\s+"
        if ($p[1] -eq "refs/heads/main") { $remoteMain = $p[0] } elseif ($p[1] -eq $tag) { $remoteTag = $p[0] }
    }
    if ($remoteMain -ne $public) {
        $how = if (-not $public -and $remoteMain) { " To carry on from it: git fetch $Remote main:$publicRef" } else { "" }
        throw "The remote's main is '$remoteMain' and $publicRef here is '$public': they must be the same (both empty the first time). Nothing was changed.$how"
    }
    $mainTree = $publicTree
    if ($remoteTag) {
        if ($remoteTag -eq $public -and (Get-One (git -C $Repo rev-parse "$public^{tree}")) -eq $mainTree) {
            Write-Host "v$version is already published as $public. Nothing to do."
            return
        }
        if ($remoteTag -eq $public) { throw "v$version is published with another tree: main's files changed since. The next release carries the change; nothing was pushed." }
        throw "The remote already has v$version on another commit ($remoteTag). Nothing was pushed."
    }

    # The commit: the same main always makes the same one.
    $when = "$(Get-One (git -C $Repo log -1 --format=%ct $main)) +0000"
    foreach ($n in "AUTHOR", "COMMITTER") {
        [Environment]::SetEnvironmentVariable("GIT_${n}_NAME", $cfg.name)
        [Environment]::SetEnvironmentVariable("GIT_${n}_EMAIL", $cfg.email)
        [Environment]::SetEnvironmentVariable("GIT_${n}_DATE", $when)
    }
    $parents = if ($public) { @("-p", $public) } else { @() }
    $new = Get-One (git -C $Repo commit-tree --no-gpg-sign $mainTree @parents -m "EQL Pathfinder v$version")
    foreach ($n in $gitEnv) { [Environment]::SetEnvironmentVariable($n, $savedEnv[$n]) }
    if ($new -notmatch '^[0-9a-f]{40}$') { throw "Couldn't make the commit." }

    git -C $Repo merge-base $new $main | Out-Null
    if (-not $LASTEXITCODE) { throw "The new commit shares history with main. Nothing was pushed." }
    $count = [int](Get-One (git -C $Repo rev-list --count $new))
    $before = if ($public) { [int](Get-One (git -C $Repo rev-list --count $public)) } else { 0 }
    if ($count -ne $before + 1) { throw "The new commit has $count commits behind it, not $($before + 1). Nothing was pushed." }

    Write-Host "Version: v$version"
    Write-Host "Remote:  $Remote"
    Write-Host "Commit:  $new ($(if ($public) { "on $public" } else { 'the first, no parent' })), the tree of main $main"
    Write-Host "Left out:  $(if ($leftOut) { $leftOut -join ', ' } else { 'nothing' })"
    if (-not $Push) { Write-Host "Dry run: nothing was pushed. Add -Push to push it."; return }

    git -C $Repo -c push.followTags=false push --atomic --porcelain $Remote "${new}:refs/heads/main" "${new}:$tag"
    if ($LASTEXITCODE) { throw "The push failed ($LASTEXITCODE); $publicRef is where it was." }
    $old = if ($public) { $public } else { "0" * 40 }
    git -C $Repo update-ref $publicRef $new $old
    if ($LASTEXITCODE) { throw "Pushed, but $publicRef didn't move to $new. Set it by hand: git update-ref $publicRef $new" }
    Write-Host "Pushed v$version as $new."
}
catch {
    Write-Host "$($_.Exception.Message)"
    exit 1
}
finally {
    foreach ($n in $gitEnv) { [Environment]::SetEnvironmentVariable($n, $savedEnv[$n]) }
    if ($tmp) {
        git -C $Repo worktree remove --force $tmp | Out-Null
        if (Test-Path -LiteralPath $tmp) { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
        git -C $Repo worktree prune | Out-Null
    }
}
