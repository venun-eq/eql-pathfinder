# The overlay's NEXT ranking in plain code

**Tier:** standard, 200k. Behavior doesn't change; the move is the change. Continues [verdicts-out](verdicts-out.md) ([numbers-audit](numbers-audit.md)).
**Unblocks:** cache-keys (it keys `BuildNext`'s cache; the key is left as it is).

**Why:** which places the overlay offers between crawls was decided inside `TrackerService.BuildNext`, reachable in a test only through
a log, a database and a service, so no ranking was held to a hand-built case (docs/debt.md). `NextStep.Title` and `Lines` were
written there and drawn nowhere, though `Lines.Count > 0` quietly decided whether there is a card.

## Done when
- [x] A pure `Analysis/NextRanking` takes Ask's data, the level, the clock-free rates (`FarmRateNow()`'s value, the XP multiplier) and the last place's label, and returns the options - `NextRankingTests`
- [x] Leveling by speed ranks the fastest place first; by motes it ranks by cost against farming, a null cost last - `NextRankingTests`
- [x] The route Ask would take from here moves its first place to the front; a place change within 2 levels is added as a "from level" option - `NextRankingTests`
- [x] A place with fewer than 2 visits, or closed at the level, is not offered; the last place is marked `Again` - `NextRankingTests`
- [x] Farming offers the best two places since the patch with 3+ crawls, else the best two from before it - `NextRankingTests`
- [x] Nothing a player sees changes: the card, its options and the rates through the service match what they were - `NextRankingServiceTests.BetweenCrawlsOffersWhatItOfferedBefore` (written and green before the move)
- [x] `NextStep` has no `Title` or `Lines`; the card exists when `Last` or `Options` has something - the two existing done-card tests, and the test above

## Edge cases
- Cache: `_nextCache` and its key are untouched (the clock's hour, the farm rate and the farmed difficulties are still in it).
- At the cap, with no level known, or with nothing ranked: options are empty; the card still shows the last crawl when there was one in the last 6 hours, else there is no card (as before).
- A farming player with no farm since the patch and none before: no options, only the last crawl.
- A null cost sorts last (`?? double.MaxValue`), never first; levels/h is divided by the XP multiplier once.
- The last crawl, judging it and the 6 hours stay in the service (they need `Now`, `Judge` and the history).

## Not doing
- The cache key (cache-keys); `DrawDone`'s wording in code-behind (its own debt row stays, minus the `BuildNext` tests part).

## Where
- new `src/EqlPathfinder.Engine/Analysis/NextRanking.cs`; `Live/TrackerService.cs` (`BuildNext`, `NextStep`); `App/DevSnapshots.cs` (`new NextStep`); docs/code-map.md, docs/debt.md

## Reviews

- architect: the code moved over unchanged and the overlay shows what it did. Two must fix, both fixed in tests (631d090): the sort tests had two places, so the route decided the first slot whatever the sort did (now three places), and no test failed without the route reorder (now one does). Should fix, fixed: a service test of the card with options and no last crawl; the spec's test name and a debt row's backtick. Debt: a place too slow to price can still lead the card by motes (as before).
- The red check after merging main was a time-of-day flake, not the code: the service test built its crawls from the real clock 3 h back, and it ran at 00:45. Both service tests now run at a fixed 00:30 and 12:00 (45baefd); the other tests that do the same are a debt line.
