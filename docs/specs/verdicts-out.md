# Verdicts in pure code that tests call with values

**Built 11-19** ([numbers-audit](numbers-audit.md)). **Tier:** standard, 300k. Behavior doesn't change; the move is the change.
Built on one-clock and farm-rate-value. **Unblocks:** verdicts-as-of-visit, cache-keys, sample-gates.

**Why:** the verdicts players read lived inside `TrackerService` (`Judge`, `JudgeRun`, `Judged`, the stop assembly in `History`),
reachable in a test only through a whole service and its database. The math under them was pure; the step that combines
it wasn't, so no verdict was held to a hand-built case.

## Done when
- [x] A pure `Analysis/Verdicts` (`Judge`, `JudgeAll`, `Apply`, `AssignStops`) takes segments, farmed difficulties and the cap, and `History` and `Judge` call it - `VerdictsTests`
- [x] A leveling run with no earlier visits has no usual - `ALevelingRunWithNoEarlierVisitsHasNoUsual`
- [x] A farmed difficulty is judged by its rate against its usual - `AFarmedDifficultyIsJudgedByTheRateAgainstItsUsual`
- [x] A grouped run is judged by motes - `AGroupedRunIsJudgedByMotesToo`
- [x] A raid row is a raid whatever else was judged - `ARaidRowIsMarkedARaidWhateverElseWasJudged`
- [x] Consecutive visits at one place are one stop, another place is another - `ThreeVisitsInARowAtOnePlaceAreOneStopAndAnotherPlaceIsAnother`
- [x] No message changes - `TrackerServiceTests.ConsecutiveFarmedVisitsAtOnePlaceAreJudgedAsOneStop` stays green (through History: StopFarm, FarmVerdict, the raid kind), and `PageDataTests.EveryPageGetsTheDataMessageItGotBefore` stays the proof of the message shape

## Edge cases
- The community's "others here" context is asked only for a visit with no usual of its own, and only for the live judge (History's `JudgeAll` passes none), as before.
- A raid is marked a raid even when a segment was judged for it (the old order).
- The live crawl or one Ask leaves out (DDD) is judged from its stats, as before: that part stays in `TrackerService` (it needs `Facts()`).
- Inputs are plain (segments, farmed list, cap, a community lookup), so verdicts-as-of-visit can pass earlier-only inputs later.

## Not doing
- `BuildNext`'s ranking and dropping its `Title`/`Lines`: left to a follow-up (it also reads `Now`, `FarmRateNow()` and Ask's chain; moving it is a bigger step than this one).
- Rewriting `RunPace.Judge` (xp-at-cap touches it).

## Where
- new `src/EqlPathfinder.Engine/Analysis/Verdicts.cs`; `src/EqlPathfinder.Engine/Live/TrackerService.cs` (`Judge`, `Judged`, `History`); docs/code-map.md

## Reviews

- architect: one must fix, fixed: the "no message changes" line cited a test that serializes the same History twice, so it could not catch a changed verdict; it now cites `ConsecutiveFarmedVisitsAtOnePlaceAreJudgedAsOneStop`. Should fix, fixed: the grouped-run test now proves it is judged by motes, and a new test proves the community view is asked only for a visit with no usual. The moved code matches the old line for line. BuildNext's ranking was cut from this branch; the user, 11-19: its own branch.
