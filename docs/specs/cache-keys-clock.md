# Cache keys clock: the tracker's tick reads the service's clock

## Why
The 12:25 release stopped on `CacheKeysTests.PaceFollows*`. The tests fix the service's clock at 12:00, but
`TrackerService` ticked the tracker with `DateTime.Now`. Once the wall clock passed the fixed time, the silent-log rule
closed the open crawl, so the farm pace was gone. A real player's clock is the wall clock, so only tests were hit; the
fix is in the engine because the injected clock was being bypassed.

## Done when
- [x] The service ticks the tracker with its own clock (`Now`), not `DateTime.Now`.
- [x] `CacheKeysTests` and `NextRankingServiceTests` pass with the wall clock after the fixed time (they failed at 13:27 before: 2 of 11).

## Edge cases
- Throttles (commit, publish, journeys) stay on the wall clock: they measure real seconds, not log time.
- `BetweenCrawlsOffersWhatItOfferedBefore(hour: 0)` fixes its clock at 00:30 and wall time passed it: same cause.

## Where
`src/EqlPathfinder.Engine/Live/TrackerService.cs` (Tick), tests unchanged.

No test: the existing `CacheKeysTests.PaceFollowsWhatAFragmentIsWorth` and `PaceFollowsLootThatLandsBetweenKills` are the test that caught it. They fail on main since about 12:30 on 11-19 (their clock is fixed at 11-19 12:00) and pass with the fix, three runs of three.

## Reviews

- architect: no must fix (run on sonnet). In live play `Now` is the wall clock (every caller passes no clock), so players see no change; the throttles stay on the wall clock on purpose; the tests pinned 11-19 12:00, so they failed for good from about 12:30 that day, and now can't. Should fix, fixed by the lead: the release note's test talk and jargon, and this line. It replaced debt.md's paid `TrackerService.Tick` row and added one for `FightMeterService`'s missing clock (0331ece).
