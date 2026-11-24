# What you want first: the two ranked lists on Campaign

**Built 11-19, behind the beta switch: one card, What you want first, with the ranked classes and races, their pips, Spread out | Unlock now and the engine's price and clash lines, the Pathfinder places and Don't care chips, drag and the ⋯ menu; the race tile in place of the race grid; the ready-made goals in the preset order; what came across as its own list** (the second half of [campaign-first-lane](campaign-first-lane.md), the design page's
"E · What you want first"). The design is done: campaign-first-lane's "What the player sees → Branch B" is the source,
with its mock-up. The `engineer` writes the rest of this spec, the failing tests and the code; the `architect` and the
`designer` review. **Tier:** standard with the designer: the store, the planner and every engine word are on main
from campaign-first-lane; this draws them. **Built behind** [campaign-planner-beta](campaign-planner-beta.md)'s
switch: without it, Campaign is as today. **Waits for:** campaign-first-lane. **Unblocks:** nothing.

**Why:** after campaign-first-lane, the plan already keeps a ranked list of classes and one of races, each with a
target and Spread out or Unlock now, but a beta player still sets them through today's race pips and "Earn these
first". After, one card, **What you want first**, shows both lists: rank a class or a race in one click, set its pips
and its timing, see its price before it's set, and leave the rest to Pathfinder.
**Players:** everyone with a campaign who turned on the beta. Worst for a player who wants only a few races or one
class right now.

```text
Plan → Campaign (made-up plan; the full picture is .webp)

Today (beta)                                    After
Race passives: a pip row per race (16)          Race passives: 3 of 8 ranks you want, a cell per ranked race
More options → Earn these first: up to 3        ⟦What you want first⟧
                                                ⟦Classes  1 ROG ●●● Spread out | Unlock now  ⋯⟧
                                                ⟦         Runs 10–12, with your Must SHD / ROG / SHM …⟧
                                                ⟦         Pathfinder places (14): BRD BST BER …⟧
                                                ⟦Races    1 Troll ●●● …  2 Iksar ●●○ …  3 Half Elf ●○○⟧
                                                ⟦         Don't care (13): …⟧
```

## Done when (what a screenshot shows)

- [x] What you want first between the campaign card and Your combos: two ranked classes (one Unlock now with its
  line, one with no line), "Pathfinder places (14)" chips - seen in main-campaign-first.png
- [x] The race list: three ranked races (an Unlock now, a Spread out with Unlock now's price, one with no line), the
  header "… race ranks to earn · they fit in the … runs your classes need", Don't care chips with pips and a locked
  chip, the foot - seen in main-campaign-first.png
- [x] The race tile: "N of M ranks you want", the ranked races' cells, "… races ranked · … don't care · all by run …",
  Rank races; no Race passives grid; no "Earn these first" in More options - seen in main-campaign-first.png
- [x] Nothing ranked: both empty lines, the race tile's "race ranks earned" - seen in main-campaign-first-empty.png
- [x] Every race 3/3: 16 rows in the preset's order, the header's "more runs than your classes need" - seen in
  main-campaign-first-all.png
- [x] Two Unlock now races ("after Troll") and a class row's clash with its two fixes - seen in
  main-campaign-first-clash.png
- [x] A row's ⋯ menu: Move up, Move down, Leave it to Pathfinder / Don't care - seen in main-campaign-first-menu.png
- [x] No plan yet: the card under What you want, rows with no price line - seen in main-campaign-start-first.png
- [x] Your plan came across with the First and race-goal lines - seen in main-campaign-across-first.png
- [x] At 1000 px: the seg groups wrap under the names, nothing touches - seen in main-campaign-first-narrow.png
- [x] Beta off: Campaign as today - seen in main-campaign.png

## Done when (the engine, part 1; tests in CampaignFirstLanePageEngineTests, FirstLaneStoreTests, PageDataFirstLaneTests)

- [x] A plan sent with its lists keeps them as sent, and `First` is rewritten from them - `AnEarnTheseFirstEditIsPlannedFromTheNewListsNotTheOldOnes`, `ARaceWhoseGoalIsTakenAwayLeavesTheListAndTheRestKeepTheirOrder`
- [x] Five ranked classes stay when `First` differs; `First` is the projection - `FiveRankedClassesStayWhenAnEditCarriesADifferentFirst`
- [x] `firstLane.everyRace`: 16 races, ranked first, then fewest ranks, game order, locked last - `EveryRaceIsTheRankedRacesThenTheFewestRanksThenTheGamesOrderWithLockedLast`, `TheFirstLaneIsInTheCampaignDataOnlyForTheNewPlannerWithARankedPlan`
- [x] The preset tiles are priced as the click builds them - `TheReadyMadeGoalsArePricedAsTheClickBuildsThem`
- [x] A Spread out row carries its clash; an Unlock now row does not - `ASpreadOutRowThatWouldClashWithAPinCarriesItsClash`, `AnUnlockNowRowAndARowWithNothingToClashWithCarryNoClash`
- [x] What came across is `AcrossLines`, what was left out stays in `CardLines` - `WhatCameAcrossIsItsOwnListOfLinesAndWhatWasLeftOutStaysInTheCard`
- [x] All 16 races come across as one sentence - `AllSixteenRacesComeAcrossAsOneSentenceNotSixteenNames`

**Engine edge cases:** a plan with no lists (`Ranked` false) keeps A's path and its `First` as sent; a goal of 0 or a deleted race goal drops the entry; a locked race that is ranked stays in its rank place in `everyRace`; Every class 3/3's preset ranks no race and leaves `classOrder` as the plan has it (the click does the same; don't-care races fill); `_presetCache` is keyed with the side and `Locked`, and with the class and race order only while the plan has no runs (only the start page draws the ranked presets, so a rank, reorder or timing click on a plan with runs builds nothing again). Where: `PlannerSwitch.cs` (`WithLists`, `RankLines`, `NewPlannerView`), `CampaignFirstLane.cs`, `Campaign.cs` (`Presets`), `TrackerService.cs`.

## Carried from campaign-first-lane's reviews

- **Decisions this branch needs (architect):** whenever `First` differs from the stored one, `PlannerSwitch.Store`
  rebuilds `ClassOrder` from First's classes alone, at most 3. Once this page sends more than 3 ranked classes, a
  change to First would cut the list down. Settle how the page's lists and First stay one thing.
- **The came-across lines (designer):** campaign-first-lane keeps the ranked-list lines off the "came across" card
  until this page draws the lists; this branch turns them on. An "Everything 3/3" plan comes across as a 16-name list:
  when all 16 come across, say "All 16 races are ranked now, the fewest ranks first." Put these lines under a label of
  their own: they say what did come across, not what didn't.

## Where

- `src/EqlPathfinder.App/web/js/campaign.js`: `campGoalTiles` (the race tile), `campOptionsPanel` ("Earn these
  first" goes), `campPresetBtn`, the start page (`campStart`)
- `src/EqlPathfinder.App/web/js/campcombos.js`: the row, seg group and ⋯ menu patterns to reuse
- `src/EqlPathfinder.App/web/app.css`: `.first`, `.first-sec`, `.first-row`, `.first-rest`, `.first-glance`

## Decisions

Architect, 11-19 (from 8a9d68a). Local only, as in campaign-first-lane: the lists live in settings.json, the new words
travel in the page data inside the window, nothing new leaves the PC (`reread: false`, `job: false`, privacy.html
stands). Undoable: a build without this page reads the lists and `First` as A left them (below).

**The lists and `First` stay one thing: on the new side the page writes only the lists, and `First` follows them.**

- **The page sends the lists, never `first`, on the new side.** Every edit carries `classOrder`/`raceOrder` as entries
  `{ key, unlockNow }` (the engine writes `target` from the goals in `Normalized`), plus the goals:
  - rank a class chip: append `{key}`, and set `classes[key]` to 3 when it had no goal;
  - rank a race chip: append `{key}`, and set `races[key]` to 3;
  - drag or Move up/down: reorder the array;
  - Spread out | Unlock now: set `unlockNow`, and Unlock now below 3/3 also sets the goal to 3 in the same edit;
  - a row's pips: the goal itself (`classes`/`races`), so the row and the grid cell are one value by construction; a
    goal of 0 unranks it (`Normalized` drops it);
  - Leave it to Pathfinder: remove the class entry, keep its goal;
  - Don't care: remove the race entry **and** delete `races[key]`, or `Normalized` puts it back at the bottom (every race
    with a goal is ranked);
  - Clear goals: both lists `[]` and both goal maps `{}`.

  Each edit goes through `campGoalsChanged` (a rebuild when there are runs), as goals do today. More options → "Earn
  these first" (`campOptionsPanel`, `data-first-add`/`-del`) is drawn on the old side only.
- **`PlannerSwitch.WithLists` (and so `Store`): a plan that arrives with its lists keeps them as sent, and `First` is
  rewritten from them.** Only a plan with no lists (`Ranked` false: a test's `PageEdit`, a hand edit) still takes A's
  path: its `First` orders the lists against the stored ones. The "First differs → `ClassOrder` is First's classes" cut
  never runs on a page edit again, so 16 ranked classes stay 16. One writer for each: whoever sent the lists owns the
  order, and `First` is derived from it.
- **`First` on the new side is a projection for older apps, written by the engine only:** the ranked classes in rank
  order, then the ranked races in rank order, the first `MaxFirst` (3). Not read by the planner (a ranked plan reads
  its lists). 0.10.22 reading the new plan sees true goals and those three as Earn these first; when it saves, the
  lists go (accepted in A), and `PlanFor` derives them back: classes past the third and every Unlock now are lost, and
  races past First fall back to the game's order. Only a hand-installed older build does this.
- **A's tests that describe today's page change with it.** `AnEarnTheseFirstEditIsPlannedFromTheNewListsNotTheOldOnes`
  and `ARaceWhoseGoalIsTakenAwayLeavesTheListAndTheRestKeepTheirOrder` edit `First` on a plan that has lists. Their
  purpose stays and their action becomes the page's: a class added to `ClassOrder` is planned first; a race whose goal
  goes and a class taken out of `ClassOrder` leave their lists, the rest keep their order, and `First` follows.
  `TodaysPageEditsAreKeptAsRankedListsAndReadBackAsBefore` sends no lists, so it stands as written. New store test: five
  ranked classes and a `First` that differs are sent; all five stay, and `First` is the projection.

**Ready-made goals write the preset's order, and the engine works it out.**

- **`firstLane.everyRace`**: the 16 race keys in the preset's order (ranked races in rank order, then the fewest ranks
  now, then the game's order, locked last), built in `FirstLane` from `raceRanks` and `Locked` (rule 4: an order from
  the player's data is the engine's). Every race 3/3 and the start page's Everything 3/3 set every race goal to 3 and
  write `raceOrder` from it. Off again: `races` `{}`, `raceOrder` `[]`. Every class 3/3 (either page) leaves
  `classOrder` alone: on, every goal is 3, so nothing drops; off, `Normalized` empties it with the goals. The start
  page's Every class 3/3 writes `races` `{}` and `raceOrder` `[]`. Choose my own writes nothing.
- **The start page's tiles price the plan the click builds.** On the new side `Campaign.Presets` builds ranked preset
  plans: "all" with `RaceOrder` from the same order, and "classes" with `RaceOrder` `[]` (don't-care races fill at the
  least XP, which the unranked old path doesn't do). `TrackerService.CampaignData`'s `_presetCache` key gets the side
  (`roles != null`) and `Locked`, so a flip of the switch doesn't show the other side's tiles.

**The clash rides on the row, with no new message.** `FirstPrice`'s clash half (the pinned loadouts with runs to go:
one that seats the class or is the race, with a pinned run without it after) moves into a helper that `FirstLane` calls
for each Spread out row that isn't done or locked. It needs no rebuild, so it's outside the fuse. `CampaignFirstRow`
already has `Clash`/`ClashLoadout`. A click on Unlock now draws the row's clash at once, if it has one, and sends
nothing. **Unlock now and unpin loadout N** sends one edit: `unlockNow`, the goal to 3 when below, and
`loadouts[ClashLoadout].pinned = false`, rebuilt. **Keep it Spread out** sends nothing. `FirstPrice` stays as the
engine's click price that A's tests use, and no page message is wired to it.

**The came-across lines: on, under a label of their own.** `RankLines` runs with the lists shown, and splits:
- What came across (Earn these first → your ranked lists, the race goals ranked, a ranked race still locked) goes to a
  new `NewPlannerView.AcrossLines`. campaign.js draws it under its own label, after "What didn't come across".
- What didn't ("… isn't a race in the game, so it was left out", the pins) stays in `CardLines`.
- When `RacesAcross.Order` holds all 16: "All 16 races are ranked now, the fewest ranks first." (or "in the game's
  order."), in place of the 16 names, and no don't-care sentence.

**The build, three engineers in order** (one worktree, and campaign.js shared, so not in parallel; each ends in a commit):
1. **The engine** (C# only): `WithLists`/`Store` as above with the `First` projection, A's two tests rewritten and the
   new store test, `everyRace`, the ranked `Presets` and their cache key, the clash on rows, `AcrossLines` and the
   16-race line, `PageDataTests` for the new fields. The spec's engine "Done when" lines and tests first. Ends green.
2. **The card** (campaign.js, a new `campfirst.js` if campaign.js would pass ~900 lines, app.css): What you want first
   on both pages, rows, chips, pips as goals, the seg group, the ⋯ menu, drag, the edits listed above, the price and
   clash lines drawn, the empty states, `.stale`/"Working it out…". "Earn these first" goes from the new side.
   Screenshots: main-campaign-first, -first-empty, -first-menu, -first-clash, -first-narrow.
3. **Around the card:** the race tile (`campGoalTiles`), the presets and the start page, the came-across label, the
   release note, docs/code-map.md. Screenshots: -first-all, -start-first, -across-first, and main-campaign with the
   beta off. Then the designer reviews the ten, and the architect reviews the diff.

## Reviews

- architect: must fix: the card's edits had no test. Done: `CampaignFirstCardEditsTests` (CampaignPageTests.cs) runs the card on the new side and reads what is posted after ranking a class, Don't care on a race, Clear goals, Every race 3/3 (order equals `firstLane.everyRace`, goals 3) and the start page's Everything 3/3. They passed on first run (the behaviour was built), so they were not seen failing. should fix: the "all" price test now checks `LastXp`; `_presetCache` leaves the class and race order out of its key while the plan has runs (`APlanWithRunsKeepsItsPresetsWhenOnlyTheRanksOfItsListsMove`, seen failing first); the engine edge-cases line above says what is built. Debt line 5f29736 stands.
- designer: must fix: every shot re-rendered from the committed build (main-campaign-start-first.png no longer shows the Don't care line without a plan); the snapshot run finished and main-js-errors.txt reads `[]`, with no main-error.txt. should fix: "Unlock now: run 10, the next one." for one run, plain run number when that one is not the next (`AnUnlockNowRowWithOneRunSaysItIsTheNextOneNotBackToBack`); the race line reads "Iksar first, as in Earn these first, then the fewest ranks first: ..." (`WhatCameAcrossIsItsOwnListOfLinesAndWhatWasLeftOutStaysInTheCard`); two states added to DevSnapshots: main-campaign-first-locked.png (ranked locked race, Unlock now disabled) and main-campaign-first-done.png (the done line is patched in by the shot's script, because the demo has played no reincarnation). ideas: left.
- lead's call: Every race 3/3 keeps the Unlock now of races already ranked; campaign-first-lane.md's wording follows.
- lead: the fix engineer stopped at its turn limit with one test tweak uncommitted (committed, its comment moved to the step it explains, the test passes); read the fixes and the re-rendered shots (main-campaign-start-first.png, main-campaign-first.png, main-js-errors.txt `[]`, rendered after the last code commit); no second review, since no must fix changed how the card works.
