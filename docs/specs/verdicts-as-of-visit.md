# A past visit is judged as of that visit, every part of it

**Built 11-19.** Tier standard. A visit's place check is made from the visits before it only, weighted and drift-scaled from its start. The way that holds (the proposal's second option, generalised): `AskData.PlacesAsOf` builds only the places at the visit's own difficulty, from the earlier visits, once per judged visit (no tables over time kept); `PlaceCheck.Of` also reports a faster place that was not clearly faster (`Verdict.NotClear`), and the overlay's "so no Fading" line reads that, not NEXT's top. `Verdicts.Judge`/`JudgeAll` now take Ask's instance visits instead of a built `AskData`, so the live crawl, the overlay between crawls and History's row all run the same call. The live crawl is not in Ask's segments and could not see later visits anyway.

**Why:** docs/engineering.md, Modeling 10 (the user, 11-18): "A visit's usual is made of earlier visits only, so its
verdict doesn't change once it's over." Kill speed and the farming usual follow it (usual-from-earlier). Two parts of a
past visit's verdict still don't:
- **The place check** ("<place> levels faster", Fading) reads Ask's tables built from every visit. `JudgeRun` →
  `PlaceCheck.At` gets `PaceData()`'s `AskData`, so it reads later visits too. Spec and debt both say so; check it in
  code first.
- **Weights from today:** `AskData.From` halves a visit's weight every 30 days counted from `DateTime.Now`, and `PaceDrift`
  scales times against now. So a past visit's place verdict shifts with today's date.

## Proposal

- Judge each past visit with the tables as they stood when it started: earlier visits only, weighted from its start.
  Building the tables once per visit is costly over 1,300 visits, so the builder picks a way that holds:
  - tables built once over time in order;
  - or a check of just the two places it compares, from the earlier visits.

  The spec says which, and how long History takes before and after.
- The visit going on now is judged from today, as it is now. It has no later visits.
- A test sets the clock (one-clock) and shows that an old visit's place verdict is the same at two different dates.

## Where

- `src/EqlPathfinder.Engine/Analysis/Verdicts.cs` (after verdicts-out), `RunPace.cs` (`PlaceCheck`), `Ask.cs` (`AskData.From`), `PaceDrift.cs`
- docs/pace.md: "Fading" and "levels faster" say it's as of the visit

## What the player sees

Nothing gets a new layout and no word is new. What changes is **which** place words an old crawl carries, once, with
this update. After it, a crawl's place words are fixed the moment it ends, as its kill speed already is.

### Where a past crawl's place words show

| Where | The words (unchanged) | Comes from |
|---|---|---|
| History → Crawls, Verdict column | "Fading · <place> levels faster", "<place> levels faster" (gold), after the kill speed words | the row's `placeVerdict`, `betterPlace` (`crawls.js` ~95) |
| Home, releveling, **XP per kill · the place** tile, when no crawl is going | chip "Fading · <place> levels faster", then "<place> is clearly faster at the same difficulty at 44." | the latest crawl's row (`home.js` ~153) |
| Home, **place by place** card | chip "Fading · <place> levels faster", note "2.8 → 2.1 levels/h. <place> levels faster at this difficulty." | the stop's first row with a place verdict (`home.js` ~197) |
| Overlay, between crawls | "Fading · <place> levels faster: 3.1 levels/h at 44" (amber) | `NextStep.LastPlace`, the crawl just ended |

Not changed, because they are about today and say so by where they are: Ask → Every place (the grid's dashed Fading,
the table's Fading tag, "It isn't Fading" under Why), Home's route, the overlay's NEXT card (it says "estimated"), and
the crawl going on now.

### The rule, as the player meets it

A crawl's place words compare places **as you knew them when that crawl started**: only crawls you played before it,
their weight halving every 30 days counted back from its start, your pace drift as of then. The crawl itself is not in
it: how fast you killed is kill speed's job, and the place check is the place's (docs/pace.md, "Levels an hour").

What the regular sees change on old rows, once:
- **Words go away** from a crawl played before the faster place was known. You found Kedge D4 faster at 44 on 11-12, so
  a Mistmoore D4 run at 44 on 11-05 no longer says "Kedge D4 levels faster". Mistmoore was the right call then.
- **Words appear** on a crawl whose place was clearly beaten at the time, though later play closed the gap.
- **Fading and levels faster can swap**, and the named place can change, because "best 3 levels back" is as of then too.
- **A place's first crawl near a level has no place words.** Before, its own run could make its place look slow. It
  still reads "no usual yet" for kill speed, as now.

After the update the words on a row never change again, whatever you play next and whatever the date.

### One crawl, the same words everywhere

The crawl going on now is judged from today, and nothing after its start exists yet, so it reads as it would at its
start. The moment it ends, the overlay between crawls, its History row and Home's tiles must all show the words History
keeps. No crawl may say "Fading · Kedge levels faster" in its last minute and something else in History. (Check in
code whether the live crawl is in Ask's segments; if it is, take it out of its own check.)

The overlay's reason line, "<top> isn't clearly faster than <last> at 44 (within a run's normal swing), so no Fading.",
explains the crawl's verdict, so it must come from the **same** check. Show it only when that crawl's own check (as of
its start) found a faster place that wasn't clearly faster, and name that place. Today it shows whenever NEXT's top is
faster and there is no place verdict, which would now be wrong: NEXT is built from today's tables, which include the
crawl just ended, so its top can be clearly faster today. If the check can't report "faster, but not clearly", drop the
line rather than guess (design.md rule 2).

The levels/h on the overlay's place line ("3.1 levels/h at 44") is the better place as of the crawl's start. NEXT's
number for the same place is today's estimate and may differ by what this crawl added. That's acceptable: NEXT says
"estimated", and the place line is a verdict.

### Words to change, because "now" no longer holds for a past crawl

- **Help, "Am I going at a proper speed?"** (`index.html` ~295), Fading: "Your best place 3 levels back, clearly passed
  by another at your level. A past crawl is judged as it stood when you started it: only crawls before it count.
  "Clearly" means by more than the noise of both, and at least 10%." Slower place stays as it is.
- **History → Crawls, the Verdict column's (i)** (`crawls.js` ~49): "...and another place at the same difficulty that
  levels faster, by your crawls before that one. Raids: the boss." The words before it stay.
- **The gold words' hover** (`crawls.js` ~95, and Home's place-by-place chip ~205): "<place> levels faster than this
  place at the same difficulty, at that level, by your crawls before this one".
- **Home's place tile, for a finished crawl only** (`home.js` ~166): "<place> was clearly faster at the same difficulty
  at 44, by your crawls before it." The crawl going now keeps "is clearly faster".
- **docs/pace.md**, with the code:
  - "Your usual is your earlier visits": replace "That holds for kill speed, XP per kill and T5-eq an hour; the place
    check ("another place levels faster") still reads all your play (docs/debt.md)." with "That holds for kill speed,
    XP per kill, T5-eq an hour and the place check: a past visit's Fading or "<place> levels faster" compares places as
    they stood when it started (earlier visits only, weighted from its start), so it never changes once the visit is
    over. Ask's grid and NEXT compare them as they stand today."
  - Fading: "another place beats this one at your level as of the visit's start, and this place was the best 3 levels
    back then."
  - "Ask → Every place": after "Fading (dashed amber)", add "(as of today; a past visit's Fading in History is as of
    that visit, so the two can differ)".
- **docs/debt.md**: remove the row about the place verdict on old rows (`Verdicts.cs`, `PlaceCheck`, 11-18).

### The release note (`docs/releases/next/verdicts-as-of-visit.md`)

```markdown
---
title: Past crawls keep their place verdict
reread: false
job: false
bump: patch
---
A past crawl's "levels faster" and Fading now compare places as you knew them when you played it, so they never change afterwards.

- Only crawls before it count, and their age is counted from that day, not from today.
- Some older crawls' place words change once with this update: a crawl played before you found a faster place no longer
  says so, and a place's first crawl near a level has none.
- Kill speed and T5-eq an hour already worked this way. Ask and the overlay's NEXT still use all your play up to now.
```

`job: true` only if the community tables change, and they must not: this is your own play, judged at view time.
`reread: false` unless the builder stores verdicts, which it shouldn't.

### States

- **Nothing yet** (new, log just turned on): no crawls, no rows, nothing changes.
- **Too little:** a place's first crawl near a level, or a better place with under 2 crawls before it, shows no place
  words. Never a "levels faster" from the run being judged.
- **Plenty, long history:** the regular's ~1,300 visits. On a copy of the real database, the engineer counts how many
  rows gain, lose or swap place words, and writes the counts here. One changed row is worked by hand (numbers-check).
- **Waiting:** History draws as now. The re-judge must not make its first draw noticeably slower: before and after
  times go in this spec. While it works, the old rows stay, faded (rule 9).
- **New to the app, old to the game:** setting reincarnation counts re-figures the XP penalty on both sides of the
  check alike. Words may change then, as now: that's a change of facts, not of date.
- **Not sharing, or sharing among few:** the place check reads only your own play. No change.
- **Farming, grouped, raids:** no place words, as now.

## Done when

- [x] An old visit's place words are the same with later play added a month and a half on - `APastVisitsPlaceWordsDoNotChangeWithTodaysDateOrLaterPlay` (no clock to set: the check takes none, only the visit's start)
- [x] A visit played before a faster place was known has no place words; a later one has them - `AVisitPlayedBeforeAFasterPlaceWasKnownHasNoPlaceWordsAndALaterOneDoes`
- [x] A place's first visit near a level has no place words - `APlacesFirstVisitNearALevelHasNoPlaceWordsEvenWhenItsOwnRunWasSlow`
- [x] The crawl just ended and its History row come from the same call - `NextRankingServiceTests.TheCrawlJustEndedAndItsHistoryRowAreJudgedByTheSameCheck`: through `TrackerService`, the overlay's `LastPlace` and `LastNotClear` against History's row for the crawl (History has no not-clear field: the test asserts none beside a place verdict)
- [x] "...so no Fading" shows only from the crawl's own check - `AFasterPlaceThatIsNotClearlyFasterIsReportedAsSuchAndNothingElse`
- [x] History → Crawls still shows "<place> levels faster" on rows that keep them - seen in main-crawls-all.png (latest crawl: "Plane of Fear D0 levels faster")
- [x] Home's place tile says "was clearly faster ... at <its levelStart> ... by your crawls before it" for a finished crawl, and "is clearly faster" with no suffix for the live one - `HomeGoalPageTests.ThePlaceTileOfAFinishedCrawlNamesItsOwnLevelAndTheCrawlsBeforeIt` (finished at 22, level now 30)
- [x] Help's Fading line says a past crawl is judged as it stood when you started it - seen in `help-fading.png` (the designer's review, 11-19: the "Am I going at a proper speed?" chip, scrolled to)
- [x] The counts and History's time are below
- [x] docs/pace.md, the release note and docs/debt.md say what's above

## On the real database (a copy, 11-19)

- 1,336 visit rows. Before, 5 had place words; after, 2 do. **Gained 0, lost 3, swapped 1, unchanged 1.**
  Lost: Plane of Fear D0 (11-17), Mistmoore D0 (09-30), Kedge D0 (09-11). Swapped: 09-30 21:12 "Mistmoore D0" to "Old Guk D0".
  Worked by hand (11-19, from the copy's rows by docs/pace.md's definitions, not the engine's code; a throwaway script, spread minutes
  a level evenly, 30-day half-life from the visit's start, levels 27-29 weighted 1-2-1 by crawls, rank and drift left out):
  the swapped row is Runnyeye D0 at level 28, 09-30 21:12:42 (1.55 levels/h, 10.6 active min). Visits before it, at D0, 3+ min with a rate,
  near 28: Old Guk 2 visits, 10.9 min a level (5.5/h); Mistmoore 1 visit, 14.4 (4.2/h); Runnyeye 1 visit, 35.5 (1.7/h).
  A place needs 2 visits to be offered, so Mistmoore (1) cannot be named and Old Guk (2) is the best. The gap is ln(35.5/10.9) = 1.18, against
  2 x 0.13 x sqrt(1/1 + 1/2) = 0.32 and the 10% floor (0.095): clearly faster. Runnyeye has no 2 visits at 25, so "slower", not Fading.
  So "Ruins of Old Guk D0 levels faster" follows from the visits before it. The old words (Mistmoore) used today's table, where Mistmoore had 2+.
- History's judging, timed in the app on 981 segments: the old table build 1 ms (once); judging every crawl as of its start 88 ms.
  Cached by Ask's segments as before, so a refresh pays it once.


## Reviews

- architect: no must fix. Three should fix, all fixed: `PlacesAsOf` copied Ask's visit gates, and now shares them with `From` through `Usable` and `Scaled` (8ebb9e8); a test that couldn't fail was replaced by one through `TrackerService` (`NextRankingServiceTests.TheCrawlJustEndedAndItsHistoryRowAreJudgedByTheSameCheck`); the tile's "at N" was today's level, and is now the crawl's own (5917a12, `HomeGoalPageTests.ThePlaceTileOfAFinishedCrawlNamesItsOwnLevelAndTheCrawlsBeforeIt`). The lead read the fix instead of a second review: no must fix had changed how it works.
- designer: no must fix. Help's Fading line was checked in `help-fading.png`. Should fix: the tile's level (fixed, as above); the three debt rows sat outside the table (moved); one changed row worked by hand (Runnyeye D0 at 28, above). Not this change: Help's "What changes XP per kill" table cuts off its last row (docs/debt.md).
