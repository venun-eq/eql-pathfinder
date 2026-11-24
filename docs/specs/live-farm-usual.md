# A crawl in progress is compared with the crawls you finished

**Why:** In a D4 Estate of Unrest crawl at level 20, the overlay said "No usual here yet: no visit like this one at
levels 18–22", although the log has more than ten finished Unrest D4 crawls that started at levels 18–22 (11-19). A crawl you're
still in hasn't completed yet, so it was compared only with crawls you left unfinished, and there were none. After the
fix, a crawl in progress is compared with the crawls you finished there, and its verdict, tick and note show while you
play. History was right all along: it judges crawls that are over.
**Players:** everyone who plays a difficulty they farm (D3 and D4 by default), or plays at the level cap, with the overlay
open. It's worst for players who always finish their crawls: their overlay never had a usual for a D3/D4 crawl.

## Done when
- [x] A D4 crawl still going, with five finished crawls before it at its place and level, is judged against them,
  and its usual says "crawls you finished" - `ACrawlStillGoingIsComparedWithTheCrawlsYouFinished`
- [x] A crawl that ended without "completed" is still compared only with the instances you didn't finish -
  `ACrawlStillGoingIsComparedWithTheCrawlsYouFinished`

## Edge cases
- **Too little:** a crawl in progress with fewer than 3 earlier finished crawls in reach still says "no usual yet",
  with the count of those finished crawls.
- **Same name, different thing:** a crawl in progress that you'll leave unfinished is judged against finished ones
  while it runs. Once it ends without "completed", History and the done card judge it against unfinished ones, as
  before.

## Not doing
- Live T5-eq/h before the chest: a finished crawl's T5-eq includes its chest, which a crawl in progress gets only
  at the end. That was already so for every verdict before this fix, and is unchanged.

## Where
- `src/EqlPathfinder.Engine/Analysis/RunPace.cs`: `FarmPace.Finished`, used by `Peers` and `BasisOf`

## Reviews

- architect: no findings. Only a crawl not yet ended (live; `Type == "crawl"`) reaches the new branch: History and Ask segments are built from closed visits only (`PlayFacts.Segments`), which are `Completed` or `left`, so every History row judges as before; `VisitKinds.Of` and `RunPace.Early` still read `Completed`/`Ended` as intended (Of picks the kind by difficulty, Early keeps a live crawl under 8 kills "early").
