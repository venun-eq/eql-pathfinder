# A visit is judged against the visits before it

**Built 11-18 from the draft of the same day** ([calc-audit](calc-audit.md)). **Tier:** small, 60k. The words don't change, only which visits count, so no designer.
**Waits for:** nothing. **Unblocks:** one-usual.

**Why:** a verdict's usual is every other visit at the place, those played later included. `FarmPace.For` and
`RunPace.Judge` keep out only the visit itself (`h.VisitId != run.VisitId && h.Start != run.Start`). So a row's
"+34% more T5-eq/h" in History → Crawls changes as the player keeps playing, and an old visit is judged on play that
hadn't happened yet. The user, 11-18, chose "earlier visits only": a visit's verdict is fixed once it's over.
**Players:** everyone reading History → Crawls. Home and the overlay judge the visit going on now, which has no later
visits, so they read the same.

## Proposal

- The comparison sets in `FarmPace.For` and `RunPace.Judge` take only visits that started before this one
  (`h.Start < run.Start`). The same rule applies to `PlaceCheck` if its "another place levels faster" reads later
  visits. Check it.
- A visit with under 3 earlier visits reads "no usual yet", as it does now with under 3 at all. Early visits at a
  place lose their verdict. That's right: there was nothing to judge them by then.
- `FarmPace.ForStop` weights each visit's own usual, so it follows by itself.

## Where

- `src/EqlPathfinder.Engine/Analysis/RunPace.cs`: `RunPace.Judge` (`same`), `FarmPace.For` (`same`), `PlaceCheck`
- `src/EqlPathfinder.Engine/Live/TrackerService.cs`: `Judged()` (the history it passes)
- docs/pace.md: "against your usual" says "your earlier visits"

## Not doing

- Limiting the usual to visits near the visit's level or with the same trio. That's one-usual's question.
- `PlaceCheck` for a past visit (History → Crawls' "another place levels faster"). It does read later visits: it's
  worked out from Ask's data for all play up to now (`TrackerService.JudgeRun`). As of each visit's start that's one
  build of Ask's data per visit, about 900 a refresh, so it needs the windows built incrementally: a row in
  docs/debt.md, and docs/pace.md says the place check still reads all your play.

## Done when

- [x] Kill speed's and XP per kill's usual are runs that started before the one judged -
  `RunPaceTests.AVisitIsJudgedOnlyAgainstTheVisitsBeforeIt` (five later runs at 6 a minute don't move a usual of 4)
- [x] T5-eq an hour's usual is visits that started before the one judged -
  `JudgeByPlayTests.AFarmingVisitIsJudgedOnlyAgainstTheVisitsBeforeIt` (+30% against 40 an hour, not "usual" against 60)
- [x] A place's first visits have no usual, and the fourth has three - both tests above
- [x] docs/pace.md says your usual is your earlier visits; the release note says the verdicts no longer change

## Edge cases

- **Too little:** a place's first three visits read "No usual yet", as any visit with under 3 to compare does.
- **Same name, different thing:** two runs that start in the same second (a visit split in segments) don't judge
  each other: `h.Start < run.Start` keeps out the same second as `h.Start != run.Start` did, and segments of one visit
  are kept out by their `VisitId` as before.
- **The visit going on now:** nothing started after it, so Home's live card and the overlay read as before. Home's
  earlier stops today follow `FarmPace.For`, and `ForStop` weights each visit's own usual, so they follow too.
- **Tests that built the usual after the run:** four did (a lazy `Select`, or the run first in the call), and one
  more passed only because of it. Corrected in a commit of their own, which passes with and without the change.

## Reviews

- architect: sound, no findings. Both new tests fail without `h.Start < run.Start` (later runs move the usual); the correction commit only moves each usual before its run and keeps every assertion; `PlaceCheck` reading all play is a row in docs/debt.md and is said so in docs/pace.md, and can wait.
