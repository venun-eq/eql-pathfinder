# The overlay keeps Levels/h while leveling at D3/D4, and says how many visits a usual needs

**Why:** Leveling through D4 Unrest at levels 18–22, the overlay showed only T5-eq/h and Kills/min, and the note read
"No usual here yet: no visit like this one at levels 18–22. Three make one." The user, 11-19: "'Three make one' is very
poor grammar. I would still like to see levels/hr. That should never go away while leveling from the overlay." A D3/D4
crawl is judged by T5-eq/h (design.md rule 19), so Levels/h dropped out with the judge. After this change it stays, as a
plain number beside the judge, and the note says "1 of 3 earlier visits".

**Players:** everyone leveling below the cap at a difficulty they farm (D3, D4 by default), grouped, or in an
unfinished instance. Worst for a new player, who sees the no-usual note on every crawl for a while.

## What the player sees

"Below the cap" is the crawl's level under the level cap (from the data, never a literal). "Motes-judged" is any crawl
judged by T5-eq/h (rule 19): a farmed difficulty, grouped play, an unfinished instance. A raid is unchanged: it's judged
by its boss. Everything at the cap is unchanged.

### Overlay, a motes-judged crawl below the cap

| Size | Today | After |
|---|---|---|
| Standard, Detailed | T5-eq/h (tick, verdict colour), Kills/min | **T5-eq/h** (unchanged), **Levels/h** (new), Kills/min |
| Compact | T5-eq/h | **T5-eq/h**, **Levels/h** |

The new **Levels/h** row reuses the overlay's meter row (`Row`, the same one the leveling card leads with):
- Label `Levels/h`, value `0.0` format in the plain text colour (not gold: gold is T5-eq), e.g. `Levels/h   0.4`.
- **No tick and a grey bar, always.** No verdict judges it here, so it gets no colour (rule 21); T5-eq/h stays the judge,
  and the corner word ("18% more", "no usual yet", "warming up") is still T5-eq/h's. A Levels/h usual is for later.
- The bar is solid, or dashed exactly when the other rows are (no usual, or a loadout not known), like a raid's rows.
- **It never drops out** while below the cap: before the crawl has a rate it shows `—`, as the lead row does
  ("The rate starts at 8 kills" note unchanged). The rule that hides an empty row doesn't apply to it.
- No "+20% XP to level" beside the zone: that line explains ticks on levels, and this row has none.

Standard, below the cap, D4, 18 minutes in, no usual yet:

```
Unrest D4                                   18m  no usual yet
T5-eq/h      ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄       42
Levels/h     ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄      0.4
Kills/min    ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄      3.1
No usual here yet: 1 of 3 earlier visits like this one at levels 18–22.
20 ████████░░░░░░░░░░░░░░░░░░░░░░░░░  next 34m · ≈11.2 h to 50
```

Compact, same crawl: the two rows `T5-eq/h 42` and `Levels/h 0.4`, then the level footer, as today. No new layout:
one more meter row, so no mock-up.

### Overlay, the crawl-finished card, a motes-judged crawl below the cap

Today (Standard, Detailed): header `✓ Unrest D4 done` and `12.3 T5-eq`; a T5-eq/h row with its tick and colour; the
verdict line (`18% more T5-eq/h than your usual (crawls you finished, solo, levels 18–22)`), or the rank line
(`#3 of your 8 Unrest D4 visits like this one`); then NEXT.

After: the same, with a **Levels/h** row directly under the T5-eq/h row: the whole crawl's levels an hour, completion
XP included, `0.0`, grey, no tick (`—` if the crawl has no levels/h). The lines under it name T5-eq/h themselves, so
they still read right. The header stays `12.3 T5-eq`. Compact's finished card draws no rows today and is unchanged.

### The no-usual notes (every overlay, any level)

The count needed is `RunPace.MinRuns` (3) and the level window `FarmPace.LevelWindow` (2): the words take both from
there. "Earlier" says the crawl on now isn't counted yet, so "1 of 3" doesn't read as "this is visit 1 of 3". A zero
is a true count and shows what's needed (rule 4: partial progress, as "18 of 48 ranks").

| Where | Today | After |
|---|---|---|
| Crawl going on, motes-judged, below the cap | `No usual here yet: no visit like this one at levels 18–22. Three make one.` | `No usual here yet: 0 of 3 earlier visits like this one at levels 18–22.` |
| Same, 1 / 2 earlier | `… 1 visit like this one at levels 18–22. Three make one.` | `No usual here yet: 1 of 3 earlier visits like this one at levels 18–22.` (`2 of 3`) |
| Same, at the cap | `No usual here yet: 2 visits like this one. Three make one.` | `No usual here yet: 2 of 3 earlier visits like this one.` |
| Crawl going on, leveling | `No usual here yet: 2 earlier runs within 2 levels of 20. Three make one.` (`no earlier run`, `1 earlier run`) | `No usual here yet: 2 of 3 earlier runs within 2 levels of 20.` (`0 of 3`, `1 of 3`) |
| Leveling, players who share | `No usual here yet: 2 earlier runs within 2 levels of 20. Players here: 4.0 kills a minute, 3.9 levels/h (6 players).` | `No usual here yet: 2 of 3 earlier runs within 2 levels of 20. Players here: …` (rest unchanged) |
| Leveling, a faster place | `No usual here yet: 2 earlier runs within 2 levels of 20. ` then the amber `… levels faster` part | `No usual here yet: 2 of 3 earlier runs within 2 levels of 20. ` then the amber part, unchanged |
| Finished card, leveling, no usual | `No usual here yet: 2 runs near at 20` (+ ` · players here …`) | `No usual here yet: 2 of 3 earlier runs within 2 levels of 20` (+ ` · players here …`, unchanged) |

The last row is the same note on the finished card: one-usual.md reworded the live note because "2 runs near at 43"
reads badly, and the card kept it. With no level known it ends at `2 of 3 earlier runs`. The "N of 3" words appear
only with fewer than 3; with 3 or more and no rate yet, the note is the "The rate starts at 8 kills" one, as today.
docs/specs/one-usual.md lines 96 and 113 quote the old words; they stay as history.

Words checked against the rules: "visits" for motes-judged crawls and "runs" for leveling, as the notes already say;
no "Three make one"; numbers are digits.

## Done when
- [x] A motes-judged crawl below the cap shows a grey Levels/h row with no tick between T5-eq/h and Kills/min - seen in overlay-motes-leveling-standard.png
- [x] Before that crawl has a rate, the Levels/h row shows `—` and stays - seen in overlay-early-motes-leveling.png
- [x] Compact shows T5-eq/h and Levels/h for that crawl - seen in overlay-motes-leveling-compact.png
- [x] The finished card for that crawl has the Levels/h row under T5-eq/h - seen in overlay-done-motes-leveling.png
- [x] At the cap the farming overlay is as before (no Levels/h) - seen in overlay-farming-standard.png
- [x] The farming no-usual note reads `No usual here yet: 1 of 3 earlier visits like this one at levels 18–22.` - seen in overlay-motes-leveling-standard.png
- [x] The leveling no-usual note reads `No usual here yet: N of 3 earlier runs within 2 levels of L.`, and no screen says "Three make one" - seen in overlay-leveling-standard.png or a no-usual leveling shot
- [x] The words come from one place, with the 3 and the 2 from the rules they describe - `NoUsualNoteTests`
- [x] The leveling no-usual note shows at all: it's built of coloured runs, and the overlay hid a note whose `Text` was empty - seen in overlay-leveling-no-usual.png

## Edge cases
- **Nothing yet:** 0 earlier visits reads "0 of 3", a true count.
- **Too little:** before the crawl has a rate, Levels/h shows `—` and stays; the note is the "rate starts at 8 kills" one.
- **The rules change:** a player who sets the overlay to Farming below the cap said what they're doing: no Levels/h row, as at the cap.
- A loadout not known: the Levels/h row shows, dashed like the others.

## Not doing
- A usual (tick) for Levels/h on a D3/D4 crawl: it would need each trio's XP penalty taken out (the user, 11-19: later).

## Where
- `src/EqlPathfinder.Engine/Analysis/RunPace.cs`: `RunPace.NoUsualNote`, `FarmPace.NoUsualNote`; the near-level window reads `FarmPace.LevelWindow`
- `src/EqlPathfinder.App/OverlayWindow.xaml.cs`: `DrawCrawl` (`levelsToo`, the rows, the farming note), `DrawDone` (the Levels/h row, the leveling line), `RunNote`, the note's visibility in `Update`
- `src/EqlPathfinder.App/DevSnapshots.cs`: `MotesLeveling`, `DoneMotesLeveling`, `LevelingNoUsual` and their five shots

## Reviews

- architect: no must fix. `levelsToo`, the `m == lv` filter, `LevelWindow` (2 = the old literal) and the Inlines-based note visibility read right; one should fix: the finished card's no-usual line says "3 of 3 earlier runs" when the verdict is "early" with enough runs (OverlayWindow `DrawDone`, `lp` default case). Fixed by the lead: "early" now reads "No verdict: the rate starts at 8 kills, and this crawl had N". The condition decided twice went to docs/debt.md.
- designer: the renders match "What the player sees": a grey Levels/h with no tick under T5-eq/h in Standard, Compact, warming up (`—`, kept) and on the finished card; no Levels/h at the cap; "1 of 3 earlier visits" / "0 of 3" / "2 of 3 earlier runs"; and the leveling note shows. **Should fix, release note:** line 10's "how many of the visits it takes you have" → `With no usual yet, the note counts your earlier visits toward one: "No usual here yet: 1 of 3 earlier visits like this one at levels 18–22."`, and line 11's "It had stopped appearing" is untrue (it never showed) → `The leveling note for a place with no usual yet now shows on the overlay.` **Idea:** a bar with no usual always fills 80% (`Row`), so on overlay-done-motes-leveling.png Levels/h 1.1 is as long as the green T5-eq/h 55 bar. **Not this change:** on overlay-leveling-no-usual.png the header cuts "+20% XP to level" to "+20…" beside "Estate of Unrest D4". The lead applied both release-note lines; the header went to docs/debt.md; the 80% bar is how every row without a usual draws, left as is.
