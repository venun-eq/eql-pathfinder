# Campaign as a fresh player meets it: runs near the top, marks that say what they mean, only what you chose

**Index, 11-19. Not a change of its own:** this page lists the drafts the Plan → Campaign redesign was split into, and
the order they go in. Each is built in its own branch and session, sized by the `feature` skill. It follows
[campaign-combos](campaign-combos.md), whose decisions all stand. Everything is behind the beta switch
([campaign-planner-beta](campaign-planner-beta.md)): players without it see no change.

**Why:** the user walked Campaign as a new player (beta and the new planner on, 0.10.23): Start over, Plan every
reincarnation → Everything 3/3, then scrolled. The ready-made goals vanished after the click; every class and race
tile turned gold with rings of two brightnesses and nothing saying which was which; Races you want listed all 16
races, none chosen by the player; and the first run was 2,043 px down at full width, 2,785 px narrow. Each run row was
mostly empty space. After: the header is one band, goals and what you want sit beside the runs, the first run is
324 px down, each mark has one meaning on screen, a fresh plan's lists are empty, and the runs read as a table by
round.
**Players:** everyone on the beta with a campaign. Worst for a new player, and for a portrait monitor like the user's
(about 1,707 page px wide at 150%).

| | Full width (1,625 px) | Narrow (906 px) |
|---|---|---|
| Today, empty plan | [today-full-empty.png](.webp) | [today-narrow-empty.png](.webp) |
| Today, Everything 3/3 | [today-full-1.png](.webp), [-2](.webp) | [today-narrow-1.png](.webp), [-2](.webp) |
| Recommended page (B), empty plan | [page-b-empty-full.png](.webp) | |
| Chosen page and runs (B + B, the user, 11-20) | [runs-b-full.png](.webp), [opened](.webp) | [runs-b-narrow.png](.webp) |
| The other designs | page A, C; runs A, C: in each draft's table | |

## What the user found, and the draft for each

| The user found | Draft | Tier | Budget |
|---|---|---|---|
| A race's goal is its rank, so Everything 3/3 ranks all 16 ("the zero state is the default, and the plan holds only what I override", now design.md rule 23) | [campaign-race-goals](campaign-race-goals.md) | Risky + designer | 700k |
| The header, tiles and tools take a lot of room before the first run | [campaign-page-layout](campaign-page-layout.md) | Standard + designer, one branch with the next two (the user, 11-20). **Shipped in 0.10.24** | 500k for the three |
| The ready-made goals vanish after one click | [campaign-goal-presets](campaign-goal-presets.md) | in campaign-page-layout's branch. **Shipped in 0.10.24** | |
| The goal tiles' yellows and rings mean nothing on screen | [campaign-goal-tiles](campaign-goal-tiles.md) | in campaign-page-layout's branch. **Shipped in 0.10.24** | |
| Should a fresh plan come with a combo? | [campaign-combos-example](campaign-combos-example.md): offered, not stored | Small + designer | 160k |
| Your runs is long, empty and hard to read across | [campaign-run-list](campaign-run-list.md), replacing [campaign-rounds](campaign-rounds.md) | Standard + designer | 400k |
| Classes you want first starts empty: keep it | (no draft: kept) | | |

About 1.75M in all, about $14 at 11-19's rate (1.5M in sizes was $12).

## Kept

The user's decisions in campaign-combos.md and docs/design.md: the cheapest path by default; Musts and Unlock now
held above it, with their price; a rank only breaks ties; everything behind the beta switch. Classes you want first
starts empty, as today.

## The order

```
campaign-race-goals ─────────────────────────────────────────────┐
campaign-page-layout (+ goal-presets, goal-tiles) ─┬─► campaign-combos-example ─┐
                                                   ├─► campaign-run-list ───────┴─► campaign-synergies ··► (later) community ratings
                                                   └─► campaign-came-across-rebuild  (also after race-goals)
```

| Row | Drafts | Waits for |
|---|---|---|
| 1 | race-goals, and page-layout with goal-presets and goal-tiles (side by side: one is the engine, one the page). Race-goals makes the race tile's cells buttons; whichever finishes second carries that into the other's tile | nothing |
| 2 | combos-example, run-list | page-layout. Run-list takes the dot meaning page-layout's designer settles for the tiles |
| 3 | [campaign-came-across-rebuild](campaign-came-across-rebuild.md) (already drafted) | race-goals (a plan brought across keeps race goals as goals, so the card's race line changes) and page-layout (the card sits above the runs, run-column wide) |
| 3 | [campaign-synergies](campaign-synergies.md) (already drafted) | combos-example (its Core pairs can become the empty card's example) and run-list (where "(suggested)" goes: on the line or in what a click opens); its "Pairs worth a look" card goes in the side column under Your combos |

## Found on the way, folded into the drafts

- Run 28's red "Two pets: only one is up at a time…" on a trio Pathfinder chose itself (rule 22): run-list.
- The empty plan's "0 of 0 ranks you want" (rule 4): goal-tiles.
- The empty plan shows Every class 3/3 twice; a preset click empties the loadouts, pins included: goal-presets.
- Narrow, "39 runs from here · 46 with the 7 / you've played" wraps inside the big number: page-layout.
- Leveling runs show no rank dots in run list B: the page data doesn't send what they earn: run-list.

## The user's answers (11-20)

Asked on a review page with every design's mock-ups (saved under `campaign-page-redesign/`); the user took every
recommendation, with no notes: "I agree with all of your picks."

1. **The page:** B, a side column (campaign-page-layout).
2. **The runs:** B, a table by round. campaign-rounds is retired, and every round to come stays open with Fold
   (campaign-run-list).
3. **Combos:** no combo in a fresh plan; the empty card offers one, a trio the player has played twice or more, else a
   pair already together in one of their runs, as a Prefer (campaign-combos-example).
4. **The goal ring** on the goal rank alone (campaign-goal-tiles).
5. **Plans saved by 0.10.23 with all 16 races ranked** (all at 3/3, none Unlock now): the list empties once, with a
   line saying so; goals stay; any other list stays as stored (campaign-race-goals).
6. **Layout, presets and tiles are one branch,** under campaign-page-layout's name, about 500k.

The mock scripts (`page-mock.js`, `runs-mock.js`, `today.js` and `run.ps1`) are in the session's scratchpad
`campaign-redesign` folder and may be gone by the time a draft is built; the PNGs here are the record.
