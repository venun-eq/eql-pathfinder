# Calculations: every number a player sees, and what holds it

For each number: what it is, how it's worked out, whether it is stored or worked out when shown, its one function, and the
test that holds it to a value worked out by hand. **Where no test holds the number, its row says so.** The user, 11-18: "I
would also like documentation of how all calculations are made. Even things like 'Active time': what exactly that means";
11-19: "I want 100% confidence in our numbers, and our math." The real-data check
(`.claude/skills/numbers-check/SKILL.md`) recomputes most of them from the log and the database by these definitions, never
by the engine's code: its coverage is in "What the real-data check proves" at the end.

- **Stored** is a parse-time fact in the database (`visits`, `kills`, `loot`, `levels`; docs/code-map.md section 1).
  Anything else is worked out when shown, from stored facts, by the one function named. Nothing adjusted is ever stored or
  shared (docs/pace.md: facts stay facts, the view compares like for like).
- **Hand-checked** means the expected value was worked out from these definitions and the raw log lines, with the
  arithmetic written beside it, not copied from what the engine printed. The fixtures are `TestLog.BefallenUnrest` (five
  crawls at levels 1-23, four held by value) and `TestLog.CapNagafen` (five instances at level 50, two held by value).
- Test names below are in `tests/EqlPathfinder.Engine.Tests`. Short forms: GN = `GoldenNumbersTests`, GT = `GoldenTests`,
  CG = `CapGoldenTests`.

## 1. The golden History

Each fixture's whole History is held, not only its numbers:
- **Befallen**: `GN.TheWholeHistoryIsTheFourHeldCrawlsAndAFifthNotYetHeld` lists every instance visit (five). Four hold
  every number by hand: GT `CrawlsMatchHandCheckedNumbers` (kills, kill and reward levels, T5, chest T5) and GN
  `ACrawlsClockAndRatesAreTheHandWorkedOnes` (active, whole and after minutes, kills a minute, levels/h, T5-eq, T5-eq/h).
  **The fifth, The Estate of Unrest 4 at 13:50, holds no hand value yet.** Their verdicts are "none" (no earlier visit to
  be a usual); verdicts on a fixture are held with built visits instead (section 5).
- **Cap**: `CG.TheWholeHistoryIsTheseInstancesAndNoOthers` lists the five instance visits. `CG` holds two by hand: the
  raid (Plane of Hate - Solo 4) and the finished crawl (Nagafen's Lair 4). **The other three hold no hand value.**
- **Not held anywhere by value:** DPS (needs every damage line summed), Kill s from the log (needs each mob's first hit),
  and at-cap idle (no fixture gap passes 3 minutes). Those rows say so below.

## 2. One visit (History → Visits and Crawls, the overlay's numbers, Home's tiles)

All of a visit's numbers come from `VisitStats.From` (`Tracking/VisitStats.cs`) over the stored `Visit`.

| Number | What it is, and how | Stored / shown | Function | Held by |
|---|---|---|---|---|
| **Time inside** (crawl seconds) | Zone-in to "completed" (or to leaving, or to now while inside), minus trips outside (`Pauses`). A kill after "completed" extends it to that kill. | Stored (`ActiveSeconds`) | `Visit.CrawlSeconds` | GN `ACrawlsClock...` (four crawls), CG (two visits) |
| **Idle** | For every gap over 180 s (zone-in to the first kill, between kills, the last kill to the end), the part past 180 s, with trips outside left out of the gap. A visit with no XP kills has no idle. Capped at the time inside. | Shown | `ActiveTime.IdleSeconds`, `Visit.IdleSeconds` | GN `IdleIsEveryGapPastThreeMinutes...` on picked gaps (the fixtures have none: idle 0 in all nine visits) |
| **Active time** | Time inside minus idle: the minutes the player was playing. Every rate divides by it. | Shown | `ActiveTime.Minutes` | GN `ActiveMinutesAreTimeInsideLessIdle`, `ACrawlsClock...`, CG |
| **Whole clock** | Zone-in to zone-out (to now while inside) = active + idle + after + outside, so the parts add up to it. | Shown | `VisitStats.Clock` (private, in `From`) | VisitClockTests `ThePartsOfAVisitsClockAddUpToTheWhole` (the identity), GN and CG (values) |
| **After** | Inside after "completed" with no kill to count (looting, walking out), floored at 0. A raid never completes: 0. | Shown | `VisitStats.Clock` | GN `ACrawlsClock...`, CG (the raid's 0) |
| **Outside** | Out of the instance mid-visit (a death, an errand). | Stored (`Pauses`) | `VisitStats.Clock` | VisitClockTests (the identity only). **No fixture has a trip outside: no hand value.** |
| **Kills** | Every slain line that is yours: your own, your pet's, your group's inside an instance (open world: another player's only with party XP), and a charmed pet's while the charm holds. Two in one second count twice. A player's name is one word of letters; `C\`rate` is not one, "Name\`s pet" is. **XP kills** are kills with a sized XP line (a no-XP mob counts 0 in XP per kill; a kill whose XP line had no percent is left out of the divisor, and at the cap no kill is an XP kill). | Stored | `Tracker.Apply` (`Tracking/Tracker.cs`); `Visit.XpKills` | GT (four Befallen crawls), CG (raid 4, crawl 65 incl. the pet's), KillsAtCapTests and CharmTests for the rules on built lines. **Charm on a real log is only partly proved** (docs/debt.md, `fullcheck.mjs` row) |
| **Kill levels / reward levels** | The XP lines' percents with a slain line in their second, over 100; the crawl's completion XP is the reward. XP lines read at the cap are the bar filling (`CapPercent`), never levels. A kill's level is the level its XP line was read at. | Stored | `Tracker.Apply`; `Visit.LevelsGained` | GT (four crawls), CG (CapPercent) |
| **Kills a minute** | Kills over active minutes, when there is a rate. | Shown | `Rates.PerMinute` | GN `ACrawlsClock...`, CG |
| **Has a rate** | The one rule: a finished crawl, or 8 kills. Else "—", never 0 and never a floor. No time minimum. | Shown | `Rates.Has` / `Early`, `Rates.MinKills` | RateRuleTests (`EightKillsMakeARate...`, `AFinishedCrawlHasRates...`); CG (4 kills: null) |
| **Levels an hour** | (kill levels + reward levels) x 60 / active minutes, when there is a rate. | Shown | `Rates.PerHour` via `VisitStats.From` | GN `ACrawlsClock...` |
| **T5-eq** | Each mote at its tier's value (T5 = 1, each tier up doubles, each down halves: `MoteTiers.T5Value`), each fragment at that x the fragment value. | Stored (counts by tier); valued when shown | `LootTally.T5`, `Valuation.T5` | GT, GN (at fragment value 1.6), CG (at 3.56) |
| **The fragment value** | Shown at the recommended value (3.56, for gear fed to +9 with Void-Touched Potentials doing +9 to +10) while `FragmentValueRecommended` is on, else the player's own; never under 2.0. | Setting | `Potential.FragmentValue`, `RecommendedFragmentValue` | PotentialTests (`AFragmentIsWorthItsBestRatioOverAMote`). docs/motes-and-fragments.md has the derivation |
| **T5-eq an hour** | T5-eq x 60 / active minutes, when there is a rate. | Shown | `Rates.PerHour` | GN, CG |
| **Chest** and **above T5** | The Reward Chest's part of T5-eq; Greater and up. `spent` loot (condensing at Constructed Potential) never belongs to a visit. | Stored (chest rows) | `LootTally.ChestT5`, `HighTierT5` | Chest: GT. **Above T5: no hand value** (CommunityTests and TrackerTests use it, neither holds a log's value) |
| **Kill s** | The median seconds from a kill's fight start to its death over every kill whose fight start is known (not XP kills only). | Stored (`KillSeconds`) | `Visit.MedianKillSeconds` | GN `KillSecondsIsTheMiddleOne...` on picked numbers. **Not held on a fixture's log** |
| **XP per kill** | 100 x kill levels over XP kills, % of a level; completion XP never in it; null with no XP kill. Every kill at the cap is left out of both. | Shown | `Rates.XpPerKill` | XpAtCapTests (`XpPerKillIsOneFunction`, `CapKillsAreNotInXpPerKillsDivisor`); CG (null at 50). **No fixture value below the cap: it is a division of two held numbers** |
| **DPS** | Damage over active seconds (pets' spell damage included), when there is a rate; until damage-lines it misses 1-point hits and a new pet's first hits. | Stored (damage total) | `Rates.PerSecond` | RateRuleTests `DpsDividesByActiveTime...` (the division). **No hand-summed value from a log** |
| **Deaths** | Death lines in the visit. | Stored | `Tracker.Apply` | **No hand-checked value** |

## 3. Verdicts and usuals (History's Verdict, Kill speed, Place words; the overlay's words and ticks)

Judged by `Verdicts.Judge` (`Analysis/Verdicts.cs`), once, as of the visit's start: only earlier visits count, so a past
crawl's words never change (VerdictsTests `APastVisitsPlaceWordsDoNotChange...`). Pace.md has the reasoning.

| Number | How | Function | Held by |
|---|---|---|---|
| **Kind** (raid, grouped, farming, leveling) | Raid by its boss; grouped if most kills gave party XP; farming if at the cap, at a difficulty in Settings → How you play (D3, D4 by default) or an instance that ended without "completed"; leveling otherwise. | `VisitKinds.Of` | JudgeByPlayTests (rules on built visits) |
| **The usual, leveling** | Median kills a minute of earlier leveling visits at the same place and difficulty, solo or group the same, within 2 levels, each with a rate and played minutes. Needs 3. | `RunPace.For` | RunPaceTests, OneUsualTests (built visits with picked numbers) |
| **Kill speed** | The run's kills a minute over the usual. "On pace" within +/-15% (+/-18% on 3-4 runs behind the usual), else "N% slow / fast". A stop's or stretch's is each visit's, weighted by active minutes. | `RunPace.For`, `KillSpeeds.Over`, `KillSpeeds.Word` | RunPaceTests, KillSpeedsTests |
| **XP per kill, low / normal / high** | The run's XP per kill (8 kills at least) against what the place gives at that level and XP penalty: earlier runs re-figured to rank 0, fitted by least squares on the level they were played at (the place's own slope with 4+ runs over 3+ levels, held between -20% and +5% a level; else -8%), read at the run's level, put back at its penalty. Past +/-15% is low or high. | `RunPace.ExpectedXp`, `Fit.Slope` | RunPaceTests (`XpPerKillIsReadAtTheRunsOwnLevel...`, `AFewRunsAtOneLevelUseTheTypicalSlope`); KillSpeedsTests (the slope's fit) |
| **The usual, farming and grouped** | Median T5-eq/h of earlier visits of the same kind there (this place, solo or group, finished or not like this one, within 2 levels, any trio), each with a rate, peers at 0 counted; a finished crawl with no refund ("too low-level") left out. Needs 3. At the cap every level is 50. | `FarmPace.Peers`, `FarmPace.For` | GN `AVisitIsMoreOrLess...`, JudgeByPlayTests, TooLowCrawlTests |
| **The farm band** | 0.25 by default; once there are 5 earlier visits with a rate above 0, e^(sample standard deviation of ln T5-eq/h) - 1, held to 0.15-0.6. More or less only past the band. | `FarmPace.For`, `FarmPace.DefaultBand/MinBand/MaxBand` | GN `TheBandIsTheSpreadOfYourOwnVisits...` |
| **A stop** | Consecutive farming visits at one place and difficulty, judged as one: its T5-eq over its active hours against the usual weighted by minutes. | `FarmPace.ForStop`, `Verdicts.AssignStops` | JudgeByPlayTests (`AStopIsJudgedWhole...`), VerdictsTests |
| **Fading / Slower place** | Another place at the same difficulty (open world only with open world) beats this one at the level, as of the visit's start: faster by more than 2 standard errors of a run's 13% swing over both sides' visits, and by at least 10%. The faster place needs at least 2 visits at the level, your own at least 1. Fading when this place was the best 3 levels back on 2 or more visits. | `PlaceCheck.Of`, `AskData.PlacesAsOf` | RunPaceTests (`APlaceThatWasBestAndIsNowClearlyPassedIsFading`...), VerdictsTests. **Not checked against real play** (docs/debt.md) |
| **Overlay bar tones** | The verdict's own: slow and low are red, on pace, fast, normal and high green, grey with no verdict. | `RunPace.KillSpeedTone/XpTone`, `FarmPace.Tone` | OverlayTonesTests |
| **Players here** | With 3 or more sharing players at the place and level: their kills a minute and levels/h at your XP penalty. Context, never a verdict. | `OthersHere.At` | RunPaceTests `WithNoUsualOtherPlayersShow...` |

## 4. Places, routes and the farm rate (Ask, Home, Upgrades)

| Number | How | Function | Held by |
|---|---|---|---|
| **Minutes a level at a place** | Each usable visit (3+ active minutes, not below a zone's gate) is spread evenly over the levels it covered, at rank 0 (time divided by the XP penalty), each level weighted by the visit's age (halves every 30 days) and by the pace drift. A level with under 0.3 of a level played is blank. | `AskData.From`, `PlaceLevels` | AskWindowTests, AskNumbersTests (picked numbers). **No fixture hand value** (golden-numbers left it out) |
| **Around a level** | Levels within 1 of it, nearer and better-covered ones counting more: weight (2 - distance) x visits. Visits is the most behind any one level. | `PlaceLevels.Window` | AskWindowTests `ALevelIsReadWithTheOnesBesideIt` |
| **Pace drift** | How much faster the player levels now: the same place and level on different dates at rank 0, within each cell. Needs 5 cells and 20 visits; held to 40% faster / 25% slower per 30 days, a visit's scaling to 1.5x. | `PaceDrift.From` | PaceDriftTests |
| **Levels/h fall a level** | The least-squares slope of ln levels/h on level over the place's runs (your trio's with 3, else all; 5+ minutes, with kills), exp(slope) - 1. Needs 3 runs over 3 levels. | `Ask.Falls` | AskNumbersTests `NoFallUnderThreeRunsOrThreeLevelsApart` |
| **The route** | Minutes for each leg of levels at a place, 6 minutes added per change of place, 2 where a place rests on one visit; with motes counted, a place gets credit for the farming time its motes save. Your minutes blend with sharers': one visit of yours is half. | `Ask.Chain`, `Ask.MineShare` | AskTests, AskQueryTests (picked numbers) |
| **You against players who share** | Your minutes a level over theirs, minus 1, at each shared place; coloured past +/-15%; the cause split in kill speed and XP per kill, each cause named past 5%. | `Ask.Against`, `Versus.Why` | AskQueryTests. **No hand-checked value** |
| **Cost per level** | (farm rate - T5-eq/h at the place) over levels/h, null at 0.05 levels/h or under. | `RouteAnalysis.CostPerLevel` | GN `ACostPerLevel...`, AskNumbersTests |
| **The farm rate** | Median T5-eq/h of your crawls at the cap since the patch (11-10 19:50 UTC, on the PC's clock) at the difficulties you farm (D3, D4 by default), each with 3+ active minutes and a rate; "best" is the best zone's median among zones with 3+ crawls. Needs 3 crawls; else the rate you typed, else the app's default. | `RouteAnalysis.FarmRates`, `FarmSource`, `EffectiveFarmRate`, `FarmPatchIn` | GN `TheFarmRateIsTheBestPlacesMedian...`, FarmRateAtCapTests (the patch on Sydney and Eastern clocks, the difficulty rule), SampleGatesTests (the counts) |
| **Sample gates** | One named smallest sample per purpose: 8 kills for a rate, 3 active minutes for a usual or table, 3 crawls for a farm rate, 5 active minutes to fit a slope, 3 runs for a verdict. | `Rates`, `Samples` | SampleGatesTests, RateRuleTests |
| **A session** | Consecutive visits at one place and difficulty within 30 minutes: T5-eq and levels over crawl (active) time and over wall time. | `SessionStats.From` | RateRuleTests `ASessionHasARateOnlyWhen...` |

## 5. Relevels (History → Relevels, Home's race)

| Number | How | Function | Held by |
|---|---|---|---|
| **A relevel's hours** | Active hours of its crawls, other instances and open-world hunting from reincarnation to the cap; open-world minutes are first kill to last plus a minute for the pull. | `Journeys.Build`, `ActiveTime.OpenWorldMinutes` | GN `AnOpenWorldStretch...`. **Journeys' hours: no hand value** (golden-numbers left them out) |
| **The level cap** | Read by the day of play from `Expansions.All` (a day on the player's own clock): 50 before 2026-12-08, 55 from it. A ding past the day's cap means the next cap has come (the cap of the first later entry that holds the level). A kill at the cap adds no XP; a visit or class at it is at the cap; a relevel that ended at the cap of its day stays done. Play before the day keeps 50 after it. `ReachedCap` means the cap of that day. | Stored (re-read when the list changes: `CurrentVersion` 17) | `Expansions.CapOn`, `TrackerOptions.CapOn` | KunarkCapTests, RereadGuardTests (the list's (day, cap) pairs are in the hash) |
| **The race** | Each finished relevel's hours and curve scaled by the target XP over its own, sorted best first; the band is the middle half; splits at levels 10, 20, 30, 40, 46, 50; where an open relevel stands against the best at its level. | `JourneyRace.All`, `OfKind`, `Next`, `For` | JourneyRaceTests (built relevels with picked numbers) |
| **Pace by relevel** | Each relevel's rate in a cell over the median of every relevel's rate there, cells shared by 2 or more, a weighted geometric mean. | `RelevelPaces.Build` | RelevelPacesTests |
| **Levels/h fell N% a level** | The same least-squares fit over a relevel's crawls with a rate. | `Stretches.Assign`, `Fit.Slope` | KillSpeedsTests (the fit). **No hand value for the stretch line** |

## 6. The fight meter and the community

- **Fight meter** (damage, DPS by fight, kills, "your usual"): the definitions are in docs/fight-meter.md; the functions are
  `FightTracker`, `MeterViews`. FightMeterTests and CharmTests hold the rules on built lines (a fight ends after 8 quiet
  seconds, two minutes end a crawl's fight, a usual needs 5 fights). **No test holds a value from a real log.**
- **Community numbers** (players, crawls, coverage, Others, the pool's places): built by `CommunityBuilder` from shared facts
  with the app's own rules. A cell needs 2 players and 5 crawls, and no player may weigh over 20% (`CommunityRules`).
  CommunitySameRulesTests `OneCrawlReadsTheSameThroughTheJobAsInTheApp` holds the job to the app's numbers for one crawl;
  CommunityTests the gates. docs/community-job.md has the schedule and brakes. The day-level patch cut is the job's own
  clock (docs/debt.md).

## What the real-data check proves

`tools/analysis/numbers-check/` recomputes, from the database rows and the raw log, every visit's kills, T5-eq, times, idle,
rates, DPS, kill XP, the farming usual, band and verdict, and the leveling kill speed. On 11-19, 1,337 visits matched
except kills by a charmed pet (69 visits differed). It does **not** check place words (Fading, "<place> levels faster"),
Ask's grid and routes, relevel hours or the community's numbers.
