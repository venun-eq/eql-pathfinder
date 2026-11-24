# A failed release leaves main as it was

**Why:** Players see no change. When a release fails, the main checkout is meant to come back as it was, but on
11-19 (18:25) and 11-20 (01:25) the app's `.csproj` stayed at the new `<Version>`, and every later ship refused with
"uncommitted changes" until someone put it back by hand: players waited hours for fixes that were ready. The ship
log also named the red tests without saying why they failed.
**Players:** everyone waiting for a release. Worst when a failure comes at night and nobody is there to clear it.

## What goes wrong (for the architect)
- `release.ps1:64-66` writes the new `<Version>`; on a failure `ship.ps1:109` puts it back with
  `git -C $root checkout -- $proj`. That `git` is `pipeline-common.ps1:5-9`'s function, which runs git.exe with
  `2>$null`, and the catch never reads `$LASTEXITCODE`. A restore that fails says nothing, and the alert still reads
  "Nothing was published.": that's why no git line appears in any of the four logs.
- `git checkout -- <path>` needs `.git\index.lock`. Any git command running in the main checkout at that instant
  holds it: a `git status` from a Claude Code session (they all start in the main checkout), an editor,
  `feature.ps1 list`/`ready`, or `finish`'s own status check (`feature.ps1:121` runs before it takes the pipeline
  lock). git then exits 128 with "Unable to create ...index.lock: File exists", and nobody sees it. The `lock` case
  reproduces 11-19 18:25 and 11-20 01:25 exactly: same log, same alert, `.csproj` left at the new version. "Works
  some of the time" is whether another git command was running in that second. Which one it was on those dates
  can't be known now: its message was dropped.
- Ruled out on a throwaway repository: git's stderr not reaching the log under `cmd.exe ... > log 2>&1` (it does,
  without the wrapper), the scheduled run versus a console (both restore when nothing else holds the index), and
  another program holding the file open with delete sharing (the restore still takes).
- `release.ps1:69` runs `dotnet test ... -v q`, which prints each red test's name and not its message.

## Done when
- [x] A release that fails at its tests, its packing or its upload leaves the main checkout exactly as it was:
  HEAD, the index and every file that isn't ignored, by content, `docs/releases/<version>.md` included. Run as the
  hourly task runs it and from a console - `shipcheck.ps1` cases `tests-fail-scheduled`, `tests-fail-console`,
  `pack-fails`, `upload-fails` (they pass today; each failed with ship.ps1:109-110 taken out)
- [x] The same holds when another git command has the index for two seconds as the step fails - `lock` (fails:
  the `.csproj` is left at 0.1.1)
- [x] When a file can't be put back, the FAILED alert names each file still changed and a git command that clears
  it, and running those commands leaves main as it was - `held` (fails: the alert names no file)
- [x] A later run that finds the main checkout changed waits with an alert that names each file and a git command
  that clears it - `dirty` (fails: it names the file, with no command)
- [x] The ship log says why each red test failed, its message and not only its name - `why` (fails: the log names
  `TheSampleFails` without its reason)

## Edge cases
- **The pipeline itself, PowerShell 5.1:** the cases run ship.ps1 with powershell.exe 5.1, `-Scheduled`, through
  its own `cmd.exe ... > log 2>&1`. A native command's stderr under `$ErrorActionPreference = "Stop"` throws there.
- **A second copy:** another git command in the main checkout (above). The restore can't count on the index being
  free, and whatever fails says so in the log.
- **Nothing yet:** a failure before the version is written (the signing login) leaves nothing to put back, and the
  alert says nothing was left.
- **Starting and stopping:** the run killed mid-release (a reboot, the task's time limit) runs no catch. The next
  run's "shipping waits" alert is what names the files and the command (`dirty`).
- **A pack that passes and then fails its signature check** (`release.ps1:91`) leaves the new version's packages in
  `releases\` and in its `releases.win.json`. They're ignored by git, so the check above doesn't see them, but the
  next run packs the same version number again, and the next upload sends what's listed. Should be: `releases\` as
  it was. No test: the throwaway has no signing; the architect decides how it's put back and proven.
- **Released, then the commit failed** (`ship.ps1:117`): the version is out, so nothing is put back. Its alert
  stays as it is.
- **Something leaving the PC:** the alert goes to ntfy on the NAS. It carries file paths and git commands, never a
  test's output; the messages stay in the log on this PC.
- **Re-sends and limits:** alerts of one kind go out once in 20 hours (`Send-Alert`). A second failure that day
  still writes its files and commands to the log.

## Not doing
- Putting a changed main checkout back by itself on a later run. The alert says how; a person decides.
- Finding the page tests' clash with another run (the other half of docs/debt.md's 18:25 row: the page-tests branch).

## Where
- `tools/ship.ps1`: the catch at 106-113; the "shipping waits" alert at 83-87.
- `tools/release.ps1`: the version at 63-66, the tests at 68-71, the files it writes after (79, 82-86, 96-100).
- `tools/pipeline-common.ps1`: the `git` wrapper at 5-9.
- `tools/shipcheck.ps1`: the cases, on a throwaway repository with stand-ins for the logins, build, upload and
  ntfy. `tools/check.ps1` runs it when a release script changes (about 3 minutes).
- `docs/debt.md`: the engineer removes the 18:25 row once this is in (the page-tests branch cuts it to this half).

## Decisions
- **Put files back without git.** Before it calls release.ps1, ship.ps1 keeps the bytes of each file the release
  writes in the checkout (the `.csproj`, and `docs/releases/<version>.md` or that it was absent). Its catch writes
  them back with .NET file calls, and deletes the note only when it was absent. No index lock is needed, so a
  `git status` running elsewhere can't block it. The bytes are what was there, and `git checkout` would rewrite them
  through autocrlf. The file list stays explicit: the catch never deletes or resets a file the release didn't write.
- **Waiting on a busy file:** each write-back is retried every second for up to 30 s while it throws an IOException
  (a scanner, an editor, a git command). Past 30 s it's a stuck process or a person, and the alert is the right
  answer. The `held` case waits those 30 s. Each failed attempt's message goes to the log once, with the file.
- **Nothing is hidden: check, then say.** After the write-back, the catch checks two things: each kept file is back
  by content, and `git status --porcelain=v1 -z --untracked-files=all` is empty with exit code 0. If either fails,
  the alert names each file still changed with one command for it: `git -C "<root>" checkout HEAD -- "<path>"` for a
  tracked file, `git -C "<root>" clean -f -- "<path>"` for an untracked one, and says these discard the change. If
  git couldn't run, the alert says main couldn't be checked and to run `git status` there. When everything is back,
  the alert says "The main checkout is as it was." in place of guessing. The restore and the check sit in their
  own try, so nothing they throw can stop the alert or the rethrow. The list is written to the log before
  `Send-Alert`, so a second failure inside the 20 hours still records it.
- **One formatter for "file -> command",** used by the FAILED alert and by the "shipping waits" alert (83-87). That
  check also reads git's exit code. A `git status` that fails waits with an alert ("couldn't check the main
  checkout"), and doesn't ship as if clean, because today's empty output reads as clean and fails open.
- **The `git` wrapper stays as it is for the whole pipeline.** Its callers capture its output (`status --porcelain`,
  `rev-parse`, `branch`), and merging stderr would put line-ending warnings into that data. feature.ps1, check.ps1
  and the hourly ship all load it. The restore no longer needs git to write. Its one git call, the check, reads
  `$LASTEXITCODE`, and for the log it runs `git.exe` with `2>&1` under `Continue`, turning the error records into
  text. The other unchecked calls are a debt.md row.
- **`releases\` is in: a failure from `vpk pack` through the signature check puts it back.** Otherwise the next run
  packs the same version over a `releases.win.json` that already lists it, so ship stays blocked or the next upload
  sends an unsigned installer. It's done in release.ps1, around the pack, the signature check and the kept note,
  because only it knows the upload hasn't started. Just before `vpk pack`, it keeps the names of every file in
  `releases\` and copies each file that isn't a `*.nupkg` to `artifacts\releases-before-<version>\`. vpk rewrites
  those in place (json, RELEASES, Setup.exe, Portable.zip), about 320 MB, seconds, and only on a release. On a throw
  in that stretch, it deletes files that weren't there, copies the kept ones back, and rethrows. On success, it
  deletes the copy before `publish.ps1`. A failure inside publish.ps1 leaves `releases\` as it is, because part of
  the upload may have gone out (debt.md).
- **How `releases\` is proven:** a new shipcheck case, `sign-fails`, written failing before the fix. The throwaway
  gets a stand-in `tools\signing.json` and a seeded `releases\` (a 0.1.0 json, nupkg and Setup.exe). The vpk
  stand-in writes a 0.1.1 nupkg and rewrites the json and Setup.exe. A `Get-AuthenticodeSignature` stand-in function
  returns `NotSigned` (a function wins over the cmdlet in 5.1). The case compares `releases\` by names and hashes,
  and the checkout as the other cases do. `pack-fails` should leave a partial file in `releases\` too, and gets the
  same check.
- **Why tests failed:** `dotnet test ... -v q --logger "console;verbosity=minimal"`, which prints each red test's
  message and stack and no passed names. If `why` shows the console logger ignores it under `-v q`, use a trx
  logger to `artifacts\` and write each failed test's name and message to the log. The messages stay in the log on
  this PC. The alert keeps the exception's one line.
- **Taking it back / who else runs it:** scripts on this PC, undone by a revert. pipeline-common.ps1 doesn't change,
  so the hourly load and worktrees in flight see nothing new until main has it. Nothing new leaves the PC beyond
  file paths and git commands in the ntfy alert, which the alert already carries. No question for the user.

## Progress
Part 1 done (Decisions 1-5 and 8): restore without git with retries, the check and the alert, the shared formatter, the fail-closed "shipping waits" check, the tests' messages in the log.
Part 2 done (Decisions 6-7): release.ps1 keeps `releases\` names and a copy of each non-nupkg file in `artifacts\releases-before-<version>\` just before `vpk pack`; a throw from the pack through the kept note deletes files that weren't there, copies the kept ones back and rethrows; success deletes the copy before publish.ps1. New case `sign-fails` was red first (and `pack-fails`, with its partial nupkg, too); `upload-fails` now also proves `releases\` is left alone. All 9 shipcheck cases pass.
Unverified: what `vpk pack` does with a version already listed in `releases\` couldn't be checked without the real vpk (the stand-in just overwrites). The put-back makes it moot for failed runs; it also assumes vpk only adds `*.nupkg` and never deletes one (a delta or full package it removed would not come back).
## Reviews
- architect: (11-20) one must fix, one should fix. Proofs sound: each Done-when case would fail if its part broke
  (`lock` holds a real `index.lock`, `held` and `dirty` run the alert's own commands, `why` runs the real `dotnet test`
  line against xunit 2.9.3 under VSTest as the real project does); a83d410 only strengthened the cases; the throwaway
  has its own `.git`, so its pipeline lock, logs and alerts never meet the real ship's; debt.md is whole (52 rows,
  -1 +4, no conflict markers, the 18:25 row gone).
  - **Must fix:** `release.ps1`'s catch only `Write-Host`s when `releases\` can't be put back, and prints "put back as
    it was" without checking (a kept `*.nupkg` that vpk removed, or a copy that threw, is silent). The alert then says
    "The main checkout is as it was." and the next run packs over a feed that lists the version. After the copy-back,
    list kept names that are missing or whose copy didn't land, and put them with `$before`'s path and how to copy it
    back into the rethrown message, so ship's alert carries it. Proof: a `sign` variant whose busy process holds
    `releases\EQLPathfinder-win-Setup.exe` open, and the FAILED alert names it.
  - **Should fix:** `release.ps1`, the `Remove-Item $before` after the catch is outside the try and unguarded: if it
    throws (a scanner, an open handle), a good pack fails before publish with `releases\` left packed. Log and go on
    (`try { ... } catch { Write-Host ... }`).
- engineer: (11-20) must fix done: after the copy-back release.ps1 checks every kept name (missing, or hash differs from
  its copy) and any leftover file, and a miss goes into the rethrown message with `releases-before-<version>\` and the
  copy command, so ship's FAILED alert carries it; "put back as it was" prints only when that check passed. Proof: new
  case `sign-held` (the busy process holds `EQLPathfinder-win-Setup.exe` open as the signature fails), written red first.
  Should fix done: the final `Remove-Item $before` is wrapped, logs and carries on.
- lead: (11-20, read the fix) `releases\` goes back file by file and is then checked by name and hash. A miss is thrown
  into the FAILED alert with where the copy is, and "put back as it was (checked)" prints only after that check. The
  leftover copy's removal is wrapped. No second review: the must fix added a check and a message, not a new way of
  restoring.
