# Pathfinder suggests class pairs that work together, and you decide

**Draft, 11-19, not started** ([campaign-combos](campaign-combos.md), the design page's "C · Suggestions"). The
`designer` goes first, then `qa` writes the rest of the spec and the failing tests, then the architect's Decisions.
**Tier:** risky. The design page sized it standard, but it stores what the player hides and the suggestions switch,
and suggested pairs break the planner's last ties: both are risky by CLAUDE.md. **Built behind**
[campaign-planner-beta](campaign-planner-beta.md)'s switch: without it, Campaign is as today. **Waits for:**
campaign-planner-beta, campaign-your-combos ("Add as combo" makes one). **Unblocks:** later, community ratings (the
index's note).

**Why:** the user, 11-19: some classes are strongest together, such as Berserker stance with skill users. Today the
plan doesn't know it, and a new player can't tell which pairs multiply each other (BER with PAL or SHD), which overlap
(BER with ROG, MNK or WAR, which have Striker already) and which clash (SHM with ENC, two pets). After, a card lists
the pairs worth a look, each with what it does in the game's words and its source; the player adds the ones they like
as combos or hides the rest, and where a suggestion seated a trio, the run says "(suggested)".
**Players:** everyone with a campaign, most of all a new player who doesn't know the classes yet.

The new card is the Campaign Combos design notes' section "C · Suggestions".
In short:

```text
Plan → Campaign (made-up plan)

Today                          After
(nothing)                      ⟦Pairs worth a look   For your plan | All 24 | Hidden (1)⟧
                               ⟦BER + PAL  Core  Berserker stance … · In your plan: free in run 5 · source⟧
                               ⟦          Add as combo   Not for me⟧
Run 5  …  BER / PAL / CLR      Run 5  …  BER / PAL / CLR  ⟦stance + plate (suggested)⟧
```

## The cause

The planner's last tie-break (`Campaign.Quality`) knows a carry, heals and one penalty, two pets (`MAG`, `NEC`,
`BST`). There's no library of pairs, no source for any claim, and nothing the player can hide or add.

## What the user decided (11-19)

- **Suggestions break the ties that are left,** after the player's carries ("carries should be able to CARRY the
  trio"), on by default, labeled where they acted, with a switch to turn them off.
- A suggestion never changes the runs or the XP. "Add" makes it a combo the player owns.
- **Every claim has a source**, and nothing guesses how fast a trio levels (design.md rules 1 and 2).
- **Tier lists and player ratings are wanted later,** out of scope now; the design must leave room ("plan for a future
  feature").

## What the player sees, in short

The design page's "C" mock-up is the source; the designer writes the exact words and states here.
- **Pairs worth a look**, with For your plan | All 24 | Hidden (1). Each card: the pair, its strength (Core, Overlaps,
  Clash), what it does in the game's words, "In your plan: …" (free in a run, already in N runs, kept apart), its
  source, and Add as combo / Not for me.
- Where a suggestion acted, the run row's chip says "(suggested)" (campaign-run-chips). Run chips were parked by the
  user, 11-19, so the designer decides whether a suggestion shows on the row at all.
- A dashed "Later: what guides and players rate" placeholder is in the mock-up; the designer decides whether it shows
  before ratings exist (design.md rule 2: nothing on screen that isn't there yet).

## Proposal

- The pair library is data in the engine: classes, strength, mechanism, sources, and an empty "rated by" field kept
  for later.
- Core pairs count for a trio and Clash pairs against it, as the last tie-break (the index's "How a plan ranks",
  item 8). Hidden pairs don't count.
- Hidden pairs and the switch are stored with the new plan (campaign-planner-beta), never in the old one.

## Open checks

- Hidden pairs and the switch: per character, or one for all of a player's characters?
- Each pair's sources checked again when built; the Oct 22 and Nov 10 patches changed Berserker stance and DDD after
  most tier lists were written.
- "All 24": the count comes from the library, never a fixed number in the page.

## Where

- A new data file and its reader under `src/EqlPathfinder.Engine/` (beside the class roles table)
- `src/EqlPathfinder.Engine/Analysis/Campaign.cs`: the tie-break (`Quality` today); its "two pets" penalty
  (`MAG`, `NEC`, `BST`) becomes a Clash pair
- `src/EqlPathfinder.App/AppSettings.cs`: hidden pairs and the switch, per character or for everyone (open check)
- `src/EqlPathfinder.App/web/js/campaign.js`: the cards
