# Say which classes and races you want first, and only those

**Built 11-19, behind the beta switch: the ranked lists of classes and races stored with each plan, an old plan brought across with them, the planner placing Unlock now classes and races first and races with no goal at the least XP to level, and every word the page will draw; nothing new on screen until [campaign-first-lane-page](campaign-first-lane-page.md)** ([campaign-combos](campaign-combos.md), the design page's "E · What you want first").
The `designer` goes first, then `qa` writes the rest of the spec and the failing tests, then the architect's
Decisions. **Tier:** risky: it changes what each character's plan stores (the race goals and the "first" list become
ranked lists) and how the planner places races. **Built behind** [campaign-planner-beta](campaign-planner-beta.md)'s
switch: without it, Campaign is as today. **Waits for:** campaign-planner-beta, campaign-your-combos (it shares
Spread out | Unlock now, the prices, and the clash line). **Unblocks:** nothing.

**Why:** today a player sets a goal per race and picks up to three things "first", with no target or timing for each
and no way to say "only these races". A player who wants three races, not 16 at 3/3, and a player who wants ROG
unlocked now with the rest left to Pathfinder, can't say either. After, the player ranks the classes and races they
want, each with a target and Spread out or Unlock now; unranked means "don't care", and Pathfinder fills those at the
least XP to level.
**Players:** everyone with a campaign. Worst for a player who wants only a few races or one class right now.

The new layout is the Campaign Combos design notes' section "E · What you want first". In short:

```text
Plan → Campaign (made-up plan)

Today                                           After
Race passives: a pip row per race (16)          ⟦Classes you want first⟧
More options → First: up to 3                   ⟦1 ROG ●●● Spread out | Unlock now · Runs 1–3. ROG is in your Must⟧
  (class:ROG, race:Iksar)                       ⟦  SHD / ROG / SHM, so that trio moves with it.⟧
                                                ⟦Races you want, most wanted first · 8 race ranks to earn · they fit⟧
                                                ⟦in the 16 runs your classes need⟧
                                                ⟦1 IKS ●●●  2 ERU ●●○  3 HUM ●○○⟧
                                                ⟦Don't care (13): BAR ELF HIE …  (click to rank, drag to reorder)⟧
```

## The cause

A plan has a goal pip row per race and a "first" list of three (`CampaignPlan.First`, "class:MAG" or "race:Iksar",
`MaxFirst = 3`). `Generate` places races by goal (`Prio`, lines ~340–404): every race with a goal counts the same, and
a goal of 0 isn't "don't care", it's "never". Nothing holds a timing per wish.

## What the user decided (11-19)

- **"prefer the cheapest race pathways … let the user rank the races in their order of preference, where they could
  choose not to rank some … plan for users that DON'T plan on getting all races 3/3."** Unranked races are "don't
  care": they fill runs at the least XP.
- **Classes get the same list:** "You can recommend everything, but I want to unlock rog right now." Ranked classes
  each have a target and Spread out or Unlock now; unranked ones are Pathfinder's to place. Must combos still win: if
  a ranked class is in a Must, the combo moves with it, or a clash line offers the fix.
- **Unlock now aims at 3/3,** which unlocks a class's active (the pips set how far "now" goes).
- **Unlock now is held like a Must** (later on 11-19: "Unlock now -> must: yes"): a class or race set to Unlock now
  is placed above the cheapest path, with its price shown before it's set. A rank without Unlock now only breaks ties.
- **Unlock now past a goal wins, and the goal says so,** as for a Must combo (campaign-your-combos: "… so this is
  ignored, you can remove it"). No extra runs for the goal, never raised silently.
- Races and classes are planned separately but share the run list: every run has one race and three classes.

## What the player sees

Only a player with Settings → "Join the beta" on and the new planner on (More options) sees any of this. With either
off, Campaign is today's, and the old plan is untouched.

### Branch A (campaign-first-lane): nothing new on screen

The race pips, Every race 3/3 and More options → "Earn these first" look and read as in 0.10.22; they read and write
the new store. What a beta player can notice: on the next rebuild, a race with no goal is **don't care**, so a run can
take it where that costs the least XP to level (before, a goal of 0 meant "never"). Every word on screen stays true:
the race cell's tooltip already says "not a goal", and "Earn these first" still offers only classes and races with a
goal. The release note says it in one line: "Races you haven't set a goal for now fill runs where that costs the least
XP to level." No came-across line: the plan isn't brought across again.

### Branch B (campaign-first-lane-page): the two ranked lists

![What you want first: two ranked classes and three ranked races, the race tile without its grid](.webp)

*Mock-up in the real window, made-up lists on a real plan (run 10 is next, classes 3/3 by run 16); the price lines
are made up too, so ROG's names a Must that this plan's combos don't have. Changed from the design page: both lists
are one card, **What you want first**, between the campaign card and Your combos, so the class goals (the pip grid)
and the order (the lists) sit one above the other. The race tile keeps its progress (rule 4) with the ranked races
as read-only cells, in place of the grid. "Free" is gone from every line and from the race header (design.md: a thing
that costs nothing says nothing; Your combos dropped it for the same reason). A row whose Spread out and Unlock now both
cost nothing has no line at all (BER, Half Elf). Unranked classes are chips too ("Pathfinder places"), so ranking a
class is one click, like a race.*

**Where.** A new `.card` **What you want first**, right after the campaign card (its goal tiles and tools) and before
Your combos. On the "no plan yet" page (`campStart`) it sits after **What you want** and before Your combos. It
replaces the Race passives pip grid and More options → "Earn these first". The class pip grid stays: it's every
class's goal, and most players want all 16 (Completionist).

#### The race tile (in the goal tiles)

- `.lbl` "Race passives", `.pbig` "3 <span pk>of 8 ranks you want</span>", as today (have up to the goal, over the goals).
- Under it, one read-only `.gcell` per ranked race, in rank order (abbreviation, three pips: have, planned, gold ring
  to the goal). Not buttons: the pips are set in the list.
- `.psub`: "3 races ranked · 13 don't care · all by run 16 · **Rank races**" (`.link`, scrolls to the race list and
  focuses its first chip). "all by run N" is today's `campBy`.
- **No race ranked:** `.pbig` "9 <span pk>race ranks earned</span>" (the sum of ranks now, never "0 of 0"), `.psub`
  "No race is a goal: each run takes the race that costs the least XP to level. · **Rank races**".

#### Classes you want first

- `.h2` **Classes you want first**; right, dim: "The rest are Pathfinder's to place".
- Dim, under it: "Ranked classes are earned first wherever that's free. Unlock now plays a class's runs back to back
  to the rank its pips set, held like a Must, at the price shown."
- A row per ranked class (`.first-row`): the rank number (drag handle, title "Drag to reorder"), the class
  (abbreviation, as in the grid), three `.gpip` buttons, `.seggroup.sm` **Spread out** | **Unlock now**, ⋯ (`.iconbtn`,
  aria-label "More for ROG"), and the price line under it (`.price`).
- **A row's pips ARE the class's goal.** The row and the grid cell are one value: a click on either changes both, at
  once. So Unlock now can never sit past a class's goal, and the goal tile's "past your goal" line never shows for a
  ranked row (it stays for Must combos, as campaign-your-combos built it).
- `.lbl` "Pathfinder places (14) · click one to rank it", then a `.chip` per unranked class, in the game's class
  order. A click adds it at the bottom, Spread out, with its goal as its pips, or 3/3 when it had no goal (the row
  shows it at once). A class that is 3/3 now isn't a chip: dim after the chips, "3/3 already: BRD, WAR".
- **No class ranked:** in place of the rows, `--muted`: "Nothing ranked: Pathfinder places every class the cheapest
  way. Click one below to earn it first, or to unlock it now."

#### Races you want, most wanted first

- `.h2` **Races you want, most wanted first**; right, dim, the engine's header (below).
- Dim, under it: "Only the races you rank are goals. Unlock now plays a race's runs back to back, one race at a time,
  held like a Must, at the price shown."
- A row per ranked race: as a class row, with the race's full name ("Half Elf"). Its pips are the race's goal: the
  only place a race goal is set.
- `.lbl` "Don't care (13) · " and the engine's fill line (below), then a `.chip` per unranked race, in the game's
  race order, each with small `.pips3` (have gold, planned dim: the race's rank at the end of this plan, rule 4). A
  click ranks it at the bottom, Spread out, at 3/3.
  - A race 3/3 now: its chip is disabled, full pips, title "Human is 3/3".
  - A locked race (More options → "Races you haven't unlocked", unchanged): "Froglok · locked", `--faint`, title
    "You haven't unlocked Froglok yet: it's planned last". It can still be ranked.
- Foot, dim: "Click a race to rank it. Drag a row's number, or use its ⋯, to reorder."
- **No race ranked:** in place of the rows, `--muted`: "No race ranked, so none is a goal: each run takes the race that
  costs the least XP to level. Click one below to rank it." No header on the right.

#### A row's ⋯ menu (`.cmenu` in `#campPop`)

- **Move up** / **Move down** (disabled at the ends): the keyboard's way to reorder; drag is the mouse's.
- A class: **Leave it to Pathfinder**, its line "Unranks ROG. Its goal stays 3/3."
- A race: **Don't care**, its line "Unranks Iksar and takes its goal away. Runs take it only where that costs the
  least."
- Either goes at once; the card flashes (`.flash`) "ROG is Pathfinder's to place again." / "Iksar is don't care now."

#### The price line (the engine's words, `.price`, 12px `--muted`)

Each row's price is the plan with everything else you've set, with this row's timing against the other one. The grammar
is Your combos': where it lands, then what the other timing costs. A line appears only for a cost or a choice.

| State | Words |
|---|---|
| Spread out, and Unlock now costs the same or nothing | no line |
| Spread out, Unlock now costs | "It lands in runs 13 and 16. **Unlock now** would play it in runs 12–13, after Troll: run 13 would need +30% XP to level, not +20%." (or "…: +1 run, 17 not 16.") |
| Unlock now set, Spread out costs nothing | "Unlock now: runs 10–11, back to back. Run 11 needs +30% XP to level, not +20%. **Spread out** costs nothing: runs 12 and 15." |
| Unlock now set, the same price either way | "Unlock now: runs 10–12, back to back. **Spread out** costs the same." |
| Unlock now, a class in a Must trio | "Unlock now: run 10, the next one. ROG is in your Must SHD / ROG / SHM, so that trio moves to run 10 with it. The same runs and XP to level, but Completionist 3 comes at run 16, not 15. **Spread out** costs nothing: run 13." |
| Unlock now, below 3/3 | "Unlock now: runs 10–11, back to back, to 2/3. ROG's active unlocks at 3/3." then the other timing's price |
| Spread out, its pips below 3/3 | "It lands in runs 12 and 15. **Unlock now** would raise ROG's goal to 3/3 and play runs 10–12: +1 run, 17 not 16." (the price is the one with 3/3) |
| A second Unlock now race | "Unlock now: runs 12–13, after Troll." (one race a run, so Unlock now races go one after another, in rank order) |
| Unlock now classes together | "Unlock now: runs 10–11, with ROG." (a run seats three classes, so up to three ride together, in rank order; a fourth: "after ROG, BER and SHM") |
| An Unlock now race in the same runs as an Unlock now class | "Unlock now: runs 10–12, with ROG's runs, so it needs no more XP to level." A class's and a race's Unlock now never compete: a run has one race seat and three class seats |
| Kept apart by a Never combo | "Unlock now: runs 12–13, after ROG (your combo Never ROG with BER keeps them apart)." |
| A locked race | "Froglok is locked, so it's planned last. Unlock it in the game, then tick it off in More options." **Unlock now** disabled, title "Froglok is locked" |
| Done (the plan or your play reached the pips) | dim "3/3: done at run 12." (or "3/3 already."), the seg group hidden |
| A clash, found on the click | `.warn` "**Clashes with your pinned loadout 12 (Half Elf · WAR / RNG / ROG):** it seats ROG in run 12." then `.btn.sm` **Unlock now and unpin loadout 12** · **Keep it Spread out**. The row stays Spread out until one is clicked |

Order of the held things: Unlock now ranked classes and races first (in rank order), then Unlock now trios from Your
combos, each "after" what came before; then Must combos, then the cheapest path; a rank alone only breaks ties.

**Unlock now aims at 3/3.** Clicking **Unlock now** on a row whose pips are below 3 raises them to 3/3 with the click
(the line beforehand said so), and the card flashes "ROG's goal is 3/3 now. Click a pip to stop sooner." Lowering the
pips after keeps Unlock now, to that rank.

#### The race header (the engine's words, right of the title, dim)

| State | Words |
|---|---|
| They fit | "5 race ranks to earn · they fit in the 7 runs your classes need" |
| They don't | "40 race ranks to earn · 33 more runs than your classes need (40, not 7)" |
| No class goals | "8 race ranks to earn, one a run" |
| None ranked, or all ranked races done | nothing |

The Don't care fill line, after "Don't care (13) · ": "they fill your other 2 runs at the least XP to level"; with no
other runs: "no run is left for them".

#### Ready-made goals and the tools row

- The hint: "Click a class's pip for the rank you want: a gold ring marks your goal. Races are ranked below."
- **Every class 3/3**: every class goal to 3/3, as today; ranked rows' pips follow. Off again (all at 3/3): every
  class goal goes and the class list empties.
- **Every race 3/3**: ranks all 16 races at 3/3. Races you'd ranked stay on top in your order and keep their own Spread out or Unlock now (rule 8: the timing is the player's own choice); the rest are Spread out and
  follow with the fewest ranks now first, then in the game's race order, locked races last. The card flashes "Every
  race ranked at 3/3: yours first, then the fewest ranks first. Drag to reorder." Lit while all 16 are ranked at 3/3;
  off again, every race is don't care ("Every race is don't care again.").
- **Clear goals**: every class goal and both lists, as today.
- No plan yet (`campStart`): **Everything 3/3** = every class 3/3 and Every race 3/3 as above. **Every class 3/3** =
  class goals only, no race ranked. **Choose my own** focuses the first class pip; no race ranked.
- With no plan yet, rows have no price line (there are no runs to price); "Build the plan" and its floor line are as
  today.

#### Your plan came across (the engine's lines, under "What didn't come across")

- "· Earn these first is your ranked lists now: ROG first among your classes, Iksar first among your races. Neither is
  set to Unlock now."
- "· Your race goals are ranked now, the fewest ranks first: Iksar, Troll, Half Elf. The 13 races with no goal are
  don't care: Pathfinder uses them where that costs the least XP to level."
- "· Froglok is ranked and still locked: it's planned last until you unlock it."
- "· "race:Drakkin" in Earn these first isn't a race in the game, so it was left out." (a hand-edited settings.json)

#### States

- **Waiting** (a rank, timing or pip changed, the plan rebuilding): rows keep their last lines, faded (`.stale`), the
  title gets the `updating…` chip; a new row's line is "Working it out…". Never blank (rule 9).
- **Many:** all 16 races ranked: every row, no cap, nothing scrolls inside the card; Don't care's label reads "Don't
  care (0)" with the muted "Every race is ranked."
- **Narrow (under 1150 px):** the seg group wraps under the name; the price line keeps the full width; the chips wrap.
  Nothing touches (rule 12).

#### Which words are whose

- **The engine's** (in the page data, campaign.js only draws them): every price line and clash line, the race header,
  the Don't care fill line, "done at run N", the came-across lines.
- **The page's**: titles, the dim sub-lines, chip and menu labels, the empty states, the flashes, the foot.

#### Reused and new

- **Reused:** `.card`, `.h2`, `.lbl`, `.dim`, `.muted`, `.warn`, `.chip`, `.seggroup.sm`, `.iconbtn`, `.cmenu` in
  `#campPop`, `.gpip` (`have`, `plan`, `want`, `past`), `.pips3`, `.gcell`, `.ptile`/`.pbig`/`.psub`, `.btn.sm`,
  `.link`, `.flash`, `.stale`, the `updating…` chip.
- **New:** `.first` (the card), `.first-sec` (a list, with a line between the two), `.first-row` (grid: number 22px,
  name 92px, pips, seg, ⋯; the price spans from the name), `.first-rest` (the chips), `.first-glance` (the race tile's
  cells). The sizes in the picture are the ones to use.

## Proposal

- Each character's new plan stores a ranked list of classes and one of races, each with a target (1 to 3) and its
  timing. The old plan is brought across once, and never rewritten: this draft adds that row to the bring-across
  (campaign-planner-beta copies `First` as it is): `First` at the top, then today's goals, with its "didn't come
  across" line.
- Ranked races are placed in the player's order, unranked races fill the rest at the least XP to level, and a plan
  for a few races is as short as its classes allow.
- With all 16 races ranked at 3/3, races set the length: 48 runs, classes done by run 16.

### Done when (what a screenshot shows)

Branch A:
- [x] Beta on, new planner on: the race pips, Every race 3/3 and More options → "Earn these first" as in 0.10.22 -
  seen in main-campaign-beta.png and main-campaign-combos.png
- [x] Beta off: Campaign as today - seen in main-campaign.png

Branch B's screenshots are its own draft's boxes: [campaign-first-lane-page](campaign-first-lane-page.md).

## Open checks

- What a race's rank order means when two races cost the same: the player's order, then the least XP.
- Bringing an old plan across: a goal of 0, a locked race that's ranked, a hand-edited `settings.json`.
- Settled by the design (11-19): a class's and a race's Unlock now never compete (one race seat, three class seats a
  run); Unlock now races go one after another in rank order; up to three Unlock now classes share runs. A ranked row's
  pips are its goal, so Unlock now is never past a goal and that line never shows for a row.
- The designer's three questions, taken by the lead at the designer's recommendation (the user, 11-19: "Just keep
  going"); the user may overturn any of them:
  - Unlock now on a row below 3/3 raises its goal to 3/3 in the same click; its price line says so first, and the
    player can lower the pips after.
  - When both claim the next runs, ranked Unlock now classes and races go before Unlock now trios from Your combos
    (the card is "what you want first").
  - "free" is dropped from the race header, by design.md's rule that a thing that costs nothing says nothing.

## Done when (branch A: the store, the planner and the engine's words)

Tests in `CampaignFirstLaneTests` (planner, words) and `FirstLaneStoreTests` (store). All 21 fail today on their
assertions: they compile against stubs (`CampaignWant`, `CampaignPlan.ClassOrder`/`RaceOrder`, `Campaign.FirstLane`,
`Campaign.FirstPrice`, `PlannerSwitch.SetOn(s, on, raceRanks)`), whose names the architect may change.

**The store** (the new plan only; the old plan never holds the lists)
- [x] An edit from today's page keeps its race goals as the ranked races (Earn these first's races on top, then the
  others in the game's order, locked last) and Earn these first's classes as the ranked classes, each at its goal; the
  page reads back the same goals and Earn these first - `TodaysPageEditsAreKeptAsRankedListsAndReadBackAsBefore`
- [x] A race whose goal goes leaves the list, a new one goes at the bottom, the rest keep their order; a class taken out
  of Earn these first is unranked - `ARaceWhoseGoalIsTakenAwayLeavesTheListAndTheRestKeepTheirOrder`
- [x] An old plan comes across once: Earn these first on top, then its race goals, the fewest ranks now first; the two
  came-across lines in the design's words; the old plan unchanged - `AnOldPlanComesAcrossWithEarnTheseFirstOnTopThenItsRaceGoalsFewestRanksFirst`
- [x] A locked race that's ranked and a key that isn't a race each get the design's line - `WhatDidntComeAcrossNamesALockedRankedRaceAndAKeyThatIsntARace`
- [x] A plan brought across before this change gets its lists from its own goals, with no card again - `APlanAlreadyAcrossGetsItsListsWithoutComingAcrossAgain`

**The planner**
- [x] Ranked races that fit in the class runs cost no more XP to level than none; races nobody ranked fill the rest - `RankedRacesThatFitInTheClassRunsCostNoMoreXpToLevel`
- [x] Ranked races that cost the same go in the player's order - `RankedRacesGoInThePlayersOrderWhenTheyCostTheSame`
- [x] Unlock now races take the next runs back to back, one after another in rank order - `UnlockNowRacesTakeTheNextRunsOneAfterAnotherInRankOrder`
- [x] An Unlock now class takes the next runs back to back - `AnUnlockNowClassTakesTheNextRunsBackToBack`
- [x] Up to three Unlock now classes share runs; a fourth comes after - `UpToThreeUnlockNowClassesShareRunsAndAFourthComesAfter`
- [x] An Unlock now class in a Must trio brings the trio with it - `AnUnlockNowClassInAMustTrioBringsTheTrioWithIt`
- [x] A Never combo keeps two Unlock now classes apart, the second after - `ANeverComboKeepsUnlockNowClassesApart`

**The engine's words** (`Campaign.FirstLane`, in the data message as `campaign.firstLane`, beta and new planner only)
- [x] The race header's three states, and none with no race ranked - `TheRaceHeaderSaysHowManyRanksAndWhetherTheyFit`
- [x] The Don't care fill line, "no run is left for them", and none with every race ranked - `TheDontCareLineSaysWhichRunsTheyFill`
- [x] "3/3 already." and "3/3: done at run N." - `ARowAtItsTargetSaysItIsDone`
- [x] Unlock now below 3/3 - `AnUnlockNowRowBelowThreeSaysTheActiveUnlocksAtThree`; a second Unlock now race -
  `ASecondUnlockNowRaceSaysItComesAfterTheFirst`; classes together - `UnlockNowClassesTogetherSayWhoTheyRideWith`;
  kept apart - `ANeverComboSaysWhyAnUnlockNowClassWaits`; a locked race - `ALockedRankedRaceSaysItIsPlannedLast`
- [x] Spread out: where it lands, then what Unlock now would cost - `ASpreadOutRowSaysWhereItLandsAndWhatUnlockNowWouldCost`
- [x] The clash on a click (`Campaign.FirstPrice`): the pinned loadout, and the run it seats the class in -
  `UnlockNowThatClashesWithAPinSaysWhichLoadout`, written by the engineer once the Decisions say which pin clashes
  (a rebuild keeps pins ahead of new runs, so the design's "loadout 12" case needs the architect's reading)
- [x] `campaign.firstLane` is listed in `PageDataTests`, and absent outside the new planner - `PageDataTests`
- [x] Beta off, or the new planner off: Generate places races exactly as today - the existing `CampaignTests` pass unchanged

## Edge cases

- **Nothing yet** (no plan, `campStart`): no runs to price, so rows have no price line; the header still counts ranks.
- **Play the log never saw:** a race at its goal from counts or play before the log reads "3/3 already."; "done at
  run N" only names a played run the plan's history holds.
- **A hand-edited settings.json:** a list entry that isn't a class or race, a repeat, a target of 0 or past 3 is
  dropped or clamped (`Normalized`); target 0 means unranked. Unlock now on a race or class at its goal is done.
- **Old versions in the field:** 0.10.22 in the beta reads the new plan's `Races` and `First` and drops the lists when
  it saves. Both stay written in step with the lists, so the next read rebuilds the lists from them (the order past
  Earn these first falls back to the game's order). The old plan (`Campaigns`) never holds a list.
- **A second copy / a flip:** an edit drawn before the switch flipped is still not saved (`Store(…, shownNew)`).
- **Same name:** race keys in any case or spacing ("half elf") land on the game's name; Drakkin isn't a race.
- **Locked and ranked:** stays in its place in the list, planned last; Unlock now has the locked line, no runs.
- **All 16 ranked at 3/3:** races set the length (48 runs), classes done by run 16; no Don't care line.
- **One of something:** "1 race rank to earn …"; "they fill your other run at the least XP to level".
- **Ties:** the player's order, then the least XP, then the game's order.
- **Ranks not known at the bring-across** (another character's plan): its race goals follow Earn these first in the
  game's order, and the line says "in the game's order" in place of "the fewest ranks first".
- **Nothing leaves the PC:** the lists live in settings.json; nothing new is sent.

## Not doing (branch A)

- Anything on screen: the card, the chips, the ⋯ menu and the click's message are branch B
  ([campaign-first-lane-page](campaign-first-lane-page.md)). In A, Unlock now is set only by a test or a hand edit.
- Every race 3/3's "fewest ranks first" order: in A it's a page edit like any other, new races at the bottom.

## Where

- `src/EqlPathfinder.Engine/Analysis/Campaign.cs`: `CampaignPlan` (`ClassOrder`, `RaceOrder`, `Normalized`, the copy
  near `Fixes`), `CampaignWant`; `Generate` (`RaceForClassRun`, `ClassRun`, the Unlock now slots, step 2's `raceOrder`,
  `Prio`), `Improve` (it trades classes only: races placed for XP need a pass too), `Floor`; `FirstLane` and
  `FirstPrice` beside `ComboRows`/`ComboPrice`; `CampaignView` gets `FirstLane`
- `src/EqlPathfinder.Engine/Analysis/PlannerSwitch.cs`: `ComeAcross`, `Store`, `PlanFor`, `PinLines` (the came-across
  lines go with them), `SetOn` with ranks, `StartAgain`
- `src/EqlPathfinder.Engine/Live/TrackerService.cs`: `CampaignData` (the lane, cached like `_comboCache`)
- `src/EqlPathfinder.App`: wherever `PlannerSwitch.SetOn` is called, to pass the ranks; `AppSettings.Campaigns`
- `tests/EqlPathfinder.Engine.Tests/PageDataTests.cs`, `PlannerSwitchTests.cs` (its `CameAcross` helper)
- `docs/releases/next/campaign-first-lane.md`: the design's one line

## Decisions

Architect, 11-19 (from eeaa574). The stubs keep their names; the few additions are named below.

**The store**

- **The lists live on the new plan, beside the fields they extend: `CampaignPlan.ClassOrder`/`RaceOrder`, lists of
  `CampaignWant` (key, target, Unlock now).** Like `Combos`, they're part of the plan, so every edit, rebuild and
  `RebuildAround` carries them with no new store. The old side never holds one: `Store` and `PlanFor` with the switch
  off write them null (beside `WithoutCombos`), so `Campaigns` serializes as today (`WhenWritingNull`). Local only:
  no table, nothing sent, `reread: false`, `job: false`; privacy.html stands.
- **The goals stay the truth for a target; the lists hold order and timing.** A class's goal is `Classes[k]`, a race's
  `Races[r]`; a list entry's `Target` is written from them in `Normalized`, and an entry whose goal is 0 is dropped
  (target 0 means unranked). Every race with a goal is in `RaceOrder` (missing ones appended: the game's order, locked
  last). So today's pips, Every race 3/3 and the goal tiles work unchanged, branch B's "a row's pips ARE its goal"
  holds by construction, and 0.10.22 reads true goals. `Normalized` keeps null as null and empty as empty: **a plan
  with either list non-null is a ranked plan** (`CampaignPlan.Ranked`, `[JsonIgnore]`), the one switch `Generate`
  reads.
- **`First` stays stored on the new plan, as the page wrote it, and orders the lists when it changes.** Today's page
  can't be read back from the lists (only Troll of three ranked races was "first"), so `First` is kept. In
  `PlannerSwitch.Store` (new side), against the stored plan: when `First` differs, or the stored plan has no lists,
  `ClassOrder` becomes First's classes in First's order (Unlock now carried by key), and `RaceOrder` becomes First's
  races on top in First's order, then the rest in their stored order, new ones at the bottom. When `First` is the
  same, the lists as sent stand (a loadout edit, a rebuild, branch B's page). `PlanFor` (new side) derives the lists
  the same way when they're absent, without writing: a plan across before this change, or one 0.10.22 saved. On a
  ranked plan the planner reads the lists, never `First`. Branch B writes `First` in step for 0.10.22 when its page
  changes the lists; B's Decisions settle that projection.
- **An older app saving the new plan drops the lists, accepted (lead, at qa's recommendation).** 0.10.22 skips the
  unknown fields (no `UnmappedMemberHandling` is set) and saves without them; goals and Earn these first survive, and
  the next read derives the lists, the order below First falling back to the game's order. An Unlock now set in
  branch B is lost the same way: a hand-installed older build only.
- **Taking it back:** a build without this drops the lists at its first save, and the old plan is untouched. Undoable.

**The bring-across (once, never rewritten)**

- **`ComeAcross(old, keep, raceRanks)` adds the lists in the copy it already makes:** First's classes, then First's
  races, then the other race goals **fewest ranks now first** (ties: the game's order), locked last. Where the
  character's ranks aren't known (null), the game's order, and the line says "in the game's order" (lead, at qa's
  recommendation). `SetOn(s, on, raceRanks)` passes `raceRanks(character)`; `StartAgain` gets an optional
  `raceRanks` too. The App passes the current character's ranks (a public accessor beside `TrackerService.Ranks`) and
  null for every other character. The old plan is read, never written. Plans already across get their lists from
  `PlanFor`'s derivation, and no card again.
- **New on `NewCampaignPlan`: `RacesAcross` (`{ Order, ByRanks }`), written by `ComeAcross` only.** The card's lines
  are worked out at read time from the old plan, as `PinLines` is, but `PageInput` has no ranks, so the race order as
  it came across and how it was ordered are kept as facts. Null (across before this change, or 0.10.22 saved it): no
  race line. The others: Earn these first's line from the old plan's normalized `First`; "isn't a race in the game"
  from its **raw** `First` (a hand edit survives there, since the old plan is never rewritten); "ranked and still
  locked" from `RacesAcross.Order` against the new plan's `Locked` now.

**The planner (`Generate` on a ranked plan; an unranked plan is today's, line for line)**

- **One lane, read at the start of `Generate`:** from the lists on a ranked plan, from `First` otherwise, so the old
  path keeps its code. `ClassRun`'s ride-along takes ranked Spread out classes still short (rank order, at most 3), and
  `Improve`'s "never moves a class you want first later" reads the same lane.
- **Held order (the lead's): ranked Unlock now classes, then Unlock now trios from Your combos, then Musts spread out,
  then the cheapest path.** Unlock now classes are grouped in rank order: each joins the earliest group with a free
  seat and no Never against a class already in it; a group has three seats, counting the partner a held Must pair
  pulls in. A held Must trio holding a group's class *is* that group's runs (its slots become Unlock now at the
  group's positions), and a group class outside that trio goes to the next group. A group plays back to back for as
  many runs as its furthest class needs to its target; `Greedy` fills free seats. Groups go one after another from the
  first new run (`unlockAt`), and the combos' Unlock now trios continue from where they end.
- **A race has its own seat, so a class's and a race's Unlock now never compete.** Each new run's race (class run or
  race run): the current Unlock now race (rank order, back to back; a locked one never, it's planned last); else the
  race whose next rank costs the least XP to level in that run, and among those a ranked race still short (rank
  order), then a race at or past its goal or with none, lowest rank now, then the game's order (review, 11-19: built
  XP first, since ranked-first put Iksar's second rank in a run of rank-1 classes; a rank only breaks ties). Step 2's race runs: Unlock now races left,
  then ranked races in rank order, locked last. On a ranked plan a race with no goal is never a goal.
- **Against a pinned loadout, pins win, as they do over Musts.** A rebuild keeps pins ahead of new runs; Unlock now's
  runs are the first runs Pathfinder places, and a pinned run that seats the class (or is the race) counts toward it.
  **It clashes** when a pinned loadout with runs to go seats the class (or is the race) and a pinned run without it
  comes after, so its ranks can't come back to back. `FirstPrice` names the first such pin and its first run to go in
  the plan as it stands: "Clashes with your pinned loadout 1 (Half Elf · WAR / RNG / ROG): it seats ROG in run 1."
  (a race: "it plays Iksar in run 1."). The fix unpins it. New: `CampaignFirstRow.ClashLoadout` (int?), the loadout
  B's button unpins. **The test** (`UnlockNowThatClashesWithAPinSaysWhichLoadout`): the "classes" preset, loadout 1
  Half Elf · WAR / RNG / ROG and loadout 2 Human · CLR / ENC / MAG, both pinned, once each: Unlock now ROG gets that
  clash and `ClashLoadout` 0; with only loadout 1 pinned, none.
- **Every plan the engine builds from another carries the lists** (`Normalized`, the private copy, `Fixes`,
  `ComboPrice`'s variants, `FirstPrice`'s): a copy that drops them is priced as an unranked plan, silently.

**The engine's words**

- `Campaign.FirstLane` → `CampaignView.FirstLane`, in the data message as `campaign.firstLane`, set in
  `TrackerService.CampaignData` only with the switch on (`roles != null`) and a ranked plan, cached on the combos' key
  (`fixKey`). A fuse: a row's price is one rebuild with the other timing, at most 32 per plan change; rows at their
  target and locked races aren't priced. `FirstPrice` is the click's answer (branch B's message), never in the data.
  Every line, the header and the Don't care line are built in the engine; campaign.js only draws (rule 4).

**The build, in three parts, each a fresh engineer ending in a commit, in this order** (each builds on the one
before, in one worktree and one `Campaign.cs`, so not in parallel):
1. **The store and the bring-across:** `CampaignWant`, `Normalized`, `Ranked`, `Store`/`PlanFor`, `ComeAcross`,
   `RacesAcross`, the card's lines, `SetOn`/`StartAgain` with ranks and their App calls. `FirstLaneStoreTests` and
   `PlannerSwitchTests` green.
2. **The planner:** the lane, Unlock now groups and races, the race seat, `Improve`, the copies. The seven planner
   tests green, `CampaignTests` unchanged.
3. **The words:** `FirstLane`, `FirstPrice` with the clash test above, `CampaignView`, `CampaignData`,
   `PageDataTests`, the release note.

## Reviews

- architect: two must fixes, one should fix, and a Decisions update (81dc797) accepting XP-first race choice. Done: a ranked race at its goal no longer outranks don't-care races for a class run (test `ARankedRaceAtItsTargetNeverEarnsARankPastIt`, and the don't-care test now counts the runs); `PlannerSwitch.WithLists` puts the lists in order before the rebuild in `SetCampaign` and Store keeps using it (test `AnEarnTheseFirstEditIsPlannedFromTheNewListsNotTheOldOnes`); the page-data assertion reads as `Assert.True`.
- designer: one must fix, two should fix here. Done: the card (`PinLines`) leaves out the ranked-list and "ranked and still locked" lines until branch B draws the lists (`RankLines` keeps them behind `listsShown`); release note says beta and new planner; the Spread out price line puts the reason in parentheses. A third should fix and the architect's First/ClassOrder note are carried to campaign-first-lane-page.
- lead: read the fix commit (4bda238) against both reviews; no second review, since the fixes are the ones the architect prescribed.
