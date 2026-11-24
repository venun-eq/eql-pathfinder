# Runs grouped by round, so you can see which hand edits are free

**Replaced, 11-19, by [campaign-run-list](campaign-run-list.md)** ([campaign-page-redesign](campaign-page-redesign.md)):
its round headers and folding are kept there; "swapping inside a round is free", "all hold up" and the straddle note
are dropped, and that draft says why. Kept for the record. **Was:** a draft ([campaign-combos](campaign-combos.md), the
rest of the design page's "D · Runs by round"). The `designer` goes first; the `engineer` writes the rest of the spec, the failing tests and the code.
**Tier:** standard with the designer: it groups and labels runs the planner already made, and changes no choice.
**Built behind** [campaign-planner-beta](campaign-planner-beta.md)'s switch: without it, Campaign is as today.
**Waits for:** campaign-planner-beta (campaign-run-chips was parked by the user, 11-19). **Unblocks:** nothing.

**Why:** the runs fall in three rounds (every class to rank 1, then 2, then 3), and within a round every run costs
the same XP to level, so moving a class between two runs of one round is almost always free. Today the run list is
one long list, and the player can't tell a free hand edit from a costly one. After, runs are grouped by round, each
round says its XP to level, and Swap… marks the moves that are free.
**Players:** everyone with a campaign who edits it by hand.

```text
Plan → Campaign, the run list (made-up numbers)

Today                          After
Run 1  IKS  SHD / ROG / SHM    ⟦Round 1 · every class to 1 · runs 1–5 · +10% XP to level · swapping inside a round is free⟧
Run 2  ERU  PAL / MNK / ENC    Run 1  IKS  SHD / ROG / SHM
…                              …
Run 6  HUM  WAR / CLR / WIZ    Run 6  HUM  WAR / CLR / WIZ  ⟦WIZ reaches 1 while WAR and CLR reach 2, so +20%⟧
…                              ⟦Round 3 · every class to 3 · 5 trios · all hold up · Show⟧
```

The full mock-up is the Campaign Combos design notes' section "D · Runs by round".

## The cause

The engine sends each loadout with its gains but no round; `campList` draws one flat list. Swap… (`Options`) prices
each move with `CampaignDelta`, but nothing ties a free move to the round it's in.

## What the user decided (11-19)

- Time first ("relevel faster"): a hand edit's cost is shown, never hidden (design.md rule 8).
- Nothing guesses a trio's pace: a round's line says XP to level and runs, never hours.

## What the player sees, in short

The design page's "D" mock-up is the source; the designer writes the exact words and states here.
- **A round's header:** "Round 1 · every class to 1", its runs, its XP to level ("+10% XP to level"), and "swapping
  inside a round is free".
- **A round that isn't next** folds to one line: "5 trios · all hold up · Show".
- **A run that straddles two rounds** (run 6: WIZ reaches 1 while WAR and CLR reach 2, so +20%) has a note that says so.
- **Swap…** marks the moves inside a round as free.

## Proposal

- The engine says each run's round from the ranks it raises and sends it with the page data; campaign.js groups.
- "Free" on a move comes from the move's own price (`CampaignDelta`), never from being in the same round.

## Open checks

- "Swapping inside a round is free" is said of the whole round but true only "almost always" (design.md rules 2 and 3):
  the header may say it only when every move in that round is free, or say less.
- Runs past 16 in a plan with races, where classes are done: what a round is there (a race's rank, or no rounds).
- Played runs (locked, rule 5): a round that's all played, and one that's part played.
- A plan shorter than three rounds (goals at 1 or 2).

## Where

- `src/EqlPathfinder.Engine/Analysis/Campaign.cs`: a run's round, `Options` (Swap…)
- `src/EqlPathfinder.App/web/js/campaign.js`: `campList`, `campSwapPanel`, `campAskOptions`
