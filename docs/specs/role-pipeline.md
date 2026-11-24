# Roles for every change

**Why:** "Lately, the work committed to this repo hasn't been superb. We've been creating bugs. We've been making
lackluster UI/UX decisions. I feel like I'm wasting tokens on a SDLC for this" (the user, 11-17). A change went from
a request straight to code, the session that wrote it was the only one that looked, and conversations grew past
900k tokens with every step re-reading all of it.
**Players:** nobody directly. This changes how changes are made, so that fewer broken or confusing releases reach
them.

## Done when
- [x] A new session finds four agents: qa, designer, engineer, architect - a headless session started in this worktree listed all four
- [x] The qa definition turns a real request into a spec and failing tests - a trial on docs/debt.md's page-names line gave a spec and four failing tests in 21 steps; its six notes on the definitions are in them now
- [x] `finish` refuses a branch for each missing step, and for nothing else - the "role gate" samples in `tools/check.ps1`; this branch was itself refused while it had no spec
- [x] A branch that left main before the roles finishes as it always did, even after merging main in, and one that left after is held however it was made - the sample on a throwaway repository in `tools/check.ps1`
- [x] A review counts only for the change it's of, and a name is used once - the follow-up samples in `tools/check.ps1`; `start` refuses a name whose spec is on main
- [x] An edit inside the main checkout is refused, with where to make it instead, and edits anywhere else pass - the "hooks" samples, run through the commands in `.claude/settings.json`; a live headless Write into the main checkout was refused, and nothing was written
- [x] A conversation is told once as it passes 200k, 400k and 700k tokens, and a prompt is never blocked - the "hooks" samples; the hook read 742k and 642k from two real transcripts, as a full parse did
- [x] docs/code-map.md can't name what's gone - `tools/mapcheck.mjs` failed on 18 names before main was merged in, passes now, and fails a sample that names something made up
- [x] An agent or a skill that Claude Code would skip or half-read, or a settings file it would ignore, stops the gate - the "agents, skills" and "hooks" steps
- [x] Breaking any of these parts fails the gate - thirteen deliberate breaks, one at a time, each stopped `tools/check.ps1`
- [x] What sessions spend can be measured again - `tools/analysis/session-cost.mjs` gives the 11-17 numbers

## Edge cases
- **Branches in flight when this lands:** they left main before docs/roles.md existed, so they aren't held.
- **A session started in a worktree, not the main checkout:** the guard finds the main checkout through the
  worktree's `.git` file.
- **A hook that breaks, or input it can't read:** the call goes through. A broken guard must never stop work. The
  gate before main is the other way round: what it can't read, it refuses.
- **The clean copy `finish` checks in:** the samples use only temp folders, and find the main checkout from there.
- **Windows PowerShell 5.1:** a pipe would put a byte-order mark in front of a hook's input, so the samples use a
  file; captures use `[regex]::Match`; `-Command` turns an exit code of 2 into 1, so the hook commands end with
  `exit $LASTEXITCODE`.
- **Sessions already running:** settings load live. A lower compaction window would compact them mid-work, so it's
  left for the user to switch on (docs/roles.md).
- **Nothing leaves the PC:** the session-cost script reads local transcripts and prints locally.

## Not doing
- **A page-test harness.** It's the biggest hole in testing (docs/debt.md, first line), and a change of its own.
- **A cheaper model per role.** Roles follow the session's model. docs/roles.md says how to change one.
- **Path-scoped rules and nested CLAUDE.md files.** They don't load for files in a worktree outside the folder a
  session started in, which is where all work here happens.
- **Workflows and agent teams.** For changes this size they spend more tokens, not fewer.
- **A test that runs `start`, `ready` and `finish` themselves.** `tools/check.ps1` samples the rules they call
  (docs/debt.md). This branch's own finish is the first run of the new `finish`.

## Where
- `tools/pipeline-common.ps1`: `Get-RequiredRoles`, `Test-NeedsDecision`, `Test-HeldToRoles`, `Read-Spec`,
  `Get-RoleProblems`
- `tools/feature.ps1`: `ready`, the gate in `finish`, the commit it merges. `tools/check.ps1`: the samples
- `tools/mapcheck.mjs`, `tools/analysis/session-cost.mjs`
- `.claude/agents/`, `.claude/hooks/`, `.claude/settings.json`, `.claude/skills/feature`, `.claude/skills/session-cost`
- `docs/roles.md`, `docs/design.md`, `docs/quality.md`, `docs/engineering.md`, `docs/debt.md`, `docs/code-map.md`,
  `docs/specs/README.md`, `CLAUDE.md`

## Reviews
- architect: four must-fix and seven should-fix, each reproduced, all fixed. Settings files and the rules themselves needed no review. A review line from an earlier merge counted for a follow-up. The mark that held a branch could silently not be written (a branch is now held by where it left main). Two skills were listed without their descriptions while the gate was green. The hook samples didn't run the settings' own commands. `/c/` paths passed the guard. A wording change in the engine didn't need the designer. `finish` could merge a commit it hadn't checked. Two debt lines added, one of them paid here.
