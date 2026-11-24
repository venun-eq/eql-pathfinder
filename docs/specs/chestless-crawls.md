# Finished crawls with no motes

**Spec, 11-18 (qa): small, in the views.** From the draft of 11-18 ([calc-audit](calc-audit.md)). The log says the
chests were opened and held no motes: a fact, not a parsing slip. **Waits for:** nothing.
**Unblocks:** calc-docs, and a fairer band for one-usual.

**Why:** four finished crawls in the user's history have 0 T5-eq. Every other finished crawl has motes in its chest,
about 70% of a crawl's T5-eq (Estate D4: about 15 of 17–25). A finished crawl with no motes sits in its place's usual,
and a single 0 rate stops `FarmPace.For` from measuring the band (`rates.All(r => r > 0)`), so Estate D4 falls back
to ±25%.
**Players:** at the cap, farming places below their level; anyone whose usual includes such a crawl.

## Findings (Venun's log, and `pathfinder crawls --from 2026-11-05`)

Each of the four ends the same way, in one second: `You have completed the Dungeon Crawl and earned reward loot!`, then
`The Dungeon Crawl opponents were too low-level to earn a refund of your instance charge.`, then 9 to 20 lines
`… from Reward Chest`: gear, tradeskill items, food. Not one mote or fragment. That line is in the log exactly 4 times,
on these four, and the 232 completions are 228 `You earned a refund` and these 4.

| Start | Place | Chest lines | Motes | The tracker |
|---|---|---|---|---|
| 11-17 19:57 | Permafrost Keep D0 | 10 | 0 | Done, not refunded, T5 0, chest 0 |
| 11-08 14:06 | The Estate of Unrest D4 | 20 | 0 | the same |
| 11-08 11:14 | The Castle of Mistmoore D0 | 9 | 0 | the same |
| 11-05 14:56 | Temple of Cazic-Thule D4 | 13 | 0 | the same |

The tracker read every line as it should: the completion sets `Completed`, the "too low-level" line isn't parsed (so
`Refunded` stays false), and the chest's lines go to the visit but hold nothing tiered, so its T5-eq is 0. Nothing was
put in another visit or missed, so no re-read. The chest isn't missing: it gave no motes, because the crawl's mobs
were below the player's level.

## Done when
- [x] A finished crawl with no motes doesn't stop its place's band from being measured: the band from five visits
  with motes and one without is the band of the five, not the default ±25% - `AChestWithNoMotesDoesNotStopTheBand`
- [x] Under five visits with motes, the band stays the default ±25% - `AChestWithNoMotesDoesNotStopTheBand` (second part)

## Edge cases
- **Too little:** four visits with motes and one without: five visits, but the band needs five rates above 0, so ±25%.
- **Nothing yet:** every visit there gave no motes: no rate above 0, usual 0, band ±25%, as today.
- **The visit judged is itself one with no motes:** it reads "less" (−100%) against the usual, as today. It's true.
- **The rules change:** if a patch puts motes in a too-low-level chest, those visits are just rates above 0.
- **Re-sends:** nothing stored, sent or read from the log changes, so no re-read and no re-send.

## Not doing
- Which visits make the usual. usual-from-earlier is changing that set in `FarmPace.For` now, so the test keeps its
  history all earlier than the visit judged, and a 0 stays in the median here.
- Reading the "too low-level" line, or leaving such crawls out of farming (a question for the user).
- The 11-05 crawl's time: its first 18.4 minutes (from 14:38, zoned out and back in at 14:56) are a separate unfinished
  visit, so it reads 2.2 minutes. Zoning out and back in is in docs/log-formats.md; it's a tracking change of its own.
- Kills at the AA cap: these crawls show 0 kills, since a full AA pool logs `You gain experience!` with no `(x%)`.

## Where
- `src/EqlPathfinder.Engine/Analysis/RunPace.cs`: `FarmPace.For`, the band (`same.Count >= 5 && rates.All(r => r > 0)`)
- `tests/EqlPathfinder.Engine.Tests/ChestlessCrawlTests.cs`

## Reviews
Needs: the architect after the build (engine view change only; nothing stored, sent or read).
- architect: sound, no findings. `AChestWithNoMotesDoesNotStopTheBand` fails on the old `rates.All(r => r > 0)` (five
  rates give a band near 40%, not 25%) and its second part holds the "four rates is too few" edge. A 0 stays in the usual's
  median, so a place that mostly gave nothing still reads "usual", as before. The edit touches only the band lines, so
  usual-from-earlier's change to `same` merges apart from it. Nothing stored, sent or read changes: no re-read.
