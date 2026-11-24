# Page tests: what they share, and the cap

Page tests open the real page in headless Edge through `tests/EqlPathfinder.Engine.Tests/PageHarness.cs` (what a run
is: docs/quality.md). Several test runs can be going on this PC at once: ship's tests, a worktree's `check.ps1`, a
`finish`'s clean copy, and each suite's own parallel runs (`PageRuns.AtOnce`).

**What they share, and what they don't.** Each run has its own copy of the page and its own Edge profile
(`pf-pagerun-<guid>` under %TEMP%): no folder, port or window is shared. What they share is the CPU. On 11-20, three
suites at once put 74-80 of 107 page tests red, each "msedge.exe took longer than 20 seconds": Edge starts crowd
each other past the harness's fuse. Run alone, a start takes about half a second.

**The cap.** `EdgeSlots` (tests/EqlPathfinder.Engine.Tests/EdgeSlots.cs) holds named mutexes, `Global\EqlPathfinderEdgeSlot<n>`,
half the cores in number (at least 2). `HeadlessEdge.Dump` takes one before starting Edge and gives it back when
Edge has ended; the 20 s fuse counts from when the slot is taken, so waiting your turn is never a timeout. A run
waits up to 10 minutes for a slot, then fails saying all slots stayed taken. A killed test host's mutex is abandoned
and the next run takes it. A timeout message that says "it had a browser slot from the start" means the browser
itself was slow or hung, which is a real failure to look at.

**Proving two runs at once.** Start three `dotnet test tests\EqlPathfinder.Engine.Tests -c Release --no-build
--filter "FullyQualifiedName~PageTests|FullyQualifiedName~PageHarness"` together from a worktree and wait: all pass.
Before the cap the same command put 74, 80 and 79 red. Results of the sample on this branch are in
docs/specs/page-tests-run-together.md. `tools\check.ps1` needs nothing extra: its engine-tests step uses the cap.

**Why the ship's tests still went red after the cap (11-20).** A fixture runs its pages once, and a failed fixture
fails every test of its class with the same message, so "12 red at the same instant" was one hung Edge. A hung Edge has
no renderer (browser, gpu, crashpad and utility processes alive, nothing on stderr but a task-manager line). The
hourly task (`EQL Pathfinder ship`) runs at "priority 7", and Windows gives its processes low CPU, **disk (1, not 2)
and memory (2, not 5) priority**, which every Edge inherits. The same suite run as a scheduled task: priority 4, 0 red
in 3 runs of 32-37 s; priority 7, red in 9 of 9 (21-84 red, 100+ s). A simple page opened by 12 Edges at once is fine
at priority 7, so it takes the real pages' load. From an interactive shell, `start /belownormal` does not reproduce it.
`HeadlessEdge.Dump` now puts the test host back to normal CPU, disk and memory priority before it starts Edge
(`RaiseToNormal`, then also CPU), which cut the red from 21-84 to 7-38 in the same task: not enough. What does fix it is the task's
own priority, which lives in `tools/` (see docs/specs/page-tests-steady.md). A timeout now lists the processes still
running with the run's profile (type and command line), the crash dumps in it and what Edge wrote on stderr.

**One retry (11-20).** The stall leaves no `renderer:` process (see the timeout's list). `HeadlessEdge.Dump` runs such a
browser once more, in a fresh slot and a fresh profile; a hang that keeps its renderer is a real one and stays red. Each
retry logs one line, `PAGE-RETRY <time> <class.test (page)> retry n of 2: <the whole dump>`, to stdout and appends it
to `artifacts\page-retries.log` (kept, never emptied). `tools\ship.ps1` keeps its log only when it matches
`SHIPPED|ALERT|FAILED|Exception|At ...ps1`, and test stdout isn't in that log at `-v q`, so read the file, not the ship
log. A run in which a third browser needs a retry is red (`PageRetries`, `PageHarnessRetryTests`).

**Repeating the task's conditions.** Register a task as `EQL Pathfinder ship` is (conhost --headless, Interactive,
Limited, `Settings.Priority = 7`) that runs `dotnet test ... --no-build`, start it and read its output file.

**Dev work and the game (11-20).** With priority 4 the ship's tests are green, so the fix for the hang is memory and disk
priority at normal; CPU can stay low. `RaiseToNormal` (now in `PageLimits`) therefore raises only memory and disk priority
and leaves the CPU class and affinity as inherited. The slots count the cores the run may use (its affinity), half of them,
and 4 at most while `eqgame.exe` runs. Everything dev work starts is pinned to the E-cores plus 2 P-cores:
docs/developing-while-playing.md.
