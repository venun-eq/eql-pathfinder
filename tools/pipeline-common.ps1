# Shared by tools\feature.ps1, tools\check.ps1 and tools\ship.ps1 (dot-sourced). See CLAUDE.md for the contract.

# git.exe, without Windows PowerShell 5.1 turning its stderr (line-ending warnings, progress) into terminating errors
# under ErrorActionPreference Stop. Callers check $LASTEXITCODE.
function git {
    $eap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { & git.exe @args 2>$null } finally { $ErrorActionPreference = $eap }
}

# The repository's shared .git folder (the same from the main checkout and every worktree).
function Get-CommonDir {
    $d = git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir
    if ($LASTEXITCODE -or -not $d) { throw "Not inside the EQL Pathfinder repository." }
    [IO.Path]::GetFullPath($d)
}

# The main checkout: the release lane.
function Get-MainCheckout { [IO.Path]::GetFullPath((Split-Path (Get-CommonDir) -Parent)) }

# One finish or ship at a time. The lock is an open file that deletes itself when its process ends, so a crash never
# leaves it behind. Returns the handle for Exit-PipelineLock, or $null when -NoWait and it's taken.
function Enter-PipelineLock([string]$What, [int]$WaitMinutes = 30, [switch]$NoWait) {
    $path = Join-Path (Get-CommonDir) "eqlp-pipeline.lock"
    $until = (Get-Date).AddMinutes($WaitMinutes)
    while ($true) {
        try {
            $fs = [IO.FileStream]::new($path, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None, 4096,
                [IO.FileOptions]::DeleteOnClose)
            $bytes = [Text.Encoding]::UTF8.GetBytes("$What, pid $PID, $(Get-Date -Format o)")
            $fs.Write($bytes, 0, $bytes.Length); $fs.Flush()
            return $fs
        }
        catch [IO.IOException] {
            if ($NoWait) { return $null }
            if ((Get-Date) -gt $until) { throw "Another finish or ship is still running (lock: $path)." }
            Write-Host "Waiting for another finish or ship to end..."
            Start-Sleep -Seconds 20
        }
    }
}
function Exit-PipelineLock($Lock) { if ($Lock) { $Lock.Dispose() } }

# docs\releases\next\*.md: one release note per finished feature, waiting for the next release (see its README.md).
function Get-PendingNotes([string]$Root) {
    $dir = Join-Path $Root "docs\releases\next"
    if (-not (Test-Path $dir)) { return @() }
    foreach ($f in Get-ChildItem $dir -Filter *.md | Where-Object Name -ne "README.md" | Sort-Object LastWriteTime, Name) {
        $text = [IO.File]::ReadAllText($f.FullName).Replace("`r`n", "`n")
        $m = [regex]::Match($text, '(?s)^---\n(.*?)\n---\n(.*)$')
        if (-not $m.Success) { throw "$($f.Name): needs front matter (see docs\releases\next\README.md)." }
        # [regex] objects, not -match: -match would overwrite $Matches line by line and the body with it.
        $meta = @{}
        foreach ($line in $m.Groups[1].Value -split "`n") {
            $kv = [regex]::Match($line, '^\s*([\w-]+)\s*:\s*(.*?)\s*$')
            if ($kv.Success) { $meta[$kv.Groups[1].Value] = $kv.Groups[2].Value }
        }
        $body = $m.Groups[2].Value.Trim()
        if (-not $meta.title) { throw "$($f.Name): needs a title." }
        if (-not $body) { throw "$($f.Name): has no text for players." }
        [pscustomobject]@{
            File = $f.FullName; Name = $f.BaseName; Title = $meta.title; Body = $body
            Reread = $meta.reread -eq "true"; Job = $meta.job -eq "true"; Minor = $meta.bump -eq "minor"
        }
    }
}

# A notification on the NAS's ntfy (topics homelab-updates and homelab-alerts). The script goes to the scheduler
# container over the key-based ssh login on stdin, and uses the container's own write token (/scripts/secrets.env, as
# its jobs do): no token on this PC. The login is the private setting EQLP_NAS_LOGIN, read from the environment: with
# none it returns false at once, with no ssh and no default. Returns whether ntfy took it; never throws.
function Send-Ntfy([string]$Topic, [string]$Title, [string]$Message, [string]$Tags = "") {
    $login = "$env:EQLP_NAS_LOGIN".Trim()
    if (-not $login) { Write-Warning "ntfy: not sent, EQLP_NAS_LOGIN isn't set (tools\private.ps1 loads it)"; return $false }
    $docker = "/share/CACHEDEV1_DATA/.qpkg/container-station/bin/docker"
    $safeTitle = ($Title -replace "[^\x20-\x7E]", "-") -replace "'", ""
    $safeTags = $Tags -replace "[^a-z0-9_,-]", ""
    $script = @(
        ". /scripts/secrets.env"
        "curl -sf -o /dev/null -H `"Authorization: Bearer `$NTFY_TOKEN`" -H 'Title: $safeTitle' -H 'Tags: $safeTags' --data-binary @- http://ntfy:8080/$Topic <<'EQLP_NTFY_END'"
        ($Message -replace "`r", "")
        "EQLP_NTFY_END"
    ) -join "`n"
    # A file and cmd's < rather than a PowerShell pipe: Windows PowerShell 5.1 puts a byte-order mark in front of piped text.
    $tmp = [IO.Path]::GetTempFileName()
    try {
        [IO.File]::WriteAllText($tmp, $script + "`n", (New-Object Text.UTF8Encoding $false))
        cmd.exe /c "ssh.exe -o BatchMode=yes -o ConnectTimeout=10 $login $docker exec -i scheduler sh < `"$tmp`" > nul 2>&1"
        if ($LASTEXITCODE) { Write-Warning "ntfy: not delivered (exit $LASTEXITCODE)"; return $false }
        return $true
    }
    catch { Write-Warning "ntfy: $_"; return $false }
    finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
}

# ---- Private settings (docs\specs\private-settings.md): account IDs, the NAS login and the owner's names live in one
# file outside the repository, %APPDATA%\EQLPathfinder-dev\private.env. Every tool reads its values from the
# environment; Import-PrivateSettings is the only reader of the file. The "private settings" samples in
# tools\check.ps1 say what each of these must do.

function Get-PrivateSettingsPath { Join-Path $env:APPDATA "EQLPathfinder-dev\private.env" }

# The names in a NAME=value file, in its order, each with its value: everything after the first =, trimmed. No quotes,
# no export, no $ expansion, no comment after a value. Nothing for a file that isn't there.
function Read-PrivateFile([string]$Path) {
    $values = [ordered]@{}
    if (-not $Path -or -not (Test-Path -LiteralPath $Path)) { return , $values }
    # ReadAllText drops the byte-order mark Notepad writes; the Trim is for one it didn't recognise.
    foreach ($line in ([IO.File]::ReadAllText($Path) -replace "`r", "") -split "`n") {
        # [regex], not -match: -match in a loop overwrites $Matches (docs\engineering.md).
        $kv = [regex]::Match($line.Trim([char]0xFEFF, [char]32, [char]9), '^([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$')
        if ($kv.Success) { $values[$kv.Groups[1].Value] = $kv.Groups[2].Value.Trim() }
    }
    return , $values
}

# Puts every NAME=value line of $Path into this process's environment, so the programs it starts see them. The file
# wins over a variable already set; the environment fills what the file leaves out or blank (a CI service has no file,
# and its secret variables are the same names). Returns an object whose Missing is each name in $ExamplePath
# (tools\private.example.env: the names, no values) that has a value neither in the file nor already in the
# environment, in the example's order. Names is every name of the example, Path the file, Found whether it's there.
function Import-PrivateSettings([string]$Path = (Get-PrivateSettingsPath), [string]$ExamplePath = (Join-Path $PSScriptRoot "private.example.env")) {
    $file = Read-PrivateFile $Path
    foreach ($name in @($file.Keys)) {
        if ($file[$name]) { [Environment]::SetEnvironmentVariable($name, $file[$name]) }
    }
    $names = @((Read-PrivateFile $ExamplePath).Keys)
    $missing = @($names | Where-Object { -not "$([Environment]::GetEnvironmentVariable($_))".Trim() })
    [pscustomobject]@{ Missing = $missing; Names = $names; Path = $Path; Found = [bool]($Path -and (Test-Path -LiteralPath $Path)) }
}

# What ship.ps1 and tools\private.ps1 say when settings are missing: the names and the file, never a value.
function Format-PrivateMissing($Settings) {
    $where = if ($Settings.Found) { "They go in $($Settings.Path)" } else { "There is no $($Settings.Path): copy tools\private.example.env there and fill it in" }
    "Missing private setting(s): $($Settings.Missing -join ', '). $where (docs\specs\private-settings.md)."
}

# What $Text holds that must stay out of a public repository, one object for each line it's on: Line (from 1) and
# Kind, which is "path" (a home folder), "address" (a home-network address) or "private" (one of $Deny, as a whole
# word, in any case). A home folder is Users and a name, however the path is written: with one backslash or two, with
# forward slashes, as Git Bash writes a drive, or with dashes as Claude Code names a project's folder. Public, the
# tests' Bob and someone, <name> and a path cut short with dots aren't one.
function Find-PrivateText([string]$Text, [string[]]$Deny = @()) {
    $patterns = [ordered]@{
        path    = '(?i)(?:[a-z]:[\\/]+|/[a-z]/|[a-z]--)users(?:[\\/]+|-)(?!(?:public|bob|someone|default)(?![a-z0-9]))[a-z0-9_]'
        address = '(?<![0-9.])192\.168\.[0-9]{1,3}\.[0-9]{1,3}(?![0-9])'
    }
    # A denied word matches whole: a four-letter first name sits inside ordinary words. A blank entry matches nothing.
    $words = @($Deny | ForEach-Object { "$_".Trim() } | Where-Object { $_ } | ForEach-Object { [regex]::Escape($_) })
    if ($words) { $patterns["private"] = '(?i)(?<![a-z0-9])(?:' + ($words -join '|') + ')(?![a-z0-9])' }
    $found = @{}
    foreach ($kind in $patterns.Keys) {
        # Matches come in order, so the line feeds are counted once through the text for each kind.
        $line = 1; $at = 0
        foreach ($m in [regex]::Matches($Text, $patterns[$kind])) {
            for ($i = $Text.IndexOf("`n", $at); $i -ge 0 -and $i -lt $m.Index; $i = $Text.IndexOf("`n", $i + 1)) { $line++; $at = $i + 1 }
            if (-not $found[$line]) { $found[$line] = $kind }
        }
    }
    foreach ($line in ($found.Keys | Sort-Object)) { [pscustomobject]@{ Line = $line; Kind = $found[$line] } }
}

# Find-PrivateText over every file git tracks under $Root, its name and its text: "file:line kind" for each hit in
# the text, "file:name kind" for one in the name, and "file: not read" for a file it couldn't open. It never prints
# the text it found, so its output can go in a log; a name that's private is printed, as the only way to say which.
function Find-PrivateFiles([string]$Root, [string[]]$Deny = @()) {
    # Git writes a name with a letter outside ASCII quoted and escaped unless core.quotepath is off, and writes it in
    # UTF-8, which PowerShell reads only when the console's encoding says so.
    $was = [Console]::OutputEncoding
    try {
        try { [Console]::OutputEncoding = New-Object Text.UTF8Encoding $false } catch { }   # no console: such a name is then reported below
        $tracked = @(git -C $Root -c core.quotepath=off ls-files)
        $failed = $LASTEXITCODE
    }
    finally { try { [Console]::OutputEncoding = $was } catch { } }
    if ($failed) { throw "Couldn't list what git tracks in $Root." }
    # The names are published with the files, so they're searched too: line n of the list is file n.
    $named = @{}
    foreach ($hit in @(Find-PrivateText ($tracked -join "`n") $Deny)) { $named[$hit.Line - 1] = $hit.Kind }
    for ($i = 0; $i -lt $tracked.Count; $i++) {
        $f = $tracked[$i]
        if ($named.ContainsKey($i)) { "${f}:name $($named[$i])" }
        # A tracked file that can't be opened is a problem, never a pass: nobody looked inside it.
        $text = $null
        try { $text = [IO.File]::ReadAllText((Join-Path $Root $f)) } catch { }
        if ($null -eq $text) { "${f}: not read"; continue }
        if ($text.IndexOf([char]0) -ge 0) { continue }   # an image, a font: not text
        foreach ($hit in @(Find-PrivateText $text $Deny)) { "${f}:$($hit.Line) $($hit.Kind)" }
    }
}

# The gate's question (tools\check.ps1, "nothing private"): does a tracked file under $Root hold a home folder, a
# home-network address, a word of EQLP_DENY or the value of any other private setting? It reads the settings to know
# what to deny and then puts the environment back as it was, so the build and the tests after it run as a
# stranger's clone would, with no variable set. Problems is one line each; Denied is how many words it looked for.
# With no settings at all it passes on the two patterns; with some but not all it fails naming the missing ones: a
# half-filled file is a half-armed guard.
function Test-NothingPrivate([string]$Root, [string]$Path = (Get-PrivateSettingsPath), [string]$ExamplePath = (Join-Path $PSScriptRoot "private.example.env")) {
    $names = @(@((Read-PrivateFile $ExamplePath).Keys) + @((Read-PrivateFile $Path).Keys) | Select-Object -Unique)
    $saved = @{}
    foreach ($n in $names) { $saved[$n] = [Environment]::GetEnvironmentVariable($n) }
    try {
        $settings = Import-PrivateSettings $Path $ExamplePath
        $deny = @()
        foreach ($n in $settings.Names) {
            $v = "$([Environment]::GetEnvironmentVariable($n))".Trim()
            if ($n -like '*_DENY') { $deny += @($v -split ',') } else { $deny += $v }
        }
        $deny = @($deny | ForEach-Object { "$_".Trim() } | Where-Object { $_ } | Select-Object -Unique)
    }
    finally { foreach ($n in $names) { [Environment]::SetEnvironmentVariable($n, $saved[$n]) } }
    $problems = @()
    if ($settings.Missing -and $settings.Missing.Count -lt $settings.Names.Count) {
        $problems += "Some private settings are filled and these aren't, so the guard would look for only part of what's private: $($settings.Missing -join ', ') ($Path)."
    }
    $problems += @(Find-PrivateFiles $Root $deny)
    [pscustomobject]@{ Problems = $problems; Denied = $deny.Count }
}

# ---- Roles (docs\roles.md): which agents review a change, and whether its spec says it's ready. tools\check.ps1 tests
# these on samples, so a change here that reads a change or a spec differently stops at the gate.

# The roles that review a branch, from the paths it changed. Tests, docs, skills and release notes alone need nobody,
# and neither do the rules as prose (CLAUDE.md, the agents, the feature skill): the user reads that diff, and their
# go-ahead is its review.
function Get-RequiredRoles([string[]]$Changed) {
    $paths = [ordered]@{
        # What a player sees: the app's pages, its windows and overlays, the website's pages.
        designer  = @('^src/EqlPathfinder\.App/web/', '^src/EqlPathfinder\.App/[^/]+\.xaml$',
            '^src/EqlPathfinder\.App/(Meter|Overlay|Welcome|Tray)[^/]*\.cs$', '^site/[^/]+\.(html|css)$', '^site/partials/')
        # Anything that runs, or decides how things run: the app and its solution, the server and the site's build and
        # settings (not the server's tests), the infrastructure, the pipeline's scripts, and the hooks with the settings
        # that call them.
        architect = @('^src/', '^[^/]+\.(slnx|props|targets)$', '^server/(?!test/|README\.md$)', '^infra/',
            '^site/(build/|public/_headers$|[^/]+\.(ts|json)$)', '^tools/[^/]+\.(ps1|mjs|json)$',
            '^\.claude/(hooks/|settings\.json$)')
    }
    foreach ($role in $paths.Keys) {
        foreach ($p in $paths[$role]) {
            if ($Changed -match $p) { $role; break }
        }
    }
}

# Whether a change touches what's stored, what's sent, what the server keeps or how updates arrive. Those are hard to
# take back once players have them, so the architect decides before code and the spec's "## Decisions" records it.
# The scripts that release are part of how updates arrive: release.ps1, publish.ps1, and ship.ps1, which runs both
# every hour with nobody watching. The gate's own scripts aren't on the list: every change to them gets the
# architect's review, with docs\engineering.md's three questions for the pipeline.
function Test-NeedsDecision([string[]]$Changed) {
    foreach ($p in @('^server/(src|migrations|nas)/', '^server/cloudflare\.config\.ts$', '^infra/',
            '^src/EqlPathfinder\.Engine/(Sharing|Community|Storage|Usage)/', '^src/EqlPathfinder\.Engine/Combat/FightStore\.cs$',
            '^src/EqlPathfinder\.App/(Sharing|Usage|Community|Feedback|Updates)\.cs$', '^site/privacy\.html$',
            '^tools/(feed\.json|signing\.json|release\.ps1|publish\.ps1|ship\.ps1|publish-github\.ps1|github\.json)$')) {
        if ($Changed -match $p) { return $true }
    }
    return $false
}

# Whether finish holds a branch to the roles: one that left main after the roles came in, which is to say the commit it
# left from has docs\roles.md. A branch already in flight then finishes as it always did, even after merging main in,
# and a branch made by hand is held like any other. Nothing has to be written for this, so nothing can fail to be.
# The user can set it either way by hand: git config branch.<branch>.roles true (or false).
function Test-HeldToRoles([string]$Repo, [string]$Branch, [string]$Base = "main") {
    $mark = git -C $Repo config --get "branch.$Branch.roles"
    if ($mark -eq "true") { return $true }
    if ($mark -eq "false") { return $false }
    # The branch's own commits, oldest first: the first one's parent is where it left the base. With none, its tip is.
    $own = @(git -C $Repo rev-list --reverse --first-parent "$Base..$Branch")
    $from = if ($own.Count) { "$($own[0])^" } else { $Branch }
    $null = git -C $Repo cat-file -e "${from}:docs/roles.md"
    return $LASTEXITCODE -eq 0
}

# Whether a spec is a draft: written before anyone built it, from an audit or a review (docs\specs\README.md). Its first
# line after the title starts "**Draft". start takes a draft's name; a finished change's spec keeps its name as its record.
function Test-DraftSpec([string]$Text) {
    $lines = @(($Text -replace "`r", "") -split "`n" | Where-Object { $_.Trim() })
    $lines.Count -ge 2 -and $lines[0] -match '^#\s' -and $lines[1] -match '^\*\*Draft\b'
}

# A spec (docs\specs\<name>.md; the format is in docs\specs\README.md) as the gate reads it: the boxes ticked and not,
# the reviews recorded, whether decisions are written down, whether it describes something players see, and a stated
# reason for adding no test.
function Read-Spec([string]$Text) {
    $open = @(); $done = 0; $reviews = @{}; $decisions = $false; $sees = $false; $noTest = $null; $section = ""
    foreach ($line in ($Text -replace "`r", "") -split "`n") {
        $h = [regex]::Match($line, '^##\s+(.+?)\s*$')
        if ($h.Success) { $section = $h.Groups[1].Value.ToLowerInvariant(); continue }
        $box = [regex]::Match($line, '^\s*[-*]\s+\[([ xX])\]\s+(.+?)\s*$')
        if ($box.Success) {
            if ($box.Groups[1].Value -eq " ") { $open += $box.Groups[2].Value } else { $done++ }
            continue
        }
        # "No test: <why>", on a line of its own or as a list item. A reason shorter than ten characters isn't one.
        $why = [regex]::Match($line, '(?i)^\s*(?:[-*]\s+)?\**no test\**\s*:\**\s*(\S.{9,})$')
        if ($why.Success) { $noTest = $why.Groups[1].Value.Trim(); continue }
        if ($section -eq "reviews") {
            # "- architect: what it found and what was done". A role with nothing after the colon hasn't reviewed.
            $r = [regex]::Match($line, '(?i)^\s*[-*]\s+\**([a-z]+)\**\s*:\**\s*(\S.*?)\s*$')
            if ($r.Success) { $reviews[$r.Groups[1].Value.ToLowerInvariant()] = $r.Groups[2].Value }
        }
        elseif ($section -eq "decisions" -and $line.Trim()) { $decisions = $true }
        elseif ($section -eq "what the player sees" -and $line.Trim()) { $sees = $true }
    }
    [pscustomobject]@{ Open = $open; Done = $done; Reviews = $reviews; Decisions = $decisions; Sees = $sees; NoTest = $noTest }
}

# What stands between a branch and main, one line each, all at once so one pass can fix them. Nothing when the branch is
# ready, or when it changed nothing a role reviews. $Root is the branch's worktree, and $MainSpec is the spec as main
# has it (empty for a new one).
function Get-RoleProblems([string]$Name, [string[]]$Changed, [string]$Root, [string]$MainSpec = "") {
    $roles = @(Get-RequiredRoles $Changed)
    if (-not $roles) { return }
    $file = Join-Path $Root "docs\specs\$Name.md"
    if (-not ($Changed -contains "docs/specs/$Name.md") -or -not (Test-Path $file)) {
        "No spec: docs\specs\$Name.md says what done means, and is written with the failing tests before any code (docs\specs\README.md)."
        return
    }
    $spec = Read-Spec ([IO.File]::ReadAllText($file))
    # Words and numbers players read can change in the engine, where no path says so: a spec that says what the player
    # sees gets the designer's review whatever the paths.
    if ($spec.Sees -and $roles -notcontains "designer") { $roles += "designer" }
    if ($spec.Done + $spec.Open.Count -eq 0) { 'The spec has no "Done when" boxes, so nothing says what done means.' }
    foreach ($o in $spec.Open) { "Not done: $o" }
    if (($Changed -match '^(src|server/src)/') -and -not ($Changed -match '^(tests/|server/test/|tools/webcheck\.mjs$)') -and -not $spec.NoTest) {
        'Code changed and no test did. Add the test that would have caught it, or give the reason in the spec ("No test: ...").'
    }
    if ((Test-NeedsDecision $Changed) -and -not $spec.Decisions) {
        'It changes what is stored, sent or kept on the server, and the spec has no "## Decisions": the architect decides before code.'
    }
    # A line main already has is the last change's review, not this one's (a follow-up after finish -Keep).
    $before = (Read-Spec $MainSpec).Reviews
    $missing = @($roles | Where-Object { -not $spec.Reviews[$_] -or $spec.Reviews[$_] -eq $before[$_] })
    if ($missing) {
        "No review of this change recorded from: $($missing -join ', '). Run each on the finished branch, act on what it finds, then add its line under ""## Reviews"" in the spec. A line main already has doesn't count."
    }
    # "- designer: not needed, <why>" stands in for the designer's review of a change that draws nothing new, which the
    # paths alone can't tell (a page script moved about, a window's code tidied). The reason is part of it. It can't
    # stand in when the spec itself says what the player sees, and never for the architect: what runs is read by
    # someone who didn't write it.
    foreach ($role in $roles) {
        $line = "$($spec.Reviews[$role])"
        if ($line -notmatch '^not needed\b') { continue }
        if ($role -ne "designer") { "The $role's review can't be set aside as ""not needed"": it reads every change to what runs." }
        elseif ($spec.Sees) { 'The spec says what the player sees, so the designer reviews it: "not needed" is for a change that draws nothing new.' }
        elseif ($line -notmatch '^not needed\W+\S.{9,}') { 'Say why the designer isn''t needed: "- designer: not needed, <what shows nothing drawn changed>".' }
    }
}
