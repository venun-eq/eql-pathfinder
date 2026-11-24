# The page scripts' math moves to the engine, in part

**Started 11-19** ([numbers-audit](numbers-audit.md)). **Tier:** standard, 300k. Players see no change; a number that
moves is a bug found, and this spec says which. It finishes part of what engine-owns-numbers (calc-audit) left.

**Why:** docs/engineering.md, Modeling 4: "The engine decides, the page draws." These page scripts still work numbers
out: ask.js `askVersus` (the level-weighted gap, the thresholds, the kill-speed / XP-per-kill split), race.js `xpOf`
(its own copy of `Planner.XpMultiplier`), race.js `relevelSplits`/`hoursAt`/`levelAt`, home.js `ahead` and `scale`, the
overlay's bands. None had a page test.

## Done when
- [x] The engine sends each Ask versus row's gap, its cause (`both`/`kill`/`xp`/none) and the two percentages, and the overall gap; ask.js draws them - `TheGapAgainstThePlayersSplitsIntoKillSpeedAndXpPerKillInTheEngine`, `BothCausesAreNamedWhenEachIsAtLeastFivePercent`, `AsksVersusPanelDrawsTheEnginesGapAndItsSplit`
- [x] The XP penalty for each rank is the engine's list (`reinc.xpByRank`); no page script has the formula - `TheXpPenaltyForEveryRankIsTheEnginesAloneForThePagesToRead`, `ThePagesReadTheXpPenaltyFromTheEngineNotFromAFormulaOfTheirOwn`
- [x] Nothing a player reads changes: the page data is the same but for the new field, and 55 of 58 renders are byte-identical before and after (the 3 that differ are Settings share-on and the two Upgrades shots, which do not use these numbers) - `PageDataTests`, `scratchpad before/after`

## Edge cases
- No kills a minute on either side (a community file from before 11-16): no cause is named, as before.
- A gap under 15% is "about the same", and a cause under 5% isn't named: both constants move to `Ask.VersusNoise`/`VersusCause`.
- A rank past the list: the last (4x) holds, as `Math.Min(3, ...)` did.
- Before the first data message: the penalty reads as 1, as `xpOf(0)` did.

## Not doing (left for a fresh branch, with what each needs)
Drafted 11-19: [journey-race-in-engine](journey-race-in-engine.md), [tile-sums-in-engine](tile-sums-in-engine.md),
[overlay-metric-tones](overlay-metric-tones.md), [ask-xp-per-kill](ask-xp-per-kill.md).
- race.js `relevelSplits`, `hoursAt`/`levelAt`, `comparable`'s scaling and home.js `ahead`/`scale`: they work on the journeys' curves, so the engine must send each journey's curve re-figured per target XP (or its points per level) and its splits. Larger than this branch's step budget.
- Loot's and Visits' tile sums per filter, and the overlay's `Metric` bands.
- ask.js's "Both" XP per kill still comes from levels/h over kills a minute: Ask's windows hold no XP-kill counts (debt row stays).

## Where
- `Analysis/Ask.cs` (`Versus`, `VersusOverall`), `Pages/AskPage.cs`, `Analysis/Planner.cs` (`XpByRank`), `Live/TrackerService.cs` (`ReincarnationData`)
- `web/js/ask.js`, `race.js`, `core.js`

## Reviews

- architect: two must fix, both fixed (dfdc45d): nothing tested what AskPage sends (now `AsksVersusRowsCarryTheGapItsCausesItsToneAndTheOverallGapForThePage`, which fails on a swap, a 0 or a dropped overall gap), and ask.js kept its own 15% threshold beside `Ask.VersusNoise` (the engine now sends `Slower`, `Tone` and `VersusSame`; no threshold is left in the page, which also pays the leg-tone debt row). The rest of the draft is under "Not doing", for later branches. The lead read the fix instead of a second review.
- designer: no must fix; nothing players read changed (the three renders that differ differ only in clock text; the versus panel and the XP penalty were checked by running the page code before and after). Should fix, fixed: the release note no longer says "engine".
