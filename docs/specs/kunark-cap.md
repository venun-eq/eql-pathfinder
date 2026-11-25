# Ready for Kunark's level 55

**Why:** On 12-08 Kunark raises the cap to 55, and today the app still treats 50 as the end: Home says a main at 50 is
farming at the cap with five levels to go, the farm rate counts leveling at 50 as farming, and Campaign marks a run done
five levels early. After this, everything that asks "at the cap?" reads the cap of the day the play happened: 55 from
launch day, 50 before it, so what was finished at 50 stays finished. The next expansion that raises the cap is one
more entry in a list and its test, not another branch like this one (docs/engineering.md rule 13).
**Players:** everyone with a character at 50 on 12-08, worst for the regular whose main farms at 50. New players who
turn the log on after launch with a character at 50 see it as below the cap, as it is.

## What the player sees
No new layout: the same cards, chips and bars, with the cap, the hours and the XP penalty read by level. Every number
below comes from the expansion list (rule 16): "Kunark", "12-08", the old cap 50 and the cap 55 are its entries, so
the page data carries them (`reinc.levelCap`, and `reinc.newLevels` = { expansion, day, from: old cap, to: cap } while
the newest expansion's levels lack numbers). Before 12-08 every screen reads exactly as today.

```text
Home, the regular at 50 on 12-09, the trio's relevel done at 50 on 12-05      (numbers made up)
BEFORE (today's code)                          AFTER
Farming at 50 · Auto: you're at 50             Leveling                                       50 / 55
Where to farm at 50 ...                        WAR  CLR  RNG   [No extra XP to level past 50] (i)
                                               No route past 50 yet: Kunark opened levels 51 to 55 on 12-08,
                                               and neither your logs nor the players who share have crawls there.
                                                                               Ask: where to level →
```
- **Home at the old cap, after launch** (`levelingHero`): "Leveling", the trio, "50 / 55". No hours, no strip: Ask's
  route has no legs. The line under it, in place of "Ask fills in a route once you've played…", is the text above (`.sub`);
  not sharing: "…on 12-08, and your logs have no crawls there yet. Once other players share, their places show here."
  The done relevel stays in Relevels and Campaign as done; no "Finish …" step in Up next.
- **The XP chip** (`.chip.xp`, as the relevel hero has it) shows on this card too, once the level is at the old cap
  or past it and the trio's rank is above 0: "No extra XP to level past 50". Its title: "Levels 51 to 55 came with
  Kunark: none of WAR, CLR, RNG or your race has reincarnated since reaching them, so each needs its usual XP. Levels 1
  to 50 need +30%." It's news for a player who expects +30%, so it shows (not "all is well" noise). The overlay shows
  the penalty of the level you're at: past 50 its "+30% XP to level" and the Lv/h "at +30%…" note are absent, as for a
  trio never reincarnated.
- **Home, a relevel going on 12-08** (relevel hero): "32 / 55". Hours stop at the highest level a finished trio
  reached: "12.4 h so far · ≈ 6.2 h to 50" (was "to go"), and the (i) estimate adds "Past 50 there's nothing to time
  yet." The engine's level model must not fill 50-54 from level 49 (Journeys `Curve` today copies the nearest level):
  a level nobody played has no hours. The chip reads the level you're at: "+30% XP to level · WAR ×3" below 50 (title
  adds "Levels 51 to 55 need their usual XP…"), "No extra XP to level past 50" from 50. Ghost race and "ahead of your
  best" compare at the same level, as today; past 50: "Nothing to race past 50 yet: your relevels so far ended at 50."
- **Up next on Home:** "Finish WAR/CLR/RNG: 32 → 55, about 6.2 active hours to 50. Next: …". The big "+30%" for the
  next run is the penalty to level 50; its title adds "51 to 55: +0%, new levels."
- **Campaign** (`campLevelBits`, the leveling list): "32 / 55 · ≈ 6.2 h to 50", "Leveling now · 2 runs reincarnated,
  below 55". A run done at 50 before 12-08 stays done and keeps its number ("Run 7 of 48"). The "XP to level" column
  stays the run's penalty to the old cap, and its title gives the new levels' ("+30% to level 50, +0% for 51 to 55").
- **Roster:** a class whose loadout is at 50 reads "parked at 50" with a muted bar at 50 of 55 (true: below the cap).
- **Ask, where to level:** presets "1–55 · 1–20 · 20–30 · 30–40 · 40–50 · 50–55" (the last is old cap to cap; before
  Kunark "46–50" as today). Steppers go to the cap. 50–55 with nothing: heading (`.h2`) "No route past 50 yet", then
  (`.muted`) "Kunark opened levels 51 to 55 on 12-08, and neither your logs nor the players who share have crawls
  there yet. Ask fills this in as players level there." (not sharing: the Home variant's last two sentences). 1–55 with
  numbers to 50: "≈ 28 h" then "to 50" in `.muted`, not `--warn` (nobody failed: the levels are new, rule 21), and the
  sentence "No route past 50 yet: Kunark opened…" in place of "No numbers from level 50 to 55…". The bar's gap blocks
  stay. A gap that isn't wholly above the old cap keeps today's words. Once numbers arrive to 52: "No route past 52 yet".
- **Ask, the penalty line:** "Times are for the XP WAR/CLR/RNG needs now: +30% to level 50 for 3 reincarnations of its
  most-reincarnated class, and its usual XP past 50, where none has reincarnated yet." For 50–55 alone: "Times are for
  the usual XP: levels 51 to 55 are new for WAR/CLR/RNG and your race." The crawl table's "XP to level" column shows
  each crawl's own ("+0%" at 51+).
- **Ask, best farm:** presets "10 · 20 · 30 · 40 · 50 · 55" (before: as today); at the cap the label is "at 55", and 50
  is a band like any other ("counts levels …"). At 55 on launch day it's today's "No farming at 55 since the patch in
  your logs" with "Other players answer this one".
- **Help:** the "+% XP to level" (i) and Help's Reincarnation row add: "A level needs the extra XP only for the
  reincarnations made after your class or race reached it: levels 51 to 55, new with Kunark, need their usual XP until
  then."
- **States.** New player after launch at 50: as the regular above, with no chip (rank 0). Not sharing: the variants
  above. Too little: one crawl at 51 is no route (the sample gates); hours reach the highest level with numbers.
  Waiting: the app open over midnight flips to Leveling on its next data, nothing blanks. Narrow and 150%: the chip
  wraps under the trio as the relevel hero's does.

## Done when
- [x] The cap is one list of expansions, each from its day: Classic 50, Kunark 55 from 2026-12-08 - `TheCapIs50UntilKunarkAnd55FromItsLaunchDay`
- [x] A made-up third expansion is one more entry, and the tracker follows it with nothing else changed - `AnotherExpansionThatRaisesTheCapIsOneMoreEntry`
- [x] A kill at 50 adds no XP before 12-08 and adds levels from 12-08; a visit at 50 is at the cap only before - `AKillAt50AddsXpOnlyFromKunarksLaunch`
- [x] A ding past the old cap before the listed day means the new cap has come (the launch moved, or the list is late) - `ADingPastTheOldCapMeansTheNewCapHasCome`
- [x] Reaching 50 marks a class at the cap before 12-08; after it, reaching 55 does - `ReachingFiftyIsTheCapBeforeKunarkAndNotAfter`
- [x] A relevel that reached 50 before 12-08 stays done at 50 when played on after; one at 50 after 12-08 is still going - `ARelevelThatReached50BeforeKunarkStaysDoneAndOneAt50AfterItIsStillGoing`
- [x] Crawls at 50 from 12-08 leave the farm rate; the ones before stay in it - `PlayAt50AfterKunarkIsLevelingAndLeavesTheFarmRate`
- [x] The community job applies the cap of a shared crawl's day to its kills, from the raw facts it already gets - `TheCommunityJobReadsAKillAt50ByTheDayOfTheCrawl`
- [x] At 50 on 12-09 Home is leveling, the page data's cap is 55, and Ask takes 50 to 55 and has no route yet; on 12-05 Home farms at 50 - `At50AfterKunarkHomeIsLevelingTowards55AndAskHasNoRouteYet`
- [x] A level needs the extra XP only for reincarnations made after the class or race reached it: rank 3 at 50 before 12-08 is +30% to 50 and none for 51-55; reincarnated again after reaching 55, +40% to 50 and +10% for 51-55 - `TheXpPenaltyCountsOnlyReincarnationsMadeAfterReachingTheLevel`
- [x] A relevel's hours to go stop at the highest level a finished trio reached, with no hours filled in past it - `TheEtaStopsAtTheHighestLevelAnyonePlayed`
- [x] Home at 50 after launch: "Leveling", "50 / 55", "No route past 50 yet: Kunark opened levels 51 to 55 on 12-08…", the chip "No extra XP to level past 50" - seen in `kunark-home-50of55.png`
- [x] Home's relevel at 32 after launch: "32 / 55", "≈ … h to 50", "+30% XP to level" - seen in `kunark-home-relevel.png`
- [x] Campaign: "32 / 55 · ≈ … h to 50", "below 55" - the screenshot shows "32 / 55 · ≈ 6.2 h to 50" and "below 55"
- [x] A run done at 50 before 12-08 is still done - `ARelevelThatReached50BeforeKunarkStaysDone...` and `ATrioThatDinged50OutsideACrawl...` (the Campaign mock can't draw a done run; its shot is in docs/debt.md's devsnapshots work, queue Later #13)
- [x] Ask 50–55: "No route past 50 yet" and its sentence; presets "1–55 … 50–55"; the stepper reaches 55 - seen in `kunark-ask-no-route.png`
- [x] Ask 1–55: "≈ … h" "to 50" in grey, the no-route sentence, the penalty line "+30% to level 50 … usual XP past 50" - seen in `kunark-ask-1to55.png`
- [x] Before 12-08 every page reads as today ("/ 50", "46–50") - seen in the `main-*.png` of a snapshot dated before

## Edge cases
- **Nothing yet:** on launch day nobody has play above 50. Ask and Home say plainly there's no route there yet: no
  zeros, no guess, nothing new stored (the user, 11-24).
- **Too little:** the first crawls at 51-55 go through the sample gates every route already has; one crawl is no route.
- **Play the log never saw:** a character at 50 whose relevel ended before the log was on is below the cap from 12-08.
  Campaign still counts that relevel done, from the roster's counts.
- **Starting and stopping:** the app left open over midnight into 12-08 changes the cap with no new log line, so every
  cache keyed on the settings (`SettingsKey`, the journeys' key) holds today's cap. A full re-read after launch gives
  play before 12-08 the cap of 50.
- **The rules change:** the entry is a local day, read on the log's own clock. Play on 12-08 before the patch's downtime
  reads as Kunark: a farming crawl at 50 that night is a leveling crawl with no levels (question 2 in the report).
- **Same trio across the launch:** a relevel done at 50 on 12-05 and played 50 to 55 after it stays done at 50, and
  that play is leveling on Home, not a new Campaign run (question 1).
- **A loadout swapped in:** the cap-loadout stopgap (`CapLoadout.Played`: below the cap, kills with no XP percent)
  reads the cap of the visit's day, so a 50 loadout after launch with full-bar kills isn't taken for a swap.
- **Old versions in the field:** an app without this build sends crawls at 50 after 12-08 with derived numbers that
  say 0 levels. The job recomputes from the raw kills with the day's cap, so no server change and no new field.
  Older apps reading the community file see bands past 50 they never ask for.
- **Re-sends and limits:** if the derived math changes only for play from 12-08, only those units are re-sent, not
  every player's history (the daily limit).
- **Stored facts:** `CapKills`, `CapPercent`, `KillPercent` and `ReachedCap` are worked out with the cap when read. A
  player who updates after 12-08 has play since launch stored as at the cap, and needs it read again (Decisions).
  `RereadGuardTests` sees only levels 2 to 23: scrubbed lines at 50 go into what it hashes first (docs/quality.md).
- **Something leaving the PC:** nothing new. Open-world stretches at 50 become "below the cap" from 12-08 and are
  shared as other leveling stretches are; the day is already sent.
- **Locale and clock formats:** the community job reads a unit's `Day` (yyyy-MM-dd, the player's local day).

## Not doing
- The token's 8 crawls for Kunark: routes-you-can-shape adds them to this list later. The list takes another value
  per expansion, but carries only the cap now (the user, 11-24).
- Leveling data from eqlwiki or any outside source for 51-55 (the user, 11-24).
- Gaining no levels at the cap and Levels/h stopping at the ding: queue row at-cap-levels, after this.
- A measured default farm rate: queue row farm-rate-default.

## Where
- `src/EqlPathfinder.Engine/Tracking/Expansions.cs` (stub: `Expansion`, `Expansions.All`), `EngineState.cs`:
  `TrackerOptions.Expansions`, `CapOn` (stubs), `LevelCap`
- `Tracking/Tracker.cs`: `EmitKill`'s cap kills, `Touch`'s `ReachedCap`; `Tracking/VisitStats.cs` `AtCap`;
  `Tracking/Records.cs` `PlayedOnACapLoadout`
- `Analysis/Journeys.cs` (`Build`, `Curve`, the model), `RelevelPaces.cs`, `RouteAnalysis.cs` (`FarmRates`,
  `FarmingOptions`, `LevelingOptions`, `Bands`), `PlayFacts.cs`, `Planner.cs`, `FieldHunts`, `Relevels.Of`
- `Community/CommunityBuilder.cs`: the static `LevelCap`, `Recompute` and the kill count read the crawl's `Day`
- `Sharing/SharePayload.cs`: the field stretches and crawls below the cap
- `Live/TrackerService.Queries.cs` (`ReincarnationData`, Home's Ask query, `HomeState.Of`), `.Snapshots.cs`
  (`IsFarmingMode`, next step), `.Campaign.cs`, `.cs` (`SettingsKey`); `Pages/AskPage.cs` (`CreateContext`'s 49/50 clamps)
- `src/EqlPathfinder.App/web/js/ask.js` (the 1-50 presets, the steppers' 50, `f.band === "50"`, `askLevel`'s gap
  words), `core.js` `cap()` and the `xp`/`estimate` (i), `campaign.js` `campLevelBits`, `home.js` (`levelingHero`, the
  relevel hero's chip and hours, `renderUpNext`), `race.js` `xpOf`; `index.html` Help's Reincarnation row;
  `OverlayWindow.xaml.cs` (`run.XpMultiplier` by the level now); the XP penalty by level wherever `Rank` sets it
- `src/EqlPathfinder.Cli/Program.cs` (`Bands`)
- Tests: `tests/EqlPathfinder.Engine.Tests/KunarkCapTests.cs`

## Decisions
The architect, 11-24. The user's choices of 11-24 are given: the cap rises at local midnight on 12-08, nothing from
outside sources, done at 50 stays done, the penalty is by level, Campaign shows one XP number.

**The list**
1. **It ships in the release.** `Expansions.All` is code and `TrackerOptions.Expansions` stays `[JsonIgnore]`: no
   setting, nothing fetched. A fetched date would be a server change and a new way to fail on launch night. A release
   reaches every copy within the hour, and old logs can be read again.
2. **`From` is a day on the player's own clock,** with no time zone: compared with a log line's time in the tracker,
   with the service's `TimeProvider` for "now" (never `DateTime.Now`: the dated tests must pass after 12-08 too), and
   with a unit's `Day` in the job. All three are the player's local day, so the app and the job agree.
3. **Room for the next value.** A later per-expansion value (the token's crawls) is an optional property on
   `Expansion`, null meaning "as the expansion before", read by one helper: the latest entry at or before the day that
   sets it. The cap stays the required third argument, so a three-argument entry keeps compiling.
4. **`TrackerOptions.LevelCap` is deleted,** not kept as "the cap now": a cap with no day is the bug. The compiler
   then finds every reader, and each takes one of three: `CapOn(the play's time, its level)`; the cap of now, from
   the service; the highest cap in the list, for sizing arrays only. Literals the compiler won't find: `Ask.Levels`
   (`< 50`, twice), `AskPage.CreateContext` (49, 50), `CommunityBuilder` (`> 49`, twice: the cap of the unit's day
   less one). Tests that set `LevelCap = n` (CacheKeys, ClassRolesWiring, JourneyRaceWiring, KillsAtCap, Verdicts) give
   a one-entry list and keep their meaning.
5. **The ding fallback is a rule, not a stored state:** `CapOn(t, level)` is the cap of the day, and when the level
   is above it, the cap of the first later entry that holds the level. It reads a kill, a visit and a shared kill row
   (which carries its level) alike, and a re-read gives the same answer. Its limit: between an early launch and the
   listed day, a loadout sitting at exactly 50 still reads as at the cap until the release with the corrected day. A
   level past every listed cap keeps the day's cap: nothing is guessed, and the release that adds the entry re-reads.

**What's stored**
6. **No new table or column, and one re-read.** `CapKills`, `CapPercent`, `KillPercent` and `ReachedCap` stay what the
   tracker makes of the log with the cap of each line's day. Raise `EngineState.CurrentVersion` to 17 here, release
   note `reread: true`: a copy that updates after 12-08 has an older checkpoint, so its play since launch is read again
   at 55. The same for every later change to the list. `RereadGuardTests` holds it: made-up Standin lines at 50 on both
   sides of 12-08 go into the fixture, and the list's (day, cap) pairs into what it hashes, so an entry added or moved
   without the version fails.
7. **`ReachedCap` means "reached the cap of that day".** Whoever asks "at the cap now?" compares the level with the cap
   of now (Roster's "parked at 50").
8. **A relevel is done** when one of its visits ends at or above the cap of that visit's day, or its classes have
   reincarnated again. `Journeys.Build` takes no clock, and later play by the same trio never reopens it.
9. **Caches follow the cap of now:** `SettingsKey` carries it, so midnight drops them with no log line. A test:
   `TheAppLeftOpenOverMidnightTurnsToLeveling` (the clock moves from 12-07 23:59 to 12-08 00:01, nothing else).

**Hours to go** (`TheEtaStopsAtTheHighestLevelAnyonePlayed` pins these)
10. `HoursPerLevel` stays one function for Journeys and RelevelPaces. Its top is the highest level a finished relevel
    reached. Below the top today's fill stays, so before 12-08 every number is as today. From the top up a level has
    no hours. The journey carries the level its hours reach (`EtaTo`), and the page writes "to 50" from it. The test:
    at 32 on 12-09 with relevels finished at 50, the hours are levels 32 to 49, `EtaTo` is 50 and the projection ends
    at 50; at 51 there are no hours; with a relevel finished at 55, `EtaTo` is 55; before 12-08 the numbers of today.

**The XP penalty by level** (`TheXpPenaltyCountsOnlyReincarnationsMadeAfterReachingTheLevel` pins these)
11. **The fact** (the user, 11-24): a reincarnation counts for a level only if the class had reached that level before
    it. So with each reincarnation the log sees, the tracker keeps the highest level that class or race had ever been
    seen at (new, in the checkpoint beside `Seen`, rebuilt by the re-read of 6). Never seen: the cap of that day, as
    `Journeys` already takes it ("reincarnating needs the cap"). This is right whether or not the game lets a class
    reincarnate at 50 after launch, and for one reincarnated on 12-08 before the patch.
12. **Reincarnations the log didn't see** (a count typed in Settings, the gap below a stated rank) count for levels up
    to 50 only. Right for every count typed before launch; a count typed later can't say how many came after 55
    (question for the user).
13. **One function** gives the count at a level for a class at a time, and the trio's is the highest among its
    classes at that level (classes only, as docs/pace.md has it). `Rank` keeps its meaning, every reincarnation, which
    is the count to level 50, so play below 50 is unchanged. `Planner.XpMultiplier(count)` stays the only formula.
    Whatever spreads time over levels (`Ask.Levels`, `RelevelPaces`, `JourneyRace`, `RunPace`, the overlay) takes the
    multiplier of each level, not one for the visit.
14. **The test:** three seen before 12-08 give +30% at level 49 and none at 50 to 54 (the bar at 50 is a new level);
    one more after reaching 55 gives +40% and +10%; class A with three old and class B with one new give 3 below 50
    and 1 past it; a count typed after 12-08 counts to 50; and a number made with it: a rank-3 trio's crawl at 52 is in
    Ask's minutes a level as played, not divided by 1.3.
15. **Other players' play past 50** is taken as played: the job counts no reincarnation for a shared unit's levels
    past 50. True until someone reaches 55, reincarnates and levels past 50 again, and off by 10% for that player's
    units after. `Rank` as sent is unchanged. The exact count is its own branch (queue: penalty-by-level-shared).

**What's sent, who else reads it, the server**
16. **Nothing new leaves the PC,** and `site/privacy.html` stays. Shares keep the raw kill rows and the day, and the
    job applies `CapOn(the unit's day, the kill's level)` in `Recompute` and `KnownXpKills`.
17. **No unit from before 12-08 changes.** `SharePayload` reads the cap of each kill's and visit's own time, never
    now's, or every old open-world kill at 50 turns into a new stretch and goes up. A test:
    `SharesOfPlayBeforeKunarkAreTheSameUnitsAfterIt` (the same history built with the clock before and after: the same
    ids and hashes). `Derived` is outside the hash, so its new numbers re-send nothing.
18. **No server change.** The Worker already takes levels to 200 and rank to 999 (`server/src/sharing.ts`), and facts
    are a blob to it. The job needs this build: release note `job: true`. If `CommunityFile.Rules` states a cap, the
    new one is a field added beside it.
19. **Older apps** send crawls at 50 and above with raw kills, which the job reads by the day; their open-world kills
    there don't arrive until they update. Reading the file, they take others' crawls from 50 up as farming at the cap.
    Those are real loot rates at levels they call 50, and it ends when they update.

**Updates, limits, taking it back**
20. A copy that is offline or versions behind on 12-08 keeps 50 until it updates, then re-reads (6). Shipping after
    12-08 is still right, for the same reason.
21. Re-sends: for a copy that updates late, only its open-world play at 50 and above since launch is new. A re-read
    gives the same facts, and identical re-sends cost nothing against the daily limit.
22. **Taking it back:** a launch that moves is the entry's day edited, the version raised and `reread: true`. Every
    stored cap number comes from the logs and nothing sent is worked out with the cap, so nothing is left behind.

**Docs:** the engineer records 10 to 15 in docs/pace.md (beside "A relevel's XP rank counts the trio's classes only"
and "The farm rate at the cap"), the cap rows of docs/calculations.md, docs/community-job.md's "Same rules as the
app", `Expansions.cs` in docs/code-map.md, and takes "Owed: kunark-cap" off rule 13.

**The build is split in three, one after the other** (it spans the engine, the job and five page scripts):
- **A, the cap by day:** 1 to 9 and 16 to 22. The engine, the tracker, the services, the job, the CLI, the nine
  failing tests and the three named in 9, 10 and 17.
- **B, the penalty by level:** 11 to 15, with its test first, the overlay and docs/pace.md.
- **C, the pages:** the page data (`reinc.newLevels`), the scripts, Help, the screenshots and the release note.

## Reviews

- designer: 2 must fix (the race ring drawn at 55 while the hours stop at 50; "No route past 50 yet" after crawls at 51+), 4 should fix (Home's route line under no route, Campaign's done-before-12-08 shot, Help's order, the release note's "anyone") and Solo/Group "to 50": all fixed except the Campaign shot, whose box is unticked (the engine test covers it). Three findings outside the branch went to docs/debt.md.
- architect: 3 must fix (a relevel dinged 50 outside a crawl read done at 55; Ask's and the overlay's penalty at 50; "Best farm at 55" listing level-50 farms), 1 should fix (shared segments carry their day's cap) and the literal caps: all fixed with tests. The lead made RunPace's bar use the run's own day cap rather than the first cap, so the next raise needs no change there. Left to the queue's Inbox: ask-bands-past-cap, planner-rank-past-cap, penalty-by-level-shared, ask-not-sharing-past-cap.
