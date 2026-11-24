# The relevel race is worked out in the engine

**Built 11-19** ([numbers-audit](numbers-audit.md), from [page-math-to-engine](page-math-to-engine.md)'s "Not doing").
**Tier:** standard. Players see no change: the renders before and after match (see Done when). **Unblocks:** row 13's fix
in docs/debt.md.

**Why:** docs/engineering.md (engine-owns-numbers): the page draws numbers, the engine works them out. Relevels and
Home's leveling cards still re-figure every finished relevel to a target XP in the page, and no test reaches that
math: only the curve itself is tested (`LoadoutSwapTests`, `ReincarnationTests`). No page test opens Relevels (debt row
"The page tests ... open").

## What the page works out today (as of 05405d9)

- **race.js:** `scaleCurve` (:55), `hoursAt` (:58), `quantile`/`median` (:68), `relevelStops` (:79), `comparable`
  (:160, f = target XP ÷ the relevel's own XP, applied to hours and curve, best first), `mixedRanks` (:168),
  `raceBand` (:171, the middle half), the ghost's gap (:213), `gapLegend` (:257), `relevelRows` (:356),
  `relevelSplits` (:399, split minutes and best of each).
- **home.js:** `levelNowOf` (:94), `relevelHero` (:99: best and typical at your level, `ahead` :106, `bestOwn`),
  `splitsCard` (:238), `betweenHero`'s `scale` (:268, Ask's route minutes, T5-eq and legs re-figured for the next run's
  XP), `relevelsCard`'s typical (:325).
- Every input is sent already: `Journey` (Journeys.cs:31), `Planner.XpByRank`, `homeNow.now.journeyIndex`, the next
  run's `xpMultiplier`, `reinc.home.route`; all from `TrackerService.ReincarnationData` (:731).

## Proposal

- One engine type, a race: given a target XP and an optional kind (what the relevel was mostly), the comparable
  relevels re-figured, the band (lo, middle, hi), hours at each split level, split minutes, best of each, and `ahead`.
  Relevels asks it with the open relevel's XP (or the latest's); Home with the open relevel's XP and kind, or the next
  run's for the between-relevels card. `relevelStops`' folded stops go on `Journey` (debt row 41's last part).
- The engine sends full doubles; the page only rounds (`fmt`) and draws. It scales the curve points as the page does
  today (already rounded to 3 dp), and ties sort as the page's stable sort does (LINQ `OrderBy` matches).
- `levelAt` stays in race.js: it is the tooltip's interpolation under the mouse, drawing and not a number.
- Tests: the race's numbers on hand-built journeys (two relevels at different XP, a mixed kind, one open); a page test
  that opens Relevels and reads its rows and splits; Home's existing tests (`LevelingNowRacesTheBestRelevel...`,
  `NextRelevelOnAMotesGoal...`, `YourRelevelsNamesTheKind...`) pass unchanged, with the fixture's journeys given the
  new fields.

## Done when

- [x] The engine works out the race: `JourneyRace` (`Analysis/JourneyRace.cs`) gives the rows re-figured to a target XP, best
  first, the band, typical, splits, best of each, the open relevel's `ahead`, and the next run's scaled route.
  Proof: `JourneyRaceTests` (hand-worked on made-up relevels: two XPs, a kind, a mixed kind, one open, one that never
  reached a split, one whose curve ran past `LevelNow`, the route at the next run's XP).
- [x] Relevels draws the engine's rows, splits and header. Proof: `RelevelsPageTests` opens the real page and reads them.
- [x] Home's cards (leveling race, splits, between-relevels, Your relevels, done card) draw `reinc.journeyRaces`. Proof: the
  existing `HomeGoalPages` tests pass with their assertions unchanged.
- [x] Nothing the page shows changed. Proof: `--snapshot` from main (bf3cfac) and from the branch, both on one copy of the
  real database: every Home shot (home, home-flipped, home-places, home-norelevel, home-goal-info, home-notice) and every
  Relevels shot (relevels, -splits, -hours, -bottom) is byte-identical. (A first run on a fresh copy differs in Ask's route, which
  Ask works out from what the run reads of the logs, so both were run on the database a first run had left.)
- [x] race.js and home.js work out no race number: `scaleCurve`, `hoursAt`, `median`, `comparable`, `mixedRanks`, `raceBand`,
  the ghost's gap and `levelNowOf` are gone from the pages. Left: `levelAt` (the tooltip's interpolation), `relevelStops` and
  `relevelSplits` (they only fold and label stops; debt row 41's last part, the folded stops on `Journey`, is not done here).

## Edge cases

- A relevel with no curve, or still open: not in the rows. No kind: nothing to race. No next run: the route is scaled to
  this trio's own XP (scale 1). No route: `Next` has no minutes. Ties keep the page's old order (LINQ `OrderBy` is stable).
- The engine sends full doubles and the page rounds, as before. Curve points scale already rounded to 3 dp, as the page did.

## Test changed

`HomeGoalPageTests` (only the fixture, no assertion): the page now reads `reinc.journeyRaces`, so the fixture's messages go
through `HomeGoalPages.WithRaces`, which makes them with `JourneyRace.For` as `TrackerService` does. The tests' meaning is the
same: each still sends a message and reads what Home draws.

## Not doing

- Row 13's bug (at 34 / 50, Home says "reached 50" and "6.6 h ahead"). A faithful move keeps it: `Journey.ReachedLevel` is
  the loadout's level or the curve's last point, whichever is later, as `levelNowOf` was, and `JourneyRaceTests` pins that. It is
  now easy to test at 34: a small branch after this one.

## Where

- `src/EqlPathfinder.Engine/Analysis/JourneyRace.cs` (new), `Journeys.cs` (`ReachedLevel`), `Live/TrackerService.cs` (`journeyRaces`)
- `src/EqlPathfinder.App/web/js/race.js`, `home.js`; `DevSnapshots.cs` (the no-relevel shot empties the races too)
- `tests/EqlPathfinder.Engine.Tests`: `JourneyRaceTests`, `RelevelsPageTests`, `HomeGoalPageTests`

## Reviews
- architect: no must fix. Two should fix, both tests, added: `TheRaceIsMadeForTheOpenRelevelsIndexAndTheCampaignsNextRunXp` and `BestOfEachTakesTheFastestInEachColumnEvenFromAnotherRelevelAndSaysHowFarUnderTheBestItIs`. Three "if"s still in the page scripts went to docs/debt.md (c650a34).
- designer: not needed, Home's 10 states and Relevels render byte-identical from main and the branch on one copy of the real database (jr\outC, jr\outD).
