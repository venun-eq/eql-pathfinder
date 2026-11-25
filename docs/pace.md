# Pace: am I going at a proper speed?

*How every number is worked out, and which test holds it: [calculations.md](calculations.md).*

A player should be able to tell whether they're playing at a proper speed wherever they are in a relevel: level 12 at
rank 0 or level 44 at rank 2. Levels an hour alone can't say. It falls every level, and the XP penalty, a group or a
different place move it too. (Designs: canvas page "Am I at a proper speed?", decided with the user 11-16.)

## The rule: facts stay facts, the view compares like for like

The user, 11-16: "Our numbers are FACTS. I do think we should normalize the VIEW."

- Stored and shared numbers are what happened: each kill's XP %, the mob, your level, solo or party, and the trio's rank
  as a separate fact. Nothing adjusted is ever written to the database or the pool.
- A number on screen is shown as it happened. A comparison sits beside it, never in its place.
- Where two runs needed different XP (reincarnation), the older one is re-figured for yours and labeled so: "9.3 h, or
  10.2 h at +20% XP to level".
- Words never on screen: rank 0, normalized, pay, multiplier, "×" for an XP penalty or a pace.
- The XP penalty is a bigger bar, never less XP from a kill: say "+20% XP to level" (`xpWords`), with `xpTip` beside it.
  Mobs give the same XP; XP per kill (a share of a level) reads smaller only because the bar is bigger. Paces read
  "8% faster than your usual" (`paceVs`).

## Levels an hour = kill speed × XP per kill

`Analysis/RunPace.cs` splits a leveling run in two:

- **Kill speed** is the player's part: kills a minute against your usual at the same place and difficulty, within 2
  levels, solo or group the same. Your XP penalty doesn't change it.
- **XP per kill** is the place's part: kill XP per kill (`Rates.XpPerKill`: kill levels over the kills whose XP the log sizes, one function for the overlay, History, Ask and the verdict; a crawl's completion XP lands at the end, so it isn't counted; XP lines read at the cap are the bar filling with no level, kept as `CapPercent`, not levels; a kill's level is the level its XP line was read at, its slain line's with no XP line, so the kill whose XP filled the bar before the ding counts as levels even when its slain line is read after the ding, and every kill at the cap, whatever its line says, is left out of the divisor and of kill levels),
  against what this place gives at this run's level and XP penalty. The place's earlier runs are each re-figured to rank 0,
  moved along the place's slope to this run's level, and averaged, then put back at this run's penalty. The average is
  over the runs that started within 2 levels of this one (kill speed's usual) when there are 3, and over all of them
  otherwise. The slope is the place's own when its runs span 3 or more levels, and otherwise the typical −8% a level.

Every place gives less as you level: XP per kill falls about 8% a level (median of 14 places), and levels an hour 4–7%
(Kedge D4 −6% ±0.7 over 31 runs, Kedge D0 −7% ±1.6). That's why XP per kill is compared at the run's own level. A ±2-level
window made a level-44 run look 37% low against level 41 when it was normal for 44, so each run is moved along the slope
first. The slope isn't the same at every level, so the nearby runs set the height: one line through Kedge Keep D0's 17
runs, 31 to 49, read 9% high at 34 and called a normal run low (report #15).

## Verdicts and the noise behind them

Measured 11-16 on 60 solo crawl runs since the patch, each against others at the same place and level, one run swings:

| | One standard deviation |
|---|---|
| Kill speed | ±14% |
| XP per kill | ±8% |
| Levels an hour | ±13% |

Single kills swing far more (named mobs against trash), so verdicts use whole runs.

- **Your usual is your earlier visits** (the user, 11-18): a visit is judged against the runs that started before it,
  never ones played after, so a verdict in History → Crawls is fixed once the visit is over. A place's first visits
  have no usual. That holds for kill speed, XP per kill, T5-eq an hour and the place check: a past visit's Fading or "<place> levels
  faster" compares places as they stood when it started (earlier visits only, weighted from its start), so it never changes once the visit is over. Ask's grid and NEXT compare them as they stand today.
- **A verdict needs 3 runs** near the level. Before that: **No usual yet**. Other players' numbers will show beside it
  as context once 3 players share the place. They're never a verdict: their trios and gear differ.
- **On pace**: kill speed within ±15% of your usual, or ±18% when the usual rests on 3–4 runs. **N% slow** and
  **N% fast** are beyond that.
- **XP per kill** is low or high past ±15% of what the place gives at that level.
- **Fading**: another place beats this one at your level as of the visit's start, and this place was the best 3 levels back then. **Slower
  place**: the same test for a place that never was. "Beats" means faster in Ask's level-by-level table by more than the
  noise of both: 2 standard errors of a run's ±13% swing over each side's runs, and at least 10%. The faster place needs at least 2 visits at the level, your own place at least 1, and Fading needs 2 visits at your place 3 levels back.
  (`PlaceCheck` in `Analysis/RunPace.cs`.) Only places at the **same difficulty** compare (open world only with open
  world), and the words are "<place> levels faster" and "Fading · <place> levels faster": D4 never loses to D0.
- **Judged by what was played** (docs/specs/judge-by-play.md, `Analysis/VisitKinds.cs`): a visit is a raid (its boss), grouped
  (most kills gave party experience), farming (a difficulty in Settings → How you play, D3 and D4 to start; an instance that
  ended without "completed"; any visit at the cap) or leveling (the rest). Everything but leveling is judged by T5-eq an
  hour against your usual of the same kind at that place and difficulty, within 2 levels of the visit's (`FarmPace`; its
  `Levels` field names them, null at the cap), its band measured from your own visits, with 3 earlier visits needed. Only
  earlier visits with a rate count (an instance not finished, or under 8 kills, has none), so at the cap, where no kills are
  counted yet, an unfinished instance has "no usual yet". A crawl you finished with no refund ("opponents were too low-level", a gear-only chest) is left out of the usual, though it is still shown and judged (docs/specs/too-low-crawls.md). Only leveling uses the kill speed and place checks above. A relevel is "mostly D4"
  from where its hours went, and counts motes when the farmed difficulties hold over half of them.

## Where it shows

- **Overlay, in a crawl (leveling):** rows Levels/h, Kill speed and XP per kill. Each tick is your usual here at this
  level, at your XP penalty. The header word is the kill speed verdict. The note gives the reason in this order: slow
  kills, low XP per kill, Fading or Slower place, completion XP still to come, then what the tick is. Farming is
  judged in T5-eq an hour against your usual within 2 levels (below the cap; at the cap every level is 50).
- **Overlay, between crawls:** the run's levels an hour as it happened, then the verdict line, plus Fading or Slower
  place when one applies.
- **History → Crawls:** a Verdict column after Lv/h and Kill speed after K/min.
- **Home, releveling:**
  - Under the trio, the XP penalty chip ("+20% XP to level · Shadow Knight ×2"), then the kill speed verdict and the hours
    ahead of or behind your best at this trio's penalty. The sentence underneath gives the best's hours as played.
  - Two tiles: **Kill speed · you** (this relevel against your usual, with your other relevels' range) and **XP per kill ·
    the place** (Normal, Low, Fading or Slower place for the crawl now, or the latest one).
  - A **place by place** card that says, for each stop, whether its slower levels were you or the place.
  - The race already re-figures other relevels for this trio's penalty; the note now says so in "+20% XP" words.
- **History → Relevels:** a Kill speed column. Under each relevel's re-figured hours are its hours as played.
- **Ask → Every place** (rebuilt 11-17 from the canvas design):
  - **The grid:** places down the side and 12 levels across, levels/h in each cell at your XP penalty. Your level's
    column, Ask's route (gold outline), the ★ by your ranking and Fading (dashed amber; as of today, a past visit's Fading in History is as of that visit, so the two can differ) are marked. Italic means from the
    levels beside it, faint means under 3 visits. ‹ Lower and Higher › move the grid 6 levels.
  - **Under the grid:** Ask's route from here, and **Why levels/h falls at <place>**: kill speed and XP per kill by
    level, from your latest 8 runs there as they happened (your current trio's when it has 3+). The trend in levels/h a
    level comes from all of them at rank 0, and is called normal for 2–9% (most places fall 4–7%).
  - **Below:** every place at the picked level (the table), then your runs at the picked place within a level, as they
    happened, each with its XP penalty.
  - Picking a cell sets both the level and the place. The old map of visits is gone; its runs are in the runs list.
- **Overlay extras:** a "+20% XP" pill beside the place while leveling. Between crawls, "2.3 levels/h = 4.0 kills/min ×
  0.97% a kill" with completion XP counted so it adds up, the minutes beside the levels gained, and "NEXT · Lv/h at your
  +20% XP". When the crawl's own check (as of its start) found a faster place that was not clearly faster, a line says so ("…so no Fading"). With no usual yet, the bars are
  dashed and empty, and Slower place shows beside the no-usual note.
- **History → Crawls in time order:** a line over each relevel ("Relevel 6 · SHD/ROG/BER · +20% XP · levels/h fell from
  28.5 to 2.3, about 6% a level · kill speed 105% … on pace"). Kill speed is a small bar against a tick for your usual.
- **Help:** the card "Am I going at a proper speed?" (the equation, what changes XP per kill and how each is kept fair,
  the words).
- **Home's race:** "The race, all at your +20% XP", a dashed gap line at your level to your best, and its words in the
  legend ("2.1 h ahead of your best at 46").
- **No usual yet, with other players:** with 3 or more players sharing that place around that level (solo against
  solo, group against group, instances only), the overlay adds "Players here: 3.9 kills a minute, 2.6 levels/h (3
  players)" at your XP penalty. It's context only and never a verdict (`OthersHere` in `Analysis/RunPace.cs`).
- A relevel's XP rank counts the trio's classes only, not the race (`Journeys.Build`, 11-17).
- **The XP penalty is by level** (11-24, the user; `Incarnation.RankAt`, `Roster.TrioRankAt`): a reincarnation makes a bar
  bigger only if the class had reached that level before making it. Each reincarnation the log sees keeps the highest level
  its class had been seen at (the cap of that day when never seen, and never below the first expansion's cap), beside
  `Seen`. So three made at 50 before Kunark are +30% to level 50 (bars 1 to 49) and nothing for 51 to 55; one more made after
  reaching 55 is +40% to 50 and +10% for 51 to 55. A trio counts its highest class at each level (classes only, above). `Rank`
  stays every reincarnation: the count to level 50. A count typed in Settings, and a gap below a stated rank, count to the
  first cap only (a count typed after 12-08 can't say how many came after 55). Players who share: their play past 50 is taken
  as played, their `Rank` as sent (decision 15, queue row penalty-by-level-shared). Ask, the relevel paces and the overlay take
  the multiplier of each level (`LevelingSegment.XpAt`, `Journey.XpAt`, `Xp`), never one for the whole visit.
- **Hours to go stop at the highest level a finished relevel reached** (`Journeys.HoursPerLevel`, `Journey.EtaTo`): a level
  nobody played has no hours, so at or above it a relevel has none ("≈ 6 h to 50" at 32; nothing at 50).
- **Ask keeps a bar for every level the list reaches** (`AskData.BarCount` = highest cap - 1), not 49. A visit below the cap of
  its day is leveling, not cap play: it never feeds band "50" (the cap band, whatever the cap is).
- **Ask → Level from A to B, You against players who share** (11-17, the user: "being able to see where I'm lagging
  behind the community and what can improve"): each place you and they have both played at the asked levels, your
  minutes a level against theirs at your XP penalty, most time lost first (`Ask.Against`). It says "17% more time",
  never "faster" (26% less time is 35% faster). Past ±15% (as on pace) it's coloured, and the gap splits into kill
  speed and XP per kill, exactly, since levels an hour = kill speed × XP per kill. It's a comparison, not a pace
  verdict: their trios and gear differ. When you share, your own play is taken out of theirs (`CommunityView.Without`,
  docs/ask-backtest.md). How the route blends the two:
  docs/ask-backtest.md.

## The farm rate at the cap

The farm rate (Ask's "Count motes too", Every place's ranking, Upgrades' hours) is the median T5-eq an hour of your crawls
at the cap, at the difficulties you farm for motes (Settings → How you play, D3 and D4 by default): a quick D0 run at 50 is
not a farm. It counts crawls from the patch's instant, 11-10 19:50 UTC (the servers came back at 2:50 PM EST), compared
on the PC's own clock (`RouteAnalysis.FarmPatch`); pages say "the 11-10 patch" and never its hour. Open-world hunts at 50
aren't crawls and aren't counted. `FarmRateInfo.NotFarmed` counts the crawls at the cap since the patch at other
difficulties, so a page can say "none of your 8 crawls were at D3 or D4" and never "no farming". Nothing stored changes,
so no re-read. The community build's day-level patch cut (`CommunityBuilder`) is unchanged.

## The farm band

How far from your usual a farming or grouped visit's T5-eq an hour may be and still read "usual" (the user, 11-19):
- **0.25 by default**, until there is enough of your own play to measure one.
- **From the log spread once there are 5 runs**: the visits behind the usual that have a rate above 0, e^(sample standard
  deviation of ln T5-eq/h) − 1. A visit with no motes counts in the usual but has no log, so it isn't in the spread.
- **Held to 0.15 to 0.6**, whatever the spread says.

More or less is only past the band (`FarmPace.For`: `DefaultBand`, `MinBand`, `MaxBand`; GoldenNumbersTests
`TheBandIsTheSpreadOfYourOwnVisitsInLogsAndIsClamped`). The real-data check's `usual.mjs` holds these numbers from this
section, not from the code. Every other number's definition is in [calculations.md](calculations.md).
