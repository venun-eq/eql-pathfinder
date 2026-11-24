# Everything 3/3 leaves your race list empty: a race can be a goal without a rank

**Built 11-20** ([campaign-page-redesign](campaign-page-redesign.md)): the `designer`'s states, `qa`'s spec and failing
tests, the `architect`'s decisions, two `engineer` parts (the engine and store, then the page), and both reviews. **Tier:** risky: it changes what each plan stores (a race's goal no longer puts it in the ranked list), how the
planner orders races, and what a plan saved by 0.10.23 reads as. **Built behind** the beta switch
([campaign-planner-beta](campaign-planner-beta.md)): without it, Campaign is as today. **Waits for:** nothing.
**Unblocks:** campaign-came-across-rebuild. It goes side by side with campaign-page-layout's branch; this one
makes the race tile's cells buttons again, and whichever finishes second carries that into the other's tile.

**Why:** after Everything 3/3, Races you want, most wanted first lists all 16 races, each with Spread out | Unlock
now: 1,100 px before the first run, and none of it chosen by the player. The classes start empty after the same
click. It's because a race's goal is set only by ranking it. After, Everything 3/3 sets every race's goal to 3/3 and
ranks none: the race list starts empty, as the class list does, and Pathfinder plays each race where it costs the
least XP. A race's goal is set on the race tile, as a class's is; a race in the list is one the player put there.
**Players:** everyone on the beta with a campaign. Worst for a new player, handed 16 rows to read after one click.

```text
Plan → Campaign after Everything 3/3 (today: today-full-1.png; after: the chosen page design's mock-up)

Today                                              After
Race passives  9 of 48 ranks you want              Race passives  9 of 48 ranks you want
[HIE ◎◎◎] [DEF ◎◎◎] … read-only cells              [HIE ●◎◎] [DEF ◎◎◎] …   ⟦click a pip to set the goal, as classes⟧
⟦Every race ranked at 3/3: yours first, …⟧         Races you want first
Races you want, most wanted first                  ⟦Nothing ranked: Pathfinder plays each race where it costs⟧
 1 High Elf  ◎◎◎  Spread out | Unlock now  ⋯        ⟦the least XP. Click one below to earn it first or unlock it now.⟧
 2 Dark Elf  ◎◎◎  Spread out | Unlock now  ⋯       Pathfinder places (16): HIE DEF HEF DWF TRL …
 … 16 rows                                         (Your combos follows straight after)
```

## The cause

- `CampaignPlan.Normalized` appends every race with a goal to `RaceOrder` (Campaign.cs, "every race with a goal is
  ranked"). The class list has no such step, which is why classes already work this way.
- Everything 3/3 writes all 16 into `RaceOrder` (`Campaign.Presets` with `EveryRace`; `firstEveryRace` on the page).
- campaign-first-lane-page made a ranked row's pips the only place a race goal is set, and the race tile read-only.

## What the user decided (11-19)

- "It should start like the classes: nothing ranked, and Pathfinder does its thing. The rule: the zero state is the
  default, and the plan holds only what I override." (design.md rule 23)
- Kept: a rank only breaks ties; Unlock now is held like a Must, with its price (campaign-combos.md).

## What it means for the plan

| A race that is… | Stored | The planner |
|---|---|---|
| Not a goal, not ranked | nothing | don't care, as today: fills a run where that costs the least, never a goal |
| A goal, not ranked (new) | its goal | Pathfinder's to place: earned where it costs the least XP, the order its own |
| Ranked, Spread out | goal, and its place in the list | as today: breaks ties ahead of unranked races |
| Ranked, Unlock now | goal, place, timing | as today: held like a Must, at its price |

- Ranking a race with no goal gives it 3/3, as today. Taking a race out of the list keeps its goal (today it clears
  it); the goal is cleared on the tile.
- The race seat, among the races that cost the least XP in a run: a ranked race still short (rank order), then an
  unranked race still short of its goal (fewest ranks now, then the game's order), then the rest. Race runs past the
  classes: Unlock now races, ranked races, then the other goals in the same order, locked last. That's the order
  Everything 3/3 writes today, so its runs and XP don't change.
- A plan brought across from the old planner keeps its race goals as goals and ranks only Earn these first's races
  (the "came across" card's race line changes to say so).

## A plan saved by 0.10.23 (the user, 11-20)

A plan where Everything 3/3 ranked all 16 still has them in its list. Read once, on the first load after this ships:
a list that holds all 16 races, all at 3/3 and none Unlock now, is what the preset wrote, so the list empties and the
goals stay. The race list says it once: "Your race list was every race at 3/3: that's your goal now, and Pathfinder
picks the order. Rank a race to put it first." Any other list stays as stored (it can't be told from one ranked by
hand). The user took this over leaving every saved list as it is ("I agree with all of your picks").

## What the player sees

On today's page (the tiles, then the What you want first card). campaign-page-layout moves both into the side column
and owns the tiles' colours and rings; the words below carry over unchanged. No new layout, so no mock-up: the race
tile becomes the class tile's twin, and the race list becomes the class list's twin.

**Changed from the draft's picture, and why:** the empty list's line is the class list's own sentence with "race"
("Pathfinder places every race the cheapest way"), because "where it costs the least XP" is only half true when the
races are goals. The list's right-hand header is "The rest are Pathfinder's to place", as the classes', and the
race-rank count that sat there goes: it counted ranked races only, which now reads as a fraction of the goal, and the
Cheapest path line already says "39 race ranks to earn". The tile's "Rank races" link goes: it pointed to where race
goals were set, and they're set on the tile now.

### The race tile (every state)

The 16 cells are pip buttons, exactly as the class tile's: click a pip for that rank, click your goal's pip again to
lower it, and the tile's tooltip and aria words are the class tile's ("High Elf: has 1/3, this plan takes it to 3/3;
your goal 3/3"; "High Elf: want 3/3" / "your goal is 3/3, click to lower it"). No "Set in What you want first.".

```text
Empty plan, a regular with 9 race ranks              After Everything 3/3 (planned)
RACE PASSIVES                                         RACE PASSIVES
9 race ranks earned                                   9 of 48 ranks you want
[HUM ●○○] [BAR ○○○] … 16 cells, every pip a button    [HUM ●◎◎] [BAR ◎◎◎] … a ring on every rank wanted
No goals here yet.                                    0 of 16 at your goal · all by run 48
```

- **No race is a goal:** the big line stays today's "9 race ranks earned" (never "0 of 0 ranks you want", rule 4),
  and the cells show now. Foot: "No goals here yet." (the class tile's).
- **Some race goals:** "N of M ranks you want", and the class tile's foot: unplanned "3 goals"; planned "1 of 3 at your
  goal · all by run 20".
- **The hint under the tiles** is the old planner's, for both tiles: "Click a pip for the rank you want: a gold ring
  marks your goal. Click your goal's pip again to lower it." ("Races are ranked below." goes.)
- **Lowering a ranked race's goal to nothing** on the tile takes it out of the list, as a class's does.

### Races you want first (the list), after Everything 3/3, and on an empty plan

```text
Races you want first                                                         The rest are Pathfinder's to place
Ranked races are earned first wherever that's free. Unlock now plays a race's runs back to back, one race at a time,
held like a Must, at the price shown.
 Nothing ranked: Pathfinder places every race the cheapest way. Click one below to earn it first, or to unlock it now.
PATHFINDER PLACES (15) · CLICK ONE TO RANK IT
[HUM] [BAR] [ERU] [ELF] [DEF] [HEF] [DWF] [TRL] [OGR] [HFL] [GNM] [IKS] [KRN] [FRG · locked] [HDE]
3/3 already: HIE
```

- The heading is "Races you want first" (was "Races you want, most wanted first"), the twin of "Classes you want first".
- The intro is the classes' with "race" ("Only the races you rank are goals." is no longer true and goes).
- The chips are the race abbreviations (rule 14, and the same chips as the classes'), in the game's order, the full
  name in the tooltip. A race not yet unlocked reads "FRG · locked", tooltip "You haven't unlocked Froglok yet: it's
  planned last". A race at 3/3 has no chip: it's listed in the dim "3/3 already: HIE" line, as classes are.
- "Don't care (N)" and its "they fill your other runs…" line go, and so does the flash "Every race ranked at 3/3: yours
  first, then the fewest ranks first. Drag to reorder.": Everything 3/3 says nothing; the rings on the tile show it.
- Reused: `.first-sec`, `.first-intro`, `.first-none`, `.first-rest .chip`, `.chip.locked`, `.lbl`. Nothing new.

### A ranked race

Clicking a chip ranks it, at the bottom of the list. A race with a goal keeps it (HIE at 2/3 is ranked at 2/3); one
with none gets 3/3, and its tile cell gains its rings. No flash: the row appears where the player is looking.

```text
Races you want first                                                         The rest are Pathfinder's to place
…intro…
 1  High Elf  ◎◎◎  [Spread out | Unlock now]                                                                    ⋯
    (the row's price line, as today)
 2  Troll     ◎◎◎  [Spread out | Unlock now]                                                                    ⋯
PATHFINDER PLACES (14) · CLICK ONE TO RANK IT
[HUM] [BAR] …
Drag a row's number, or use its ⋯, to reorder.
```

- The row is today's: its pips, Spread out | Unlock now, its price line and its ⋯. A row's pips and the tile's pips
  are one goal: a click on either shows on both.
- "Drag a row's number, or use its ⋯, to reorder." shows with two rows or more ("Click a race to rank it." goes: the
  chips' label says it).
- Unlock now on a race below 3/3 raises it, as today: "High Elf's goal is 3/3 now. Click a pip to stop sooner."

### A race taken out of the list

The row's ⋯ reads as a class's:

```text
Move up
Move down
Leave it to Pathfinder
  Unranks High Elf. Its goal stays 3/3.
```

The row goes, its chip comes back under Pathfinder places, the tile keeps its rings, and the card's flash says
"High Elf is Pathfinder's to place again." (the class one's words, with the race's full name). "Don't care … takes its
goal away" goes: the goal is cleared on the tile.

The tools row's "Every race 3/3" (until campaign-goal-presets removes it): on sets every race's goal to 3/3 and leaves
the list as it is; off clears every race goal and empties the list (a rank needs a goal). Neither shows a flash
("Every race is don't care again." goes).

### A plan saved by 0.10.23, the first time Campaign opens

```text
Races you want first                                                         The rest are Pathfinder's to place
…intro…
┃ Your race list was every race at 3/3: that's your goal now, and Pathfinder picks the order. Rank a race to put it first.
PATHFINDER PLACES (15) · CLICK ONE TO RANK IT
[HUM] [BAR] …
```

- The user's words, exactly, in a `.banner.flash` (role="status") in place of the "Nothing ranked" line. Plain, not
  `--warn` (rule 21: nothing broke).
- It shows the first time the player opens Campaign after the update, whenever that is, and stays until they change
  the plan or leave Campaign. It never shows again, and never on a list that stayed as stored.
- The tile shows all 16 races with their rings; runs and XP are as before.

### The "came across" card

The race line goes. Today it reads "Your race goals are ranked now, the fewest ranks first: …" or "All 16 races are
ranked now, …", with "The N races with no goal are don't care: …". After, the race goals came across as they were,
which the card's first sentence already says ("Your goals, runs, pins and the runs you've played are as you left
them"). A line saying nothing changed is noise (design.md). Under "What came across as your ranked lists" stay:

- "Earn these first is your ranked lists now: ROG first among your classes, Iksar first among your races. Neither is
  set to Unlock now." (unchanged; with no Earn these first, the section doesn't show)
- "Iksar is ranked and still locked: it's planned last until you unlock it." only for a ranked race (from Earn these
  first) that's locked.

A card still waiting for Got it from 0.10.23 loses its race line too: "All 16 races are ranked now" would be false
once the list empties.

### The players

- **New, log just turned on:** the race tile shows its 16 cells to click, with "0 race ranks earned" (today's words).
  After Everything 3/3 the list is one line and 16 chips, not 16 rows.
- **New to the app, old to the game:** nothing new; the ranks are a guess until they set their counts, as today.
- **The regular** with a hand-ranked list: it stays, row for row. One that Everything 3/3 wrote empties once, with the
  line above.
- **Narrow, 150%:** abbreviation chips wrap in two rows at 906 px; nothing on the row changes.

## Done when

The plan (`CampaignRaceGoalsTests`, `FirstLaneStoreTests`):
- [x] A race's goal doesn't rank it: a plan with goals for Troll and Iksar and only Troll ranked reads back with only
  Troll ranked and both goals - `ARaceGoalWithoutARankStaysOutOfTheList`
- [x] Taking a race out of the list keeps its goal - `TakingARaceOutOfTheListKeepsItsGoal`
- [x] Unranked race goals are earned the fewest ranks first, then in the game's order, locked last -
  `UnrankedRaceGoalsGoFewestRanksFirstThenTheGamesOrder`
- [x] A ranked race goes ahead of an unranked goal that costs the same, in the class runs and the race runs -
  `ARankedRaceGoesAheadOfAnUnrankedGoalThatCostsTheSame` (passes today: seen to fail with the rank's tie-break taken out)
- [x] Everything 3/3 with nothing ranked plans 0.10.23's runs, run for run, at its XP to level, and its price is that
  plan's - `EverythingThreeOfThreeWithNothingRankedPlansTheRunsItDidWhenItRankedEveryRace`,
  `TheReadyMadeGoalsArePricedAsTheClickBuildsThem`
- [x] A plan saved by 0.10.23 with all 16 races ranked at 3/3, none Unlock now, reads with its race list empty and every
  goal, class list and run kept; a list of 16 ranked by hand afterwards stays -
  `APlanSavedWithEveryRaceRankedAtThreeOfThreeReadsWithItsListEmptyOnce`
- [x] 15 ranked, one Unlock now, or one at 2/3: the list stays as stored, with no line - `AnyOtherSavedRaceListStaysAsStored`
- [x] The one-time line is in the page data from that first read until the plan changes or Campaign is left, and never
  again - `TheEmptiedListSaysSoUntilThePlanChangesOrCampaignIsLeft`
- [x] An old plan comes across with only Earn these first's races ranked and its other race goals kept; the card has no
  race goals line, also on a card from 0.10.23 still waiting for Got it; a locked race from Earn these first still says
  it's planned last - `AnOldPlanComesAcrossWithOnlyEarnTheseFirstRankedAndItsOtherRaceGoalsKept`,
  `NoLineSaysTheRaceGoalsCameAcross`, `ALockedRaceFromEarnTheseFirstStillSaysItIsPlannedLast`

The page (`CampaignRaceGoalsPageTests`):
- [x] A race tile pip sets that race's goal and ranks nothing; lowering a ranked race's goal to nothing on the tile takes
  it out of the list - `TheRaceTilesPipsSetGoalsAndRankNothing`
- [x] With no race goal: "N race ranks earned", "No goals here yet." and the old planner's hint under the tiles -
  `WithNoRaceGoalTheTileSaysTheRanksEarnedAndHowToSetOne`
- [x] Everything 3/3 and Every race 3/3 set every race's goal and rank none (Every race 3/3 leaves a ranked list as it
  is; off, it clears the goals and the list), with no flash - `TheReadyMadeGoalsSetRaceGoalsAndRankNone`
  (campaign-page-layout later removed the Every race 3/3 toggle and its "off" run: Everything 3/3 is the one ready-made goal that sets races.)
- [x] The empty list: "Races you want first", "The rest are Pathfinder's to place", the "Nothing ranked" line, Pathfinder
  places (N) as abbreviation chips, "FRG · locked", "3/3 already: HIE", no Don't care - `TheEmptyRaceListReadsAsTheClassList`
- [x] Ranked rows with "Drag a row's number, or use its ⋯, to reorder."; a chip ranks its race keeping its goal, or at
  3/3 with none - `ARankedRaceIsARowAndAChipRanksIt`
- [x] The row's ⋯ reads "Leave it to Pathfinder / Unranks Troll. Its goal stays 3/3."; the click unranks it, keeps its
  goal, and says "Troll is Pathfinder's to place again." - `LeaveItToPathfinderUnranksARaceAndKeepsItsGoal`
- [x] The one-time line in place of "Nothing ranked"; gone after a change to the plan, and after leaving Campaign and
  coming back - `TheOneTimeLineShowsUntilThePlanChangesOrCampaignIsLeft`
- [x] Every race cell ringed after Everything 3/3 - seen in campaign-first-all.png; the one-time line - seen in
  campaign-first-once.png; the came-across card with no race line, its lines from the engine - seen in campaign-across.png

## Edge cases

- **Nothing yet** (new, log on today): the tile shows 16 cells to click and "0 race ranks earned", never "0 of 0".
- **Play the log never saw:** "fewest ranks first" reads the ranks the counts give; without counts every race is 0 and
  the game's order decides, as Everything 3/3 does today.
- **Old versions in the field:** an older app reads `First` and the goals only; `First` stays the lists' projection, so
  an emptied race list leaves races out of it. A downgrade to 0.10.23 ranks every goal again, as it always did, and
  still reads the plan. Fields are added, none given a new meaning.
- **A second copy / re-reads:** the emptying is decided once. A second read, a re-read of the log, a self-test or an
  isolated copy never empties a list again, and never empties one saved after the update.
- **Starting and stopping:** closing the app between the read and the first look at Campaign keeps the line for next time.
- **The switch off, or the old plan:** untouched (it has no lists). Start again from the old plan brings it across with
  only Earn these first's races ranked.
- **A race locked and ranked:** stays where the player put it in the list, planned last, as today.
- **Narrow, 150%:** the abbreviation chips wrap; no row changes.

## Not doing

- Any layout, preset or tile colour (campaign-page-layout's branch). Whichever branch finishes second carries the race
  tile's pip buttons into the other's tile.
- Telling a hand-ranked list of 16 at 3/3 from the preset's: it can't be told, and the user chose to empty it.
- A line saying race goals came across (the designer's pick, 11-20: the card's first sentence says it).

## Where

- `Engine/Analysis/Campaign.cs`: `CampaignPlan.Normalized` (stop appending goals to `RaceOrder`), `Generate`'s race seat
  (`RaceForClassRun`) and race runs (`raceOrder`), `Presets`, `EveryRace`
- `Engine/Analysis/PlannerSwitch.cs`: `PlanFor`/`Store` (the one-time read), `ComeAcross`, `RankLines`, `NewPlannerView`
  (`RaceListEmptied`, a stub), `RaceListEmptiedSeen` (a stub); `App.xaml.cs` calls it when Campaign is left
- `Engine/Analysis/CampaignFirstLane.cs`: `RaceHeader` and `DontCare` are no longer drawn; they go, with
  `TheRaceHeaderSaysHowManyRanksAndWhetherTheyFit` and `TheDontCareLineSaysWhichRunsTheyFill`
- `App/web/js/campfirst.js`: `firstRanked` (a goal with no place isn't ranked), `firstEveryRace`, the race section,
  chips and menu, `unrank`; `campaign.js`: `campGoalTiles`, the hint, `campClick`'s presets and Every race 3/3
- Tests changed to the new rule: `FirstLaneStoreTests` (the lists now hold only ranked races), `PlannerSwitchTests.CameAcross`,
  `CampaignFirstCardEditsTests` (Don't care, Every race 3/3, Everything 3/3), `TheReadyMadeGoalsArePricedAsTheClickBuildsThem`

## Decisions

The architect, 11-20, before code.

- **What's stored: one field, `NewCampaignPlan.Lists` (`int?`).** Absent: saved by 0.10.23 or older, whose race list
  ranked every goal. `1`: a race goal isn't a rank. This version writes `1` on every write of a new plan (`Store`,
  `ComeAcross` from `SetOn` and `StartAgain`). It must be nullable with no initializer: System.Text.Json keeps an
  initializer's value when the field is absent, which would read every old plan as new. No database, no re-read.
- **The emptying is worked out on read, and written only by the player.** One private read of the stored entry's plan
  (used by `PlanFor`, `WithLists`' `stored` and `PageInput`) empties the race list when `Lists` is absent and the stored
  list is all 16 races, each goal 3, none Unlock now (order ignored, the user's rule). It sets `RaceOrder = []`, never
  null (so `Ranked` stays true), rewrites `First` from the lists, and keeps the loadouts as stored (no rebuild).
  `PlanFor` stays "never written by a read": a second read, a re-read of the log, a self-test or an isolated copy
  (its own settings.json, rule 5) decide the same thing again and write nothing, and closing the app before Campaign is
  seen keeps the line by itself.
- **The one-time line is that same test, not a stored flag.** `RaceListEmptied` is set when the entry has no `Lists`
  and the read emptied its list (switch on, current character). It goes when the plan is written: any `Store` (an
  edit, a rebuild, a roles change) stamps `Lists`, and `RaceListEmptiedSeen` stores the emptied plan as read, stamped,
  changing nothing else. The flag reaches the view as an init property on `PlannerInput`, so its positional calls stay.
- **"Campaign is left" is a message the page sends,** not the usage-count `seen`: when the page leaves Campaign (a rail
  button or the Class roles tab) after drawing the line, it posts `setting` `raceListSeen`, and `ApplySetting` calls
  `RaceListEmptiedSeen` and saves. `TheOneTimeLineShowsUntilThePlanChangesOrCampaignIsLeft` asserts the post; without
  it nothing proves the engine ever hears. Closing the app on Campaign isn't leaving: the line shows once more.
- **Everything 3/3 keeps the races the player ranked** and ranks no other, as Every race 3/3 does and as
  `TheReadyMadeGoalsArePricedAsTheClickBuildsThem` already assumes (rule 23: the plan holds what the player put
  there, and one price per click). `TheReadyMadeGoalsSetRaceGoalsAndRankNone` gets a run from a ranked list to prove it.
- **`RacesAcross` is no longer written or read;** the property goes. A downgraded app with a waiting card finds it absent,
  which it already reads as "across before the ranked lists".
- **Who else reads it:** settings.json is local. An older app ignores `Lists` and drops it on save; a downgrade to
  0.10.23 ranks every goal again, and coming back up empties that list again with the line, as the rule says. `First`
  keeps its meaning (the lists' projection). The NAS job and the server never see the plan: `job: false`, `reread: false`.
- **What's sent:** nothing new. No usage count for the emptying.
- **Taking it back:** once a player edits or leaves Campaign, the emptied list is gone from their file; a later version
  can't tell it was ever there. The goals and runs are kept, and the user chose this (11-20), so it stands.
- **How updates arrive, limits, the server:** not touched.

## Reviews

- designer: no must fix; the screens match. Should fix: the release note's "plays each where it costs the least XP",
  "runs and XP" and a guess stated as fact ("If Everything 3/3 had ranked all 16"). Fixed by the lead. Three findings
  outside this branch went to docs/debt.md: the narrow shot's carried-over Troll rank, the class list's missing reorder
  line, and the unread `firstLane.everyRace`.
- architect: must fix: the page's Everything 3/3 dropped the races you ranked (`p.raceOrder = []` before
  `firstEveryRace`), so the button's price was for a plan the click didn't build. Fixed in `campClick`, with a run in
  `TheReadyMadeGoalsSetRaceGoalsAndRankNone` from Troll and Iksar ranked (Iksar on Unlock now) that fails without the
  fix. Should fix: the release note said the list always empties. Fixed. Two stale comments fixed (campfirst.js,
  Campaign.cs).
