# Campaign: a run is done when its loadout is back at the cap

## Why

Feedback report #6 (app 0.10.7, Campaign): "What does "done" mean? I reincarnated, but they're not level 50 yet.
Also, because I reincarnated, but haven't finished leveling them up, the campaign says Run 9 of 48. I'm still on run
7." The player had reincarnated Kerran (run 7, now level 32) and then Barbarian (run 8, level 1), the second only to
spend a dungeon-crawl token they couldn't hold. Campaign counted both runs done the moment the reincarnation was
logged: both rows read "done" in green, while the header said "Leveling now: Kerran · WAR / CLR / RNG (run 7)", and
the big number jumped to the next run to reincarnate, "Run 9 of 48".

Reincarnating a parked loadout to spend a token is normal play, not off-plan: two or more runs can be open at once.

## What the player sees

Mock-up, drawn on the report's own data in the real window: `mock-campaign-open-relevels.png` in the design
session's scratchpad (the hero and runs 7 to 15). The (i) after "+20% XP to level" is left out of the mock only.

**Words.** A run is **done** when its loadout is back at the cap after that run's reincarnation. Reincarnated and
below the cap, it is **leveling**, whichever loadout the player is on now: Campaign doesn't say which one is on (Home
does, from /who). "The cap" is the data's (`cap()` / `levelCap`) everywhere below: 50 now, 55 from Kunark. Never a
literal 50 in code. A run's level and hours are the ones Home shows for the same relevel (branch home-follows-loadout:
`32 / 50 · ≈ 6.2 h to go`, or `not played yet` with no crawls since its reincarnation).

A run counts as leveling only when the log saw its reincarnation and its relevel is still open. A run marked done by
hand ("Mark a run done", for a reincarnation while the log was off) has no level Pathfinder can see, so it reads
done, as today: never a level it guessed (rule 2).

### The hero

**No run leveling** (every reincarnated run is back at the cap): as today. "Run 9 of 48", then "Next: **Wood Elf ·
MNK / SHM / MAG** at +20% XP to level (i)". The gold "Leveling now: …" line goes in every state.

**One or more runs leveling** (the report's case):

```
Run 7 of 48
Leveling: Kerran · WAR / CLR / RNG · 32 / 50 · ≈ 6.2 h to go
Also leveling: run 8 · Barbarian · BRD / DRU / NEC · 1 / 50 · not played yet
Next to reincarnate: run 9 · Wood Elf · MNK / SHM / MAG at +20% XP to level (i)
```

- The big number (`.fc-big`) is the **earliest run still leveling**, "of" the plan's last run, both through
  `campRunNo`.
- "Leveling:" line: `.muted`, margin-top 8px, the race and trio in `<b style="color: var(--text)">` as the Next line
  is today; the level `.num` as "32 / 50" (the run's level / the cap); then `.num.gold` "≈ 6.2 h to go". With no hours:
  `.dim` "not played yet" when the relevel has no crawls, otherwise nothing after the level.
- "Also leveling:" line, only when another run is leveling: `.muted`, 12.5px, margin-top 4px. Each other leveling run,
  earliest first, as "run 8 · Barbarian · BRD / DRU / NEC · 1 / 50 · not played yet" (hours as above), joined with
  "; ". At most two; more reads "…; and 2 more below".
- "Next to reincarnate:" line: `.muted`, margin-top 4px, "**run 9 · Wood Elf · MNK / SHM / MAG** at +20% XP to level"
  and the `xp` (i) as today. With no run left to reincarnate: "Every run in this plan is reincarnated. It's done when
  the last one is back at the cap."
- **Done** (big "Done", "Every run in this plan is played.") only when no run is next and none is leveling.
- Home's "Up next · relevel 9 · run …" keeps the next run to reincarnate: `campNext()` still returns run 9, not the
  big number.

### Your runs

A loadout row with a run leveling (runs 7 and 8 in the report):

| Part | Today | Now |
|---|---|---|
| Title (`.lt`) | `Kerran · WAR / CLR / RNG` | the same, then `<span class="chip">leveling</span>` (plain chip: the gold one stays "next") |
| Line under (`.le`) | `run 7` | `run 7 · ≈ 6.2 h to go` (hours `.gold`), or `run 8 · not played yet` |
| Dot (`.ld`) | filled gold, as a done run | `.ld.leveling` **half filled**: `background: linear-gradient(90deg, var(--gold) 50%, #5a4820 50%)`, gold border. Title "Run 7: leveling, 32 / 50" |
| Right column (`.lxp`) | green `done` | `<span class="gold">32 / 50</span>`, title "Level 32 of the cap, 50. The run is done when this loadout is back at 50." |
| Row | `.lrow.done` (title muted) | not `.done`: the title stays full colour until the run is done |
| Swap… | disabled, "Played: these runs keep their loadout" | disabled, "Reincarnated: this run keeps its loadout" |

- A loadout with several runs, one of them leveling and one still planned: the `.le` line reads "runs 7–15 · run 7
  leveling, 32 / 50 · ≈ 6.2 h to go · " then what the planned runs earn, as today; the right column keeps the planned
  runs' XP. Done and leveling together, nothing planned: the right column shows the level.
- The green `done` and the muted title are kept for a loadout whose every run is done, by the rule above.
- The summary line in the list's header: "6 played before · 0 done · 2 leveling · 40 to go · 26 loadouts".
  "· N leveling" only when N > 0. Played before + done + leveling + to go is the plan's last run (6 + 0 + 2 + 40 = 48).
  "40 runs to go" in the cheapest-path line and the total line keeps meaning reincarnations still to make.

### Help

Help → How Pathfinder counts → Campaign (index.html, the item "Reincarnations your log sees…"): "Reincarnations your
log sees after you build the plan tick its runs off." becomes "A run is done when its loadout is back at the cap after
its reincarnation. Until then it reads leveling, with its level: you can have several leveling at once, say to spend
a crawl token on a parked loadout." The rest of that item stays.

### The players

- **The regular** (the report): as above. Run 7 of 48, both open runs with their levels, run 9 next.
- **New, log just turned on**, no plan or no reincarnation in the log: nothing changes.
- **New to the app, old to the game**: the runs played before the plan are unchanged ("1–6 played before"). A plan run
  marked done by hand reads done (no level to show).
- **Casual**: "32 / 50" and the hours are Home's for the same loadout; the word "leveling" is Campaign's own for a run below the cap (Home says "Parked" for a loadout not on now: docs/design.md rule 13).
- **Waiting** (`busy`): as today, "updating…", and the rows keep their last state faded.
- **The window**: at 150% (1701 px) the mock-up fits; the right column is 108px and "32 / 50" is about 45px. The hero's
  "Also leveling" line wraps under 1150px like the Next line does today.

### Not in this branch

Home's states (home-follows-loadout), the token progress (a roadmap maybe), the goal lens.

## Done when

- [x] The hero reads "Run 7 of 48", then "Leveling: Kerran · WAR / CLR / RNG · 32 / 50 · ≈ …", "Also leveling: run 8 ·
  Barbarian · BRD / DRU / NEC · 1 / 50 · not played yet" and "Next to reincarnate: run 9 · Wood Elf · …" - seen in
  main-campaign.png (or a shot of this state added to DevSnapshots)
- [x] Runs 7 and 8 have the "leveling" chip, a half-filled dot and "32 / 50" / "1 / 50" in gold where "done" was, and
  their titles aren't muted - seen in the same shot
- [x] The list header reads "… · 0 done · 2 leveling · 40 to go · …" - seen in the same shot
- [x] With every reincarnated run back at the cap: "Run 9 of 48", "Next: …", no "Leveling" lines, runs 7 and 8 green
  "done" - seen in a second campaign shot
- [x] No "Leveling now:" line anywhere, and no literal 50 in campaign.js - seen in both shots
- [x] Help's Campaign item has the new sentence - seen in main-help.png

## Edge cases

Each of the cases that have bitten (docs/quality.md) that applies, and what happens:

- **Numbers (decided by the lead):** runs already reincarnated take runs 1..D of the plan in the order they happened
  (a run ticked off by hand, with no time, before them), then the runs still to make in plan order. Home numbers
  relevels by when each started, so the two agree. A loadout's runs are no longer always consecutive: the row lists
  them as "runs 7, 9-10".
- **Which relevel is which run:** `Relevels.Of` (Home's own, built from the log's reincarnations; a journey is paired by time alone, within 300 s). A run takes the relevel whose reincarnation event is its own (`Relevel.EventAt`), exactly: no race or trio is compared, so a burst of reincarnations minutes apart cannot swap two. Done at the cap or once a class reincarnated again.
- **Two open relevels of one trio** (reincarnated twice, the first never finished): both leveling, each with its own level.
- **A run ticked off by hand** reads done: no journey, no level, never a guess.
- **Reincarnated, never played:** `Journeys.Build` makes no journey without a visit, so there is none: the relevel has no journey, `Crawls` 0 and no hours, "not played yet", the level the roster holds. It reads leveling, never done.
- **Reincarnated before the plan began** (the report's own state): the open relevel is matched to its played-before event, taken out of "played before" and sent as a run of its own (`Loadout` -1, numbered first). Header "6 played before · 0 done · 2 leveling · 40 to go", hero "Run 7 of 48"; the row has no Swap…, pin or menu. Back at the cap it goes back to "played before".
- **The cap** is `levelCap` (55 on Kunark): the page writes "32 / 55" when the data says 55; the engine's "done" is the
  journey's own (`Journey.Done`, at `Options.LevelCap`).
- **Busy / no plan / no relevels:** as today; with no relevels sent (`relevels` null) every reincarnated run is done.
- **A swap of a loadout with a leveling run:** disabled with "Reincarnated: this run keeps its loadout"; one with a
  leveling run and planned runs splits as today (the reincarnated count stays `done`).

## Not doing

Which loadout you are on (Home's job), Kunark's cap change itself, Home's states.

## Where

- `Analysis/Campaign.cs`: `Matched`, `Evaluate` takes `Relevels.Of`'s rows in place of journeys (`Relevel.EventAt` added), orders runs by when they happened, gives `CampaignRun` `Level`, `EtaHours`, `Crawls`, and `CampaignView` `Leveling`.

- `Live/TrackerService.cs`: `CampaignData` builds `Relevels.Of` and passes it.
- `web/js/campaign.js`: hero, rows, header; `web/index.html`: the Help sentence.
- Tests: `CampaignTests` (the engine), `CampaignPageTests` (the page, with a cap of 55 in the data).
- Same helper as Home: `Relevels.Of` (Analysis/Relevels.cs), so the numbers agree.

## Reviews
- architect: first review found a reincarnated, never-played run read "done" (no journey exists for it), and proofs resting on a hand-made 0-crawl journey; Campaign now takes `Relevels.Of`, matches by reincarnation time (`EventAt`), and the tests use the real path (roster events, no journey for an unplayed relevel). Second review: no must fix, no conflict markers left; should fix: a leveling run's trio from `Relevel.Trio` (Home's) not the event's or plan's order, and an open off-plan relevel after the plan's start numbered by time with the matched runs rather than first. Fixed in the next commit: the trio from `open.Trio` (tested with the roster in another order), and off-plan open relevels after `Since` merged by time (tested).
- designer: first review, on the real data, found Kerran and Barbarian (reincarnated before the plan) left out: Run 9 of 48, "8 played before"; they are now leveling runs of their own (7 and 8, "6 played before · 0 done · 2 leveling"), and the shot sets `loadouts[i].done` so Swap… draws disabled. Second review (r4 renders): hero, header and rows as specified, numbers 7, 8, next 9 and "1 / 50 · not played yet" as Home; left to act on: trio order (CLR / RNG / WAR, the architect's call), "leveling" where Home says "Parked" or "Open relevels", the leveling rows' dot and level about 190px right of the column below, and the DevSnapshots leveling shots throwing on real data (`loadouts[-1]`), so "back at the cap" was not seen. Fixed in the next commit: the open rows keep an invisible Swap... cell so the columns line up, DevSnapshots guards loadout -1 and the back-at-the-cap shot now drops the two runs into "8 played before"; rendered on a copy of the real data: Run 7 of 48 leveling with WAR / CLR / RNG Kerran and BRD / DRU / NEC Barbarian, then Run 9 of 48, "1-8 played before", "Next: Wood Elf". The word stays "leveling" (design.md rule 17).
