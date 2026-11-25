# Mid-relevel Home shows your real level

**Why:** The queue row named four faults on Home while a player is mid-relevel (found 11-18 to 11-19, from the designer's renders). Re-checked 11-24 against the code and a render of the real log: two were already gone, two were real, and one more turned up.
**Players:** anyone releveling. Worst for a player who has just reincarnated into a relevel and not crawled yet.

## What each cause turned out to be
- **Pace line and Splits "reached 50" at 34:** not reproducible. The engine's `RaceNow.Level` is the level the relevel is at (34) and splits past it are projected; the render of a real relevel at 21 / 50 reads 21 throughout. Pinned by a test, no code change.
- **Home's mode "farming" below the cap:** not reproducible. `HomeState` says farming only at or above the cap of the day, and says "relevel" at 50 under a cap of 55. Pinned by tests, no code change. (`modeSwitch` only draws in farming/between, so it can't print the cap below it.)
- **Session counts a relevel crawl (real):** a crawl at 32 in Nagafen's Lair D4 counted in the Farming card's session after the player reached 50 and kept crawling the same place ("0.0, 1 crawl").
- **`relevelHero` for an unplayed relevel (real):** a relevel on now by /who with no crawl fell back to a bare "Leveling" card with no relevel number or race.
- **Found while checking:** Parked relevels read "47.12 / 50" (a curve level with its fraction).

## Picture
Before: "Leveling" / "Your character"-style card, no number; parked "47.12 / 50". After: "Leveling now · relevel 11", trio and race, "1 / 50", "not played yet"; parked "47 / 50".

## Done when
- [x] Relevel at 34 of 50: Home's mode is relevel, the race's level is 34, splits at 30 and below are reached, 40 and above projected - `A_relevel_at_34_of_50_reads_34_on_the_pace_line_and_the_splits_and_Home_is_on_the_relevel`
- [x] At 50 under a cap of 55 Home stays on the relevel - `Home_is_on_the_relevel_not_Farming_below_a_cap_of_55_at_50`
- [x] A farming session at the cap leaves out a relevel crawl that began below it - `The_farming_session_at_the_cap_leaves_out_a_relevel_crawl_in_the_same_place`
- [x] An unplayed relevel on now is headed "Leveling now · relevel N" with its race and "not played yet" - seen in `main-home-unplayed.png`
- [x] Parked relevels show whole levels - seen in `main-home.png`

## Edge cases
- Below the cap, "Farming" forced in Settings: the session is untouched (the filter applies only at the cap).
- A crawl with no known start level counts, as before.
- Cap 55 and level 50: not at the cap, session untouched.

## Not doing
- Home's leveling-versus-motes reading: leveling-follows-play rewrites it.

## Where
- `Tracking/VisitStats.cs` `SessionStats.CurrentSession` (`counts`); `Live/TrackerService.Snapshots.cs` (the session at the cap)
- `web/js/home.js`: `renderHome`, `levelingHero(r, relevel)`, `levelOf`; `DevSnapshots.cs` `home-unplayed`
- `tests/EqlPathfinder.Engine.Tests/HomeBelowCapTests.cs`

## Reviews
- architect: the farming-session test passed its own lambda, so reverting the filter in `TrackerService.Snapshots.cs` left it green; the debt rows for the website's farming-below-cap render and the pace-line leftovers had been dropped, leaving site/README.md pointing at nothing; one assertion could pass on an empty splits list. Done: the test now reads a log (a crawl at 32, two at 50, same place) through `BuildSnapshot` under caps 50 and 55 and fails with the filter reverted (checked); both debt rows are back, reworded; the 34-of-50 test asserts splits at 30 and 40 exist.
- designer: the unplayed-relevel hero had no XP penalty chip; the `home-unplayed` fixture kept the real run's route, kind and a level from two sources; the release note named "the Farming cards session" and left the XP out. Done: the hero draws the trio's top-class chip with its tip at every level; the fixture has a level-1 route, a "your last relevel" kind and one level; the note says "card's session" and "and its XP to level".
