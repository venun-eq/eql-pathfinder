# The community numbers follow the app's rules

**Building, 11-19** ([numbers-audit](numbers-audit.md)). **Tier:** risky (the user, 11-19: `Community/` is on the risky
list), 600k, `job: true` (the NAS job publishes new numbers). **Unblocks:** sample-gates. share-without-derived also
touches `CommunityBuilder` and is left out (the user, 11-19).

**Why:** your own numbers and the community's are compared side by side (Ask's "You against players who share", the
overlay's "Players here"), but the job works some of them out by other rules.
**Players:** everyone who reads the community numbers. Worst for a sharer among few sharers, whose own play is taken
out of the pool by `Without` and must come out by the rule it went in by.

## Findings, checked 11-19
- **DPS: applies.** `CommunityBuilder.Visit` divides by time inside (`c.Damage.Total / c.ActiveSeconds`); the app's
  `VisitStats` uses `Rates.PerSecond` over active time, null without a rate (docs/engineering.md, Modeling 9).
- **Which visits count: applies.** `PlayerAsk.From` calls `AskData.From` without `onlyWithRate`; every app call passes
  `true`. The job's `Visit` also never sets `Completed`, so turning the gate on alone would drop finished crawls under
  8 kills. "Ask.Bands still counts visits with no rate" is still true (`Ask.cs`, `AskData.From`: bands use `usable`).
- **Without: applies with it.** `CommunityData.Build` takes your play out with its own `AskData.From` call, also
  without `onlyWithRate`. Change one and not the other and a sharer's view is wrong.
- **Idle: doesn't apply.** `IdleThresholdSeconds` is no setting: nothing in `AppSettings`, the window or the pages
  sets it, so the app and the job both use `TrackerOptions`' 180 s.
- **Fragment value: fine.** `AskPage` passes `o.FragmentValue` to `For` both times. `TrackerService.JudgeRun` calls
  `For` without one (2.0), but `OthersHere.At` reads only minutes and kills a minute, which no fragment value moves.
  `Without` subtracts your T5-eq at your value from a pool recombined at the value `For` was given: the same one.

## Done when
- [x] One crawl from log lines reads the same through sharing and the job as in the app: active minutes, levels,
  kills, deaths, finished, DPS, and T5-eq at the player's fragment value - `CommunitySameRulesTests.OneCrawlReadsTheSameThroughTheJobAsInTheApp`
  (fails now: the job's visit isn't finished)
- [x] The job's DPS divides by active time, and is 0 for a visit with no rate - `TheJobsDamagePerSecondDividesByActiveTime`
  (fails now: 36.7, time inside, where 50 is owed)
- [x] Visits with no rate (unfinished, under 8 kills) rank no place and no difficulty in the file, still count in its
  bands, and the job's log line counts them as "no rate"; a finished crawl ranks whatever its kills -
  `VisitsWithNoRateAreLeftOutOfThePoolsPlacesButNotItsBands` (fails now: the place is published)
- [x] A sharer's view without their own play equals the pool built from the others alone - `ASharersPlayComesOutByTheRulesItWentInBy`
  (passes today; seen to fail with the gate on in the job and not in `Without`)
- [x] A kill row at the level cap adds no levels to the job and isn't counted in XP per kill, while it still counts as a kill - `KillsAtTheCapAddNoXpAndAreNotCountedForXpPerKill`

## Edge cases
- **Too little:** an in-and-out with 5 kills has no rate in the app; it now has none in the pool either. A cell held up
  only by such visits goes, and Ask says "not enough players" there, as it already does for a cell under the rules.
- **Old versions in the field:** no field is added or given a new meaning. DPS stays damage a second, now over active
  time like the app's; older apps read it as before. The file's schema stays 2.
- **Shared by an older app:** a crawl without `End`/`Pauses` keeps the active minutes it was sent with, and its DPS
  divides by those. `Completed` has been sent since schema 1, so the gate reads it for every crawl.
- **Open-world stretches:** no finish, so they rank with 8 kills or more, as `LevelingSegment.OpenWorld` does in the app.
- **Re-sends and limits:** nothing changes in what apps send, so nothing is re-sent: the job recomputes from the
  facts it holds.
- **Sharing, among few sharers:** `Without` must use the job's gate (the fourth box).
- **Not sharing:** reads the new numbers as they're published; nothing of theirs is in them.

## Not doing
- The idle threshold as a setting: it isn't one.
- `TrackerService.JudgeRun`'s `For` without a fragment value: what it reads doesn't depend on one.
- One smallest sample for each purpose: sample-gates, after this.

## Where
- `src/EqlPathfinder.Engine/Community/CommunityBuilder.cs`: `Visit` (DPS through `Rates.PerSecond` over the active
  minutes; `Completed` on the segment), `PlayerAsk.From` (`onlyWithRate: true`), `Build` (count "no rate" in `stats`)
- `src/EqlPathfinder.Engine/Community/CommunityData.cs`: `Build`'s `AskData.From` for `Without`
- `tests/EqlPathfinder.Engine.Tests/CommunitySameRulesTests.cs`; docs/community-job.md; docs/code-map.md's test list

## Decisions
- **One gate, three calls.** `PlayerAsk.From`'s `Run` passes `onlyWithRate: true` (so the motes and fragments tables get
  it too), and `CommunityData`'s `Without` call does the same. `Visit` sets `Completed = c.Completed` on the segment;
  open-world stretches stay unfinished and rank from `Rates.MinKills`, as in the app. The job's "no rate" count is the
  sum of each player's `all` table's `AskData.NoRate` (not solo or group, which would count a visit twice), so it means
  what the page means.
- **DPS:** `Rates.PerSecond(c.Damage.Total, active, c.Completed, c.Kills.Count) ?? 0`, with `active` the minutes
  `Recompute` gives (less idle; an older app's crawl without `End` keeps the minutes it was sent with). The segment's `Dps`
  stays a double, so no rate is 0; no-rate visits never reach a place's damage anyway, being out of `ranked`.
- **XP at the cap (from xp-at-cap's review).** A crawl's kill row whose level (column 4) is `>= LevelCap` adds nothing to
  `killXp` in `Recompute(SharedCrawl)` and isn't counted by `KnownXpKills`; it still counts in `Kills` (rule 11). That is
  the app's rule (`Tracker.EmitKill`: `xp.Level >= Options.LevelCap` goes to `CapPercent`). Read the cap once, as
  `IdleThresholdSeconds` is: `static readonly int LevelCap = new Tracking.TrackerOptions().LevelCap` in `CommunityBuilder`.
  A row with no level is kept: unknown isn't the cap. Rewards at the cap still count, as they still do in the app (its
  debt line); when the app stops counting them, the job follows in that branch. Stretches need nothing: apps send only
  kills under the cap (`SharePayload`), and `KnownXpKills` covers both anyway. The rule reads only shared rows, so it
  doesn't need xp-at-cap's code: the engineer adds a failing test of the job alone (a crawl with rows at 49 and at 50:
  levels and XP per kill's divisor leave out the 50s, kills keep them), and merges main first if xp-at-cap is there.
- **What's stored, what's sent:** nothing new. No column, no field, no new meaning; the file stays schema 2; apps send
  what they did, so nothing is re-sent and nothing re-read (`EngineState.CurrentVersion` stays). Privacy unchanged.
- **Who else reads it:** `job: true`, so ship.ps1 deploys the job with the app that carries the new `Without`. An app not
  yet updated takes its no-rate visits out of a pool that no longer holds them: at a level where it had only those, a
  cell loses a player it never counted (it says "not enough" sooner) and its rates shift, clamped at 0 by `Without`. Only
  that sharer's own view, until it updates: accepted rather than holding the job back a release.
- **Taking it back:** revert the commit. The job recomputes every number from the facts it holds every 30 minutes, so
  the first run after that release publishes the old numbers. Nothing to undo in the database.

## Reviews

- architect: no must fix; all five tests fail without the change (the cap test worked through by hand), the gate sits in both PlayerAsk.From and Without, and the DPS gate matches the app. Should fix: shipping before xp-at-cap would make the note's "as in your own play" untrue for a release; xp-at-cap reached main first and this branch merges it. The cap rule had no Done-when line: added.
