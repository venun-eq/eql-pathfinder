# Ask's shared difficulties answer the zone you asked about

**Why:** feedback report #11 (0.10.19, Ask → Which difficulty): "No matter which zone I select for 'Which
difficulty' the bottom shows 'Players who share' and it's always Plane of Hate 46-50. Why?" The question read
"Which difficulty is worth it in Lair of the Splitpaw at levels 31–35?", the player's own D0 and D4 were Splitpaw,
and under them "Players who share · Plane of Hate 46–50" answered a different zone at different levels. The shared
numbers had no Splitpaw 31–35, so they fell back to the zone nearest the player's own level, whatever was picked.
After: the shared part answers the zone and levels the question names, or says it has none yet. It never shows
another zone.
**Players:** everyone who shares and opens Which difficulty. Worst for a player whose level is far from the zone
they pick, because the fallback is always the same zone.

The same fallback hit the other way: a chip only the shared numbers have (Plane of Hate 46–50 was one) showed the
player's own nearest zone in the question and the top block, not the zone on the chip.

## What the player sees

One zone per answer. The question names it, its chip is lit, and both blocks (yours, then players who share) are
that zone at those levels, or say they have none. Before, the report's case:

```
Which difficulty is worth it in Lair of the Splitpaw at levels 31–35?   [Lair of the Splitpaw 31–35] lit
  D4 wins on both            D0 | D4            (yours, Splitpaw)
  PLAYERS WHO SHARE · PLANE OF HATE 46–50
  D0 for speed, D4 for motes D0 | D4            (theirs, another zone)
```

After, every state. The question line and the lit chip follow your pick, or the shared zone when you have none
(`d.pick ?? d.community`). The words are exact.

| State | Question line (gold part) | Your part | Shared part |
|---|---|---|---|
| **Both have it** | `Lair of the Splitpaw at levels 31–35` | your D-columns, as today | `Players who share · Lair of the Splitpaw 31–35`, their D-columns, as today |
| **Yours, theirs too thin** (the report) | `Lair of the Splitpaw at levels 31–35` | your D-columns | the same label, then one muted line: `Not enough players who share have played two difficulties here yet.` |
| **Yours, Where or Who with not in the shared file** | same | your D-columns | the same label, then: `Shared numbers don't cover <gap> yet.` (Level from A to B's words, line 66) |
| **Theirs only** (a chip only they have, or no logs of your own) | `Plane of Hate at levels 46–50` | one muted line: `Your logs don't have two difficulties here yet.` | `Players who share · Plane of Hate 46–50`, their D-columns |
| **No shared file** (not shared yet, offline) | your zone | your D-columns | nothing, as today |
| **Neither** | `Which difficulty is worth it?`, no chip lit | `No zone in your logs has two difficulties at the same levels yet`, as today | nothing |
| **Waiting** (a chip clicked) | as today: what's there stays, faded, until the answer lands | | |

Side panel, "Where this answer comes from": in **Theirs only**, Your logs reads `Nothing at two difficulties in Plane of
Hate at levels 46–50 yet.` (today it says "No zone with two difficulties at the same levels.", which is wrong once a
zone is named). The Community line is unchanged.

Reused: `lbl` for the shared label, `muted` at 12.5px with `line-height: 1.45` for the none-lines (as Level from A to
B's empty tiles), `askpre on` for the lit chip, `gold` in the question. Nothing new in `app.css`.

Changed from today's **Theirs only** heading ("None in your logs yet: players who share, in Plane of Hate at levels
46–50"): the zone now shows in the question, so the heading only repeated it. The muted line plus the same
"Players who share · ..." label gives all three states one shape: your part on top, theirs below, each saying when it
has none.

## Done when
- [x] A zone you've played that the shared numbers don't have: your difficulties, and no shared zone in its place - `TheSharedDifficultiesAnswerTheZoneYouAsked`
- [x] Opening the question with no pick: your nearest zone, and the shared numbers for that same zone or none - `TheSharedDifficultiesAnswerTheZoneYouAsked`
- [x] A chip only the shared numbers have: the question and the block are that zone, from the players who share alone - `TheSharedDifficultiesAnswerTheZoneYouAsked`
- [x] No logs of your own at any zone: the shared zone nearest your level, as before - `TheSharedDifficultiesAnswerTheZoneYouAsked`
- [x] A pick neither has any more (Where or Who with changed): the question starts from your level again - `TheSharedDifficultiesAnswerTheZoneYouAsked`
- [x] Both have it: one zone in the question, the lit chip, your block and the shared label - seen in main-ask-diff-pool.png: this PC has Plane of Hate 46–50 too, so the shot shows both, the shared label under yours
- [x] Yours, theirs too thin: your block, then "Players who share · <your zone>" and "Not enough players who share have played two difficulties here yet." - seen in main-ask-diff-mine.png (Lair of the Splitpaw 31–35, the report's case)
- [x] Theirs only: the question and lit chip are the shared zone, "Your logs don't have two difficulties here yet.", the shared block, and the side panel's Your logs line names the zone - no chip on this PC is the shared numbers' alone, so seen once in theirs-only.png: preview.html in headless Edge with diff-pool's pick taken out, as the engine sends it (`TheSharedDifficultiesAnswerTheZoneYouAsked`)

## Edge cases
- **Too little:** the shared numbers have the zone with only one difficulty, or under their player gate: it isn't in
  their list, so it reads as none, the same as a zone nobody shared.
- **Nothing yet:** no community file (not sharing yet, or offline): no shared part at all, as before.
- **Old versions in the field:** the page data's fields keep their meaning; `difficulty.community` is null more often.

## Not doing
- Showing a nearby shared zone in its place. A different zone at different levels doesn't answer the question.

## Where
- `src/EqlPathfinder.Engine/Pages/AskPage.cs`: `diff` and `cDiff` in `Build`
- `src/EqlPathfinder.App/web/js/ask.js`: the question line and chips (`askQuestion`), `askDiff`
- `tests/EqlPathfinder.Engine.Tests/CommunityReaderTests.cs`

## Reviews
- architect: no must fix. Should fix: the test never had the zone on both sides, the commonest state; a case with Kedge Keep 41–45 in your logs and the shared file now asserts both answer it. Done.
- designer: nothing to fix; every state matches What the player sees in the four shots. Also rendered the Where gap ("Shared numbers don't cover the open world yet.", gap-state.png) and found it in line with the rest.
