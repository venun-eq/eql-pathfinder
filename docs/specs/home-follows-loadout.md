# Home follows the loadout you're on

**Why:** feedback report #7 (0.10.7): the player swapped to their main, PAL/MNK/SHM Froglok at 50, and Home still
showed "Leveling now · relevel 7 · WAR/CLR/RNG Kerran 32 / 50", the relevel they'd swapped away from. Home picks the
releveling state whenever any relevel is open (`homeMode`, home.js), whichever loadout is on. The user decided:
Home shows a relevel only once it knows the character on now is below the cap; at the cap it opens on Farming,
always. The cap comes from the data (`levelCap`): 50 now, 55 from Kunark on 2026-12-08. This branch also pays
docs/debt.md row 11: the engine sends Home's state (`reinc.home`), and home.js keeps only the player's pick.
**Players:** every regular who swaps loadouts, and especially one with several relevels open at once (the reporter
spent a reincarnation token mid-relevel: run 7 Kerran WAR/CLR/RNG at 32 and run 8 Barbarian BRD/DRU/NEC at 1 are
both open).

## What the player sees

Home's hero has five states. The engine picks one from what the log says about the character on now; nothing on
the page guesses. "Known" means a /who since the last loadout swap (or a reincarnation), or the trio carried from
one with no level jump since. "The cap" is `levelCap` everywhere below, never a literal 50.

| The character on now | Home shows |
|---|---|
| Level known and at the cap | **Farming** (auto), or **Next relevel** if the player picked it |
| Level known, below the cap, trio known, and a relevel of that trio and race is open | **Releveling**: that relevel |
| Level known, below the cap, trio known, no open relevel for it | **Leveling** (as today) |
| Level known, below the cap, trio not known, no relevel open | **Leveling** (as today, "Your character") |
| Level known, below the cap, trio not known, a relevel open | **Swapped loadouts** (new) |
| Level not known (no level line in the log yet) | **Swapped loadouts**, its "no level yet" wording |

Never the releveling state on a guess: not from "the first open relevel", and not from a relevel whose level
happens to match.

### At the cap: Farming, always first

Mock-up: the lower card in the design session's `mock-home.png` (drawn on real data: its session numbers are that log's).
- The hero is `farmingHero` as today, on the trio and race on now.
- The switch's line under it: **"Auto: you're at 50"** (the cap). With Next relevel picked: **"Your pick · auto says
  farming"** (as today). The old "Auto: a crawl at 50 in the last 2 hours" / "no crawl…" lines go, with
  `farmingLately`.
- **The pick lasts while the app runs and Home stays at the cap.** It is no longer kept in localStorage: every start
  opens on Farming, and leaving the cap (a swap to a loadout below it) drops the pick.
- At the cap with the trio not known (a swap that jumped the level to the cap, before /who): the trio line reads
  **"Loadout not known yet"** with the race left out, and the same `.trio` size. Today it says "At the cap".

### Parked relevels (new, on every state but Swapped loadouts)

Under the trio line in the hero, when a relevel is open that isn't the one on now:

```
PARKED RELEVELS                                  (.lbl; "PARKED RELEVEL" for one)
WAR/CLR/RNG Kerran      32 / 50   ≈ 6.2 h to go
BRD/DRU/NEC Barbarian    1 / 50   not played yet
```

- One row per open relevel other than the one on now, oldest reincarnation first. A three-column grid,
  `font-size: 12.5px`, 16px between columns, 4px between rows, 12px above the label. Trio in 600 weight, race
  `.muted`; level `.num.muted` as "32 / 50" (the cap); then `.num.gold` "≈ 6.2 h to go" from the relevel's
  `etaHours`. With no `etaHours`: "not played yet" (`.dim`) when it has no crawls, otherwise nothing in that column.
- At most three rows; more reads "+ 2 more in History → Relevels" (`button.link`, `data-go="relevels"`).
- Shown on Farming, Next relevel, Releveling and Leveling. Nothing at all when no other relevel is open.

### Releveling: the relevel of the loadout on now

- Everything in the state (hero, the race chart, Splits, Place by place, the route strip, Up next's first step,
  the roster's "now" cells and race chip) reads the open relevel of the trio and race on now, not the first open one.
- **"Leveling now · relevel 8"**: the number is the relevel's place among every relevel by when it started (its
  reincarnation), finished or not, so Kerran stays 7 and Barbarian 8 whichever finishes first. Today it is
  "finished + 1", which calls both of them 7.
- Other open relevels show as Parked under the trio and XP chip.

### Swapped loadouts (new)

Mock-up: the upper card in the same `mock-home.png`. Below the cap, the level jumped (a swap), no /who yet, and a relevel is
open. Reuses `.card.hero`, `.lbl`, `.trio`, `.big.num`, `.muted`.

- Label: **"SWAPPED LOADOUTS"**.
- Title (`.trio`): **"Type /who in game"**.
- Under it (`.muted`, max-width 620px): **"Your level changed to 32 without a ding, so you're on another loadout. The
  log names it at your next /who, and Home follows it from there."**
- Right: the level as on the other heroes, **"32 / 50"**, and under it (`.num.muted`) **"loadout not known yet"**.
- Below: the open relevels in the Parked grid, labelled **"OPEN RELEVELS"** (none of them is assumed to be on).
- Main and side: as Leveling (the relevels card; nothing on the side). Up next and the roster show, with no "now"
  cells and no "· now" race chip.
- **No level yet** (a log with no level line): label **"YOUR CHARACTER"**, the same title, and **"Pathfinder hasn't
  seen your level in this log yet. /who shows your level, classes and race, and Home follows it from there."** No
  level on the right. The open relevels list if any. Today this player sees "Leveling · 1 / 50", a guess as a fact.

### Next relevel (the player's pick at the cap)

- `betweenHero` as today, with Parked relevels under the trio.
- **"Up next · relevel 9 · run …"**: the relevel number counts every relevel started, open ones too (two open after
  six finished: 9, not 7). The run number is Campaign's, and stays as it is (branch campaign-open-relevels).

### Up next (every state that shows it)

- One "Finish" step for each open relevel, the one on now first: **"Finish WAR/CLR/RNG Kerran"** (the race added,
  since two open relevels can share a trio), "32 → 50 in about 6.2 active hours." plus the route sentence only for
  the one on now. A parked one's text ends **"Parked."** after its hours, and one with no hours reads "1 → 50, not
  played yet. Parked."

### What doesn't change

Leveling's and Farming's cards themselves, the Ask route lens on Home's route card (a later branch), Campaign's run
numbers and done (campaign-open-relevels), the overlay. Waiting: "Reading your history…" as today.

### The window

At 150% (1701 px wide) and under 1150 px the Parked grid keeps its three columns (the longest row is about 330 px);
the Swapped hero's level column stays `flex: none` beside text that wraps.

## Done when

- [x] At the cap on PAL/MNK/SHM Froglok with WAR/CLR/RNG Kerran (32, ≈ 6.2 h) and BRD/DRU/NEC Barbarian (1, no crawls) open, Home opens on Farming, "Auto: you're at 50", with "Parked relevels" listing both rows - seen in main-home-farming-parked.png
- [x] Next relevel picked at the cap: "Your pick · auto says farming", Parked relevels under the trio, "Up next · relevel 9" - seen in main-home-flipped.png
- [x] On BRD/DRU/NEC Barbarian at 1 (known by /who) once it has a crawl, Home shows "Leveling now · relevel 8 · BRD / DRU / NEC Barbarian", its own race and splits, and Kerran as a parked relevel - seen in main-home-relevel-parked.png
- [x] Below the cap after a level jump with no /who: "Swapped loadouts", "Type /who in game", "Your level changed to 32 without a ding…", "32 / 50 · loadout not known yet", both relevels under "Open relevels", no "now" cells in the roster - seen in main-home-swapped.png
- [x] No level line in the log: "Your character", "Type /who in game", "Pathfinder hasn't seen your level…", no level on the right - seen in main-home-no-level.png
- [x] Up next lists "Finish WAR/CLR/RNG Kerran" and "Finish BRD/DRU/NEC Barbarian", the parked one ending "Parked." in the flipped and farming shots (Swapped has none) - seen in main-home-flipped.png
- [x] With the cap set to 55 in the data, a character at 50 is below the cap: Home shows its relevel or Leveling, "32 / 55", never Farming - an engine test of Home's state

Not played yet (no journey: `Journeys.Build` needs visits after the reincarnation): the relevel is open from the roster's reincarnation with its level from the roster, listed Parked and in Up next ("not played yet"). On now by /who it draws as Leveling until its first crawl (docs/debt.md).

## Reviews

- designer: reviewed the five shots against the spec: Up next said "Parked." on Swapped and no-level (fixed in 34cbda7); an unplayed relevel's "0.0 h" comparisons and the "+20% XP to level" header with no loadout known went to docs/debt.md; "finish open relevels before a new one" is a later branch (user, 11-18).
- architect: three reviews. First: an unknown race matched any race, and the page matched journeys a second time (both fixed in 34cbda7, with the other branch's finding that an unplayed reincarnation has no journey). Second: `Relevels.Of` counted the real log's relevels twice and an unplayed trio read DRU/BRD/NEC (fixed in 05fa8cc). Third: clean; the burst test fails on the old code, and the real log reads Kerran 7, Barbarian 8, next 9.
- Designer, first review: Up next said "Parked." on the Swapped and no-level states (fixed in 34cbda7); two items to docs/debt.md.
- Architect, second review (34cbda7), 11-18: must fix. On the real log `Relevels.Of` counts 12 relevels, not 8: Home reads "Up next · relevel 13" and the parked rows 11 and 12. The four reincarnations of 11-11 came within 3 minutes, `Campaign.Events` gave them other classes than the journeys have, so no journey matched an event and each was counted twice. Swapped and no-level Up next have no "Parked." (rendered); the parked rows draw from the engine's row.
- Engineer, second review fixes: `Relevels.Of` pairs each journey with the nearest unused event within 300 s by time alone (nearest pairs first) and takes trio and race from the journey; an unplayed relevel's trio is in key order; tests for the crossing burst, done-once-reincarnated-again, and trio order.
- Architect, third review (05fa8cc), 11-18: clean. The burst test fails on 34cbda7's Relevels.cs (1 of 16 HomeStateTests red) and passes on 05fa8cc's; the real log's `homeNow` (preview built 13:32, after the commit) reads Kerran 7, Barbarian 8, next 9. Ready to finish.
