# The engine reads the time from one clock it's given

**Built 11-19** ([numbers-audit](numbers-audit.md)). **Tier:** small. Nothing a player sees changes.
**Waits for:** nothing. **Unblocks:** verdicts-out, verdicts-as-of-visit.

**Why:** `TrackerService` reads `DateTime.Now` 17 times, and passes it into the math:
- `PaceData` (line 597), `AskData()` (line 879) and `BuildNext` (line 473) pass it to `AskData.From`. That halves a
  visit's weight every 30 days, counted from `now`.
- `PaceDrift` scales each visit's time against it.

No test can pin a number that depends on the date. History's "another place levels faster" also shifts with today's
date (verdicts-as-of-visit).

## Proposal

- `TrackerService` takes a `TimeProvider` (the system one by default), and every reading of the time on its analysis
  path goes through it. The tracker's loop timers can stay on the system clock. Say which in the spec.
- A test sets the clock and shows that a number that depends on the date (Ask's weights, `PaceDrift`) is the same at
  the same set time.
- Leave out the clock in log time (`engine.State.LastTime`); that's the log's own and already a fact.

## Where

- `src/EqlPathfinder.Engine/Live/TrackerService.cs` (every `DateTime.Now`), its constructor and callers
  (`App.xaml.cs`, `Cli/Program.cs`, the tests)

## Done when

- [x] `TrackerService` takes an optional `TimeProvider` (the system one by default); callers are unchanged - it compiles
- [x] Its analysis path reads the time only through it: `BuildSnapshot`, `History`, `BuildNext`, `PaceData`, `AskData()`, the Plan's `Since` fallback, the Ask cache's hour key and the upgrade basis - `TheAnalysisReadsTheClockItIsGiven`
- [x] The same set time gives the same "last crawl" card, and a clock 10 hours on drops it - same test

## Edge cases

- **The rules change / old data:** a visit's weight is still measured from `now`; only where `now` comes from changes. With the system clock nothing differs.
- **Starting and stopping:** timers stay on the system clock (below), so the loop starts and stops as before.
- **Same name, different thing:** `engine.State.LastTime` (log time) is left alone; it's a fact from the log.

## Stays on the system clock

Loop and cache timers measure real elapsed time, and no number depends on a date through them: the commit and publish
intervals (`_lastCommit`, `_lastPublish`), the 30 s journeys cache (`_journeysBuilt`), `Tick`, and the live-drop check
(`HighTierDropped`, 3 real minutes). `FightMeterService`, `Ask.From`'s own fallback and the App's uses are not touched.

## Reviews

- architect: on Sonnet, no must fix or should fix; every date-dependent read in the service goes through the given clock, the ones left on the system clock time real intervals, and the test fails on the old code for the right reason. One debt line: `Tick` still ends a silent visit against the system clock (verdicts-as-of-visit to decide).
