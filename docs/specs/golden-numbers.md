# Every number on a visit has a hand-checked value

**Built 11-19** ([numbers-audit](numbers-audit.md)). **Tier:** trivial (tests only), built by an engineer. **Unblocks:** cap-fixture.
Every value is worked out from the docs' definitions and the raw log lines, never from what the engine printed. All agreed with the engine: no bug found.

**Why:** `GoldenTests.CrawlsMatchHandCheckedNumbers` holds only what's read from the log: kill and reward levels, kills,
T5 and chest T5 on four Befallen crawls. None of these is held to a value worked out by hand:
- active and idle minutes, the visit's clock and its parts;
- levels/h, T5-eq/h, kills a minute, DPS, Kill s (`MedianKillSeconds`);
- kill speed and the farming verdict.

`RateRuleTests` checks a rate against the same formula written again, and `PageDataTests` checks the engine against
itself. A rule could be wrong in both places and every test would still pass.

## Proposal

- For the four fixture crawls, work each number out by hand from their log lines, and write the arithmetic in a
  comment beside each value. Then hold `VisitStats.From(v, fixedNow, options)` to those values.
- One value test each for the functions with none: `ActiveTime.OpenWorldMinutes`, `RouteAnalysis.CostPerLevel`, the farm
  rate (`RouteAnalysis.FarmRates`/`EffectiveFarmRate`), `FarmPace`'s measured band (the log-SD clamp), Ask's weighted
  band rates (`Ask.cs`, around line 333), and `Journeys`' hours.
- Where a hand value disagrees with the engine, the test isn't bent to fit. It goes in the spec as a bug for its own
  branch.

## Where

- `tests/EqlPathfinder.Engine.Tests/GoldenTests.cs`, `RateRuleTests.cs`, and new value tests beside the functions' tests

## Done when

- [x] Each of the four Befallen crawls holds its active, whole and after minutes, kills a minute, levels/h, T5-eq and T5-eq/h to hand values - `GoldenNumbersTests.ACrawlsClockAndRatesAreTheHandWorkedOnes`
- [x] The idle rule (gaps past 3 minutes, a pause, a late first pull, no kills) and active minutes - `IdleIsEveryGapPast...`, `ActiveMinutesAreTimeInsideLessIdle`
- [x] Open-world minutes, Kill s (the median), cost per level - `AnOpenWorldStretch...`, `KillSecondsIs...`, `ACostPerLevel...`
- [x] The farm rate (best, usual, typed, default; three crawls needed) and the farming verdict's band (log SD, clamps, default under 5) - `TheFarmRate...`, `TheBandIs...`, `AVisitIsMoreOrLess...`

## Not doing

- XP per kill at level 50 or kills at 50 that aren't XP kills: numbers-check-cap is in flight. The fixture stops at level 23.
- Ask's weighted band rates (`Ask.cs` around 333) and `Journeys`' hours: left for a later branch, with cap-fixture.
- DPS and the kill-speed verdict by hand: DPS needs every damage line summed; the fixture's crawls have fewer than 3 earlier visits, so their verdicts are "none".
- Kill s from the log: it needs each mob's first hit; the median is held on picked numbers instead.

## Edge cases

- The fixture's idle is 0 in all four crawls, so idle is held on picked gaps. At the app's own fragment value (3.56) the 13:34 crawl is 17.68 T5-eq; the other three are held at 1.6.

## Reviews
