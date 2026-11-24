# Your runs start near the top: the campaign header slims, and your goals sit beside the runs

**Built 11-20 in campaign-page-layout's branch, shipped in 0.10.24** ([campaign-page-redesign](campaign-page-redesign.md)). The `designer` goes first
(`Mode: design`, from the layout the user picks), then one `engineer`, then the `architect` and `designer` review.
**Tier:** standard with the designer: it moves and slims what Campaign already draws, and stores and sends nothing new.
**One branch with [campaign-goal-presets](campaign-goal-presets.md) and [campaign-goal-tiles](campaign-goal-tiles.md)**
(the user, 11-20), under this name, about 500k for the three: they all redraw the goals card. The engineer works
from all three drafts, and the spec keeps each one's "Done when". **Ships to every player** (the user, 11-20),
not only behind the beta switch ([campaign-planner-beta](campaign-planner-beta.md)): the old planner gets the layout too,
and the lists and combos stay the new planner's. **Waits for:** nothing (layout B
chosen, 11-20). **Unblocks:** campaign-combos-example (the card's place) and campaign-run-list (the run column, and
the dot meaning this branch settles).

**Why:** after Everything 3/3 the first run is 2,043 px down at the user's window width (1,625 px) and 2,785 px down
narrow (906 px): the Your campaign card (520 px, with a big run number, three goal tiles, a tools row, a totals line
and two buttons), then What you want first, then Your combos, all before a single run. The player scrolls past their
settings every time they look at their plan. After, the header is one band of a few lines, and goals, What you want
first and Your combos sit in a column beside the runs: the first run is at 324 px, and what the player changes stays in
view beside what it changes.
**Players:** everyone on the beta with a campaign. Worst for the user's own window: a portrait monitor, where today's
stack is longest.

Today: [today-full-1.png](.webp), [today-narrow-1.png](.webp).
The three layouts the designer drew; the user chose B (11-20):

| | What it is | First run, full / narrow (today 2,043 / 2,785) | What it costs the player | Mock-ups |
|---|---|---|---|---|
| **A** | The same stack, compact: the header in two columns, classes and races one row of 16 pips each | 832 / about 1,100 | Everything is still above the runs | [full](.webp), [narrow](.webp) |
| **B** (chosen) | A side column: Your goals, What you want first and Your combos beside the runs; under 1,150 px it becomes A's stack | 324 / 1,145 | The runs get about 1,030 px of width at full | [full](.webp), [narrow](.webp), [empty plan](.webp) |
| **C** | One goals bar: the ready-made goals and a count each ("Classes 27 of 48 ranks…", "First: nothing ranked", "Combos: none"), and "Goals, first and combos ▾" opens the rest | 408 / 505 | One click to see or set any pip | [full](.webp), [narrow](.webp) |

```text
B at full width (made-up numbers; the mock-up is page-b-full.png)

┌ YOUR CAMPAIGN · EVERYTHING 3/3   Leveling: Barbarian · DRU/BRD/NEC · 25/50 · ≈ 9.8 h  [Rebuild the rest][Start over] ┐ ┌ Your goals ──────────────┐
│ Run 8 of 48                      Also leveling … · Next to reincarnate … · 39 runs to go · +60% … · the cheapest path  │ │ ready-made goals, pips   │
└───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘ │ (goal-presets, tiles)    │
┌ Your runs ─────────────────────────────────────────────────────────────────────────────────────────────────────────┐ ├ What you want first ─────┤
│ 8  Barbarian · DRU / BRD / NEC  leveling …                                                                          │ │ Classes: nothing ranked  │
│ …                                          (campaign-run-list)                                                      │ │ Races: nothing ranked    │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘ ├ Your combos ─────────────┤
```

## What the user decided (11-19)

- "The 'Your campaign' header, its goal tiles and its tools take a lot of room before the first run. Slim them."
- The user's window is a portrait monitor at 150% (about 1,707 page px wide): "full" is that, "narrow" about 1,000.
- Layout B, the side column; layout, presets and tiles in one branch (11-20, "I agree with all of your picks").

## What the player sees

For all three drafts in this branch: the page's layout (this draft), the ready-made goals
([campaign-goal-presets](campaign-goal-presets.md)) and the goal tiles ([campaign-goal-tiles](campaign-goal-tiles.md)).
The layout, the ready-made goals and the tiles are for every player, old planner included; What you want first and Your combos stay the new planner's. The mock-ups were
drawn in the real window from the user's data (`EQLPATHFINDER_SELFTEST=mock`). The runs in them are today's rows,
which campaign-run-list redraws. The script is [layout-mock.js](campaign-page-layout/layout-mock.js) (run with
`EQLPATHFINDER_SELFTEST=mock`, `MODE` set to full, mix, empty or empty-n first).

| State | Mock-up |
|---|---|
| After Everything 3/3, full width (1,625 px) | [everything-full.png](.webp) |
| A hand-set mix with a goal the plan misses, narrow (906 px, the stacked form) | [mix-narrow.png](.webp) |
| Empty plan, full width | [empty-full.png](.webp) |
| Empty plan, narrow | [empty-narrow.png](.webp) |

**Changed from the drafts' pictures, and why:**
- "the cheapest path" no longer ends the totals line when the plan is the cheapest path: a line that only says all is
  well is noise (design.md). The (i) stays at the end of the line with today's long version. When the plan costs
  more, the line says the price in plain text: "· 1 run more than the cheapest path".
- Each tile's count is one line ("27 of 48 ranks you want · all by run 16"), not a big number and a foot: the side
  column is 460 px wide, and "0 of 16 at your goal" right after Everything 3/3 is the rule 4 trap.
- More options sits on its own line under the three ready-made goals, so they never wrap at 460 px.

### The page at full width (window content 1,150 px and wider)

Two columns. The left one (the rest of the width, about 1,100 px at the user's 1,625 px) holds the header band, the
came-across card when it shows, and Your runs. The right one is a 460 px side column from the top of the page: Your
goals, What you want first, Your combos, in that order, 16 px apart. Both scroll with the page. Nothing scrolls inside
a card and nothing sticks. The first run starts about 330 px down (everything-full.png).

**The header band:** one card, the campaign's name and run on the left, its lines in the middle, the buttons right.

```text
YOUR CAMPAIGN · EVERYTHING 3/3   Leveling: Barbarian · DRU / BRD / NEC · 25 / 50 · ≈ 9.8 h to go       [Rebuild the rest] [Start over]
Run 8 of 48                      Also leveling: run 9 · Wood Elf · MAG / MNK / SHM · 1 / 50 · not played yet
                                 Next to reincarnate: run 10 · High Elf · BST / ENC / BER at +20% XP to level (i)
                                 39 runs to go · +60% XP to level on average · the last needs +100% XP to level (i)
```

- Each line's words are today's (`campHero`); only their place changes. The name is today's (Everything 3/3, Every
  class 3/3, "7 passives you picked").
- The totals line takes in today's Cheapest path card, and the card goes. At the cheapest path the line ends with its
  (i). Otherwise it ends "· 1 run more than the cheapest path (i)" (or "N runs more", or the XP it adds), in plain
  text, never `--warn` (rule 22: the extra comes from the player's own Musts). The (i) holds the card's long, per-plan text (the fewest runs, when the classes are done, what keeping a combo costs). When Musts cost runs the line stays silent: the combo's own row shows the +runs. With 0 runs of goals left it says "No goals left to earn: none of these runs is needed", never "N runs more".
- "Run 8 of 48" is one line at any width; the lines wrap beside it.

**The came-across card** (campaign-came-across-rebuild) sits between the band and Your runs, as wide as the run column.

**More options** opens today's options panel inside Your goals, under the ready-made goals.

### The stacked form (window content under 1,150 px, the shared breakpoint, down to the app's 980 px minimum)

One column: the header band, Your goals, What you want first, Your combos, then Your runs (mix-narrow.png). Your goals
puts its labels (CLASSES, RACES, COMPLETIONIST (i)) in a 140 px column on the left with the cells beside them, eight
to a row, at most 560 px wide, so no label runs into its pips. The band's lines wrap beside the run number and its
buttons stay top right. At 150% scaling the page stacks; the side column never squeezes below 460 px.

### Your goals

```text
Your goals
[✓ Everything 3/3 · 39 runs] [Every class 3/3 · 7 runs] [Clear goals]
More options
CLASSES
 WAR   CLR   PAL   RNG   SHD   DRU   MNK   BRD
 ●●◉   ●●◉   ●◉◉   ●●◉   ●●◉   ●●◉   ●●◉   ●●◉      (the last pip of each also ringed: the goal)
 ROG …                                              (second row of 8)
27 of 48 ranks you want · all by run 16
RACES
 HUM … DWF, then TRL … HDE
9 of 48 ranks you want · all by run 48
COMPLETIONIST (i)
●◉◉
1 of 3 · rank 2 at run 11
● you have it   ◉ this plan earns it   ○ not planned   ◎ your goal
Click a pip for the rank you want. Click your goal's pip again to lower it.
```

**The ready-made goals** (campaign-goal-presets), on every plan that has runs:

- Three `.btn.sm` buttons: "Everything 3/3 · 39 runs", "Every class 3/3 · 7 runs", "Clear goals", the counts dim.
  The one the goals match is lit (`.on`, "✓ " before the name, gold edge). Everything 3/3 is every class and race at
  3/3; Every class 3/3 is every class at 3/3 and no race goal. A hand-set mix lights neither (mix-narrow.png).
- **The count** is the runs to play from here if that goal were set now, keeping this plan's pins, played and leveling
  runs, combos, ranks and timings: the number the plan shows after the click. A lit button's count is the band's "39
  runs to go". Tooltip: "39 runs from here, 46 with the 7 you've played · +60% XP to level on average · the last needs
  +100% XP to level". A goal already reached reads "Every class 3/3 · done", never "0 runs". Before the counts
  arrive, the buttons show their names alone, never a zero.
- **A click on an unlit one** sets those goals (Every class 3/3 also takes race goals away) and rebuilds the runs to
  play around everything above, as a pip click does. It lights at once; the runs and counts stay, faded, until the
  answer (rule 9). It ranks nothing and adds no combo (rule 23). It is never Start over.
- **The lit one** does nothing when clicked: it's a state, not a switch (`aria-pressed="true"`, no hover). Clear goals
  takes every goal away, with today's tooltip, and is disabled with no goals.
- Today's tools row goes, with its "Every class 3/3" and "Every race 3/3" toggles. Every race and no class goals is
  set on the race pips.

**The tiles** (campaign-goal-tiles). Classes and races are twins: 16 cells in two rows of 8, each the abbreviation
(rule 14, full name in the tooltip) over three pip buttons. On both tiles a click on a pip sets that rank as the goal,
and a click on the goal's pip lowers it, as on today's class tile. The race tile's buttons are campaign-race-goals'
too: whichever branch finishes second carries the other's code.

- **One meaning per mark, for the whole page** (the open check, settled here; campaign-run-list follows it):
  - ● solid `--gold`: a rank you have.
  - ◉ dark gold, a `--gold-bg` fill with a 1.5 px `--gold-dim` edge: a rank this plan earns. It differs from solid
    gold in lightness and in its edge, not only in hue.
  - ○ empty, a `--line2` edge: not planned.
  - ◎ a 2 px `--gold` ring, 2 px out from the pip: your goal. **One ring per cell, on the goal rank alone.**
  - A goal the plan misses keeps today's dashed `--warn` ring on the goal pip, and its abbreviation turns `--warn`
    (rule 21: something the player asked for that can't happen). Ranks past your goal stay faded.
- **The cell** has no fill or border of its own: `.gcell.on`'s gold background and edge go. `.gcell.miss` keeps only
  its `--warn` text.
- **The count line** under each tile:
  - goals, planned: "27 of 48 ranks you want · all by run 16" (the run as the plan numbers it);
  - a goal missed: "1 of 6 ranks you want · not all in this plan", the second part `--warn`;
  - goals set on the empty plan, before Build the plan: "27 of 48 ranks you want";
  - every goal reached already: "48 of 48 ranks you want · all yours already";
  - no goal on that tile: "27 of 48 ranks earned · no goal yet" (never "0 of 0 ranks you want", rule 4).
  The race tile's "Rank races" link and "No race is a goal: …" line go (campaign-race-goals).
- **Completionist** (i): three pips, not buttons, the same marks, no ring. Its line: "1 of 3 · rank 2 at run 11",
  "1 of 3 · this plan doesn't raise it", "3 of 3 · done", and on the empty plan "1 of 3 · goes up when every class
  reaches 1, 2 and 3".
- **The legend**, under the tiles, drawn with the real pips (not glyphs), with only the marks on screen: "you have
  it", "this plan earns it", "not planned", "your goal", and "a goal this plan misses" only when one does. The empty
  plan shows "you have it" and "your goal". Then, on its own line: "Click a pip for the rank you want. Click your
  goal's pip again to lower it." The old hint ("a gold ring marks your goal") goes: the legend says it.

### What you want first and Your combos

Moved into the side column, words unchanged: the class list as today, the race list as campaign-race-goals has it
("Races you want first", no "Don't care (N)"). Chips wrap to two rows of 8 at 460 px. Each title keeps its right-hand
words ("The rest are Pathfinder's to place", "Kept through every rebuild and goal change") on its own line.

### The empty plan (empty-full.png, empty-narrow.png)

- The left column has today's "Plan every reincarnation" card with its three larger tiles (`campStart`'s words:
  "Everything 3/3 · 39 runs from here · 46 with the 7 you've played …", "Every class 3/3", "Choose my own"), then a
  Your runs card: "Your runs show here once you pick a goal: a ready-made one, or your own with the pips."
- No header band, and Your goals has no ready-made goals line: one copy of them, where the runs will be.
- Your goals ends with "Build the plan", disabled, with "Click a pip, or pick a ready-made goal." beside it; it
  lights once a pip is set.
- Narrow, "39 runs from here" is one line and "46 with the 7 you've played" the next; nothing wraps inside the big
  number.

### The other players and states

- **New, log just turned on:** every pip empty, "0 of 48 ranks earned · no goal yet" from Roster's counts, as today;
  the counts on the ready-made goals are the whole campaign.
- **New to the app, old to the game:** ranks come from Roster as today; the tiles show what Roster holds.
- **Waiting:** every click keeps the page and fades the runs and counts until the answer; the clicked pip or button
  shows its new state at once. Nothing blanks.
- **Wrong:** nothing new is sent or stored; the page's errors are today's.

### Reused and new

Reused: `.card`, `.h2`, `.lbl`, `.btn.sm`, `.btn.sm.on`, `.link`, `.dim`, `.warn`, `.gpip` with `.have`, `.plan`,
`.want`, `.past`, `.miss`, `.gcell`, `.pips3`, `.chip`, `.first-*`, `.preset` (the empty plan), `infoTip`. New: the
page grid and side column (`.camp-grid`, `.camp-side`) with the stacked form in the shared 1,150 px `@media`; the
header band's columns; the label column when stacked (`.gblock`, `.glabel`); the ring on the goal pip alone; the
legend line (`.glegend`, real pips). The Cheapest path card and the tools row go.

### How the build splits

Two engineers in this branch, one after the other, about 200k and 250k:
1. **Layout** (page only): the grid and side column, the stacked form, the header band with the totals line taking in
   the Cheapest path card, the cards moved, and the page tests that read Campaign's cards by order. Ends committed
   and green.
2. **Ready-made goals and tiles:** the line of ready-made goals and what a click does, `Campaign.Presets` counting with
   the plan's pins (an engine test), the marks, the ring on the goal rank, the count lines, the legend, and the race
   tile's buttons if campaign-race-goals hasn't landed.

## Done when

The shots are new `DevSnapshots.cs` shots of the beta Campaign: full width after Everything 3/3, a hand-set mix with
a missed goal at 906 px, and the empty plan at both widths.

Layout:
- [x] At full width the side column (Your goals, What you want first, Your combos) sits beside the header and the
  runs, and the first run shows without scrolling - seen in campaign-layout-full.png
- [x] The header is one band, its totals line ends with the (i) and no "the cheapest path", and there's no Cheapest
  path card - seen in campaign-layout-full.png
- [x] A plan dearer than the cheapest path ends its totals line "1 run more than the cheapest path" in plain text - the page test `APlanDearerThanTheCheapestPathSaysSoInThePlainTotalsLine` and
  seen in campaign-layout-mix.png
- [x] Under 1,150 px the page stacks with Your goals above the runs, labels in their own column, nothing touching -
  seen in campaign-layout-mix.png
- [x] The empty plan: Plan every reincarnation where the runs go, Your goals beside it without the ready-made goals,
  and "39 runs from here" never wraps inside its number - seen in campaign-layout-empty.png and
  campaign-layout-empty-narrow.png

Ready-made goals:
- [x] "✓ Everything 3/3 · 39 runs" lit, with "Every class 3/3 · 7 runs" and "Clear goals" beside it - seen in
  campaign-layout-full.png
- [x] A hand-set mix lights neither - seen in campaign-layout-mix.png
- [x] A click on a ready-made goal keeps the plan's pins and played runs, and its count matches the runs to go after
  the click - the engine test `TheReadyMadeGoalsCountTheRunsToPlayFromHereKeepingThePlansPinsAndPlayedRuns` (a pin, a played
  run and a Must combo, checked against `DoneCounts` + `Generate` as the rebuild runs them). DevSnapshots clicks nothing, so the shot proves no count

Tiles:
- [x] One ring per cell, on the goal rank; no gold cell fill; solid, dark gold and empty pips; the legend's four
  marks - seen in campaign-layout-full.png
- [x] A missed goal: the dashed red ring, its abbreviation red, "not all in this plan", and the legend's fifth mark -
  seen in campaign-layout-mix.png
- [x] With no goal: "27 of 48 ranks earned · no goal yet" on both tiles and a two-mark legend - seen in
  campaign-layout-empty.png
- [x] The race tile's pips are buttons like the class tile's, with no "Rank races" - seen in campaign-layout-full.png

## Open checks

- **The dot meaning for the whole page:** settled above (the tiles): solid gold is a rank you have, dark gold one
  the plan earns, empty not planned, the ring your goal. campaign-run-list follows it.

- Narrow: the "COMPLETIONIST (i)" label ran into its pips in the first mock-up: the 140 px label column fixes it (mix-narrow.png).
- 150% scaling and the app's 980 px minimum: the column must stack, not squeeze.
- The page tests that read Campaign's cards by order (`HomePageTests`' Campaign cases) follow the new order.

## Where

- `src/EqlPathfinder.App/web/js/campaign.js`: `renderCampaign` (the layout), `campHero`, `campCheap`, `campStart`
- `src/EqlPathfinder.App/web/js/campfirst.js`: `campFirstCard`; `campcombos.js`: `campCombosCard`
- `src/EqlPathfinder.App/web/app.css`: the column and its 1,150 px breakpoint

## Review fixes (11-20)

- [x] The band at 1,274 px keeps its middle column: "Rebuild the rest" and "Start over" sit under "Run 8 of 12", and "30 / 50", "≈ 9.3 h to go" and "+30% XP to level" stay on one line - seen in `r2\main-campaign-layout-1274.png` (scratchpad)
- [x] With no goals left to earn the totals line says "No goals left to earn: none of these runs is needed" - the page test `WithNoGoalsLeftTheTotalsLineSaysNoneOfTheRunsIsNeeded`
- [x] The legend names only the marks drawn; a hand-set mix lighting no ready-made goal, with the missed-goal ring and "not all in this plan" - seen in `r3\main-campaign-layout-mix-legend.png`

## Reviews

- architect: first review: not behind the beta switch (the user chose every player), preset counts not the click's, their test without played runs or combos; second review: the preset cache key missed Locked and the orders, a merged test ranked all 16 races. All fixed. Second review fixes: the cache key now holds Locked and both orders whatever the runs (`APlanRebuildsItsPresetsWhenAnOrderMoves`); the merged test is main's again (Troll and Iksar only); the unread `sawGoal` is gone.
- designer: first review: not behind the beta switch, the band crushed at 1,150–1,500 px, shots that didn't show their boxes, the legend, More options in the column, titles, release note; second review: release note wording, a repeated spare-runs line, red for a fact, two wraps, the (i)'s order. All fixed. Second review fixes: the release note says Every race 3/3 is set on the pips and combos are kept on the new planner; the spare-runs card shows only with real fixes (title "The cheapest fixes:"); "No goals left to earn" is plain text; "N runs more than the cheapest path" and its (i) share one `.nw`, as does "not played yet"; the (i) leads with this plan's words, "without your Musts".
