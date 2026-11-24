# The engine works the numbers out, the page only draws them

**Built 11-18** ([calc-audit](calc-audit.md)). Standard. The engine now sends kill pace (with its word and tone) for each relevel, each stop and each History stretch, the relevel line's slope and old/new levels/h, Ask's cost per level and the fall a level at each place, and the runs a usual needs. Crawls, Home, Relevels and Ask only draw them. Renders of main's scripts and the branch's, over a copy of the same database, matched in all but three shots (settings-share-on and the two upgrade ones, which the change does not touch). **Left out** (below): the Crawls and Loot tile sums, race.js's route legs, Loot's per-tier T5-eq. **Waits for:** one-rate (on main). **Unblocks:** one-usual, visits-tab.

**Why:** docs/engineering.md, Modeling 4: "The engine decides, the page draws." Page scripts still work out numbers the
engine has, or should have, and only the pages a page test opens are tested as drawn (docs/debt.md). So that logic ships
unproven and drifts from the engine's.
**Players:** everyone. No number should move.

## Checked again after one-rate (11-18)

- Already done, dropped: Home's stop T5-eq/h (`stopFarm` from `FarmPace.ForStop`, home.js only reads it); the Crawls tile
  and Loot's per-hour tile use the engine's null per visit (a rate over visits that have one). What's left of those two is
  the sum over what the player's filters show.
- Still in page scripts: `killSpeedOf`, `paceWord`/`paceTone` (race.js; used by race, Home x4); the relevel header's
  slope, old/new rate and leveling test (crawls.js `relevelGroups`); `fitSlope` (ask.js: the relevel line and why-panel);
  Ask's cost per level and why-panel slope; "Under 3" / "3 or more" literals.

## Done when

- [x] Kill speed over visits is one engine function (minutes-weighted, only visits with a usual), and its word and tone use
      `RunPace.NarrowBand` - `KillSpeedsTests`
- [x] One least-squares fit, `Fit.Slope`, is what `RunPace.ExpectedXp` and the new slopes use - `KillSpeedsTests`
- [x] History rows carry the stretch they're in (a relevel, at the cap, leveling outside one) and `reinc.stretches` has each
      one's kill pace, old/new levels/h, whether it fell and about what % a level - `StretchTests`
- [x] Each relevel (`reinc.journeys`) carries its kill pace; each History row its stop's (`stopPace`) - `StretchTests`
- [x] Ask's every-place rows carry cost per level, and `places.falls` the levels/h change a level per place - `AskNumbersTests`
- [x] The runs a usual needs (3) comes from `RunPace.MinRuns` as `reinc.usualRuns` - `StretchTrackerTests`
- [x] race.js, home.js, crawls.js and ask.js only format these; `killSpeedOf`, `paceWord`, `paceTone`, `fitSlope` are gone
      from the pages - `node tools\webcheck.mjs`, the page tests, renders before and after the same

## Edge cases

- Too little: no run with a usual gives a null kill pace; the page says "no usual yet" as now.
- Nothing yet: a relevel with no crawls gets an empty kill pace, a stretch with under 2 rated crawls no slope.
- Same name, different thing: two relevels back to back each get their own stretch and numbers.
- A visit at the cap has no kill speed (kills at the cap aren't counted): its stretch's kill pace is null.
- Filters: a stretch's numbers cover the whole stretch (the user's choice, 11-18), so with a search typed a header may describe visits the
  search hides (before, only the matching ones). The header then says "numbers for the whole relevel; your search shows 3 of its 14
  visits", counting only visits the two checkboxes let through; nothing hidden, no cue. "At 50" and "Leveling outside a relevel" carry no numbers.

## Not doing (and why)

- **The Crawls and Loot tile sums.** They follow the player's filters (search text, zone, time), which only the page
  knows. The rule (which visits have a rate) is the engine's; moving the sum needs a question to the engine for each
  filter change. Left to its own draft; asked of the user in the report.
- **A band per relevel.** A relevel's "slow/fast" keeps the fixed ±15% (`NarrowBand`) as now, in one place. Using each run's
  band (15% or 18%) would move words near the edge; the user decides.
- **Clamping the slopes.** The relevel and why-panel slopes stay unclamped as now (ExpectedXp clamps XP per kill's own fit:
  a different quantity). The two now share one fit.
- **race.js route legs (`relevelStops`), Loot's per-tier T5-eq.** `relevelStops` folds and scales the legs to the levels the relevel covered, for the bar; Loot's tiers come from the pickups the player's filters keep. Both are debt (docs/debt.md), each its own change.

## Where

- `src/EqlPathfinder.Engine/Analysis/PaceNumbers.cs` (new), `RunPace.cs`, `Journeys.cs`, `Ask.cs`, `Pages/AskPage.cs`
- `src/EqlPathfinder.Engine/Live/TrackerService.cs` (`History`, `ReincarnationData`), `Tracking/VisitStats.cs`
- `web/js/crawls.js`, `race.js`, `home.js`, `ask.js`; `tests/`: `KillSpeedsTests`, `StretchTests`, `AskNumbersTests`

## Decisions (the user, 11-18)

- Relevel and stop words keep the fixed +/-15% (`NarrowBand`). The relevel line's and why-panel slopes stay unclamped.
- History's relevel header gives whole-relevel numbers, with the search cue above.
- A stop and the Relevels page's leg are the same visits: rows without a difficulty are skipped by both (`StretchWiringTests`).

## Reviews

- architect: 2 must fix, 2 should fix. Done: `RunPace.ExpectedXp` now uses `Fit.Slope`; wiring tests added (`StretchTrackerTests`, `StretchWiringTests`, `AskPlacesWiringTests`: usualRuns, journeys' killPace, row stretch ids and stopPace, Cost at the farm rate, Falls of the trio, two back-to-back relevels); the Home stop chip matches the leg (both skip rows with no difficulty, tested, so no engine change); `Stretches.Describe` reads the ids on the rows and `ReincarnationData` no longer assigns again.
- designer: the 3 differing PNGs were the clock. 1 must fix (search cue): done in `relevelGroups`, rendered with "Kedge". The History table cutting off its last column header at the right edge goes to docs/debt.md.
