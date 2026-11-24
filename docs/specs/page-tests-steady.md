# Page tests stay green in the hourly ship

**Why:** After the cap (page-tests-run-together) the hourly ship's tests still went red: 26 page tests on 11-20 04:25,
every one "msedge.exe took longer than 20 seconds [...] it had a browser slot from the start".
**Players:** nobody sees it; a red ship holds five release notes back for an hour at a time.

**Cause (reproduced 11-20):** the bursts were never 12 Edges hanging together. A class's fixture runs its pages once
(`PageRuns.AtOnce`) and a failed fixture fails every test in the class with the same message: 12 red is one class of 12
tests, 7 red another. The ship log showed 5 hung Edges, not 26. A hung one has no renderer process (browser, gpu,
crashpad and utility processes alive, stderr only `fallback_task_provider.cc:126`), and no output pipe is held by a
stray helper. The difference is the ship's scheduled task: `EQL Pathfinder ship` has priority 7 (below normal), the test
host and every Edge inherit it, and the first wave of Edges started together stalls. The same suite as a task at
priority 7 (conhost --headless, same user): 31 red, from 5 hung Edges; at priority 4: 0 red and 31 s instead of 51 s.
Below normal from an interactive shell does not reproduce it, so the trigger needs the task's context too.

**Done when:**
- [x] A timeout says what was hung: stderr so far, and the processes still running with the run's profile (type and
  command line), and the crash dumps in the profile. `PageHarness.Dump`; seen in the samples above.
- [x] The burst is explained (above).
- [x] The harness raises the test run's memory and disk priority before its Edges (`PageLimits.RaiseToNormal`; the
  hour-task's low disk and memory priority is what Edge inherits). It touches nothing else: CPU class and affinity stay
  as inherited. The proof is `PageLimitsTests.RaisingTouchesOnlyMemoryAndDiskPriority` (lowers both, asserts both come
  back; red with the disk raise removed). `PageBelowNormalTests` is a smoke test: 12 starts from a below-normal host all
  come back, and it passes without the raise. The raise alone was NOT enough: see "Result" and the retry below.
- [x] The one retry: `HeadlessEdge.Dump` runs a browser that hung with no `renderer:` process left once more, in a fresh
  slot and a fresh profile; a hang that keeps its renderer is never retried; each retry is logged (`PAGE-RETRY`, class,
  test, whole dump) to stdout and to artifacts\page-retries.log (kept; ship.ps1's log-keeping regex doesn't match the
  marker and test stdout isn't in the ship log at -v q, so read that file); more than 2 retries in a run is red
  (`PageHarnessRetryTests`: retried once and gets past, renderer kept is not retried, a third is red).
- [x] 5 full-suite runs in a row green, nothing else running: 897 of 897 each (58-72 s). The same as a priority-7 task: NOT green, see "Result".
- [x] 2 full-suite runs at once green: 897 of 897 each (1 m 52 s).

**Edge cases:** a host already at normal or high is left alone; a refusal to raise the priority (no right to) is not a
failure, the run goes on as before; the host is raised once and stays (ship's later steps are not affected: it is the
test process).

**Where:** `tests/EqlPathfinder.Engine.Tests/PageHarness.cs` (`Dump`), `PageBelowNormalTests.cs`, `docs/page-tests.md`.
The ship task's own priority is `tools/` and left alone (risky list).

**Not explained:** the menu-closing flake in docs/debt.md (a lone red under three suites at once, step 5 of one test);
it is not a timeout, so this cause doesn't cover it. The row stays.

## Result
Scheduled-task samples (same task settings as `EQL Pathfinder ship`, `--no-build` suite): priority 4: 0 red in 3 runs
(32-37 s). Priority 7 before `RaiseToNormal`: 31, 84, 53, 56, 30, 59, 70, 21 red (8 of 8 red). With it: 7, 38, 34 red.
A 12-at-once simple page at priority 7 is fine, so it is the real pages under low priorities. The raise helps a little
and does not fix it. What fixes it is the task priority (`tools/ship.ps1` registers it; risky list, the lead or the
user decides): 4 instead of 7. A test that the ship's conditions pass cannot sit in the gate (it needs a task
registered and takes 2 minutes); `PageBelowNormalTests` holds the part the harness can do.

## Part 2: develop while playing (user approved 11-20)
**Why:** priority 4 for the ship fixes the hang but lets dev work take every core from the game. Dev work is pinned
to the E-cores plus at most 2 P-cores at below-normal CPU, with normal memory and disk priority (the hang's cause).
**Done when:**
- [x] `RaiseToNormal` raises only memory and disk priority; CPU class and affinity stay as inherited (`PageLimitsTests`).
- [x] Edge slots follow the cores the run may use (affinity), and are about 4 while `eqgame.exe` runs (`PageLimitsTests`).
- [x] `tools\dev-limits.ps1` picks E-cores (EfficiencyClass) + at most 2 P-cores; a sample topology proves it (check.ps1 samples).
- [x] check.ps1 pins itself and uses MSBuild -m 2 while the game runs; `tools\dev-session.ps1` starts Claude pinned;
  `install-ship-task.ps1` registers priority 4 and runs the ship through `toolsun-limited.ps1`.
- [~] 5 full-suite runs green as a probe task shaped like the ship at priority 4 with the pinning; green again with a
  CPU burner on the P-cores. Counts below. Probe tasks unregistered.
**Edge cases:** no eqgame: full speed. Topology unreadable or no E-cores: no pinning, only below-normal. A process
already narrower than the mask keeps its own affinity (intersection, never widened).
**Where:** `PageHarness.cs`, `EdgeSlots.cs`, `PageLimits.cs`, `tools/dev-limits.ps1`, `run-limited.ps1`, `dev-session.ps1`,
`check.ps1`, `install-ship-task.ps1`, `docs/developing-while-playing.md`, `docs/page-tests.md`.

## Result, part 2
Probe task shaped like the ship (conhost --headless, Interactive, Limited, priority 4, through run-limited: affinity FFF000, below normal), 905 tests, `--no-build`:
- Pinned, nothing else running: 5 of 5 green (35-44 s). An earlier 5: 2 green, the reds being a test of this branch that started 12 Edges at once (now one per slot, commit 03b161c) and the known menu flake (docs/debt.md), 1 in 5 to 6 runs and not a CPU one.
- With 16 CPU burners on the 8 P-cores (normal priority, standing in for the game): 4 of 5 green (50-66 s); the red was 24 tests, 5 classes, each msedge past the 20 s fuse. With the burners and a fake eqgame.exe (4 slots): 3 of 5 green (54-67 s); reds were the menu flake and one class hung (3 tests).
- So the pinned run is steady alone, and under a game that holds every P-core a run is red about 1 in 5 from a hung first wave. The spec box stays open for the user: a real game uses far less than 16 spinning threads, but that is not proven. Probe tasks unregistered.

## Reviews
- architect: (11-20) two must fix: the first Done-when box still says PageBelowNormalTests fails without RaiseToNormal, but its assertion was inverted to BelowNormal and it passes without the raise (the proof is PageLimitsTests; reword the box); developing-while-playing.md says the probe was green under the burner, when it was 4 of 5 and 3 of 5. Should fix: assert disk priority too in RaisingTouchesOnlyMemoryAndDiskPriority; check.ps1's build reuses unpinned MSBuild nodes and compiler servers (--disable-build-servers). Retry of a hung browser: right if only on the no-renderer signature, once, logged to a kept file, and the run red past 2 retries. Sound: topology offsets, mask intersection, inherited affinity and below-normal through ship's cmd re-launch, per-wait game check, run-limited argument passing, task priority 4.
- engineer: (11-20) Must fix 1: first box reworded (proof is `PageLimitsTests.RaisingTouchesOnlyMemoryAndDiskPriority`; `PageBelowNormalTests` a smoke test). Must fix 2: developing-while-playing.md states 5/5, 4/5, 3/5, and the counts after the retry. Should fix 1: the test lowers disk (33) to 0 and asserts it is back to 2 or more; goes red with the disk raise removed. Should fix 2: `--disable-build-servers` on check.ps1's build. Retry built as decided: `HeadlessEdge.Dump`, no `renderer:` only, once, fresh slot and `-retry` profile, `PAGE-RETRY` line to stdout and artifacts\page-retries.log, a third is red; three gate tests (`PageHarnessRetryTests`). ship.ps1's keep-regex can't match the marker without a change to ship.ps1 (left alone), and test stdout isn't in its log at -v q, so docs point at the file. Probe, burners, 2 sets of 5: 3/5 and 4/5 green, no retry fired in a real page run; the one real hang class (12 tests) in set 1 was not retried, cause unknown (message not kept). Not fixed: the menu flake (other branch). Seen: run-limited.ps1 prints "Cannot index into a null array" at line 14 when called without extra arguments (harmless, debt).
- architect: (11-20, second) one must fix, three should fix; the retry itself is sound. Must fix: `tools\run-limited.ps1` line 14 (tested): with no extra arguments `$Rest` is `$null` yet `@($Rest).Count` is 1, so `$Rest[0]` errors and `$positional += $null` passes a null positional to the script; the hourly task passes `-Scheduled`, so only a manual call is hit. Fix, one line before `$named = @{}`: `$Rest = @($Rest | Where-Object { $null -ne $_ })` (checked: 0 items with none, 3 with `-Scheduled -Name v`). Should fix: (1) `finish` runs check.ps1 in the clean copy, so `artifacts\page-retries.log` is written under that copy's root and removed with it, and test stdout isn't shown at `-v q`: a retry during finish leaves no trace (release.ps1 runs from the main checkout, so ship's log persists); have check.ps1 print the file's `PAGE-RETRY` lines after "engine tests", or copy it to the main checkout's `artifacts\`. (2) The rule "only when Windows really answered" has no test: add `Alive = _ => ["(could not ask Windows: x)"]` and `["(Windows was not asked in time ...)"]` cases asserting no retry; also treat an empty answer while Edge's own process was still running as "not answered" (a powershell that failed silently gives an empty list, which reads as no renderer). (3) `PageRetries.Who` matches frames whose class name ends in "Tests", but after an await (and in the fixtures' `InitializeAsync`) the frame is the compiler's `<X>d__n` nested class, so the log says "(test not found)"; use `t.DeclaringType ?? t` for the class name. Sound: a real hang keeps its renderer so it stays red; `PageRetries.Shared` is static, so "more than 2" counts per test process, not per class, and the third is red; the retry's `-retry` profile sits inside the run folder and is cleaned with it; the slot is released and re-acquired; the three gate tests prove once, no-retry-with-renderer and third-is-red; docs match.
- engineer: (11-20, second) Must fix: run-limited.ps1 drops null entries from `$Rest` first; check.ps1's sample now runs it with no extras (expects `False||`) and with `-Scheduled -Dest v`, both without error. Should fix 1: check.ps1 prints the `PAGE-RETRY` lines of artifacts\page-retries.log right after "engine tests". Should fix 2: `HeadlessEdge.Attempt` treats an empty answer while Edge's own process still ran as not answered; new theory tests for "could not ask", "not asked in time" and the empty answer assert no retry. The existing retry tests' stub answer changed from `[]` to one `browser:` line (what Windows really returns when Edge's own process is up and no renderer is), since `[]` now means not answered. Should fix 3: `PageRetries.Who` uses `t.DeclaringType ?? t`, names the method from the `<X>d__n` class, and accepts fixture classes ending in "Pages"; test awaits before asking.
