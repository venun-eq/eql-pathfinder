<#
.SYNOPSIS
  The gate every change passes before it reaches main: builds the whole solution and runs every test that the change
  can affect. tools\feature.ps1 finish runs it in a clean copy of the branch; you can run it in any worktree.

.DESCRIPTION
  Always: the solution (app, engine, command-line tool) in Release, the engine tests, and the window's page scripts
  (tools\webcheck.mjs: they load, and neither a data message nor a page's own answer names anything undeclared).
  The engine tests need Microsoft Edge installed: the page tests among them open the app's page in headless Edge,
  and without it they fail and say where they looked (docs\quality.md). They are never skipped.
  Always, and first because they take a second: the pipeline's own parts on samples (the release-note parser, the
  role gate, the hooks), the agents' and skills' front matter, docs\code-map.md against the files it names
  (tools\mapcheck.mjs), and tools\webcheck.mjs against copies of the page scripts with a slip in each.
  When server\ changed (or -All): the Worker's typecheck and its tests in workerd.
  When site\ or docs\releases\ changed (or -All): the website's typecheck and build (What's new is built from the notes).
  Exits non-zero on the first failure.
#>
param(
    # What the change is measured against.
    [string]$Base = "main",
    # Every step, whatever changed.
    [switch]$All,
    # Only the pipeline's own parts on samples, and the code map: a few seconds, and nothing is built.
    [switch]$SamplesOnly
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Git\cmd;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
. (Join-Path $PSScriptRoot "pipeline-common.ps1")
# Dev work stays off the game's cores: this run and what it starts (builds, tests, Edge) are pinned (docs/developing-while-playing.md).
. (Join-Path $PSScriptRoot "dev-limits.ps1")
Set-DevLimits

$changed = @(git -C $root diff --name-only "$Base...HEAD")
function Invoke-Step([string]$Name, [scriptblock]$Run) {
    Write-Host "== $Name"
    & $Run
    if ($LASTEXITCODE) { throw "check failed: $Name" }
}

# The release-note parser ship.ps1 depends on: a sample note must come back whole.
Write-Host "== release-note parser"
$sample = Join-Path ([IO.Path]::GetTempPath()) "eqlp-notes-$(Get-Random)"
New-Item -ItemType Directory -Force (Join-Path $sample "docs\releases\next") | Out-Null
[IO.File]::WriteAllText((Join-Path $sample "docs\releases\next\sample.md"),
    "---`ntitle: Sample`nreread: true`njob: false`nbump: minor`n---`nOne sentence.`n`n- A bullet.`n", (New-Object Text.UTF8Encoding $false))
try {
    $n = @(Get-PendingNotes $sample)
    if ($n.Count -ne 1 -or $n[0].Title -ne "Sample" -or $n[0].Body -ne "One sentence.`n`n- A bullet." -or -not $n[0].Reread -or $n[0].Job -or -not $n[0].Minor) {
        throw "check failed: the release-note parser read the sample as $($n | ConvertTo-Json -Compress)"
    }
    $null = @(Get-PendingNotes $root)   # every waiting note in this branch parses
}
finally { Remove-Item -Recurse -Force $sample -ErrorAction SilentlyContinue }

# The cores dev work may use (tools\dev-limits.ps1), on a sample topology like this PC's: 8 P-cores with two threads, 8 E-cores.
Write-Host "== dev limits"
$sampleCores = @(0..7 | ForEach-Object { @{ Class = 1; Mask = [uint64](3 -shl (2 * $_)) } }) + @(0..7 | ForEach-Object { @{ Class = 0; Mask = [uint64](1 -shl (16 + $_)) } })
$m = Select-DevMask $sampleCores 2
if ($m -ne [uint64]0xFFF000) { throw "check failed: dev limits chose 0x$($m.ToString('X')), not the E-cores plus the last 2 P-cores (0xFFF000)" }
if ($null -ne (Select-DevMask @(@{ Class = 0; Mask = [uint64]3 }, @{ Class = 0; Mask = [uint64]12 }))) { throw "check failed: dev limits pinned a PC whose cores are all alike" }
if ($null -ne (Select-DevMask @())) { throw "check failed: dev limits pinned with no topology" }

# The ship's task starts ship.ps1 through run-limited.ps1: its switch and values must arrive as typed.
$echo = Join-Path ([IO.Path]::GetTempPath()) "eqlp-echo-$(Get-Random).ps1"
Set-Content $echo 'param([switch]$Scheduled, [string]$Dest, [string]$Pos) "$Scheduled|$Dest|$Pos"'
try {
    $got = (& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "run-limited.ps1") -Script $echo -Scheduled -Dest "a b" ) -join ""
    if ($got -ne "True|a b|") { throw "check failed: run-limited.ps1 passed '$got' to a script started with -Scheduled -Dest 'a b' (expected 'True|a b|')" }
    # With no extra arguments, and with a named value: no error, no null positional.
    $got = (& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "run-limited.ps1") -Script $echo 2>&1) -join ""
    if ($got -ne "False||") { throw "check failed: run-limited.ps1 with no extra arguments gave '$got' (expected 'False||')" }
    $got = (& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "run-limited.ps1") -Script $echo -Scheduled -Dest v 2>&1) -join ""
    if ($got -ne "True|v|") { throw "check failed: run-limited.ps1 with -Scheduled -Dest v gave '$got' (expected 'True|v|')" }
}
finally { Remove-Item $echo -ErrorAction SilentlyContinue }

# Private settings (docs\specs\private-settings.md): the loader ship.ps1 and the deploy scripts get their account IDs
# and logins through, and the guard that keeps those, home folders and home-network addresses out of tracked files.
# What the guard must find is put together here from pieces, so this file passes it. Every sample runs, then one
# failure lists each that didn't hold.
Write-Host "== private settings"
$pp = @()
$psDir = Join-Path ([IO.Path]::GetTempPath()) "eqlp-private-$(Get-Random)"
New-Item -ItemType Directory -Force $psDir | Out-Null
$psNames = "SAMPLE_ACCOUNT", "SAMPLE_LOGIN", "SAMPLE_DENY"
$psSaved = @{}
foreach ($k in $psNames) { $psSaved[$k] = [Environment]::GetEnvironmentVariable($k); [Environment]::SetEnvironmentVariable($k, $null) }
try {
    # The loader. The file is as Notepad saves it: a byte-order mark, CRLF, a blank line, a comment, spaces round a
    # value, an = inside one.
    $psExample = Join-Path $psDir "example.env"; $psFile = Join-Path $psDir "private.env"
    [IO.File]::WriteAllText($psExample, "# The names, no values.`r`nSAMPLE_ACCOUNT=`r`nSAMPLE_LOGIN=`r`nSAMPLE_DENY=`r`n", (New-Object Text.UTF8Encoding $false))
    [IO.File]::WriteAllText($psFile, "SAMPLE_ACCOUNT= abc123 `r`n`r`n# a comment`r`nSAMPLE_LOGIN=a=b`r`nSAMPLE_DENY=`r`n", (New-Object Text.UTF8Encoding $true))
    $r = Import-PrivateSettings $psFile $psExample
    $seen = (cmd.exe /c "echo %SAMPLE_ACCOUNT%/%SAMPLE_LOGIN%") -join ""
    if ($seen -ne "abc123/a=b") { $pp += "the loader: a program started after it saw '$seen' for SAMPLE_ACCOUNT/SAMPLE_LOGIN, not 'abc123/a=b' (the file has a byte-order mark, spaces round a value and an = inside one)" }
    if ((@($r.Missing) -join ",") -ne "SAMPLE_DENY") { $pp += "the loader: with SAMPLE_DENY left blank it reported '$(@($r.Missing) -join ',')' missing, not 'SAMPLE_DENY'" }
    foreach ($k in $psNames) { [Environment]::SetEnvironmentVariable($k, $null) }
    $r = Import-PrivateSettings (Join-Path $psDir "none.env") $psExample
    if ((@($r.Missing) -join ",") -ne ($psNames -join ",")) { $pp += "the loader: with no file it reported '$(@($r.Missing) -join ',')' missing, not every name in the example" }
    # Where releases run on a CI service there is no file: its secret variables are the same names, already set.
    [Environment]::SetEnvironmentVariable("SAMPLE_ACCOUNT", "from-ci")
    $r = Import-PrivateSettings (Join-Path $psDir "none.env") $psExample
    if ((@($r.Missing) -join ",") -ne "SAMPLE_LOGIN,SAMPLE_DENY") { $pp += "the loader: with no file and SAMPLE_ACCOUNT already in the environment it reported '$(@($r.Missing) -join ',')' missing, not 'SAMPLE_LOGIN,SAMPLE_DENY'" }
    # On this PC the file wins over a variable already set: one place to look for what a deploy will use, and a value
    # an earlier load left in a shell can't outlive an edit of the file.
    $null = Import-PrivateSettings $psFile $psExample
    if ($env:SAMPLE_ACCOUNT -ne "abc123") { $pp += "the loader: with SAMPLE_ACCOUNT in the file and already in the environment as 'from-ci', the environment held '$env:SAMPLE_ACCOUNT' afterwards, not the file's 'abc123'" }

    # The example the repository keeps: the names, and nothing after any =.
    $exampleFile = Join-Path $PSScriptRoot "private.example.env"
    if (-not (Test-Path $exampleFile)) { $pp += "tools\private.example.env isn't there: the names of this PC's private settings, with no values" }
    else {
        $named = @(Get-Content $exampleFile | Where-Object { $_.Trim() -and $_ -notmatch '^\s*#' })
        $valued = @($named | Where-Object { $_ -notmatch '^[A-Z][A-Z0-9_]*=\s*$' })
        if (-not $named -or $valued) { $pp += "tools\private.example.env must be NAME= lines with nothing after the =; it has $($named.Count) line(s), and $($valued.Count) that aren't" }
    }

    # The guard, on one line of text at a time. A home folder in each way it gets written, then a home-network address.
    $bs = [string][char]92; $who = "jo" + "sample"
    $bad = @(
        ("seen in C:" + $bs + "Users" + $bs + $who + "\AppData\Local\Temp\claude\scratchpad\shots\home.png"),   # as an agent writes a scratchpad path into a spec
        ("`"C:" + $bs + $bs + "Users" + $bs + $bs + $who + "\\AppData`""),                                       # inside a C# or JSON string
        ("d:/" + "users/" + $who + "/EQLPathfinder"),                                                            # forward slashes, another drive, lower case
        ("cd /c/" + "Users/" + $who + "/EQLPathfinder"),                                                         # Git Bash
        ("~/.claude/projects/C--" + "Users-" + $who + "-EQLPathfinder"),                                         # Claude Code's folder for a project's transcripts
        ("ssh nas@" + "192.168" + ".0.7 true"))
    $got = (@(Find-PrivateText ($bad -join "`n")) | ForEach-Object { "$($_.Line) $($_.Kind)" }) -join ", "
    if ($got -ne "1 path, 2 path, 3 path, 4 path, 5 path, 6 address") { $pp += "the guard: of five lines with a home folder, each written another way, and a sixth with a home-network address, it found '$got', not '1 path, 2 path, 3 path, 4 path, 5 path, 6 address'" }
    # What the repository keeps, and must go on keeping: the game's folder under Public, the tests' made-up Bob and
    # someone, a placeholder, a variable, the rule itself in words, an address that isn't a home network's.
    $fine = @(
        'C:\Users\Public\Daybreak Game Company\Installed Games\EverQuest Legends\',
        'cd "/c/Users/Public/Daybreak Game Company/Installed Games/EverQuest Legends"',
        'new IOException(@"C:\Users\Bob\AppData\Local\EQL Pathfinder\x for Venun")', '"selftest probe C:\\Users\\Bob"',
        'ShareKeyHome.MadeIn(@"C:\Users\someone\AppData\Local\EQL Pathfinder", copy)',
        'C:\Users\<name>\EQLPathfinder-wt, %APPDATA%\EQLPathfinder-dev\private.env and $env:USERPROFILE\.dotnet\tools',
        'C:\Program Files\nodejs', 'a home folder (C:\Users\ and a name) or an address that starts 192.168.x.x',
        'from 203.0.113.7 or 10.0.0.1', '# /c/Users/... (a path cut short, as a comment in this file has it)')
    $hits = @(Find-PrivateText ($fine -join "`n"))
    if ($hits) { $pp += "the guard: it stopped at line(s) $(($hits | ForEach-Object Line) -join ', ') of what the repository keeps (Public, Bob, someone, <name>, %APPDATA%, the rule in words, other addresses)" }
    # The deny list: whole words in any case, so a short name inside another word passes, and a blank entry matches nothing.
    $deny = @("Jo", "Sampleton", "0123abcd0123abcd", "nas-user@nas.example", "", " ")
    $text = @("Signed by Jo Sampleton", "jo's notes, sent as JO@nas", "https://0123abcd0123abcd.r2.example/updates", "ssh nas-user@nas.example true",
        "a banjo, a major join, the Sampletons", "plain text") -join "`n"
    $got = (@(Find-PrivateText $text $deny) | ForEach-Object { "$($_.Line) $($_.Kind)" }) -join ", "
    if ($got -ne "1 private, 2 private, 3 private, 4 private") { $pp += "the guard: with a deny list of a short name, a surname, an account ID and a login, it found '$got' in six lines, not '1 private, 2 private, 3 private, 4 private' (lines 5 and 6 only contain a denied word inside another, or nothing)" }

    # The guard over a repository: what git tracks, and nothing else.
    $psRepo = Join-Path $psDir "repo"
    New-Item -ItemType Directory -Force $psRepo | Out-Null
    git -C $psRepo init -q
    [IO.File]::WriteAllText((Join-Path $psRepo "tracked.md"), "fine`n" + $bad[0] + "`n")
    [IO.File]::WriteAllText((Join-Path $psRepo "untracked.md"), $bad[0] + "`n")
    git -C $psRepo add tracked.md
    $found = @(Find-PrivateFiles $psRepo)
    if ($found.Count -ne 1 -or "$($found[0])" -notmatch '^tracked\.md:2 path') { $pp += "the guard: in a sample repository whose tracked.md has a home folder on line 2 (untracked.md has one too, and isn't git's) it reported '$($found -join '; ')', not 'tracked.md:2 path'" }
    # A tracked file it can't read is a problem, never a pass: one whose name has a letter outside ASCII (git writes
    # such a name quoted unless told otherwise) is read like any other, and one that's gone from the disk is reported.
    $psRepo2 = Join-Path $psDir "repo2"
    New-Item -ItemType Directory -Force $psRepo2 | Out-Null
    git -C $psRepo2 init -q
    $odd = "caf" + [char]233 + ".md"
    [IO.File]::WriteAllText((Join-Path $psRepo2 $odd), $bad[0] + "`n")
    [IO.File]::WriteAllText((Join-Path $psRepo2 "gone.md"), "fine`n")
    [IO.File]::WriteAllText((Join-Path $psRepo2 "notes-jo-sampleton.md"), "plain text`n")
    git -C $psRepo2 add .
    Remove-Item (Join-Path $psRepo2 "gone.md")
    $found = @(Find-PrivateFiles $psRepo2)
    $got = $found -join '; '
    if ($got -ne "${odd}:1 path; gone.md: not read") { $pp += "the guard: in a sample repository whose $odd has a home folder on line 1 and whose gone.md is tracked and not on the disk it reported '$got', not '${odd}:1 path; gone.md: not read'" }
    # The names of tracked files are published too, so they're searched like the text inside them.
    $got = @(Find-PrivateFiles $psRepo2 @("Sampleton")) -join '; '
    if ($got -ne "${odd}:1 path; gone.md: not read; notes-jo-sampleton.md:name private") { $pp += "the guard: with Sampleton denied and a tracked notes-jo-sampleton.md it reported '$got', not '${odd}:1 path; gone.md: not read; notes-jo-sampleton.md:name private'" }

    # The gate's step (Test-NothingPrivate), on that repository with one more tracked file. It denies the deny list's
    # words and every other setting's value, and leaves the environment as it found it: the build and the tests that
    # follow it run as a stranger's clone would.
    [IO.File]::WriteAllText((Join-Path $psRepo "words.md"), "the account abc123`nby jo sampleton`nplain text`n")
    git -C $psRepo add words.md
    [IO.File]::WriteAllText($psFile, "SAMPLE_ACCOUNT=abc123`r`nSAMPLE_LOGIN=nas-user@nas.example`r`nSAMPLE_DENY=Jo, Sampleton,`r`n")
    foreach ($k in $psNames) { [Environment]::SetEnvironmentVariable($k, $null) }
    [Environment]::SetEnvironmentVariable("SAMPLE_ACCOUNT", "as-it-was")
    $g = Test-NothingPrivate $psRepo $psFile $psExample
    $got = "$($g.Denied) denied: $($g.Problems -join '; ')"
    if ($got -ne "4 denied: tracked.md:2 path; words.md:1 private; words.md:2 private") { $pp += "the gate: with every setting filled it reported '$got', not '4 denied: tracked.md:2 path; words.md:1 private; words.md:2 private'" }
    $left = "$env:SAMPLE_ACCOUNT/$env:SAMPLE_LOGIN/$env:SAMPLE_DENY"
    if ($left -ne "as-it-was//") { $pp += "the gate: it left SAMPLE_ACCOUNT/SAMPLE_LOGIN/SAMPLE_DENY in the environment as '$left', not as they were before it ('as-it-was//'): the build and the tests after it must run with no private setting" }
    # Some settings filled and one not: a half-armed guard fails, naming the one that's missing.
    [IO.File]::WriteAllText($psFile, "SAMPLE_ACCOUNT=abc123`r`nSAMPLE_LOGIN=nas-user@nas.example`r`n")
    $g = Test-NothingPrivate $psRepo $psFile $psExample
    if (-not (@($g.Problems) -match 'SAMPLE_DENY') -or (@($g.Problems) -match 'SAMPLE_LOGIN')) { $pp += "the gate: with SAMPLE_DENY missing from a file that fills the others, its problems didn't name SAMPLE_DENY alone: '$($g.Problems -join '; ')'" }
    # No settings at all (a stranger's clone, a clean CI): it passes on the two patterns and denies no words.
    [Environment]::SetEnvironmentVariable("SAMPLE_ACCOUNT", $null)
    $g = Test-NothingPrivate $psRepo (Join-Path $psDir "none.env") $psExample
    $got = "$($g.Denied) denied: $($g.Problems -join '; ')"
    if ($got -ne "0 denied: tracked.md:2 path") { $pp += "the gate: with no private settings at all it reported '$got', not '0 denied: tracked.md:2 path' (the home folder, and no complaint about settings)" }
}
finally {
    foreach ($k in $psNames) { [Environment]::SetEnvironmentVariable($k, $psSaved[$k]) }
    Remove-Item -Recurse -Force $psDir -ErrorAction SilentlyContinue
}
if ($pp) { throw "check failed: private settings (docs\specs\private-settings.md):`n  - " + ($pp -join "`n  - ") }

# The public repository's snapshot (docs\specs\github-launch.md): tools\publish-github.ps1 on a throwaway repository,
# with a bare repository standing in for GitHub, so nothing goes over the network. The cases run in order on one
# repository whose main moves as a release would. Every case runs, then one failure lists each that didn't hold.
Write-Host "== github snapshot"
$gp = @()
$gh = Join-Path ([IO.Path]::GetTempPath()) "eqlp-github-$(Get-Random)"
$ghRepo = Join-Path $gh "repo"; $ghRemote = Join-Path $gh "remote.git"; $ghOther = Join-Path $gh "other.git"; $ghFake = Join-Path $gh "fake"
New-Item -ItemType Directory -Force $ghRepo, $ghFake | Out-Null
$ghScript = Join-Path $PSScriptRoot "publish-github.ps1"
$ghExample = Join-Path $gh "example.env"; $ghEnv = Join-Path $gh "private.env"; $ghConfig = Join-Path $gh "github.json"
$ghUtf8 = New-Object Text.UTF8Encoding $false
[IO.File]::WriteAllText($ghExample, "SAMPLE_ACCOUNT=`r`nSAMPLE_DENY=`r`n", $ghUtf8)
[IO.File]::WriteAllText($ghEnv, "SAMPLE_ACCOUNT=abc123`r`nSAMPLE_DENY=Sampleton`r`n", $ghUtf8)
# Stand-ins for gitleaks, passed by -Gitleaks so a PC that has the real one runs the sample the same.
[IO.File]::WriteAllText((Join-Path $ghFake "finds.cmd"), "@exit /b 1`r`n")    # one that always finds something
[IO.File]::WriteAllText((Join-Path $ghFake "passes.cmd"), "@exit /b 0`r`n")   # one that finds nothing
$ghLooked = [ordered]@{}   # unread files the owner has looked at: path to blob hash
$ghLeave = @()   # path prefixes the config leaves out of the public tree
function Set-GhConfig([string]$Email) {
    [IO.File]::WriteAllText($ghConfig, (@{ account = "sample-account"; repository = "sample-repo"; name = "Venun"; email = $Email; leave_out = $ghLeave; looked = $ghLooked } | ConvertTo-Json), $ghUtf8)
}
function Write-GhFile([string]$Path, [string]$Text) {
    $f = Join-Path $ghRepo $Path
    New-Item -ItemType Directory -Force (Split-Path $f) | Out-Null
    [IO.File]::WriteAllText($f, $Text, $ghUtf8)
}
function Save-Gh([string]$Message) { git -C $ghRepo add -A; git -C $ghRepo commit -q -m $Message }
function Set-GhVersion([string]$V) { Write-GhFile "src\EqlPathfinder.App\EqlPathfinder.App.csproj" "<Project><PropertyGroup><Version>$V</Version></PropertyGroup></Project>`n" }
function Get-GhRef([string]$Repo, [string]$Ref) { $r = git -C $Repo rev-parse -q --verify "$Ref^{commit}"; if ($LASTEXITCODE -or -not $r) { "" } else { "$r".Trim() } }
function Get-GhRefs([string]$Repo) { @(git -C $Repo for-each-ref --format="%(refname)") -join "," }
function Test-GhShared([string]$Commit) { git -C $ghRepo merge-base $Commit main | Out-Null; -not $LASTEXITCODE }
# The script in a process of its own, as the user runs it; through cmd, so its stderr isn't an error here.
# CLAUDECODE is cleared for it, as in the user's terminal, unless -Session says it's run from a session.
function Invoke-GhSnapshot([switch]$Push, [switch]$Session, [string]$Settings = $ghEnv, [string]$Remote = $ghRemote, [string]$Gitleaks = (Join-Path $ghFake "passes.cmd")) {
    $out = Join-Path $gh "out.txt"
    $a = "-Repo `"$ghRepo`" -PrivateSettings `"$Settings`" -ExamplePath `"$ghExample`" -Config `"$ghConfig`" -Remote `"$Remote`" -Gitleaks `"$Gitleaks`"" + $(if ($Push) { " -Push" } else { "" })
    $was = $env:CLAUDECODE
    $env:CLAUDECODE = $(if ($Session) { "1" } else { $null })
    try { cmd.exe /c "powershell -NoProfile -ExecutionPolicy Bypass -File `"$ghScript`" $a > `"$out`" 2>&1" } finally { $env:CLAUDECODE = $was }
    [pscustomobject]@{ Exit = $LASTEXITCODE; Out = "$(Get-Content $out -Raw)" }
}
try {
    git -C $ghRepo init -q -b main
    git -C $ghRepo config user.name "Jo Sampleton"; git -C $ghRepo config user.email "jo@sample.example"; git -C $ghRepo config commit.gpgsign false
    git init -q --bare $ghRemote
    Set-GhConfig "venun@sample.example"
    Set-GhVersion "1.2.3"
    Write-GhFile "README.md" "A sample app.`n"
    Write-GhFile "docs\releases\next\README.md" "How a note is written.`n"
    Save-Gh "First, as the local history has it"
    # What the local history keeps and the snapshot must never carry: a file with a denied word, later taken out.
    Write-GhFile "notes.md" "written by Jo Sampleton`n"
    Save-Gh "Notes"
    $ghOldBlob = "$(git -C $ghRepo rev-parse HEAD:notes.md)".Trim()

    $r = Invoke-GhSnapshot -Push -Settings (Join-Path $gh "none.env")
    if (-not $r.Exit -or $r.Out -notmatch 'SAMPLE_ACCOUNT' -or $r.Out -notmatch 'SAMPLE_DENY' -or (Get-GhRefs $ghRemote) -or (Get-GhRef $ghRepo "refs/public/main")) { $gp += "no settings: with no private settings file it exited $($r.Exit), and must refuse naming SAMPLE_ACCOUNT and SAMPLE_DENY and push nothing. It said: $($r.Out)" }
    $r = Invoke-GhSnapshot -Push
    if (-not $r.Exit -or $r.Out -notmatch 'notes\.md:1' -or (Get-GhRefs $ghRemote)) { $gp += "private word: with Sampleton denied and in main's notes.md it exited $($r.Exit), and must refuse naming notes.md:1 and push nothing. It said: $($r.Out)" }
    Remove-Item (Join-Path $ghRepo "notes.md"); Save-Gh "Notes out"

    Write-GhFile "docs\releases\next\waiting.md" "---`ntitle: Waiting`nreread: false`njob: false`nbump: patch`n---`nA change not yet released.`n"
    Save-Gh "A finished feature"
    $r = Invoke-GhSnapshot -Push
    if (-not $r.Exit -or $r.Out -notmatch 'waiting\.md' -or (Get-GhRefs $ghRemote)) { $gp += "notes waiting: with waiting.md in main's docs\releases\next it exited $($r.Exit), and must refuse naming waiting.md and push nothing. It said: $($r.Out)" }
    Remove-Item (Join-Path $ghRepo "docs\releases\next\waiting.md"); Save-Gh "Released"

    # A file with a NUL (an image, a font) can't be read by the guard: it waits until its path and hash are listed.
    Write-GhFile "docs\mock.png" "PNG$([char]0)picture of a log with a name`n"
    Save-Gh "A picture"
    $r = Invoke-GhSnapshot -Push
    if (-not $r.Exit -or $r.Out -notmatch 'docs.mock\.png' -or (Get-GhRefs $ghRemote)) { $gp += "unread file: with a file holding a NUL that isn't in the config's looked list it exited $($r.Exit), and must refuse naming docs/mock.png and push nothing. It said: $($r.Out)" }
    $ghLooked["docs/mock.png"] = "$(git -C $ghRepo rev-parse HEAD:docs/mock.png)".Trim()
    Set-GhConfig "venun@sample.example"

    $r = Invoke-GhSnapshot -Push -Session
    if (-not $r.Exit -or $r.Out -notmatch 'CLAUDECODE|session' -or (Get-GhRefs $ghRemote)) { $gp += "from a session: with CLAUDECODE set and -Push it exited $($r.Exit), and must refuse naming the session and push nothing. It said: $($r.Out)" }

    Set-GhConfig ""
    $r = Invoke-GhSnapshot -Push
    if (-not $r.Exit -or $r.Out -notmatch 'email' -or (Get-GhRefs $ghRemote)) { $gp += "no email: with the config's email empty it exited $($r.Exit), and must refuse naming the email and push nothing. It said: $($r.Out)" }
    Set-GhConfig "venun@sample.example"

    git -C $ghRepo config commit.gpgsign true
    $r = Invoke-GhSnapshot -Push
    if (-not $r.Exit -or $r.Out -notmatch 'gpgsign' -or (Get-GhRefs $ghRemote)) { $gp += "signing on: with commit.gpgsign true it exited $($r.Exit), and must refuse naming commit.gpgsign and push nothing. It said: $($r.Out)" }
    git -C $ghRepo config commit.gpgsign false

    $r = Invoke-GhSnapshot -Push -Gitleaks (Join-Path $ghFake "finds.cmd")
    $r2 = Invoke-GhSnapshot -Push -Gitleaks (Join-Path $ghFake "missing.cmd")
    if (-not $r.Exit -or $r.Out -notmatch 'gitleaks' -or -not $r2.Exit -or $r2.Out -notmatch 'gitleaks' -or (Get-GhRefs $ghRemote)) { $gp += "gitleaks finds: with a gitleaks that reports a finding it exited $($r.Exit), and without one with -Push $($r2.Exit); each must refuse naming gitleaks and push nothing. It said: $($r.Out) | $($r2.Out)" }
    $r = Invoke-GhSnapshot -Gitleaks (Join-Path $ghFake "missing.cmd")
    if ($r.Exit -or $r.Out -notmatch 'not run') { $gp += "gitleaks finds: a dry run without gitleaks exited $($r.Exit), and must go on and say it wasn't run. It said: $($r.Out)" }

    $r = Invoke-GhSnapshot
    if ($r.Exit -or $r.Out -notmatch '1\.2\.3' -or $r.Out -notmatch [regex]::Escape($ghRemote) -or $r.Out -notmatch '[0-9a-f]{40}' -or (Get-GhRefs $ghRemote) -or (Get-GhRef $ghRepo "refs/public/main") -or (Get-GhRef $ghRepo "refs/tags/v1.2.3")) { $gp += "dry run: without -Push it exited $($r.Exit), and must show 1.2.3, the remote and the commit, and change no ref here or there. It said: $($r.Out)" }

    # A path the config leaves out is absent from the public commit and nothing else changes; the dry run says so.
    Write-GhFile "docs\sub\deep\shot.png" "a mock-up, as text`n"; Save-Gh "A deep mock-up"
    $ghLeave = @("docs/**/*.png", "README.md"); Set-GhConfig "venun@sample.example"
    $r = Invoke-GhSnapshot
    $leftHash = [regex]::Match($r.Out, 'Commit:\s+([0-9a-f]{40})').Groups[1].Value
    $want = @(git -C $ghRepo ls-tree -r main | Where-Object { $_ -notmatch "`t(docs/.*\.png|README\.md)$" })
    $have = if ($leftHash) { @(git -C $ghRepo ls-tree -r $leftHash) } else { @() }
    if ($r.Exit -or $r.Out -notmatch 'Left out:.*docs/\*\*/\*\.png.*README\.md' -or -not $leftHash -or ($want -join "`n") -ne ($have -join "`n") -or $have.Count -lt 1) { $gp += "left out: with the pattern docs/**/*.png and README.md left out the dry run exited $($r.Exit), and must name it and make a commit holding main's files but those, identical. It said: $($r.Out)" }
    $ghLeave = @("docs/Nothing.md"); Set-GhConfig "venun@sample.example"
    $r = Invoke-GhSnapshot
    if (-not $r.Exit -or $r.Out -notmatch 'docs/Nothing\.md' -or $r.Out -notmatch 'matches no file') { $gp += "left out: a literal entry that matches no file exited $($r.Exit), and must refuse naming it. It said: $($r.Out)" }
    $ghLeave = @(); Set-GhConfig "venun@sample.example"

    # From a worktree's branch with a file main doesn't have: the snapshot is still main's.
    git -C $ghRepo checkout -q -b feature/other
    Write-GhFile "other.md" "only on the branch`n"; Save-Gh "On a branch"
    $dry = Invoke-GhSnapshot
    $dryHash = [regex]::Match($dry.Out, 'Commit:\s+([0-9a-f]{40})').Groups[1].Value
    $r = Invoke-GhSnapshot -Push
    git -C $ghRepo checkout -q main
    $pub = Get-GhRef $ghRemote "refs/heads/main"
    $first = @()
    if ($r.Exit) { $first += "it exited $($r.Exit)" }
    if (-not $pub) { $first += "the remote has no main" }
    else {
        if (-not $dryHash -or $pub -ne $dryHash) { $first += "the pushed commit '$pub' isn't the dry run's '$dryHash'" }
        if ("$(git -C $ghRemote rev-list --count $pub)".Trim() -ne "1") { $first += "the remote's main isn't one commit" }
        if ("$(git -C $ghRemote rev-parse "$pub^{tree}")".Trim() -ne "$(git -C $ghRepo rev-parse "main^{tree}")".Trim()) { $first += "its tree isn't main's" }
        $who = "$(git -C $ghRemote log -1 --format="%an <%ae>|%cn <%ce>|%ai|%ci|%s" $pub)".Trim()
        if ($who -notmatch '^Venun <venun@sample\.example>\|Venun <venun@sample\.example>\|[^|]* \+0000\|[^|]* \+0000\|EQL Pathfinder v1\.2\.3$') { $first += "it reads '$who', not Venun's, in UTC, 'EQL Pathfinder v1.2.3'" }
        if ((Get-GhRef $ghRemote "refs/tags/v1.2.3") -ne $pub) { $first += "the tag v1.2.3 isn't on it" }
        if ((Get-GhRefs $ghRemote) -ne "refs/heads/main,refs/tags/v1.2.3") { $first += "the remote's refs are '$(Get-GhRefs $ghRemote)'" }
        git -C $ghRemote cat-file -e $ghOldBlob
        if (-not $LASTEXITCODE) { $first += "the remote holds notes.md from main's history" }
        if ((Get-GhRef $ghRepo "refs/public/main") -ne $pub) { $first += "refs/public/main here isn't the pushed commit" }
        if (Test-GhShared $pub) { $first += "it shares history with main" }
    }
    if ($first) { $gp += "first push: " + ($first -join "; ") + ". It said: $($r.Out)" }

    $r = Invoke-GhSnapshot -Push
    if ($r.Exit -or $r.Out -notmatch 'already' -or (Get-GhRef $ghRemote "refs/heads/main") -ne $pub -or -not $pub) { $gp += "again: for 1.2.3 a second time it exited $($r.Exit), and must say 'already published' and push nothing. It said: $($r.Out)" }

    Set-GhVersion "1.2.4"; Save-Gh "v1.2.4"
    $r = Invoke-GhSnapshot -Push
    $pub2 = Get-GhRef $ghRemote "refs/heads/main"
    if ($r.Exit -or -not $pub -or -not $pub2 -or $pub2 -eq $pub -or "$(git -C $ghRemote rev-list --parents -n 1 $pub2)".Trim() -ne "$pub2 $pub" -or (Get-GhRef $ghRemote "refs/tags/v1.2.4") -ne $pub2 -or "$(git -C $ghRemote rev-parse "$pub2^{tree}")".Trim() -ne "$(git -C $ghRepo rev-parse "main^{tree}")".Trim() -or (Test-GhShared $pub2)) { $gp += "next release: for 1.2.4 it exited $($r.Exit), and must add one commit with main's tree whose only parent is v1.2.3's, tagged v1.2.4, sharing no history with main. It said: $($r.Out)" }

    Write-GhFile "README.md" "A sample app, reworded after the release.`n"; Save-Gh "Docs only"
    $r = Invoke-GhSnapshot -Push
    if (-not $r.Exit -or $r.Out -notmatch '1\.2\.4' -or -not $pub2 -or (Get-GhRef $ghRemote "refs/heads/main") -ne $pub2) { $gp += "same version: for 1.2.4 with another tree it exited $($r.Exit), and must refuse naming 1.2.4 and push nothing. It said: $($r.Out)" }

    Set-GhVersion "1.2.5"; Save-Gh "v1.2.5"
    git -C $ghRepo update-ref refs/public/main main~1
    $r = Invoke-GhSnapshot -Push
    if (-not $r.Exit -or -not $pub2 -or (Get-GhRef $ghRemote "refs/heads/main") -ne $pub2 -or (Get-GhRefs $ghRemote) -match 'v1\.2\.5' -or $r.Out -notmatch 'shares history') { $gp += "shares history: with refs/public/main set to a commit of main it exited $($r.Exit), and must refuse and push nothing. It said: $($r.Out)" }

    # A remote whose main the public ref doesn't hold (a repository made on GitHub with a README): never forced.
    if ($pub2) { git -C $ghRepo update-ref refs/public/main $pub2 }
    git init -q --bare $ghOther
    git -C $ghRepo push -q $ghOther "$((Get-GhRef $ghRepo 'main~1')):refs/heads/main"
    $theirs = Get-GhRef $ghOther "refs/heads/main"
    $r = Invoke-GhSnapshot -Push -Remote $ghOther
    if (-not $r.Exit -or -not $theirs -or (Get-GhRef $ghOther "refs/heads/main") -ne $theirs) { $gp += "other history: to a remote whose main the public ref doesn't hold it exited $($r.Exit), and must be refused and leave that main as it was. It said: $($r.Out)" }
}
finally {
    Remove-Item -Recurse -Force $gh -ErrorAction SilentlyContinue
}
if ($gp) { throw "check failed: github snapshot (docs\specs\github-launch.md):`n  - " + ($gp -join "`n  - ") }

# The role gate tools\feature.ps1 finish depends on (docs\roles.md): sample changes must ask for the right reviews,
# and a sample spec must stop a branch for each thing it lacks and for nothing else.
Write-Host "== role gate"
foreach ($c in @(
        @{ Changed = @("src/EqlPathfinder.App/web/js/home.js"); Roles = "designer architect"; Decision = $false },
        @{ Changed = @("src/EqlPathfinder.App/MeterWindow.cs"); Roles = "designer architect"; Decision = $false },
        @{ Changed = @("src/EqlPathfinder.Engine/Analysis/Ask.cs", "tests/EqlPathfinder.Engine.Tests/AskTests.cs"); Roles = "architect"; Decision = $false },
        @{ Changed = @("src/EqlPathfinder.Engine/Sharing/SharePayload.cs"); Roles = "architect"; Decision = $true },
        @{ Changed = @("src/EqlPathfinder.Engine/Combat/FightStore.cs"); Roles = "architect"; Decision = $true },
        @{ Changed = @("server/migrations/0007_sample.sql"); Roles = "architect"; Decision = $true },
        @{ Changed = @("server/cloudflare.config.ts"); Roles = "architect"; Decision = $true },
        @{ Changed = @("server/package.json"); Roles = "architect"; Decision = $false },
        @{ Changed = @("site/privacy.html"); Roles = "designer"; Decision = $true },
        @{ Changed = @("site/public/_headers"); Roles = "architect"; Decision = $false },
        @{ Changed = @("site/vite.config.ts"); Roles = "architect"; Decision = $false },
        @{ Changed = @("EQLPathfinder.slnx"); Roles = "architect"; Decision = $false },
        @{ Changed = @("tools/check.ps1"); Roles = "architect"; Decision = $false },
        @{ Changed = @("tools/release.ps1"); Roles = "architect"; Decision = $true },
        # ship.ps1 runs the release every hour with nobody watching: a change to it is decided before it's written.
        @{ Changed = @("tools/ship.ps1"); Roles = "architect"; Decision = $true },
        # What becomes public can't be taken back: the script that pushes it and the settings it reads are decided first.
        @{ Changed = @("tools/publish-github.ps1"); Roles = "architect"; Decision = $true },
        @{ Changed = @("tools/github.json"); Roles = "architect"; Decision = $true },
        @{ Changed = @(".claude/hooks/guard-main.ps1"); Roles = "architect"; Decision = $false },
        @{ Changed = @(".claude/settings.json"); Roles = "architect"; Decision = $false },
        # The rules as prose are the user's to read: their go-ahead on the diff is the review (docs\roles.md).
        @{ Changed = @("CLAUDE.md", ".claude/agents/qa.md", ".claude/skills/feature/SKILL.md"); Roles = ""; Decision = $false },
        @{ Changed = @("docs/pace.md", ".claude/skills/game-data/SKILL.md", "tests/EqlPathfinder.Engine.Tests/AskTests.cs", "server/test/sharing.test.ts", "server/README.md", "docs/releases/next/sample.md", "tools/analysis/ask-blend.cs"); Roles = ""; Decision = $false })) {
    $got = @(Get-RequiredRoles $c.Changed) -join " "
    $decision = Test-NeedsDecision $c.Changed
    if ($got -ne $c.Roles -or $decision -ne $c.Decision) {
        throw "check failed: the role gate read ($($c.Changed -join ', ')) as roles '$got', decision $decision"
    }
}
$sample = Join-Path ([IO.Path]::GetTempPath()) "eqlp-spec-$(Get-Random)"
New-Item -ItemType Directory -Force (Join-Path $sample "docs\specs") | Out-Null
try {
    $touched = @("src/EqlPathfinder.App/web/js/home.js", "src/EqlPathfinder.Engine/Sharing/SharePayload.cs", "docs/specs/sample.md")
    $engineOnly = @("src/EqlPathfinder.Engine/Pages/AskPage.cs", "tests/EqlPathfinder.Engine.Tests/AskTests.cs", "docs/specs/sample.md")
    $pageScript = @("src/EqlPathfinder.App/web/js/home.js", "tests/EqlPathfinder.Engine.Tests/HomePageTests.cs", "docs/specs/sample.md")
    $drawsNothing = "# Sample`n`n## Done when`n- [x] One thing - ``ATest```n`n## Reviews`n- architect: nothing to fix.`n- designer: not needed, a refactor that draws nothing new.`n"
    $whole = "# Sample`n`n- No test: the page only moves a label, and the designer checks the render.`n`n## Done when`n- [x] One thing - seen in ``main-home.png```n`n## Decisions`nNothing new is sent.`n`n## Reviews`n- architect: nothing to fix.`n- **Designer:** one label reworded.`n"
    foreach ($c in @(
            # Lacks everything: a box unticked, a test, the decisions, both reviews (a bare "architect:" isn't one).
            @{ Want = 4; Changed = $touched; Spec = "# Sample`n`n## Done when`n- [x] One thing - ``ATest```n- [ ] Another thing`n`n## Reviews`n- architect:`n" },
            # Lacks nothing.
            @{ Want = 0; Changed = $touched; Spec = $whole },
            # A follow-up on a spec main already has: the reviews there are the last change's, until new ones replace them.
            @{ Want = 1; Changed = $touched; Spec = $whole; Main = $whole },
            @{ Want = 0; Changed = $touched; Spec = $whole.Replace("nothing to fix.", "the follow-up: one fix.").Replace("one label reworded.", "the follow-up: fine."); Main = $whole },
            # What players read can change in the engine: a spec that says what the player sees needs the designer too.
            @{ Want = 1; Changed = $engineOnly; Spec = "# Sample`n`n## What the player sees`nA new line on Ask.`n`n## Done when`n- [x] One thing - ``ATest```n`n## Reviews`n- architect: nothing to fix.`n" },
            @{ Want = 0; Changed = $engineOnly; Spec = "# Sample`n`n## Done when`n- [x] One thing - ``ATest```n`n## Reviews`n- architect: nothing to fix.`n" },
            # A change that draws nothing new says so in place of the designer's review, with its reason. Not without
            # the reason, not for the architect, and not when the spec itself says what the player sees.
            @{ Want = 0; Changed = $pageScript; Spec = $drawsNothing },
            @{ Want = 1; Changed = $pageScript; Spec = $drawsNothing.Replace(", a refactor that draws nothing new.", "") },
            @{ Want = 1; Changed = $pageScript; Spec = $drawsNothing.Replace("nothing to fix.", "not needed, it only renames things.") },
            @{ Want = 1; Changed = $pageScript; Spec = $drawsNothing.Replace("## Done when", "## What the player sees`nA new line on Home.`n`n## Done when") },
            # A spec the branch didn't write isn't its spec.
            @{ Want = 1; Changed = @($touched | Select-Object -First 2); Spec = "# Sample`n" },
            # Nothing a role reviews, so no spec is needed.
            @{ Want = 0; Changed = @("docs/pace.md", "tests/EqlPathfinder.Engine.Tests/AskTests.cs"); Spec = "" })) {
        [IO.File]::WriteAllText((Join-Path $sample "docs\specs\sample.md"), $c.Spec, (New-Object Text.UTF8Encoding $false))
        $problems = @(Get-RoleProblems "sample" $c.Changed $sample "$($c.Main)")
        if ($problems.Count -ne $c.Want) { throw "check failed: the role gate found $($problems.Count) problems where $($c.Want) were due:`n$($problems -join "`n")" }
    }
}
finally { Remove-Item -Recurse -Force $sample -ErrorAction SilentlyContinue }

# start takes a draft's name and refuses a finished change's (docs\specs\README.md).
foreach ($c in @(
        @{ Draft = $true; Text = "# Sample`n`n**Draft, 11-18, not started** ([calc-audit](calc-audit.md)). **Tier:** trivial.`n`n**Why:** a reason.`n" },
        @{ Draft = $true; Text = "# Sample`r`n`r`n**Draft, 11-18, not started**`r`n" },
        @{ Draft = $false; Text = "# Sample`n`n**Built 11-19 from a draft of 11-18.**`n`n## Done when`n- [x] One thing - ``ATest```n" },
        @{ Draft = $false; Text = "# Sample`n`n**Why:** a draft would have said so first.`n`n**Draft** notes, further down.`n" },
        @{ Draft = $false; Text = "**Draft, 11-18** with no title above it`n" },
        @{ Draft = $false; Text = "" })) {
    if ((Test-DraftSpec $c.Text) -ne $c.Draft) { throw "check failed: the draft test read this sample as draft $(-not $c.Draft):`n$($c.Text)" }
}

# Which branches finish holds to the roles, on a repository made for the purpose: one that left main before
# docs\roles.md existed isn't held, even after merging main in; one that left after is, however it was made.
$repo = Join-Path ([IO.Path]::GetTempPath()) "eqlp-held-$(Get-Random)"
New-Item -ItemType Directory -Force (Join-Path $repo "docs") | Out-Null
try {
    $g = { git -C $repo -c user.name=check -c user.email=check@example.invalid -c commit.gpgsign=false @args | Out-Null }
    & $g init -q -b main
    & $g commit -q --allow-empty -m "before the roles"
    & $g branch early
    [IO.File]::WriteAllText((Join-Path $repo "docs\roles.md"), "roles`n")
    & $g add -A
    & $g commit -q -m "the roles"
    & $g branch late
    & $g branch untouched
    & $g checkout -q early
    & $g commit -q --allow-empty -m "early work"
    & $g merge -q --no-edit main
    & $g checkout -q late
    & $g commit -q --allow-empty -m "late work"
    $held = { param($b) [bool](Test-HeldToRoles $repo $b) }
    $got = "$(& $held early) $(& $held late) $(& $held untouched)"
    & $g config branch.late.roles false
    & $g config branch.early.roles true
    $got += " $(& $held early) $(& $held late)"
    if ($got -ne "False True True True False") { throw "check failed: early, late and untouched branches, then early marked true and late marked false, were held as: $got" }
}
finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }

# The hooks, run as Claude Code runs them: the command that .claude\settings.json gives, with the call as JSON on
# stdin. Running the scripts directly would pass with a mistyped name in the settings, or without the exit code passed on.
Write-Host "== hooks"
$settings = Join-Path $root ".claude\settings.json"
try { $hooks = ([IO.File]::ReadAllText($settings) | ConvertFrom-Json).hooks }
catch { throw "check failed: .claude\settings.json isn't JSON, so Claude Code would ignore it: $_" }
$guard = $hooks.PreToolUse[0].hooks[0].command
$context = $hooks.UserPromptSubmit[0].hooks[0].command
if (-not $guard -or -not $context) { throw "check failed: .claude\settings.json has no PreToolUse or no UserPromptSubmit hook command" }
$hookTmp = Join-Path ([IO.Path]::GetTempPath()) "eqlp-hooks-$(Get-Random)"
New-Item -ItemType Directory -Force $hookTmp | Out-Null
function Invoke-Hook([string]$Command, [string]$Json) {
    # A file and cmd's <, not a PowerShell pipe: Windows PowerShell 5.1 puts a byte-order mark in front of piped text.
    [IO.File]::WriteAllText("$hookTmp\in.json", $Json, (New-Object Text.UTF8Encoding $false))
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Command))
    cmd.exe /c "powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded < `"$hookTmp\in.json`" > `"$hookTmp\out.txt`" 2> nul"
    [pscustomobject]@{ Exit = $LASTEXITCODE; Out = "$(Get-Content "$hookTmp\out.txt" -Raw)".Trim() }
}
$projectDir = $env:CLAUDE_PROJECT_DIR
$env:CLAUDE_PROJECT_DIR = $root
try {
    $main = Get-MainCheckout
    $call = { param($tool, $path) @{ tool_name = $tool; cwd = $main; tool_input = @{ file_path = $path } } | ConvertTo-Json -Compress }
    foreach ($c in @(
            @{ Want = 2; Call = (& $call "Edit" "$main\src\x.cs") },
            @{ Want = 2; Call = (& $call "Write" ("/" + $main.Substring(0, 1).ToLower() + $main.Substring(2).Replace("\", "/") + "/CLAUDE.md")) },   # /c/Users/...
            @{ Want = 0; Call = (& $call "Write" "$main-wt\some-feature\src\x.cs") },
            @{ Want = 0; Call = "not json" })) {
        $r = Invoke-Hook $guard $c.Call
        if ($r.Exit -ne $c.Want) { throw "check failed: the main-checkout guard exited $($r.Exit), not $($c.Want), for $($c.Call)" }
    }

    $session = "check-$(Get-Random)"
    $reply = '{"isSidechain":false,"message":{"role":"assistant","content":[],"usage":{"input_tokens":3,"cache_creation_input_tokens":1000,"cache_read_input_tokens":READ,"output_tokens":12}},"type":"assistant"}'
    $ask = @{ session_id = $session; transcript_path = "$hookTmp\t.jsonl"; hook_event_name = "UserPromptSubmit" } | ConvertTo-Json -Compress
    # Quiet below 200k; once at each of 200k, 400k and 700k; quiet again when the context shrinks, and it starts over.
    foreach ($step in @(@{ Read = 50000; Says = $false }, @{ Read = 250000; Says = $true }, @{ Read = 260000; Says = $false }, @{ Read = 450000; Says = $true },
            @{ Read = 750000; Says = $true }, @{ Read = 60000; Says = $false }, @{ Read = 210000; Says = $true })) {
        [IO.File]::WriteAllText("$hookTmp\t.jsonl", $reply.Replace("READ", "$($step.Read)") + "`n", (New-Object Text.UTF8Encoding $false))
        $r = Invoke-Hook $context $ask
        if ($r.Exit -ne 0 -or [bool]$r.Out -ne $step.Says) { throw "check failed: the context hook with $($step.Read) tokens behind it: exit $($r.Exit), said '$($r.Out)'" }
        if ($r.Out) { $null = $r.Out | ConvertFrom-Json }   # what it says must be JSON, or Claude Code shows it raw
    }
    Remove-Item (Join-Path ([IO.Path]::GetTempPath()) "eqlp-context-$session.txt") -ErrorAction SilentlyContinue
}
finally {
    $env:CLAUDE_PROJECT_DIR = $projectDir
    Remove-Item -Recurse -Force $hookTmp -ErrorAction SilentlyContinue
}

# The session's own model, effort and compaction window (docs\roles.md, "The week of 11-17"). Claude Code ignores a
# setting it can't read and says nothing: sessions here would run on this PC's default model and effort, and be
# compacted only near the model's limit.
Write-Host "== session settings"
function Get-SessionSettingProblem([string]$Json) {
    try { $s = $Json | ConvertFrom-Json -ErrorAction Stop } catch { return "isn't JSON" }
    if ($s -isnot [pscustomobject]) { return "isn't a JSON object" }
    # By the exact name: PowerShell finds effortlevel or Model as well, and Claude Code doesn't.
    $keys = @($s.PSObject.Properties | ForEach-Object { $_.Name })
    foreach ($k in "model", "effortLevel", "autoCompactWindow") { if ($keys -cnotcontains $k) { return "has no '$k'" } }
    # The names the agents' front matter takes. Settings take a full model ID too, but the aliases follow new models.
    if ($s.model -isnot [string] -or $s.model -cnotmatch '^(fable|opus|sonnet|haiku)$') { return "has model '$($s.model)', which Claude Code wouldn't take" }
    # max lasts one session: settings take the other four.
    if ($s.effortLevel -isnot [string] -or $s.effortLevel -cnotmatch '^(low|medium|high|xhigh)$') { return "has effortLevel '$($s.effortLevel)', which Claude Code wouldn't take" }
    # A token count, a JSON number of 100000 or more. "250000" in quotes is a string.
    $w = $s.autoCompactWindow
    if (-not ($w -is [int] -or $w -is [long]) -or $w -lt 100000) { return "has autoCompactWindow '$w', which Claude Code wouldn't take: a whole number of 100000 or more" }
}
$good = '{"model":"opus","effortLevel":"medium","autoCompactWindow":250000,"hooks":{}}'
foreach ($c in @(
        @{ Bad = $false; Text = $good },
        @{ Bad = $true; Text = $good.Replace('"model":"opus",', '') },
        @{ Bad = $true; Text = $good.Replace('"autoCompactWindow":250000,', '') },
        @{ Bad = $true; Text = $good.Replace('effortLevel', 'effortlevel') },
        @{ Bad = $true; Text = $good.Replace('effortLevel', 'effort') },
        @{ Bad = $true; Text = $good.Replace('"model"', '"Model"') },
        @{ Bad = $true; Text = $good.Replace('"opus"', '"opus-5.5"') },
        @{ Bad = $true; Text = $good.Replace('"medium"', '"max"') },
        @{ Bad = $true; Text = $good.Replace('250000', '50000') },
        @{ Bad = $true; Text = $good.Replace('250000', '"250000"') },
        @{ Bad = $true; Text = $good.Replace('250000', '250000.5') },
        @{ Bad = $true; Text = $good.TrimEnd('}') })) {
    if ([bool](Get-SessionSettingProblem $c.Text) -ne $c.Bad) { throw "check failed: the session settings test misread a sample: $($c.Text)" }
}
$problem = Get-SessionSettingProblem ([IO.File]::ReadAllText($settings))
if ($problem) { throw "check failed: .claude\settings.json $problem." }

# Claude Code skips an agent or a skill whose front matter it can't read, or lists it without its description, and
# says nothing: a role would quietly stop existing, or never be picked.
Write-Host "== agents, skills"
function Get-FrontMatterProblem([string]$Text, [string]$Name) {
    $m = [regex]::Match($Text.Replace("`r`n", "`n"), '(?s)^---\n(.*?)\n---\n')
    if (-not $m.Success) { return "has no front matter" }
    if ($m.Groups[1].Value -notmatch "(?m)^name:\s*$([regex]::Escape($Name))\s*$") { return "needs 'name: $Name'" }
    $d = [regex]::Match($m.Groups[1].Value, '(?m)^description:[ \t]*(.*)$').Groups[1].Value.Trim()
    if (-not $d) { return "needs a description" }
    # YAML ends an unquoted value at ": " or " #". Claude Code then lists the skill by its first heading, and the
    # "Use for ..." that gets it picked is gone: app-feedback and production-data were listed that way until 11-17.
    if ($d -notmatch '^["'']' -and $d -match ':\s|\s#') { return "has ': ' or ' #' in its description: reword it, or put the whole value in quotes" }
    # A role's model, effort and turn limit, when it sets them. Claude Code drops a value it doesn't know and says
    # nothing: the role then runs on the session's model at its effort, and the saving the setting was for is gone.
    foreach ($f in @(@{ Key = "model"; Ok = '^(fable|opus|sonnet|haiku)$' }, @{ Key = "effort"; Ok = '^(low|medium|high|xhigh|max)$' },
            @{ Key = "maxTurns"; Ok = '^[1-9][0-9]*$' })) {
        $v = [regex]::Match($m.Groups[1].Value, "(?m)^$($f.Key):[ \t]*(.*?)[ \t]*$")
        if ($v.Success -and $v.Groups[1].Value -cnotmatch $f.Ok) { return "has $($f.Key) '$($v.Groups[1].Value)', which Claude Code wouldn't take" }
    }
    # The same for a key spelt another way (maxturns, max_turns, Model): it's ignored, and the limit or the model with it.
    foreach ($k in [regex]::Matches($m.Groups[1].Value, '(?im)^(model|effort|max_?turns)[ \t]*:')) {
        if (@("model", "effort", "maxTurns") -cnotcontains $k.Groups[1].Value) { return "has '$($k.Groups[1].Value):', which Claude Code would ignore: the keys are model, effort and maxTurns" }
    }
}
foreach ($c in @(
        @{ Bad = $false; Text = "---`nname: sample`ndescription: Reads what players send. Use for reports.`n---`nBody`n" },
        @{ Bad = $false; Text = "---`nname: sample`ndescription: `"Reads what players send: bugs and ideas.`"`n---`nBody`n" },
        @{ Bad = $true; Text = "---`nname: sample`ndescription: Reads what players send: bugs and ideas.`n---`nBody`n" },
        @{ Bad = $true; Text = "---`nname: other`ndescription: Reads what players send.`n---`nBody`n" },
        @{ Bad = $false; Text = "---`nname: sample`ndescription: Builds it.`nmodel: sonnet`neffort: medium`nmaxTurns: 60`n---`nBody`n" },
        @{ Bad = $true; Text = "---`nname: sample`ndescription: Builds it.`nmodel: sonnet-5.5`n---`nBody`n" },
        @{ Bad = $true; Text = "---`nname: sample`ndescription: Builds it.`neffort: lowest`n---`nBody`n" },
        @{ Bad = $true; Text = "---`nname: sample`ndescription: Builds it.`nmaxTurns: sixty`n---`nBody`n" },
        @{ Bad = $true; Text = "---`nname: sample`ndescription: Builds it.`nmaxturns: 60`n---`nBody`n" },
        @{ Bad = $true; Text = "---`nname: sample`ndescription: Builds it.`nModel: sonnet`n---`nBody`n" },
        @{ Bad = $true; Text = "# Sample`nNo front matter.`n" })) {
    if ([bool](Get-FrontMatterProblem $c.Text "sample") -ne $c.Bad) { throw "check failed: the front matter test misread a sample: $($c.Text)" }
}
foreach ($role in "qa", "designer", "engineer", "architect") {
    if (-not (Test-Path (Join-Path $root ".claude\agents\$role.md"))) { throw "check failed: .claude\agents\$role.md is gone, and docs\roles.md needs all four." }
}
$defs = @(Get-ChildItem (Join-Path $root ".claude\agents") -Filter *.md | ForEach-Object { @{ File = $_.FullName; Name = $_.BaseName } }) +
    @(Get-ChildItem (Join-Path $root ".claude\skills") -Directory | ForEach-Object { @{ File = (Join-Path $_.FullName "SKILL.md"); Name = $_.Name } })
foreach ($d in $defs) {
    $problem = if (Test-Path $d.File) { Get-FrontMatterProblem ([IO.File]::ReadAllText($d.File)) $d.Name } else { "is missing" }
    if ($problem) { throw "check failed: $($d.File) $problem." }
}

# docs\code-map.md against the code. First that the check still notices: a map that names something made up must fail.
$mapSample = Join-Path ([IO.Path]::GetTempPath()) "eqlp-map-$(Get-Random).md"
# The made-up name is put together here: written out, it would be in this file, which is part of what's searched.
[IO.File]::WriteAllText($mapSample, "A real name, ``PathfinderEngine``, and one that isn't: ``NoSuch$(Get-Random -Minimum 100000)Name``.`n")
# Through cmd, so what it writes to stderr isn't an error here (Windows PowerShell 5.1 under Stop).
cmd.exe /c "node `"$root\tools\mapcheck.mjs`" `"$mapSample`" > nul 2>&1"
$missed = $LASTEXITCODE -ne 1
Remove-Item $mapSample -ErrorAction SilentlyContinue
if ($missed) { throw "check failed: tools\mapcheck.mjs passed a map that names something that doesn't exist" }
Invoke-Step "code map" { node (Join-Path $root "tools\mapcheck.mjs") }

# docs\queue.md must parse, so the board built from it (tools\queue-board.mjs) never breaks. First that the check still
# notices: each sample is a whole queue (all eight lanes) with one fault, and must fail with that fault's own message.
$qbHead = "| # | Row | What | From | Tier | Waits | P | You |`n|---|---|---|---|---|---|---|---|`n"
$qbLanes = "In progress", "Now", "Next", "Later", "Waiting on the user", "Parked", "Inbox", "Dropped"
function New-QueueSample([string]$Now, [string]$Without = "", [string]$Extra = "") {
    $t = ""
    foreach ($l in $qbLanes) {
        if ($l -eq $Without) { continue }
        $t += "## $l`n`n" + $(if ($l -eq "Now") { $Now } else { "Empty.`n" }) + "`n"
    }
    $t + $Extra
}
$qbSamples = @(
    @{ Name = "lane";    Message = "is missing";     Text = (New-QueueSample "Empty.`n" -Without "Inbox") },
    @{ Name = "cells";   Message = "has 7 cells";    Text = (New-QueueSample ($qbHead + "| 1 | a | b | c | d | e | P1 |`n")) },
    @{ Name = "name";    Message = "has no name";    Text = (New-QueueSample ($qbHead + "| 1 |  | b | c | d | e | P1 | |`n")) },
    @{ Name = "bullets"; Message = "has no table";   Text = (New-QueueSample "- a row written as a bullet`n") },
    @{ Name = "heading"; Message = "is not a lane";  Text = (New-QueueSample "Empty.`n" -Extra ("## Blocked`n`n" + $qbHead + "| 1 | a | b | c | d | e | P1 | |`n")) }
)
foreach ($s in $qbSamples) {
    $f = Join-Path ([IO.Path]::GetTempPath()) "eqlp-queue-$($s.Name)-$(Get-Random).md"
    [IO.File]::WriteAllText($f, $s.Text)
    # Through cmd, so what it writes to stderr isn't an error here (Windows PowerShell 5.1 under Stop).
    cmd.exe /c "node `"$root\tools\queue-board.mjs`" --check `"$f`" > `"$f.out`" 2>&1"
    $qbCode = $LASTEXITCODE
    $qbOut = "$(Get-Content "$f.out" -Raw)"
    Remove-Item $f, "$f.out" -ErrorAction SilentlyContinue
    if ($qbCode -ne 1 -or -not $qbOut.Contains($s.Message)) { throw "check failed: tools\queue-board.mjs, given a queue with a $($s.Name) problem, exited $qbCode and said: $qbOut" }
}
Invoke-Step "queue board" { node (Join-Path $root "tools\queue-board.mjs") --check }

# tools\webcheck.mjs against copies of the page scripts with one slip in each: a gate that passes them isn't one.
# When a page's own answer arrives, core.js writes names that other files declare (docs\code-map.md). Each copy has one
# of those names changed in core.js only, which is what a rename that missed core.js leaves behind. Three more copies:
# one takes any answer for the page data, as the scripts did until 0.10.0; one has a new answer that writes a name
# nothing declares; and one has a script that doesn't parse.
Write-Host "== page scripts gate"
$webReal = Join-Path $root "src\EqlPathfinder.App\web"
$webSample = Join-Path ([IO.Path]::GetTempPath()) "eqlp-web-$(Get-Random)"
New-Item -ItemType Directory -Force (Join-Path $webSample "js") | Out-Null
try {
    Copy-Item (Join-Path $webReal "index.html") $webSample
    Copy-Item (Join-Path $webReal "js\*.js") (Join-Path $webSample "js")
    $core = Join-Path $webSample "js\core.js"
    $coreText = [IO.File]::ReadAllText($core)
    $webcheck = {
        param([string]$CoreJs)
        [IO.File]::WriteAllText($core, $CoreJs, (New-Object Text.UTF8Encoding $false))
        # Through cmd, so what it writes to stderr isn't an error here (Windows PowerShell 5.1 under Stop).
        cmd.exe /c "node `"$root\tools\webcheck.mjs`" `"$webSample`" > `"$webSample\out.txt`" 2>&1"
        [pscustomobject]@{ Exit = $LASTEXITCODE; Out = "$(Get-Content "$webSample\out.txt" -Raw)" }
    }
    # The copy as it is must pass, or the failures below would prove nothing.
    $r = & $webcheck $coreText
    if ($r.Exit -ne 0) { throw "check failed: tools\webcheck.mjs failed an unchanged copy of the page scripts:`n$($r.Out)" }

    $keepsData = "if (e.data?.kind) return;"
    if (-not $coreText.Contains($keepsData)) { throw "check failed: core.js no longer has '$keepsData', so this sample changes nothing. Point it at whatever keeps an answer from replacing the page data now." }
    $r = & $webcheck $coreText.Replace($keepsData, "")
    if ($r.Exit -ne 1 -or $r.Out -notmatch "replaced the page data") { throw "check failed: tools\webcheck.mjs passed page scripts that take any answer for the page data:`n$($r.Out)" }

    $passed = @()
    foreach ($c in @(
            @{ Kind = "loot"; Name = "lootRows" },
            @{ Kind = "ask"; Name = "askAnswer" },
            @{ Kind = "upgrade"; Name = "upg" },
            @{ Kind = "shareStatus"; Name = "shareStatus" },
            @{ Kind = "sharePreview"; Name = "sharePreview" },
            @{ Kind = "sharePreview"; Name = "shareBusy" },
            @{ Kind = "usagePreview"; Name = "usagePreview" },
            @{ Kind = "usagePreview"; Name = "usageScroll" })) {
        $renamed = "$($c.Name)Renamed"
        # The name where it's code: not the kind's own string ("sharePreview") and not a field (data.lootRows).
        $slip = [regex]::Replace($coreText, "(?<![`"'.\w`$])$($c.Name)\b(?![`"'])", $renamed)
        if ($slip -eq $coreText) { throw "check failed: core.js no longer uses the name $($c.Name), so this sample changes nothing. Point it at the file that takes a `"$($c.Kind)`" answer now." }
        $r = & $webcheck $slip
        # Exit 1 alone isn't enough: node exits 1 for any error. The output must name the answer and the missing name.
        if ($r.Exit -ne 1 -or $r.Out -notmatch "`"$($c.Kind)`"" -or $r.Out -notmatch "$renamed is not defined") { $passed += "$($c.Name) (`"$($c.Kind)`")" }
    }
    if ($passed) { throw "check failed: tools\webcheck.mjs didn't fail, or didn't say why, on page scripts where core.js uses a name nothing declares (the answer it arrives with in brackets): $($passed -join ', ')" }

    # An answer added after webcheck was written: it reads the kinds from the scripts, so this one is sent too.
    $newAnswer = 'if (e.data?.kind === "raceTable") { raceTableRows = e.data.rows; return; }'
    $r = & $webcheck $coreText.Replace($keepsData, "$newAnswer`n  $keepsData")
    if ($r.Exit -ne 1 -or $r.Out -notmatch '"raceTable"' -or $r.Out -notmatch "raceTableRows is not defined") { throw "check failed: tools\webcheck.mjs passed page scripts where a new answer (`"raceTable`") reaches a name nothing declares:`n$($r.Out)" }

    # A script that doesn't parse never runs in the app: for boot.js that's no first show, so no page. Last, because
    # the copy's boot.js stays broken.
    [IO.File]::AppendAllText((Join-Path $webSample "js\boot.js"), "`nfunction (`n")
    $r = & $webcheck $coreText
    if ($r.Exit -ne 1 -or $r.Out -notmatch "boot\.js doesn't parse") { throw "check failed: tools\webcheck.mjs passed page scripts where boot.js doesn't parse:`n$($r.Out)" }
}
finally { Remove-Item -Recurse -Force $webSample -ErrorAction SilentlyContinue }

# No tracked file holds an account ID, a login, the owner's names, a home folder or a home-network address
# (docs\specs\private-settings.md). The words come from this PC's private settings, which are read here and then taken
# out of the environment again. A gate before main fails closed: when git can't list the files, this throws.
Write-Host "== nothing private"
$np = Test-NothingPrivate $root
if ($np.Problems) { throw "check failed: private text in tracked files, or settings half filled (file:line and the kind; the text itself is never printed):`n  " + ($np.Problems -join "`n  ") }
if ($np.Denied) { Write-Host "   no home folder, home-network address or any of this PC's $($np.Denied) private words in a tracked file" }
else { Write-Host "   no home folder or home-network address in a tracked file; no words were denied (this PC has no private settings)" }
if ($SamplesOnly) { Write-Host "check: the samples and the code map are green (nothing was built or tested)"; exit 0 }

# While the game runs MSBuild uses 2 threads, not every core.
Invoke-Step "build (Release)" { dotnet build (Join-Path $root "EQLPathfinder.slnx") -c Release --nologo -v q --disable-build-servers (Get-BuildThreads) }
Invoke-Step "C# format" { dotnet format (Join-Path $root "EQLPathfinder.slnx") --no-restore --verify-no-changes --verbosity quiet }
Invoke-Step "engine tests" { dotnet test (Join-Path $root "tests\EqlPathfinder.Engine.Tests") -c Release --no-build --nologo -v q }
# A retry of a hung page test is logged to artifacts\page-retries.log, which goes with a clean copy (finish) and isn't in the -v q output.
$retryLog = Join-Path $root "artifacts\page-retries.log"
if (Test-Path $retryLog) { Get-Content $retryLog | Where-Object { $_ -match 'PAGE-RETRY' } | ForEach-Object { Write-Host $_ } }
Invoke-Step "page scripts" { node (Join-Path $root "tools\webcheck.mjs") }
if ($All -or ($changed -match '^(server/|site/|tools/[^/]+\.mjs$|src/EqlPathfinder\.App/web/js/|package\.json$|package-lock\.json$|biome\.json$)')) {
    Push-Location $root
    try {
        if (-not (Test-Path node_modules)) { Invoke-Step "root: npm ci" { npm ci --no-audit --no-fund --loglevel=error } }
        Invoke-Step "JS/TS lint & format (Biome)" { npm run lint --silent }
    }
    finally { Pop-Location }
}
# What a failed release leaves in the main checkout and says, on a throwaway repository (about 3 minutes, so only
# when the scripts that release change).
if ($All -or ($changed -match '^tools/((ship|release|publish|pipeline-common|shipcheck)\.ps1|feed\.json|private\.example\.env)$')) {
    Invoke-Step "ship on a throwaway repository" { powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "shipcheck.ps1") }
}

foreach ($area in @(
        @{ Dir = "server"; When = '^server/'; Steps = @("typecheck", "test") },
        @{ Dir = "site"; When = '^(site/|docs/releases/)'; Steps = @("build") })) {
    if (-not $All -and -not ($changed -match $area.When)) { continue }
    Push-Location (Join-Path $root $area.Dir)
    try {
        if (-not (Test-Path node_modules)) { Invoke-Step "$($area.Dir): npm ci" { npm ci --no-audit --no-fund --loglevel=error } }
        foreach ($s in $area.Steps) { Invoke-Step "$($area.Dir): npm run $s" { npm run $s --silent } }
    }
    finally { Pop-Location }
}
Write-Host "check: all green"