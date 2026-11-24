# History's visits tab says what it holds, and the whole time

**Built 11-18** (standard; the designer settled "What the player sees" first). Was a draft of [calc-audit](calc-audit.md).
Sending a reason for each "Not judged" row waits (docs/debt.md): it touches Ask's left-out segments.

**Why:** the user, 11-18: "Should this be called crawls? These aren't all dungeon crawls yes?" and "Do we log the
full time anywhere the run took?"
- The tab is "Crawls", but it holds every instance visit. Of the user's 1,299 rows, 433 are raid instances
  ("- Solo", "- Group") and 53 have no difficulty. Its tiles already say "visits".
- "Crawls only" keeps rows with a difficulty, so raids that carry one stay in.
- Done is never ticked for a raid (it's judged by its boss), and the column doesn't say so.
- No column has a visit's whole time. Active + Idle is time inside (`minutes`, sent but not shown). Not in any column:
  time between "completed" and zoning out (looting, walking out) unless a straggler is killed, and trips outside
  mid-visit (`Pauses`). Reset is before the visit, from the last instance's exit, blank over 30 minutes.

**Players:** everyone who opens History.

## Proposal (for the designer to settle)

- Name the tab for what it holds ("Visits"?), and filter by the visit's kind (crawl, raid, unfinished, at the cap)
  from the engine's `Kind`, not by difficulty.
- One column for the clock, zone-in to zone-out, with Active, Idle and the rest in its tooltip, so the parts add up.
- Done says what it means for a raid (the boss, already in Verdict), or is blank with a tooltip.

## What the player sees

Settled by the designer, 11-18. Mock-ups (drawn in the real window from a copy of the user's data):
`scratchpad\mock-visits-all.png` (All, with the Time tooltip drawn in) and `mock-visits-raids.png` (Raids chosen).
The mock's Time cells read "NaN h NaN" and its Raids view shows every row: the window's next data message replaced
the rows the script had changed. The filter row, the Time column's place and the tooltip are as designed.

**The name.** The tab is **Visits**: History's strip reads `Visits · Loot · Relevels`, the report title
`History → Visits` (core.js `PAGE_NAMES`, `GROUPS`). The page id stays `crawls`, so a remembered page still opens.
The search box's label is "Filter visits". "Crawl" stays where it means a crawl (the note, relevel headers).

**The filter.** "Difficulty instances only" goes. In its place, first in `.filters`, a `.seggroup` (as Loot's
`#lootSourceSeg`, chosen button `.on`), one button per kind of visit, its count in `.dim`:

`All 1,266 · Leveling 50 · Farming 424 · Grouped 37 · Raids 433 · Not judged 322` (the user's numbers, 11-18)

- It filters by the engine's `kind`: `leveling`, `farming`, `grouped`, `raid`, and null for Not judged. The
  Proposal's "unfinished" and "at the cap" aren't kinds of their own: the engine judges both as farming, so they are
  in Farming, and its tooltip says so. One filter, one rule, the same one Verdict uses (rule 19).
- A count is the visits of that kind that the search and "Hide visits too short to judge" let through, so a click
  shows that many. A kind with no visits is left out, unless it is the one chosen (it then reads "Farming 0" and the
  table "No visits match.").
- All is the default. The choice is remembered, as Loot's are (`store`, `visitKind`).
- Each button's tooltip says what's in it:
  - Leveling: "Judged by levels an hour: crawls below the cap at difficulties you don't farm."
  - Farming: "Judged by T5-eq an hour: your farmed difficulties (D3, D4; Settings → How you play), instances you
    left before they completed, and everything at 50." (the farmed list and the cap from the data, never literals)
  - Grouped: "Played in a group: judged by T5-eq an hour against your other grouped visits."
  - Raids: "Raid instances (- Solo, - Group): judged by their boss."
  - Not judged: "In no pace or rate." **Changed from the design:** a null kind is any visit `Judged()` leaves out
    (not a crawl, no start level, or left out of `AskSegments` for DDD, a bonus, an open visit...), so no reason is
    named; the engine would have to send one for each row (docs/debt.md).
- Kinds follow Settings' farmed difficulties: while `farmWaiting` fades the verdicts, the `.seggroup` gets `.stale`
  too, until the next data message brings the new kinds and counts.

**The clock.** A **Time** column right after Start, so a row reads when and how long.
- Header tooltip: "From zoning in to zoning out. Hover a visit for where its minutes went."
- Cell: under an hour "14 min", from an hour "1 h 07". It sorts by its minutes.
- Cell tooltip, five lines, minutes to one decimal; the four parts add up to the first line (the engine works out
  each part and the whole, the page only draws them):
  ```
  Zoned in 16:46, out 17:03: 17.3 min
  Active 15.4
  Idle 0.0
  After it completed (looting, walking out) 1.8
  Outside the instance (a death, an errand) 0.0
  ```
  - Still inside, or out and not back yet: "Zoned in 22:08, still inside: 12.0 min so far", the parts so far.
  - A raid never completes: the fourth line is left out.
  - Clock times stay on this PC like Start's (rule 15).
- The note under the table starts: "Time runs from zoning in to zoning out; hover it to see where the minutes went."

**Done for a raid.** Header tooltip: "The instance said it was completed. A raid never says so: it's done when its
boss dies." A raid row gets `✓` (`.good`) when its boss died, with the tooltip "Its boss died: <boss>"; blank
otherwise, as Verdict's "No boss kill" says. Other rows don't change.

**States.**
- Nothing yet: only "All 0" shows; the table keeps today's "No visits match."
- Plenty: as the mock-up. 1,300 rows: the counts sum to All, and the table keeps its 2,000-row cut.
- Waiting: the `.seggroup` fades with the verdicts after a Settings change (above).
- Narrow, or 150%: `.filters` wraps; the `.seggroup` doesn't (`flex: none`), so the search box moves to the next line.
- Too little, Wrong: nothing new.

## Done when
- [x] The engine gives each visit its whole clock and four parts, and they add up - `VisitClockTests.ThePartsOfAVisitsClockAddUpToTheWhole`
- [x] A visit still inside, or out and not back, has its clock so far - `VisitClockTests.AVisitStillInsideHasItsClockSoFar`
- [x] History says "Visits" - `VisitsPageTests.TheTabIsCalledVisits`
- [x] One button per kind with its count, a kind with none left out - `VisitsPageTests.TheKindsAreCountedFromWhatTheSearchAndTheBoxLetThrough`
- [x] A kind filters the table by the engine's kind; one with no visits stays when chosen - `ChoosingAKindShowsOnlyItsVisits`, `AChosenKindWithNoVisitsStaysAndTheTableSaysSo`
- [x] A Time column, "14 min" or "1 h 07", and its tooltip's four parts - `TheTimeColumnReadsInMinutesAndHours`, `TheTimeTooltipSaysWhereTheMinutesWent`
- [x] A raid's Done is its boss - `ARaidIsDoneWhenItsBossDied`
- [x] The look, as the designer drew it - seen in `main-crawls.png` (All; the Raids view is proven by `ChoosingAKindShowsOnlyItsVisits`)

## Edge cases
- Nothing yet: only "All 0"; the table says "No visits match.".
- A visit with no kind is in All and Not judged only; a raid is in Raids.
- Still inside: the clock runs to now, "so far"; out and not back: to zoning out, "so far".
- Parts that don't add (a log oddity) are never shown: After is the remainder floored at 0, and the whole is the sum.
- Settings changes the farmed difficulties: the buttons fade with the verdicts until the next data message.
- Narrow or 150%: the filter row wraps, the buttons don't. Clock times stay on this PC (`clockShown`).
- Sent: new fields on each history row only; no share, upload or privacy change.

## Not doing
- A reason per not-judged row, and the other debt the designer noted (docs/debt.md).

## Where

- `src/EqlPathfinder.App/web/js/crawls.js` (`COLS`, `renderCrawls`'s filters), `web/index.html` (the tab, `#crawlsOnly`),
  core.js `PAGE_NAMES`
- `src/EqlPathfinder.Engine/Tracking/VisitStats.cs`: the clock minutes (`Start` to `ExitedAt`) and the time after
  completion

## Reviews
- architect: (11-18) no must fix. The clock adds up by construction (idle is capped at time inside, so Active + Idle is
  time inside; pauses are clipped to the visit; After floors only on a log oddity), nothing new leaves the PC
  (SharePayload picks its own fields), and no textual clash with one-usual. Should fix: the page test never sets
  `tooShort`, so the box's half of the counts is unproven; no page row is still inside or paused, so clockTip's "so far"
  lines are drawn untested; `hm` is a third copy of the clock-time formatter (core.js `dt`, home.js).
- designer: (11-18) no must fix. Checked the engineer's `main-crawls.png` (All) and two renders of my own, in isolated
  copies of the user's data: `scratchpad\rv\raids.png` (Raids chosen, table scrolled to Done, every tooltip's text
  drawn in) and `scratchpad\rv\zoom.png` (All at 150%). The strip says Visits; the buttons, counts (they sum to All)
  and tooltips match the design, the farmed list and the cap from the data; Time reads "18 min" with room beside Start;
  both tooltips' parts add up (16.2 = 15.4 + 0.0 + 0.8 + 0.0; a raid's 2.2 with no "After" line); 376 of 433 raids are
  Done, "Its boss died: Master Yael"; at 150% the filter row wraps after the buttons and nothing touches. Not rendered:
  an open visit's "so far" (none in this data) and Waiting's fade (`.stale`, read in the code).
  - Should fix: the All button's tooltip, "Every visit the search and the box let through.", doesn't say which box.
    Name it: `Every visit your search and "Hide visits too short to judge" let through.`
  - Idea: the design wrote "All 1,266"; the buttons say "All 1267", as the tiles do. Leave it unless the tiles get
    thousands separators too.
  - Not this change (for docs/debt.md): the search box's placeholder is cut to "(e.g. Kedge, PAL, I" at 100% and 150%
    (the box is 280 px and never grows: `flex: 1 1 280px`, or a shorter example). At 150% the tiles leave one alone on
    a second row, and the relevel header's line runs off under the table's sideways scroll.

- **Engineer, 11-18: review fixes.** Tests for a too-short row (left out of All and its kind), a still-inside and a paused row (their "so far" headers); one `hm` in core.js; the All tooltip; debt lines for 150%.
