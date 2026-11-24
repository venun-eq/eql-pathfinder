# Each pip on the goal tiles means one thing, and the page says what

**Built 11-20 in campaign-page-layout's branch, shipped in 0.10.24** ([campaign-page-redesign](campaign-page-redesign.md)). **Built in
[campaign-page-layout](campaign-page-layout.md)'s branch** (the user, 11-20), with campaign-goal-presets: no branch of
its own. The pips, the cells and their words on both tiles, page only. **Built behind** the beta switch. The race
tile's cells become buttons in campaign-race-goals; whichever of the two branches finishes second carries that in.

**Why:** after Everything 3/3 every class and race cell turns gold, and every pip is ringed, some bright and some dark.
The user: "Every class and race lights up yellow with yellow circles, some brighter than others, and nothing tells me
what the difference means." Today a cell is gold when it has a goal, a pip is solid gold for a rank you have and dark
gold for one this plan earns, and every rank up to the goal is ringed. Only the ring is explained on screen ("a gold
ring marks your goal"); the rest is in hover titles a player never finds. After, a pip means one thing: filled for a
rank you have, dark gold for one this plan earns, empty for one it doesn't. The ring sits on the goal rank alone, the
cell stays plain, and a legend under the tiles says all four.
**Players:** everyone on the beta with a campaign; worst for a new player reading the tiles for the first time.

```text
Your goals, classes (made-up ranks; mock-up: page-b-full.png and page-b-empty-full.png)

Today                                     After
[WAR ●◎◎]  gold cell, every pip ringed    WAR  ● ◉ ◎        ← ring on the goal rank only, no gold cell
[CLR ●●◎]                                 CLR  ● ● ◎
                                          ● you have it  ◉ this plan earns it  ○ not planned  ◯ your goal
                                          Click a pip to set the rank you want.
```

## What the user decided (11-19)

- "Nothing tells me what the difference means": each mark has one meaning, and the screen says it, not a hover title
  (design.md rule 13).
- The ring sits on the goal rank alone (11-20, "I agree with all of your picks"): with Everything 3/3, one ring per
  cell, not three.

## What the player sees, in short

The designer's words and states are in [campaign-page-layout](campaign-page-layout.md#what-the-player-sees),
with this draft's Done when; the lines below are the draft's.

- **A pip:** filled gold = a rank you have; dark gold = a rank this plan earns; empty = not planned. One ring, on the
  goal rank. A goal the plan misses keeps today's red dashed ring, the one warning (rule 21: something the player
  asked for that can't happen). Past your goal stays faded.
- **The cell** has no fill of its own: the ring says it's a goal.
- **The legend**, one line under the tiles: "● you have it · ◉ this plan earns it · ○ not planned · ◯ your goal",
  then "Click a pip to set the rank you want." on a line of its own (in the mock-up the "·" wrapped to the start of a
  line).
- **No goal yet:** "27 of 48 ranks earned · no goal yet", never "0 of 0 ranks you want" (today's empty plan, rule 4: a
  zero that reads as a fact).
- **Every goal met already** and other states as campaign-first-lane's race tile has them.

## Open checks

- The dot meaning: settled in campaign-page-layout's What the player sees (solid gold you have it, dark gold the
  plan earns it); campaign-run-list follows it.
- Colour-blind reading: filled against dark gold must differ in more than hue (a ring or a size).
- 150% scaling: three pips and the ring at 1,030 px column width without touching (rule 12).

## Where

- `src/EqlPathfinder.App/web/js/campaign.js`: `campGoalTiles`, `campPips`
- `src/EqlPathfinder.App/web/app.css`: the pip and cell classes (lines 462–473 at 0.10.23)
