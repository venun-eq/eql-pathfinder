# Developing while you play

Builds, tests and Edge can take every core and starve the game. Dev work is therefore kept to part of the CPU, and
backs off further while `eqgame.exe` runs. This PC: i7-13700K, 8 P-cores (16 logical) and 8 E-cores, 24 logical processors.

**What's pinned.** Everything dev work starts runs on the **E-cores plus the last 2 P-cores** (mask `0xFFF000`: logical
processors 12-23) at **below-normal CPU priority**. E-cores are found by `EfficiencyClass`
(`GetLogicalProcessorInformationEx`), not assumed. The game keeps P-cores 0-5 (logical 0-11) to itself. **Memory and disk priority stay normal on purpose**: low ones
(what the hourly task's "priority 7" gives) hung the page tests, see docs/page-tests.md. Children inherit affinity and
priority, so one pin at the top covers builds, `dotnet test` and every Edge.

| What | How it's pinned |
|---|---|
| A Claude session | start it with `.\tools\dev-session.ps1` in place of `claude` |
| `tools\check.ps1` (and `finish`'s clean copy, which runs it) | pins itself (`tools\dev-limits.ps1`) |
| The hourly ship | its task runs `tools\run-limited.ps1 -Script tools\ship.ps1` at task priority 4 (`install-ship-task.ps1`) |

**While the game runs.** The page tests use 4 Edges at most (`EdgeSlots`, counted from the cores the run may use, half of
them, and 4 at most while `eqgame` is running), and `check.ps1` builds with `-m:2`. Without the game: full speed
within the pin. Checked at each wait, so starting the game mid-run takes effect at the next browser.

**Undo.** Set `EQLP_NO_LIMITS=1` (nothing is pinned or lowered), or start `claude` the usual way. A process that is
already on fewer cores keeps them: the mask is intersected, never widened. A PC whose cores are all alike, or whose topology
can't be read, only gets the below-normal priority. To widen the ship, re-run `tools\install-ship-task.ps1` after editing.

Proof (11-20, see docs/specs/page-tests-steady.md): a probe task shaped like the ship (conhost --headless, Interactive,
Limited, priority 4, through run-limited) ran the full suite, before the retry: pinned with nothing else, 5 of 5 green;
with 16 CPU burners on the P-cores, 4 of 5; with the burners and a fake game, 3 of 5 (a hung first wave of browsers, and
the menu flake). With the one retry of a hung browser that has no renderer left (docs/page-tests.md), 16 burners on the P-cores, 2 sets of 5: first set 3 of 5 green (one red was the menu flake, one was a class of 12 tests plus PageBelowNormalTests hung with no retry logged, so that hang kept a renderer or was not the stall; the browsers' messages were not kept), second set (messages kept) 4 of 5 green, the red the menu flake; no retry fired in any real page run. Probe tasks unregistered, burners stopped.
