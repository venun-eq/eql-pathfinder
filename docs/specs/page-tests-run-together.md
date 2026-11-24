# Page tests pass when several runs overlap

**Why:** Page tests (headless Edge, `PageHarness`) went red in a burst when two test runs overlapped: 81 red in ship's
11-19 18:25 tests, 14 red on 11-20 01:25 beside a worktree's check, and campaign-page-layout's `finish` once red alone.
**Players:** nobody sees it; a red ship stops a release for an hour.

**Cause (reproduced 11-20):** load, not a shared resource. Edge's profile is already one folder a run, and no port or
desktop is shared. Three suites of page tests at once (each opens up to ~16 Edges together) put 74-80 of 107 red,
every one "msedge.exe took longer than 20 seconds": starting that many Edges at once on 8 cores takes more than the
harness's 20 s fuse, which counted from the start of the run.

**Done when:**
- [x] Every Edge start takes one of a fixed number of slots shared by every test run on this PC (named mutexes,
  `EdgeSlots`: half the cores, at least 2); the 20 s timeout counts from when the slot is taken. `EdgeSlotsTests`.
- [x] A full house waits (up to 10 min), then fails saying all slots stayed taken: `EdgeSlotsTests`.
- [x] A slot left held by a dead run frees itself: `EdgeSlotsTests`.
- [x] The timeout message says the run had its slot from the start. `PageHarnessHangTests` holds the only slot for 3 s, allows 2 s, and asserts no start while held, the timeout's wording, and elapsed of at least 4.5 s.
- [x] Several suites at once: two at once green (the architect also ran two at once, 110 of 110 each); three at once had no timeouts (before: 74, 80, 79 red), with one red on a menu flake in docs/debt.md.

**Edge cases:** a killed test host (mutexes are abandoned, not leaked); a scheduled task in another session
(`Global\` names, `Local\` where not allowed); stand-in browsers in harness tests (their own slots, so they never wait
behind real Edges); two threads of one process (a named mutex is re-entered by the thread that holds it, so the
tests hold slots on their own threads, and real runs take and give back a slot inside one synchronous call).

**Where:** `tests/EqlPathfinder.Engine.Tests/EdgeSlots.cs`, `PageHarness.cs` (`Dump`, `Slots`), `EdgeSlotsTests.cs`,
`docs/page-tests.md`. tools/check.ps1 needed no change: the cap is in the harness, so a check's suite shares it.

**Proof:** three suites of page tests started together, run 3 times (9 suites): 74, 80, 79 red before the cap (one round); after, 8 of 9 suites fully green, 1 red on `LeaveItToPathfinderUnranksARaceAndKeepsItsGoal` (a menu closing, not a timeout; docs/debt.md). Round 1 (slots, before the stand-in fix) had 2 red on the hang test, since fixed. docs/page-tests.md says how to repeat it.

## Reviews
- architect (11-20): sound in effect. Two suites at once on this branch, run by the review: 110 and 110 green. Slots
  under a killed run (abandoned, taken), another session (ship runs as the same user, Interactive, Limited, so
  `Global\` is shared; an elevated or other-user run falls back to its own slots, which fails open to the old
  behavior), finish's clean copy (merges main first, so it takes slots) and the debt flake (a menu closing in virtual
  time, nothing two runs share) all hold. **Must fix:** no test in the gate fails if `HeadlessEdge.Dump` stops taking
  a slot or the fuse goes back to counting from the run's start, and the hang test doesn't assert the new wording
  (Done when 1 and 4). Add to `PageHarnessHangTests`: a 1-slot `EdgeSlots` held by another thread for ~3 s, the hung
  stand-in with `Timeout` 2 s; assert `held.txt` doesn't exist while the slot is held, the failure is the timeout's
  (not "stayed taken"), elapsed is at least hold + timeout, and the message contains "had a browser slot from the
  start". **Should fix:** Done when 5 is ticked with 1 of 9 suites red; say what was shown (two at once green, three
  at once no timeouts).
- engineer (11-20), on the review: **Must fix** done: `AHungBrowserWaitsForItsSlotAndItsTimeoutCountsFromTheSlot` (1-slot `EdgeSlots` held 3 s, 2 s timeout; asserts no start while held, not "stayed taken", elapsed >= 4.5 s, "had a browser slot from the start"). With `Acquire` removed from `Dump` it went red ("the browser started while another run held the only slot"); put back, green. A fuse counted from the run's start would fail the elapsed line (about 2 s), not shown by a mutation. **Should fix** done: Done when 5 now says what was shown.
- lead (11-20), read the fix: the test catches both faults. A deadline taken before `Acquire` would expire while the
  slot is held and fire at about 3 s, under the 4.5 s floor. No second review: the must fix added a test and didn't
  change how the slots work.
