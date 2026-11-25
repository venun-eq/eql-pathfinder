# GitHub's CI passes on the public snapshot

Tier: small (one cause, one script).

## What was wrong

The public repository's CI (`.github/workflows/ci.yml`) runs `tools\check.ps1 -SamplesOnly` on the snapshot that
`tools\publish-github.ps1` pushes. `tools\github.json` leaves `docs/queue.md` out of that snapshot, and the "queue
board" step read it, so the Windows job failed for v0.10.34 (run 45471367956) with `ENOENT ... docs\queue.md`.

## Done when

- [x] On GitHub's runner (`GITHUB_ACTIONS` set) with no `docs\queue.md`: the five broken-queue samples still run, the
  live queue check is skipped with a line that says so, and the job goes on to build and test - the published-tree run below
- [x] Everywhere else, a missing `docs\queue.md` still fails the check, as before - the same copy without `GITHUB_ACTIONS`
- [x] Nothing else the Windows job runs reads a left-out path (`docs/debt.md`, the specs' images) - searched in
  `tools/`, `tests/`, `server/test/`, `site/src/`, and the job's every step green on the published tree

## How it was proved

A detached copy of main with `docs/queue.md` and `docs/debt.md` removed, which is the published tree:
- before the fix, `check.ps1 -SamplesOnly` with `GITHUB_ACTIONS=true` failed with "check failed: queue board";
- after it, that run is green, and so are the job's other steps there: the Release build, `dotnet format
  --verify-no-changes`, the engine tests (999 passed) and `node tools/webcheck.mjs`;
- without `GITHUB_ACTIONS`, the same copy still fails "queue board".

## Reviews
- architect: no must fix, no should fix. The guard skips only when both hold (no docs\queue.md and `GITHUB_ACTIONS` set), so the gate stays closed in worktrees, `finish`'s clean copy and ship (all have the queue), and the five broken-queue samples run everywhere. `GITHUB_ACTIONS` is the right key: github.json ships in the snapshot and always lists docs/queue.md, so keying on `leave_out` alone would also skip a queue deleted by mistake here. No durable test in this branch: one would run check.ps1 inside itself on a copy, and the cause (a CI step reading a left-out path) is better caught by publish-github.ps1 running the samples on the published tree before the push, a debt line.
