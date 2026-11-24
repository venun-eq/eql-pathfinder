<#
.SYNOPSIS
  One feature, one worktree: start it, list them, finish it into main through the gate. See CLAUDE.md.

.EXAMPLE
  .\tools\feature.ps1 start plan-v2       # EQLPathfinder-wt\plan-v2 beside the main checkout, on branch feature/plan-v2, from main
  .\tools\feature.ps1 list
  .\tools\feature.ps1 ready plan-v2       # which reviews the branch needs and what finish would refuse; changes nothing
  .\tools\feature.ps1 finish plan-v2      # merge main in, check in a clean copy, fast-forward main, remove the worktree

.DESCRIPTION
  finish stops, changing nothing on main, when:
  - the worktree has uncommitted changes, or the branch has no commits of its own;
  - the branch changes src\ but has no release note (docs\releases\next\<name>.md, see its README);
  - a role's step is missing (docs\roles.md): no spec, a "Done when" box unticked, code without a test, a change to
    what's stored or sent without the architect's decisions, or no review recorded from a role the changed paths need.
    Only for a branch that left main after the roles came in (Test-HeldToRoles in pipeline-common.ps1);
  - merging main into the branch conflicts (merge main yourself, resolve, commit, finish again);
  - tools\check.ps1 fails in a clean copy of the merged branch;
  - the main checkout has uncommitted changes (nobody should edit there).
  One finish or ship runs at a time (a lock in the repository's .git folder).
#>
param(
    [Parameter(Mandatory, Position = 0)][ValidateSet("start", "list", "ready", "finish")][string]$Command,
    [Parameter(Position = 1)][string]$Name,
    # finish: keep the worktree and branch afterwards (to carry on with a follow-up).
    [switch]$Keep
)
$ErrorActionPreference = "Stop"
$env:PATH = "C:\Program Files\Git\cmd;$env:PATH"
. (Join-Path $PSScriptRoot "pipeline-common.ps1")

$main = Get-MainCheckout
$wtRoot = "$main-wt"

function Get-FeatureWorktree([string]$Branch) {
    $lines = git -C $main worktree list --porcelain
    $path = $null
    foreach ($l in $lines) {
        if ($l -like "worktree *") { $path = $l.Substring(9) }
        elseif ($l -eq "branch refs/heads/$Branch") { return $path }
    }
    return $null
}

# The feature named on the command line, or the one whose worktree this is. start only makes lowercase names.
function Resolve-Feature([string]$Given) {
    if ($Given) { return $Given.ToLowerInvariant() }
    $cur = git branch --show-current
    if ($cur -notlike "feature/*") { throw "Name the feature: .\tools\feature.ps1 $Command <name>" }
    return $cur -replace '^feature/', ''
}

# The feature's spec as main has it: empty unless an earlier change with this name was finished, or it's a draft.
function Get-MainSpec([string]$Feature) { @(git -C $main show "main:docs/specs/$Feature.md") -join "`n" }

switch ($Command) {
    "start" {
        if ($Name -cnotmatch '^[a-z0-9][a-z0-9-]{1,40}$') { throw "Name it in lowercase words with dashes, like plan-v2." }
        if (git -C $main branch --list "feature/$Name") { throw "feature/$Name already exists: .\tools\feature.ps1 list" }
        # A finished change's spec stays on main as its record, reviews included, so a name is used once. A draft's spec
        # is on main before anyone builds it, and its name is the one to build it under.
        $null = git -C $main cat-file -e "main:docs/specs/$Name.md"
        $onMain = $LASTEXITCODE -eq 0
        $draft = $onMain -and (Test-DraftSpec (Get-MainSpec $Name))
        if ($onMain -and -not $draft) { throw "A finished change was called $Name (docs\specs\$Name.md is on main). Pick another name, so this one's spec and reviews are its own." }
        $path = Join-Path $wtRoot $Name
        git -C $main worktree add -q $path -b "feature/$Name" main
        if ($LASTEXITCODE) { throw "git worktree add failed." }
        Write-Host "Started feature/$Name in $path"
        if ($draft) { Write-Host "From the draft docs\specs\$Name.md: keep what it says, add the rest, and replace its ""Draft"" line with what was built (docs\specs\README.md)." }
        Write-Host "Size it first (the feature skill): code needs docs\specs\$Name.md and a failing test before the fix, from you, the engineer or qa by its tier."
        Write-Host "An app change also needs docs\releases\next\$Name.md. Then: .\tools\feature.ps1 finish $Name"
    }
    "list" {
        foreach ($b in (git -C $main for-each-ref --format="%(refname:short)" refs/heads/feature/)) {
            $n = $b -replace '^feature/', ''
            $ahead = git -C $main rev-list --count "main..$b"
            $behind = git -C $main rev-list --count "$b..main"
            $null = git -C $main cat-file -e "${b}:docs/releases/next/$n.md"
            $note = if ($LASTEXITCODE -eq 0) { "note" } else { "no note" }
            $null = git -C $main cat-file -e "${b}:docs/specs/$n.md"
            $spec = if ($LASTEXITCODE -eq 0) { "spec" } else { "no spec" }
            "{0,-28} {1,3} ahead {2,3} behind  {3,-8} {4,-8} {5}" -f $b, $ahead, $behind, $note, $spec, (Get-FeatureWorktree $b)
        }
        exit 0   # not the exit code of the last git question asked above
    }
    "ready" {
        $Name = Resolve-Feature $Name
        $branch = "feature/$Name"
        $wt = Get-FeatureWorktree $branch
        if (-not $wt) { throw "No worktree has $branch checked out." }
        $changed = @(git -C $main diff --name-only "main...$branch")
        $roles = @(Get-RequiredRoles $changed)
        $problems = @(Get-RoleProblems $Name $changed $wt (Get-MainSpec $Name))
        if (($changed -match '^src/') -and -not ($changed -contains "docs/releases/next/$Name.md")) {
            $problems += "No release note: an app change needs docs\releases\next\$Name.md (docs\releases\next\README.md)."
        }
        if (git -C $wt status --porcelain) { $problems += "Uncommitted changes in $wt (only commits are counted here)." }
        "$branch changed $($changed.Count) files. By their paths, the reviews it needs: $(if ($roles) { $roles -join ', ' } else { 'none' })."
        if (-not (Test-HeldToRoles $main $branch)) { "It left main before the roles came in, so finish won't hold it to them." }
        if ($problems) { "Before finish:"; $problems | ForEach-Object { "- $_" } }
        else { "Nothing is missing. finish still merges main in and runs tools\check.ps1." }
        exit 0   # a report, whatever git answered above
    }
    "finish" {
        $Name = Resolve-Feature $Name
        $branch = "feature/$Name"
        $wt = Get-FeatureWorktree $branch
        if (-not $wt) { throw "No worktree has $branch checked out." }
        if (git -C $wt status --porcelain) { throw "$wt has uncommitted changes: commit them first." }
        if ([int](git -C $main rev-list --count "main..$branch") -eq 0) { throw "$branch has no commits of its own." }
        $changed = @(git -C $main diff --name-only "main...$branch")
        if (($changed -match '^src/') -and -not ($changed -contains "docs/releases/next/$Name.md")) {
            throw "$branch changes the app but has no release note: add docs\releases\next\$Name.md (see docs\releases\next\README.md)."
        }
        if (Test-HeldToRoles $main $branch) {
            $problems = @(Get-RoleProblems $Name $changed $wt (Get-MainSpec $Name))
            if ($problems) { throw "$branch isn't ready (docs\roles.md):`n- $($problems -join "`n- ")" }
        }
        if (git -C $main status --porcelain) { throw "The main checkout ($main) has uncommitted changes; nothing can merge until they're moved off it." }

        $lock = Enter-PipelineLock "finish $Name"
        $tmp = Join-Path ([IO.Path]::GetTempPath()) "eqlp-check-$Name-$(Get-Random)"
        try {
            git -C $wt merge --no-edit -q main
            if ($LASTEXITCODE) {
                git -C $wt merge --abort
                throw "Merging main into $branch conflicts. In $wt run 'git merge main', resolve, commit, and finish again."
            }
            # main gets exactly the commit that was checked: one made in the worktree while the check runs waits for
            # the next finish.
            $checked = git -C $main rev-parse $branch
            Write-Host "Checking $branch in a clean copy ($tmp)..."
            git -C $main worktree add -q --detach $tmp $checked
            try {
                & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tmp "tools\check.ps1") -Base main
                if ($LASTEXITCODE) { throw "The check failed: main is unchanged. Fix it in $wt, commit, and finish again." }
            }
            finally { git -C $main worktree remove --force $tmp }

            git -C $main merge --ff-only -q $checked
            if ($LASTEXITCODE) { throw "main moved while checking; finish again." }
            Write-Host "main is now $(git -C $main log --oneline -1)"
            if ((git -C $main rev-parse $branch) -ne $checked) {
                Write-Host "$branch has commits made during the check. They aren't on main: the worktree and branch stay, and finish again takes them."
            }
            elseif (-not $Keep) {
                git -C $main worktree remove $wt
                git -C $main branch -q -d $branch
                Write-Host "Removed $wt and $branch."
            }
            Write-Host "tools\ship.ps1 releases it (the scheduled task 'EQL Pathfinder ship' runs it every hour)."
        }
        finally { Exit-PipelineLock $lock }
    }
}
