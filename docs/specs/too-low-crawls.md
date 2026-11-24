# Crawls too low-level to refund stay out of the usual

**Spec, 11-18 (engineer): small, in the views.** From [chestless-crawls](chestless-crawls.md). The user, asked whether
such crawls belong in the farming usual: "Leave them out". **Waits for:** nothing. one-usual will change rate gates in
the same file later; this touches only the `same` filter and what feeds it.

**Why:** a crawl finished at a level where its mobs were too low ("…too low-level to earn a refund of your instance
charge") has a chest with gear only, 0 T5-eq. Counting it drags the usual down and says nothing about how the player
farms the place. **Players:** anyone whose history has one (4 in the user's log).

## How they are told apart (checked)
The log has 232 completions: 228 `You earned a refund` and the 4 too-low ones, and all 135 completions in the
2026-10-14 split have a refund too, so `Completed && !Refunded` is exactly "too low" in the logs on this PC. Refunds
appear from Sep 9 (first completion). The too-low line is not parsed, so nothing new is stored: no re-read.
A raid is already its own kind in `VisitKinds`, so it never meets this filter.

## Done when
- [x] A finished, unrefunded, non-raid crawl in the history isn't in `FarmPace.For`'s usual or its run count -
  `TooLowCrawlTests.ATooLowCrawlIsLeftOutOfTheUsual`
- [x] The visit judged can itself be one: it is still judged (0 against the usual reads "less") -
  `TooLowCrawlTests.ATooLowCrawlIsStillJudged`
- [x] A crawl that did refund stays in, even with 0 T5-eq - `TooLowCrawlTests.ARefundedCrawlStaysIn`

## Edge cases
- **Too few left:** two good crawls and a too-low one: two runs, "none", as with two visits.
- **Older logs:** a log from before refunds existed would show every crawl as too low. None on this PC; if one turns
  up, the usual would be empty ("none"), not wrong numbers.
- **History → Crawls** still lists and judges the visit; only the usual drops it. `ForStop` follows by itself.
- **Community numbers:** shared segments don't set `Refunded` or `Completed`, so they are untouched; job false.

## Where
- `src/EqlPathfinder.Engine/Analysis/Ask.cs`: `LevelingSegment.Refunded`, `From`
- `src/EqlPathfinder.Engine/Analysis/RunPace.cs`: `FarmPace.For` (`same`)
- `tests/EqlPathfinder.Engine.Tests/TooLowCrawlTests.cs`; docs/pace.md

## Reviews
Needs: the architect after the build.
- architect: no must fix or should fix. The filter is one clause in `FarmPace.For`'s `same`, so the band follows with the usual; `Refunded` was already parsed (since the first engine commit), tracked and shared, so no re-read, nothing new sent, `AskData` and the job never read it (job false is right). The three tests would fail without the change (the judged-itself one guards over-filtering), the helper fixes keep their meaning, and the TrackerService one proves `From` copies the flag. Older logs: an unrefunded history only empties the usual until the next refunded crawl, so no guard is needed.
