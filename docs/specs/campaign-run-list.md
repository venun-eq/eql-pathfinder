# Your runs read as a table by round: one line per loadout, every rank it earns in view

**Designed 11-20** ([campaign-page-redesign](campaign-page-redesign.md)). **Replaces
[campaign-rounds](campaign-rounds.md)** (its round headers and folding are kept here; what's dropped is below). The
`designer` went first; one `engineer` builds, then the `architect` and `designer` review. **Tier:** standard with the
designer: the engine says each run's round and what the leveling runs earn in the page data; the planner chooses
nothing differently and nothing is stored. **For** every player with a campaign, as the page layout it sits in
(campaign-page-layout shipped to every player). **Built for every player, not behind the beta switch (the user, 11-20): the old list's code is gone; the "Two pets" warning is dropped everywhere, Swap… included.** **Waited for:** campaign-page-layout (the
run column, and the dot meaning it settled), now on main. Run list B chosen, with layout B (the user, 11-20).
**Unblocks:** campaign-synergies (where "(suggested)" goes on a run).

**Why:** Your runs is 21 rows of 52–60 px after Everything 3/3, about 1,300 px. Each row's words end near 500 px and
its dots start near 1,370, so 850 px of every row is empty, and what a run earns ("run 11 · PAL 2/3 · WIZ 2/3 · BST
3/3 · High Elf 2/3 · Completionist 2") is a sentence to read, different in every row. The user: "a long list that's
hard to scan. Each row is mostly empty space, and reading left to right is hard even at a narrow width." After, each
loadout is one line in aligned columns (run, race, three classes, XP to level, actions), each race and class with
three pips for its ranks, grouped by round under a header that says what the round does and its XP to level.
**Players:** everyone with a campaign; worst at narrow width and on a long plan (48 runs).

Today: [today-full-1.png](.webp), [today-full-2.png](.webp).
The three run lists the designer drew, inside page layout B; the user chose B (11-20):

| | What it is | Fits (Everything 3/3, 23 loadouts with played) | What it costs the player | Mock-ups |
|---|---|---|---|---|
| **A** (the user's idea) | A chip per loadout in reading order: run, race, trio, XP, dots; played grey, gold at 3/3; a click opens what it earns, Swap…, pin, ⋯ | 30 chips in 490 px | Partial ranks, Completionist and "past 3/3" only on a click; XP read across a zigzag | [full](.webp), [narrow](.webp) |
| **B** (chosen) | One line per loadout in columns, grouped by round, three dots per race and class | 23 lines in about 920 px at either width | "PAL 2/3" becomes dots (a key on screen), four header lines | [full](.webp), [narrow](.webp), [opened](.webp) |
| **C** | A class grid: a column per class, the rank in the cell | 30 lines in about 1,050 px | The trio isn't readable as words; actions behind a click; barely fits narrow | [full](.webp), [narrow](.webp) |

## What the user decided (11-19, 11-20)

- "Show me several designs. One idea: a chip per loadout, laid out in reading order (left to right, then down), with
  done runs greyed out, or something cleverer." The user chose B, retired campaign-rounds, and kept every round to
  come open with Fold (11-20, "I agree with all of your picks").
- Rule 5 (played runs counted and locked), rule 10 (XP to level), rule 22 (the red "Two pets…" goes).

## What the player sees

Drawn in the real window from the user's own plan (Everything 3/3, run 8 of 48, 7 played, 2 leveling), with the page's
real `renderCampaign`, Swap… and its answer; only `campList` is the mock-up's. The script is
[runs-mock.js](campaign-run-list/runs-mock.js), run by [run.ps1](campaign-run-list/run.ps1) (`-Mode full|open|narrow`,
the window size in screen pixels; it copies the real settings and database into a scratchpad folder).

| State | Mock-up |
|---|---|
| Full width (1,625 px), played folded | [full.png](.webp) |
| Full width, played shown, Swap… open on the next run | [opened.png](.webp) |
| Narrow (906 px, the stacked page), scrolled to Your runs | [narrow.png](.webp) |

**Changed from the draft's picture (runs-b-full.png), and why:**
- **No RUNS dot column.** The run column already says "17–18", and the run dots were a second dot vocabulary on a
  page that now has one (gold = you have it, dark gold = this plan earns it). Next, leveling and part-played say it in
  words in the status column. It also gives narrow its room back.
- **The pips are the tiles' pips:** solid gold you have it, dark gold this plan earns it, empty not yet. The draft's
  grey "rank you'll have before it" and gold "rank this loadout earns" go.
- **A leveling line's level and hours are in the status column, and its XP to level in the XP column.** The draft put
  "25 / 50" under XP TO LEVEL (rule 1: a number under the wrong heading).
- **Headers say "classes to 2/3", not "every class to 2".** A plan whose goals are a few classes still has rounds,
  and "every class" would be untrue there (rule 2). The last group is "Races", with "every class is 3/3 by now".
- **A header's XP range is "+20% to +30%",** not "→": runs in a round aren't always in rising order (run 10 is +30%,
  run 11 +20% in the mock-up). A line's own range keeps "→", since one loadout's runs do rise.
- **The mock-up's run column is 40 px narrow and wraps "18–20"**: build it 48 px and never wrapped (below).

### The card's top

```text
Your runs                                                  7 played before · 0 done · 2 leveling · 39 to go · 21 loadouts
In the order you'd play them, a round at a time. Swap… shows the cheapest changes that keep every goal, or lets you
pick a race or class yourself. A loadout you change by hand is pinned: changing your goals re-plans the rest around it.
Pips show each rank once the line is played:  ● you have it  ◉ this plan earns it  ○ not yet  WAR reaches 3/3 here  WAR 3/3 already: more XP to level, no passive
   RUN  RACE             CLASSES                                                    XP TO LEVEL
```

- The counts line and the first sentence are today's, with "a round at a time" added.
- **The key** (rule 13), once, on its own line under the intro, drawn with the line's real pips, not glyphs: "Pips show
  each rank once the line is played:", then "you have it" (solid `--gold`), "this plan earns it" (the tiles' dark
  gold `.plan`), "not yet" (empty, `--line2` edge), then **WAR** in `--gold` "reaches 3/3 here", then **WAR** in
  `--faint` "3/3 already: more XP to level, no passive". It names only the marks drawn, as the tiles' legend does: on
  a plan with no line past 3/3, the last item goes; with nothing reaching 3/3, the gold WAR goes. The empty pip says
  "not yet", not the tiles' "not planned": a later line may earn it (rule 2). Narrow, it wraps to a second line.
- **The column labels**, in `.lbl` style (10.5 px, uppercase, `--faint`): RUN (right-aligned over the numbers), RACE,
  CLASSES (over the three class columns), XP TO LEVEL (right-aligned). The status and actions columns have none.

### A line

`RUN · RACE ●◉○ · WAR ●◉○ · CLR ●◉○ · PAL ●◉○ · status · XP to level · [pin] [Swap…] [⋯]`

- **Run:** the run number in your whole campaign (rule 5: "Run 7 of 48" counting played), or a range for a loadout
  played more than once ("17–18", "18–20"). Bold, `--faint`, right-aligned, 48 px, never wrapped.
- **Race, then three classes,** each a name and three pips. The race is the full name ("Half Dark Elf"); classes are
  the game's abbreviations (rule 14). The pips are the rank once this line's last run is played: solid gold for the
  ranks you have today, dark gold for those this plan earns by then (by this line or an earlier one), empty for the
  rest. A line raises each of its names one rank per run, so its last dark-gold pips are the ones it earns.
  - The name is `--gold` where this line takes it to 3/3.
  - The name is `--faint`, regular weight, where it is 3/3 already before this line: its pips are full and the run
    adds XP to level and no passive.
  - Tooltip on each name: "PAL 2/3 once this run is played" ("…once these runs are played"); for a name 3/3 already,
    "…: 3/3 already, so the run adds XP to level and no passive".
- **Status** (12 px, `--muted`, may wrap to two lines in its own column): the next run's `.chip.gold` "next"; a
  loadout with some runs played, "1 of 2 played"; otherwise empty.
- **XP to level:** "+30%", or "+40% → +60%" over a line's runs, right-aligned, today's tooltip ("XP to level: the bar
  is that much bigger. Mobs give the same XP."). Never "XP ×1.4" (rule 10).
- **Actions:** today's pin (`.iconbtn`, pressed and gold when pinned), Swap… (`.btn.sm`) and ⋯ (`.iconbtn`), right-aligned,
  every one a button in the tab order. A pinned line keeps today's gold inset edge; the next line keeps today's gold
  wash (`.lrow.next`).
- **Gone from the line:** the "Two pets: only one is up at a time…" warning (rule 22: the app doesn't flag a trio it
  chose; players know their classes). No note replaces it, in Swap… or anywhere. The "already 3/3 by then: these runs
  earn no race passive" warnings go too: the grey race name and the key say it, without red, and Swap…'s first line
  says it in words. "{race} isn't unlocked yet: max faction with its three city factions first." stays, as a plain
  `--muted` 12 px line under the line's race and classes (it is a step the player has to take, not a fault).
- Line height about 40 px at full width (7 px padding), a `--line2` rule between lines; 23 lines and 6 headers fit in
  about 1,300 px with the played runs folded.

### The groups, in order

Each group has a header line: the title bold, then a `--muted` 12 px sentence, then Fold or Show at the right as a
`.link` button with `aria-expanded`.

1. **Played**, folded at first, on today's inset band: "**Played · runs 1–7** in your ranks already: nothing in the plan
   changes them" with **Show** / **Hide**. It holds the runs played before the plan and the plan's own done runs, so
   its range is 1 to the last played run. Shown, each played run is a line in the same columns: the race and classes
   with the ranks it reached then (all solid gold, rule 4's partial ranks), its date in the status column ("Nov 10"),
   no XP, and today's lock (`LOCK`, `--faint`, tooltip "Played: nothing in the plan changes it") where the actions go.
   Runs the log didn't see stay one line: "1–3 · runs your log didn't see: in your ranks from the counts on Roster".
2. **Leveling now**: "**Leveling now** 2 runs reincarnated, below 50". No Fold: it is at most a few lines and the one
   you're on. Each line: the race and classes with the ranks you have now (the run raised them when you reincarnated,
   so they're solid gold), the status "35 / 50 · ≈ 7.2 h to go" (the hours `--gold`, Home's) or "1 / 50 · not played
   yet", its XP to level, and the lock (tooltip "Reincarnated: this run keeps its loadout").
3. **Round 1, 2, 3**: "**Round 2 · classes to 2/3** runs 10–11 · +20% to +30% XP to level · Completionist 2 at run 11".
   One XP value reads "+30% XP to level". "Completionist N at run M" only in the round where it rises. A round all
   played or leveling has no header (it is in the groups above).
4. **Races**: "**Races** runs 17–48 · +40% to +100% XP to level · every class is 3/3 by now".

- **Every group to come starts open, with Fold.** Folded, its header stays, ends "· 5 loadouts", and the link says
  **Show**. Fold is the page's state, not stored, like Show on played runs today.
- **A run's group** is the lowest class rank it raises (a run taking WIZ to 1 and WAR, CLR to 2 is in Round 1); a run
  that raises no class rank is in Races. A loadout whose runs fall in two groups is a line in each, numbered with its
  own runs; Swap… on either changes only its runs still to play, as today's tooltip says.
- A plan with goals at 1 or 2 has fewer rounds; a plan of race goals alone still has its rounds (every run raises its
  classes). The header never says what the plan doesn't do.

### Swap… opened (opened.png)

Swap… on a line becomes **Close** and opens under the line, inside it, across every column but the run number. First,
in 12.5 px: "**Run 10 earns** BST 2/3 · RNG 3/3 · ENC 2/3 · Iksar 2/3 · +30% XP to level" ("**Runs 18–20 earn** …"),
with "Completionist 2" where it rises, and where a name is 3/3 already, a `--muted` line: "WAR is 3/3 already: its
rank goes up anyway, which adds XP to level and no passive." This is the pips in words, for anyone who reads the line
aloud. Then today's panel unchanged: "CHANGES THAT KEEP EVERY GOAL, CHEAPEST FIRST", the `.fixbtn` swaps with their
costs, "Or pick one yourself:" and the race and class chips. "Working it out…" while the answer comes.

### Narrow (narrow.png)

Under 1,150 px the page stacks and Your runs is full width at the bottom (about 870 px of line). The columns tighten
(run 48, race 128, classes 76 each, status at least 70, XP 92, actions 116, 10 px gaps) and the line stays one line;
the status may wrap inside its column ("35 / 50 · ≈ 7.2 h / to go"). Nothing scrolls sideways. At 150% scaling the
user's window is about 1,707 page px: the full-width form.

### Every state

- **Nothing yet** (no plan): today's "Plan every reincarnation" card and "Your runs show here once you pick a goal…",
  unchanged.
- **New, log just turned on:** no Played band, no Leveling now; Round 1 first, every pip dark gold or empty.
- **New to the app, old to the game:** the Played band holds the runs the log didn't see, "runs your log didn't see:
  in your ranks from the counts on Roster"; the pips start from Roster's ranks.
- **Too little / short plan:** one round, or one line, still with its header and the key.
- **Plenty:** 48 runs, about 23 lines and 6 headers.
- **Waiting:** while an edit's answer is on its way, the groups and lines stay as they were, faded, with "updating…"
  in the counts (rule 9). The list never redraws as one flat list or blank.
- **Keyboard:** in order, Show/Hide on Played, then per group its Fold, then each line's pin, Swap…, ⋯. Fold and Show
  are buttons with `aria-expanded`; Swap… keeps its `aria-expanded`. Nothing is only on hover. Each line's pips are
  `aria-hidden`; the line's names carry their rank in the tooltip and an `aria-label` ("WAR 3/3 once this run is
  played").

### What's reused, what's new

- Reused: `.card.camp-list`, `.h2`, `.dim`, `.lbl`, `.pips3` with `.have` and `.plan` (the tiles' dark gold), `.chip.gold`,
  `.btn.sm`, `.iconbtn`, `.link`, `.fixbtn`, `.fixes`, `PIN`, `LOCK`, `.lrow.next`'s wash and `.lrow.pinned`'s edge,
  today's played band background (`--inset`).
- New: a line grid (run · race · 3 classes · status · XP · actions, one template for the column labels and every
  line), a group header, the key row, pips at 8 px on a line (7 px reads too small beside 13 px names), the name
  colours for "reaches 3/3" and "3/3 already". The mock-up's CSS (`RL_CSS` in runs-mock.js) is a starting point.

### What the page needs that it doesn't get today

- Each run's group (Round 1–3 or Races), from the ranks it raises: the engine says it, the page groups.
- A leveling run's gains and XP to level, as a run to play has them, so its line has pips, gold names and XP.
- For each line, the race's and each class's rank once its last run is played (the mock-up counts it up on the page
  from `classRanks` and the runs; the engine knows it already).

## Dropped from campaign-rounds

- "Swapping inside a round is free": true only almost always (rules 2, 3); Swap… prices each move.
- "5 trios · all hold up": a line that only says all is well. The straddle note: the pips show it.

## Done when

- [x] Each loadout is one line in columns (run, race, three classes, status, XP to level, pin, Swap…, ⋯), each name with three pips - seen in campaign-runs-full.png
- [x] The groups in order with their headers' words: Played (folded, Show), Leveling now, Round N · classes to N/3 with runs, XP to level and Completionist, Races · every class is 3/3 by now; every group to come has Fold - seen in campaign-runs-full.png
- [x] The key on its own line with the real pips: you have it, this plan earns it, not yet, a gold and a grey WAR - seen in campaign-runs-full.png
- [x] A name reaching 3/3 is gold, a name 3/3 already is grey, and no line has a red "Two pets" or "no race passive" line - seen in campaign-runs-full.png
- [x] The next run's line has the gold wash and "next"; a leveling line has "35 / 50 · ≈ h to go" in the status column, its XP in the XP column and a lock - seen in campaign-runs-full.png
- [x] Played shown: each played run a line with its ranks then, its date and a lock - seen in campaign-runs-opened.png
- [x] Swap… open under its line: "Run N earns …" then today's swaps and "Or pick one yourself" - seen in campaign-runs-opened.png
- [x] A folded round: its header with "· N loadouts" and Show, and no lines - seen in campaign-runs-folded.png
- [x] Narrow (906 px): one line per loadout, "18–20" unwrapped, nothing cut off or scrolling sideways - seen in campaign-runs-narrow.png
- [x] Waiting: the groups kept, faded, "updating…" - seen in campaign-runs-busy.png

## Where

- `src/EqlPathfinder.Engine/Analysis/Campaign.cs`: `Evaluate` (each run's round; a leveling run's gains and XP)
- `src/EqlPathfinder.App/web/js/campaign.js`: `campList`, `campPlayed`, `campOpenRow`, `campSwapPanel`
- `src/EqlPathfinder.Engine/Analysis/Campaign.cs` line ~987: the "Two pets" and "no race passive" warnings
- `src/EqlPathfinder.App/DevSnapshots.cs`: the shots above (opened, folded, narrow and busy are new)

## Reviews

- **architect:** FIXED 11-20 (the engineer): `Campaign.Evaluate` gives done and leveling runs their ranks at the time (today minus one per later reincarnated run holding the name) and a done run its `At`; the page draws a done run as a played line with its date (campPlayedLine); `campLines` no longer counts ranks, a line reads its last run's (`campRankOf`); the leveling test asserts on the text before "Your runs"; new engine test (ranks 1, 2, 3 and the date) and page test (a done run in Played with its date), and the snapshot shot has a done run. Original finding: one must fix. A plan's own done run (state "done", in the Played band) is drawn by `campLine` with today's ranks under "once this run is played" and no date, and no test or shot has one; the same today's-ranks reading makes the earlier of two leveling runs sharing a class show a higher XP to level than its bar. Fix: `Campaign.Evaluate` gives done and leveling runs their ranks then (today's minus one per later reincarnated run holding the name), the page draws done runs as played lines with their date, and a page test has a done run. Should fix: `CampaignPageTests.ARunReincarnatedBelowTheCapReadsLevelingWithItsLevel` keeps its old meaning by asserting the top card (before "Your runs") has no "Leveling now"; `campLines` counts ranks up on the page where each run's gains and past already say them. Sound: Round, the dropped warnings and their tests, the fold and played-band tests, consumers of the leveling runs' new gains and XP (all filter to runs to play, or return early on ranks you have), nothing new stored or sent; no clock- or order-dependent test found among the branch's own.

## Reviews
- **designer:** FIXED 11-20 (the engineer): narrow XP ranges stay on one line (nowrap, XP column 100 px), a leveling status keeps "≈ 6.2 h to go" together, Swap… has a min-width so the pin stays put; the snapshot's played-runs mismatch stays in docs/debt.md. Original: no must fix. Checked full, opened, folded, narrow and busy against What the player sees and rules 1, 2, 4, 5, 9, 10, 12, 14, 21, 22, and rendered the user's own 48-run plan narrow at Races (not in the shots). Accepted the engineer's deviations: narrow 48/118/3×72/60+/84/130 with 8 px gaps, played names plain, a leveling line's gold from current ranks, no Round 1 in the snapshot. Three should-fix to docs/debt.md: narrow XP ranges wrap to two lines on every Races line, the ≈ parted from its hours in a narrow leveling status, and the opened shot's played runs disagreeing with the tiles (plus the pin moving when Swap… becomes Close).
