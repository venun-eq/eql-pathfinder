# One "usual" for each kind of play, on every screen

**Built 11-18** ([calc-audit](calc-audit.md)). The engineer wrote the rest of the spec and the failing
tests. The designer goes first: the overlay's ticks and Home's ranking can move. **Tier:** standard, 300k + designer.
**Waits for:** usual-from-earlier, engine-owns-numbers. **Unblocks:** calc-docs.

**Why:** "your usual" means something different on each screen. The overlay's Levels/h tick is
`run?.ExpectedLevelsPerHour ?? p?.UsualLevelsPerHour` (`OverlayWindow.xaml.cs`), so one tile switches definition
depending on which exists. Its T5-eq/h tick falls back the same way, with a band of 0.1 when there's no farm verdict.
History judges the same visit by `RunPace`/`FarmPace`.
**Players:** everyone who reads a tick, a verdict or a ranking.

## Findings (11-18): the baselines

| Baseline | Where | What it is |
|---|---|---|
| Leveling, a run | `RunPace.Judge` | Usual K/min × expected XP per kill. Within 2 levels, solo/group apart, at least 6 min and 15 kills |
| Farming, a visit | `FarmPace.For` | Median T5-eq/h at the place. Same finished/unfinished, solo/group, any level and trio. Band from your own swing, 0.15–0.6 |
| In progress | `RouteAnalysis.Pace` | Median within ±2, then ±4 levels. `Usable` (300 s inside, 5 kills), solo and group mixed. Words at 0.9, 1.1 and 0.75 |
| NEXT | `TrackerService.BuildNext` | Median of last visits within ±2 levels, 2 or more |
| Ask | `PlaceLevels.Window` | Minutes a level weighted across levels, recent visits weigh more, scaled by `PaceDrift` |
| Relevels | `Journeys`, `RelevelPaces` | Median hours a level; at least 5 runs, times the XP penalty |

## Proposal

- A visit in progress is judged by the same `RunPace`/`FarmPace` as History, at the minutes it has so far. Retire
  `RouteAnalysis.Pace`'s usual and verdict words, and `BuildNext`'s median, or say in the spec why one stays.
- One band per kind of play, sent with the verdict, so no screen keeps its own ±15%.
- Ask's tables and the relevel model stay what they are (a forecast, not a verdict). Each says so where it's shown.
- Decide with the user: should the farming usual stay any level and trio? It's fair at the cap. For a relevel that
  farms D4, levels and trios differ (calc-audit).

## Where

- `src/EqlPathfinder.Engine/Analysis/RunPace.cs`, `RouteAnalysis.cs` (`Pace`), `Live/TrackerService.cs` (`BuildSnapshot`, `BuildNext`, `WithRun`)
- `src/EqlPathfinder.App/OverlayWindow.xaml.cs` (the `Metric` rows), `web/js/home.js`

## Moved here from one-rate (11-18, the user)

- **Which past visits make a usual.** one-rate gives "too little to judge" (`RunPace.Early`) the rule "a finished crawl, or 8
  kills", with no time minimum. It left alone which history visits count toward a usual: `RunPace.MinMinutes` (6) and
  `MinKills` (15) on history, `FarmPace`'s `PlayedMinutes >= 6`. Should a usual use the same rule as a rate?
- **Ask's place ranking.** `Ask.Levels` (`PlaceLevels`) and `DifficultyCompare` sum every leveling visit's minutes, levels,
  T5 and kills per level window, so a place whose visits hold no rate (unfinished, under 8 kills) is still ranked and
  barred. The spec's "a place whose crawls hold no rate isn't ranked" lands here, with which visits make a usual (one-rate
  left it; the community's half is under `Community/`, share-without-derived's).

## The user's decisions (11-18)

1. The farming usual (`FarmPace`) uses earlier visits **within 2 levels, any trio**, the leveling usual's window. At the
   cap nothing changes; a relevel farming D4 at 40 is judged against D4 visits at 38–42.
2. **An earlier visit counts toward a usual only if it has a rate** (one-rate's "a finished crawl, or 8 kills",
   `Rates.Early`), replacing `RunPace.MinMinutes`/`MinKills` and `FarmPace`'s `PlayedMinutes >= 6` for history.
3. **Ask ranks a place only from visits that have a rate:** `Ask.Levels` (`PlaceLevels`) and `DifficultyCompare`, same
   rule. The community's half stays with share-without-derived.
4. A visit in progress (overlay, Home, NEXT) is judged by the same `RunPace`/`FarmPace` as History, with one band per
   kind of play sent with the verdict. Ask's tables and the relevel model stay forecasts, and say so where shown.

## What the player sees

Designed 11-18 from the code and the 11-18 render (`overlay-leveling-standard.png`, `overlay-farming-standard.png`);
not rendered this time. The engineer renders the states below and the designer reviews them.

**The rule this branch makes visible.** A *verdict* ("on pace", "17% slow", "34% more", "Usual", "No usual yet") and
every tick and bar colour on the same card come from one judge: `RunPace` for leveling, `FarmPace` for farming, grouped
and the cap. A *forecast* (Ask's hours and rankings, NEXT, a relevel's hours to go, "next 7m") wears "≈" or the word
"estimated", and never says "usual" or "on pace". The word "usual" means only the verdict's baseline.

### Overlay, a crawl going on, leveling (the regular)

Layout, rows and words stay. What moves is where one tick and the bar colours come from:

```
Kedge Keep D0  +20% XP to level        13:05 · on pace
Levels/h     ▬▬▬▬▬▬▬|▬▬      4.2     tick: RunPace's expected levels/h          (unchanged)
T5-eq/h      ▬▬▬▬▬▬▬|▬▬      5       tick: NOW median T5-eq/h of the SAME runs  (was RouteAnalysis.Pace: ±2 then ±4
                                                                                  levels, solo and group mixed, ±10%)
Kill speed   ▬▬▬▬▬▬▬▬▬|      4.1     tick: RunPace's usual kills/min            (unchanged)
XP per kill  ▬▬▬▬▬▬▬▬▬▬|     1.39%   tick: RunPace's expected XP per kill       (unchanged)
43 ▬▬               next 7m · ≈3.3 h to 50
tick = your usual here at 43, from 6 runs
```

- "The same runs" are the earlier visits `RunPace` judged with: this place, within 2 levels, solo with solo or group with
  group, each with a rate. The note's "from 6 runs" is then true of every tick on the card. N can move after this branch
  (decision 2 drops the 6-minute floor and takes 8 kills or a finished crawl instead of 15 kills).
- **Bar colours:** Levels/h, T5-eq/h and Kill speed use the band sent with the verdict (`RunPace.Band`: ±15% at 5+ runs,
  ±18% under), XP per kill the XP verdict's band. Green from 1 − band, amber down to 0.85 − band, red below, as `Row`
  does now. The bar and the corner word can't disagree any more (today Levels/h is fixed at ±15% and T5-eq/h at ±10%).
- **"next 7m"** before the crawl has a rate uses `RunPace`'s expected levels/h, not `RouteAnalysis.Pace`'s.

**No usual yet** (fewer than 3 earlier runs with a rate within 2 levels; the new player, or a new place): corner word
"no usual yet" (Dim, unchanged). **No row has a tick**; every bar is dashed and neutral (today the T5-eq/h row can keep
a tick from the other baseline while the corner says "no usual yet"). The note is reworded, because "2 runs near at 43"
reads badly:
- `No usual here yet: 2 earlier runs within 2 levels of 43. Three make one.` ("1 earlier run", "no earlier run")
- With players who share: `No usual here yet: 2 earlier runs within 2 levels of 43. Players here: 4.0 kills a minute,
  3.9 levels/h (6 players).` The amber "… levels faster" part is unchanged.

**Too little** (this crawl has no rate yet): unchanged. Corner "warming up", values "—", the ticks stay, note
`The rate starts at 8 kills: 3 so far.`

### Overlay, a crawl going on, farming, grouped or at the cap

- T5-eq/h tick and colour: `FarmPace`'s usual and band, unchanged. The Kills/min row's tick comes from the same visits
  (was `RouteAnalysis.Pace`) and is coloured with the same band. At the cap the row stays hidden (no kills counted yet).
- **Below the cap the usual is within 2 levels** (decision 1), so every place that names the usual's basis says which
  levels: `FarmPace.Basis` below the cap gains them; at the cap it is unchanged.

| Where | At the cap (unchanged) | Below the cap, at 40 |
|---|---|---|
| Overlay note | `T5-eq/h against your usual here: crawls you finished, solo, 12 visits.` | `T5-eq/h against your usual here: crawls you finished, solo, levels 38–42, 5 visits.` |
| Overlay note, no usual | `No usual here yet: 1 visit like this one. Three make one.` | `No usual here yet: 1 visit like this one at levels 38–42. Three make one.` |

  The no-usual note replaces "three visits of this kind make one", which never said what kind.

### Overlay, a raid going on, or a crawl whose level isn't known

- **Raid:** T5-eq/h and Kills/min with **no tick**, no corner word, no note. A raid is judged by its boss when it ends
  (design.md rule 19); today its ticks come from the baseline this branch retires.
- **Level not known** (`Judge` gives nothing): rows with values and no ticks, no corner word, no note.
- **Retired words** (they belonged to `RouteAnalysis.Pace`'s usual): "ahead", "×0.92", "first here", "Rates settle after
  a few minutes.", "No crawls here at this level yet: this one sets your usual.", "fewer kills", "smaller ones: mobs may
  be conning low", "slower than your usual here", "tick = your usual here, from N crawls".

### Overlay, between crawls (the done card)

- **Farming or grouped:** the T5-eq/h row's tick is the finished visit's `FarmPace` usual; with no usual, no tick (today
  it falls back to `BuildNext`'s median of the last visits within ±2 levels).
- The verdict line puts the basis in brackets so it reads as a sentence: `34% more T5-eq/h than your usual at 50
  (crawls you finished, solo)`; below the cap `34% more T5-eq/h than your usual (crawls you finished, solo, levels
  38–42)`. The same for "less". "Usual T5-eq/h: 41 against 38 at 50" is unchanged.
- The ranking line ranks the visit among the visits its usual is made of, and shows only when there is a usual:
  `#2 of your 8 Estate D4 visits like this one` (8 = this one and the usual's 7). Today it's "#2 of your 8 Estate D4
  crawls", from another set.
- **Leveling:** unchanged (the kills × XP line and `RunPace`'s verdict line already).
- **NEXT is a forecast** (Ask's tables), so its header says so: `NEXT · estimated Lv/h`, `NEXT · estimated T5-eq/h`,
  `NEXT · estimated Lv/h at +20% XP to level` (drops "your" to fit). It must not clip at the overlay's width: wrap if it
  has to. "… isn't clearly faster than … (within a run's normal swing), so no Fading." is unchanged.

### Home

- **Releveling, the place tile** for a farming or grouped visit (live, or the latest): the same words, with the levels
  below the cap. At the cap, both as today.
  - `45 T5-eq/h here, your usual 38 at Estate D4 (crawls you finished, solo, levels 38–42).`
  - `45 T5-eq/h here. Under 3 visits like this one at Estate D4, levels 38–42, to say what is usual.`
- **The farm chip's hover titles** (`farmChip`): "Too short to judge yet" says `No rate yet: a finished crawl, or 8
  kills` (today "Under 3 minutes played here", no longer the rule since one-rate); "No usual yet" says `Under 3 visits
  like this one here`, and below the cap `… at levels 38–42`.
- **The kill speed tile and the leveling place tile:** unchanged; they already read `RunPace`.
- **Farming at the cap** (`farmingHero`): the third comparison row is the farm rate, not a usual. `Your usual at 50
  since the patch` becomes `Your farm rate at 50, since the patch` (its note unchanged: "12 crawls", or "none yet"). The
  engineer checks that `reinc.farm.usual` is the farm rate's measured value; if it's another number, it gets the name of
  what it is, never "usual".
- **Hours to go is a forecast:** `≈ 2.1 h to go` on the relevel card and in Up next gets an (i), a new `infoTip` key
  `estimate`: `An estimate, not a verdict: the hours a level your earlier runs took at each level ahead, for this trio's
  XP to level. It moves as you play. Whether a crawl went well is judged against your usual, on the overlay and in
  History.` The engineer fixes the middle clause to what `Journeys` really does.
- Home's farm list at the cap ("Yours, since the patch") is Ask's farm answer, and doesn't change here.

### Ask

- **Ranking from visits with a rate** (decision 3): in Next relevel's route, Every place and Difficulties, a visit with no
  rate (an instance not finished, under 8 kills) isn't counted. A place whose visits at a level are all like that has no
  cell at that level and isn't in that level's table or the route; a difficulty column with only such visits goes, and a
  zone left with one difficulty leaves the comparison ("No zone in your logs has two difficulties at the same levels
  yet", unchanged).
- **Say what was left out**, in "Where this answer comes from", under the other "Left out" lines (`.ak`, same style), on
  the level, places and diff answers when there are any:
  `Left out: 4 visits with no rate (an instance you didn't finish, under 8 kills). History shows them as they happened.`
  (one: `1 visit with no rate (…)`).
- **A gap that has visits** must not say the logs have nothing (a zero that reads as a fact). When every visit at a gap
  level has no rate, the route's red line reads `No numbers at level 41: your visits there have no rate yet, …` and goes
  on with the community half as today.
- **Ask is a forecast**, said once for every answer but damage, as the first sentence of the method text in "Where this
  answer comes from": `Estimates for where to go next, from your past visits. How a visit went against your usual is on
  the overlay and in History.` The route's big `≈ 4.1 h` gets the (i) `estimate`, worded for Ask: `An estimate from your
  past visits and, where you have few, from players who share. Your verdicts (on pace, slow) come from your usual
  instead.` One key with two texts or two keys: the engineer's call.
- New player, log on today: nothing in Ask changes for them but the Left out line, which may count their first short
  visits. Not sharing: unchanged.

### History → Relevels, and History's verdicts

- `+≈N` in Every relevel is already marked; the (i) `estimate` goes once beside the column's header, not on every row.
  "Your relevels, like-for-like" is facts about what happened and already says so: unchanged.
- History's verdicts move wherever decisions 1 and 2 move a usual: D4 farming on a relevel now reads against nearby
  levels only, so some old rows go from "34% more" to another number or to "No usual yet". Worth a line in the release
  note.

### Every state

| State | What the player sees |
|---|---|
| Nothing yet | Overlay: "no usual yet", no ticks, dashed bars. Home: "No usual yet" chips. Ask: no ranking, as today |
| Too little | A crawl with no rate: "warming up", "—", "The rate starts at 8 kills: 3 so far." A usual under 3 runs: the no-usual notes above. Ask: the Left out line |
| Plenty | Every tick and colour on a card from one judge; the note's count is true of all of them |
| Waiting | Unchanged: the overlay before the first read; Home and History fade verdicts while the farmed difficulties change |
| Wrong | Nothing new |
| The window | Overlay compact shows the lead row and word only, unchanged. The NEXT header wraps rather than clips. Home's tile text wraps as today |

### Reused and new

Reused: the overlay's `Metric` rows and `Row` colours, `RunNote`, `NoRateNote`; Home's `.chip`, `.ptile`, `.psub`;
Ask's `.ak` Left out lines; core.js `infoTip`. New: the `estimate` (i) text, `FarmPace.Basis` with levels below the cap,
and the usual's T5-eq/h (and, farming, kills/min) sent with the verdict for the ticks.

## Done when (the engineer, 11-18)

- [x] An earlier visit counts toward a usual only with a rate (`Rates.Has`: finished, or 8 kills), for RunPace and FarmPace; the 6-minute and 15-kill floors are gone - `AnEarlierVisitCountsToward...OnlyWithARate`
- [x] The farming usual is made of visits within 2 levels, any trio; at the cap nothing moves - `FarmUsualIsWithinTwoLevelsAnyTrio`, `FarmUsualAtTheCapIsUnchanged`
- [x] `FarmPace.Basis` names the levels below the cap, not at it - `FarmBasisNamesTheLevelsBelowTheCap`
- [x] The verdict carries the usual's T5-eq/h (RunPace) and kills/min (FarmPace), and the visit's rank among its usual's visits - `TheVerdictCarriesEveryTickTheOverlayDraws`
- [x] A visit in progress has one judge: the snapshot's pace is History's, and `RouteAnalysis.Pace` is gone - `AVisitInProgressIsJudgedByTheSameJudgeAsHistory`
- [x] Ask's places and Difficulties are made only from visits with a rate, and count what was left out - `AskLeavesOutVisitsWithNoRate`, `ADifficultyWithOnlyUnratedVisitsGoes`
- [x] The answer says how many visits it left out - `TheAnswerCountsTheVisitsItLeftOut`
- [x] The overlay, Home and Ask say it (words in "What the player sees") - seen in screenshots

## Edge cases (the cases that have bitten)
- Nothing yet / new player: fewer than 3 earlier visits with a rate is "none": no ticks anywhere, "No usual here yet" note.
- Too little: this visit has no rate: "early", values "-", ticks stay (unchanged).
- Raid, or a visit whose level isn't known: no judge, no ticks, no words.
- At the cap every level is the cap: the within-2-levels window changes nothing.
- A place whose visits at a level all lack a rate has no cell there; Ask says how many it left out, never "the logs have nothing".
- Community half untouched: `AskData.From` takes the rule as a flag the community job and `CommunityData` don't set.
- Old versions: nothing stored or sent changes. History's verdicts move (mainly D4 farming on a relevel): the release note says so.

## Not doing
- The farming usual for an unfinished instance at the cap is now always "no usual yet": no kills are counted there until kills-at-cap, so those visits have no rate and no verdict already.
- Ask's farm answer at the cap (Home's farm list, `Ask.Bands`) still counts visits with no rate: the rule would drop every unfinished instance at the cap until kills-at-cap ships (docs/debt.md).
- `RouteAnalysis.Pace`'s "reasons" (idle minutes) go with it. NEXT's own median (`LastUsual`) retires; NEXT stays a forecast from Ask's tables.

## Reviews

- architect: (11-18) 1 must fix, 3 should fix. Must: home.js `basisLevels` regex has no `\d`, so Home never names the levels; the levels should come from the engine (`FarmPace`), not be parsed from words. Should: docs/pace.md still says farming keeps the older comparison; Ask's gap words put the overall unrated count on a level that may have no visits; `RunNote` centres "within 2 levels" on the level now, not the run's start. Sound: the overlay seam (one verdict, one band, no ticks without a usual or on a raid), Rank, Ask's flag left off for the community, the three changed tests kept their meaning.
- designer: (11-18; the engineer's shots, plus `mock-farm1/home-farming.png`, a mock in the real window). Checked: the
  overlay's verdict words, ticks and dashed no-usual bars, "warming up", the done card and its `NEXT · estimated` header
  (fits, no clip), Ask's ≈ (i), method sentence and Left out line, Relevels' sub-line (i), the farm chip's "No rate yet" title.
  - **must fix** `home.js` `basisLevels`: the regex is `/levels d+–d+/` (no backslashes), so it never matches. Below the
    cap Home's no-usual farming tile and the "No usual yet" chip's title never name the levels (mock: `basisLevels` gives
    `""`, the title reads "Under 3 visits like this one here"). Use `/levels \d+–\d+/`; move `farmChip`'s doc comment back
    above it. Rule 1.
  - **should fix** main-home.png, top right: "≈ 2.9 h to / go (i)" splits mid-phrase. Keep "≈ N h to go" and its (i) in
    one `white-space: nowrap` span, here and in the parked list. Rule 12, the window.
  - **should fix** main-ask-level.png, the farm rate line: "your usual farm (the middle of your 6 farming crawls…)"
    (core.js `farmSource`, and the `farmRate` (i): "your best or usual farm") still calls the farm median "usual", the word
    this spec keeps for the verdict's baseline, while Home now says "median farm". Use one word in both, and not
    "median" (rule 14): "typical", as Home and Relevels already say for a median ("Typical at D4: 7.7 h"): "Your typical
    farm at 50 since the patch", "your typical farm (the middle of …)".
  - **should fix** No shot shows a farming crawl below the cap (with and without a usual), a raid, or a crawl whose level
    isn't known: the snapshot's farming preview is at 50, and the overlay can't be mocked. Add those states to
    DevSnapshots. From the code, the raid and level-unknown cards match the spec.
  - **idea** overlay-leveling-standard.png: the corner's "34% slow" is kill speed but sits on the Levels/h row, whose bar
    is amber. Not this change.

- **Engineer, 11-18: review fixes.** `FarmPace.Levels` (engine) replaces the parsed basis in Home and the overlay; pace.md, Ask gap words, `RunNote` level, the (i) no-wrap, "typical" for the farm median (design rule 20), the not-doing and debt lines.
- **Lead, 11-18:** read the must fix (`FarmPace.Levels` sent from the engine, drawn by Home and the overlay; the parsing is gone); no second review, since it moves where the levels come from, not how the judge works. Renamed Settings' farm-rate choice "Your usual" to "Your typical", the same median (design rule 20).
- engineer: (11-18) Flaky `AVisitInProgressIsJudgedByTheSameJudgeAsHistory`: the test log ended 1.5 h before now, so the started loop's first `Tick` ended the in-progress visit by silence (`SilentLogMinutes`) whenever it beat the query. Fixed in the test: no `Start()`, so the query only reads the imported state.
