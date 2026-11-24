# A crawl's levels belong to the loadout that played it

**Why:** Report #8 (0.10.8, Home): the race chart drew a WAR/CLR/RNG relevel reaching "50 at 3.3 h" while the header
said 33/50. The main (PAL/MNK/SHM, 50) was swapped in and out with only a /who to show it, so a crawl played at 32
started at the main's 50. Reincarnating inside a crawl ends it at 1 (50 → 1). The wrong levels are stored, shared,
and band the community numbers by their start.
**Players:** the regular who swaps loadouts, worst for one releveling several trios. Through the community numbers,
everyone who reads Ask, sharing or not. Not the new player: a single loadout never swaps.

## Done when
- [x] A crawl entered after a swap seen only through /who starts at the level /who shows inside it: the report's
  crawl is WAR/CLR/RNG 32 → 34, not 50 → 34 - `ASwapSeenOnlyThroughWhoBeforeACrawlStartsItAtThePlayedLoadoutsLevel`
- [x] The main swapped in without a /who, and /who'd inside: its crawl is 50 → 50, not 9 → 50 -
  `ASwapUpToTheCapSeenInsideACrawlIsNotALeveledCrawl`
- [x] /who showing another loadout mid-crawl: the crawl ends at the level its own loadout reached (32 → 33, not
  32 → 50) - `ASwapSeenThroughWhoMidCrawlEndsTheCrawlAtThePlayedLoadoutsLevel`
- [x] A crawl left unfinished, then a /who swap, then the next instance closes it: 31 → 32, not 31 → 50 -
  `ACrawlLeftUnfinishedIsClosedAtItsOwnLoadoutsLevel`
- [x] Reincarnating inside a crawl leaves that crawl at the level it was played: 50 → 50, not 50 → 1 -
  `AReincarnationInsideACrawlKeepsTheCrawlAtTheLevelItWasPlayed`
- [x] A /who of the same loadout after a ding inside a crawl keeps where it started (20 → 21) -
  `AWhoForTheSameLoadoutInsideACrawlKeepsWhereItStarted` (passes today; seen failing with `c.LevelStart = w.Level`)
- [x] The race chart of the report's relevel goes no higher than 34 - `TheRaceChartDoesNotReachTheCapForARelevelAt34`
- [x] History stored by engine 9 is read again once, so stored crawls are repaired - `HistoryReadBeforeTheLoadoutSwapFixIsReadAgain`
- [x] The community job leaves out a crawl whose levels go down, or that reaches the cap from below with no kills;
  it keeps farming at the cap with or without kills - `CrawlsWhoseLevelSpanCantHappenAreLeftOut`
- [x] The real log: `dotnet run --project src/EqlPathfinder.Cli -- crawls` lists no row whose Lvl goes down and no
  0-kill row from below 45 to 50. At main 3434108 it lists one of each: Nagafen's Lair 4 `50-34` (51 kills, the
  report's crawl) and Plane of Fear - Solo 4 `9-50` (0 kills)

## Edge cases
- **Nothing yet:** a crawl entered before any /who or ding has no start level, as today; the first /who or ding
  inside gives it one.
- **Same name, different thing:** a /who of another trio at the same level is still a swap: the classes tell it, not
  the level. A /who of the same trio at a higher level after a ding is not a swap (test above).
- **Swap and back with no /who between** (the report): only the /who inside can show it. A ding that jumps levels
  is already a swap (`OnLevel`), and stays so.
- **Class reincarnation of a class not in the active trio:** not this crawl's loadout; the crawl goes on as is.
- **Reincarnating, then playing on at 1 in the same instance:** the crawl's first part ends at its loadout's level;
  what the rest is (a new crawl or none) is the architect's call.
- **Starting and stopping:** the re-read is the usual one (`NeedsRebuild`); closing during it resumes it next start.
- **Old versions in the field:** 0.10.8 and older keep sharing wrong spans until they update; the job's guard drops
  them whatever the app version. `level_start`/`level_end` keep their meaning; no field is added.
- **Re-sends and limits:** the ledger re-sends a unit when its facts' hash changes (`ShareUnits.Make`: the facts
  and the schema, not the engine version), and the server upserts by id, so the re-read repairs this player's pool
  rows. But a crawl's facts hold `Seq`, one order over all crawls, hunts and reincarnations: a fix that adds or drops
  a crawl (closing at a mid-crawl swap and starting another) moves every later `Seq` and re-sends the rest of the
  history, the 11-17 case that hit the daily limit with about 2,900 units. A crawl the fix splits or drops also
  leaves its old unit on the server. Production, 11-18: 2,908 crawl units from 5 installs; 14 go down (all 5 installs
  have some), 22 reach 50 with no kill levels (3 of them from 45-49).
- **Something leaving the PC:** nothing new is sent; `site/privacy.html` is unchanged.

## Not doing
- Deleting or quarantining the bad units on the server: the job's guard leaves them out.
- Guessing a trio for a crawl no /who saw.
- Changing a page: the race chart, Place by place, Ask and the pace checks read the corrected crawls. No new words.

## Where
- `src/EqlPathfinder.Engine/Tracking/Tracker.cs`: `StartVisit` (`LevelStart = State.Level`), `OnWho`
  (`c.LevelStart ??= w.Level`), `Close` (`LevelEnd = State.Level`), `OnReincarnated`, `ForgetLoadout`; `OnLevel`'s
  swap handling is the model
- `src/EqlPathfinder.Engine/Tracking/EngineState.cs`: `CurrentVersion` 9 → 10
- `src/EqlPathfinder.Engine/Community/CommunityBuilder.cs`: `Visit`, beside "impossible time or levels"
- `docs/releases/next/visit-loadout-swap.md`: `reread: true`, `job: true`
- Read, unchanged: `Journeys.Build`/`Curve`, `TrackerService.Judge`, `SharePayload.Build`, `ShareUnits.Make`

## Decisions
The architect's, before code (11-18). No new field is stored or sent; `level_start`/`level_end` keep their meaning
and now hold the right loadout's levels. A revert is another engine bump and re-read; nothing on the server needs undoing.

- **Freeze, don't split.** The set of visits, their ids, starts and so every `Seq` stay as they are, so the re-read
  re-sends only the crawls whose levels change (dozens, not 2,900) and leaves no orphan unit. A swap or reincarnation
  ends the crawl's *levels*: `LevelEnd` is set then, to the level before it. An open visit with `LevelEnd` set is
  frozen, and `Close` does `c.LevelEnd ??= State.Level`. No new field: today only `Close` writes `LevelEnd`. With no
  level known at that moment there is nothing to freeze ("Nothing yet"). Closing the visit at the swap was ruled out:
  the instance's later kills would become an open-world stretch, a new unit that shifts `Seq`.
- **A frozen visit takes nothing more about its loadout:** no trio, race or level from a later /who, ding or
  reincarnation. It still counts kills and time until it closes, as today (docs/debt.md).
- **OnWho, visit not yet confirmed:** the /who names its loadout, as today. If its level differs from `State.Level`
  (read before OnWho updates it), the level carried in was another loadout's: `LevelStart = w.Level`. Otherwise
  `??=` as today, which keeps a ding seen inside (20 → 21).
- **OnWho, visit confirmed:** a /who whose `ClassNames.TrioKey` differs, or whose level differs from `State.Level`
  (a jump with no ding), is a swap: freeze at `State.Level`. Same trio and level: nothing.
- **OnReincarnated:** when `affectsActive`, freeze the open visit (inside or paused) before `State.Level = 1`. A class
  not in the active trio leaves it alone. Playing on at 1 in the same instance stays in that crawl, levels frozen.
- **OnLevel's jump:** keep re-starting an unconfirmed visit (`LevelStart = l.Level - 1`); a confirmed one is frozen
  instead, by the same helper, so all three paths share one rule. One test line for it in `LoadoutSwapTests`.
- **Journeys.cs:69 and :93 stay.** Ending a relevel at a crawl that reaches the cap is right once the stored level is
  the loadout's own; `TheRaceChartDoesNotReachTheCapForARelevelAt34` proves it through them.
- **`EngineState.CurrentVersion` 9 → 10,** with a comment naming report #8; `reread: true`.
- **The job's guard,** beside "impossible time or levels" with that reason, only when both levels are known:
  `LevelEnd < LevelStart`, or `LevelEnd - LevelStart >= 2` while `Recompute`'s levels (kills and rewards) are under 1.
  Under a level of XP gives at most one ding, so it catches the 22 and a swap-up with grey kills, and keeps a
  reward-only ding (49 → 50) and farming at the cap. `job: true`; older apps' units are dropped whatever their version.
- **Privacy and limits:** nothing new leaves the PC. Re-sends are hash-driven and few; the daily limit is untouched.

## Reviews
- architect: (11-18) built as decided. Freeze keeps the set of visits, their ids and every `Seq` (`SharePayload`'s order
  is closed visits, open-world stretches and reincarnations, none of which a freeze moves), so only crawls whose levels
  or inferred trio change re-send. Rule 2 is right to drop 25 -> 32 at 0.95 levels: under one level of XP gives one ding
  at most, and with either end wrong the start band can't be trusted. Must fix: nothing proves a /who of the same
  loadout and level inside a confirmed crawl leaves it unfrozen; every test still passes if `OnWho` froze on any /who
  (`AWhoForTheSameLoadoutInsideACrawlKeepsWhereItStarted` ends at the freeze level). Add a ding after its /who (20 -> 22).
  Should fix: a confirmed crawl /who'd as another trio at the same level, then a ding (ends at its own level), and the
  community test's rule 2 with kills under one level (25 -> 32 at 0.95 dropped; a 2-level span at 1.0+ kept).
  Done (11-18): all three added as tests, no source change. The same-loadout test now has a /who inside the crawl before
  the ding (the architect's version had none, so the later /who only confirmed the crawl and could never freeze it), then
  dings 20 -> 22. New `AWhoForAnotherTrioAtTheSameLevelFreezesTheCrawl` (32 WAR/CLR/RNG then /who 32 PAL/MNK/SHM then 33 ends
  at 32), and rule 2 with kills in `CrawlsWhoseLevelSpanCantHappenAreLeftOut` (25 -> 32 at 0.95 dropped, 25 -> 27 at 1.0 kept).
  Each was proven by breaking its line: freeze on every /who, TrioKey half removed, the `le - ls >= 2 && levels < 1` half removed.
