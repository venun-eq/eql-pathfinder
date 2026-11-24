# The overlay's bars say what the verdict says

**Built, 11-19** ([numbers-audit](numbers-audit.md), from [page-math-to-engine](page-math-to-engine.md)'s
"Not doing"). **Tier:** standard with the designer, about 160k (the user, 11-19: the bars follow the verdict).
**Built:** `Tone` (None, Good, Warn) and `RunPace.KillSpeedTone`, `XpTone`, `KillsPerMinute`, `FarmPace.Tone` in `RunPace.cs`; `OverlayWindow` draws them and keeps no band, 0.1 or 0.85; `Metric`/`Row` take a tone; the shots `leveling-slow` and `done-farming` in `DevSnapshots.cs`; Help's paragraph; tests in `OverlayTonesTests`. The real database on this PC has no leveling visit with a usual, so `overlay-leveling-standard` and `-farming-standard` show grey "no usual yet" bars: the colours are seen in `leveling-slow` and `done-farming`, and proved at the band edges in the tests.

**Why:** the overlay's bars are coloured in the App, not the engine, with no test, and they read different numbers
from the verdict beside them. A bar can be green while the verdict says slow.

## What it does today (as of 05405d9)

- `OverlayWindow.xaml.cs` :173-181 picks each row's band (`fm.Band ?? run.Band ?? 0.1`; `RunPace.XpBand` for XP per
  kill); `Row` (:410-424) colours it: good at ≥ 1 − band, amber at ≥ 0.85 − band, warn below. Its inputs are
  `LiveSnapshot.Current` (`VisitStats`) and `LiveSnapshot.Pace` (`RunPace`/`FarmPace`).
- Where it disagrees with the engine:
  - the kills bar uses the visit's kills a minute; the verdict uses the segment's kills over played minutes;
  - the levels-an-hour bar includes completion XP, against `ExpectedLevelsPerHour`, which is kills only;
  - the XP bar has no "high";
  - the amber line at 0.85 − band means nothing in the engine;
  - the 0.1 fallback is never reached: a row gets no ticks without a verdict.

## Proposal

- The engine gives each row its tone, from the same numbers and band its verdict uses, and the overlay draws it.
  Tests on `RunPace`/`FarmPace` values: each row good, amber and warn at the edges, and a bar never disagreeing with
  its verdict.
- **The user, 11-19: the bars follow the verdict.** The four mismatches above go, and the designer says what the amber
  means or drops it. Standard with the designer, about 160k.

## Where

- `src/EqlPathfinder.App/OverlayWindow.xaml.cs` (:173-181, `Row` :410); `src/EqlPathfinder.Engine/Analysis/RunPace.cs`,
  `FarmPace`
- `tests/EqlPathfinder.Engine.Tests`

## What the player sees

**The rule (the user, 11-19: "the bars follow the verdict"):** a bar is green or red only when a verdict judges that
bar's own number, and its colour is that verdict's. The value beside the bar is the number the verdict judged, the
tick is the usual it judged it against, and the band is the verdict's band. So a coloured bar is red exactly when its
fill falls short of its tick by more than the band, and green otherwise: it can't disagree with its tick or its verdict. A row no verdict judges is grey (`Neutral`), with its tick. The engine gives each row its tone (good, warn or
none); the overlay only draws it. No band, no 0.1 and no 0.85 stay in the App.

**Amber leaves the bars.** "0.85 − band" is in no verdict, so it can't mean anything. Amber keeps one meaning on the
overlay and in Help: the place (Fading, Slower place, the dashed amber in Ask). Good and fast/more are both green, as
the header words already are.

### In a crawl, leveling (Standard and Detailed)

| Row | Value (the verdict's own number) | Tick | Green | Red | Grey |
|---|---|---|---|---|---|
| Levels/h | the visit's levels an hour, as now | usual kill speed × what the place gives a kill (`ExpectedLevelsPerHour`), as now | never | never | always: no verdict judges levels an hour; it's the product of the two rows below, and they carry the colour |
| T5-eq/h | the visit's T5-eq an hour, gold, as now | `UsualT5PerHour`, as now | never | never | always: leveling isn't judged by motes |
| Kill speed | **the run's kills a minute as the verdict counts them** (its kills over its played minutes: `KillSpeed` × the usual), not the visit's kills a minute | your usual kills a minute | "on pace" or "N% fast" | "N% slow" (value red too) | warming up, no usual yet |
| XP per kill | **the run's kill XP per kill the verdict judged** (`RunPace.XpPerKill`) | what the place gives at your level and XP (`ExpectedXpPerKill`) | XP per kill "normal" or "high" | "low" (value red too) | no XP verdict (under 8 sized kills, or no expected XP), warming up, no usual yet |

- The header word is unchanged ("on pace", "16% fast", "24% slow", "warming up", "no usual yet") and is the Kill
  speed row's verdict: the two are always the same colour.
- The note's slow line quotes the bar's number: "Slow kills: **3.1** a minute, your usual here is 4.1." with 3.1 the
  Kill speed value on screen, never the visit's kills a minute. Every other note line is unchanged.
- The "+20% XP to level" pill, row order, labels ("Levels/h", "T5-eq/h", "Kill speed", "XP per kill"), formats and
  sizes are unchanged.

### In a crawl, farming and grouped play

| Row | Value | Tick | Green | Red | Grey |
|---|---|---|---|---|---|
| T5-eq/h | **the visit's T5-eq an hour the verdict judged** (`FarmPace.T5PerHour`), gold | your usual (`FarmPace.Usual`) | "N% more" or "usual" | "N% less" (the value stays gold; the header word is red) | warming up, no usual yet |
| Kills/min | the visit's kills a minute, as now | `UsualKillsPerMinute`, as now | never | never | always: farming is judged by T5-eq an hour |

### Compact

Only the lead row, as now. Leveling: Levels/h, grey, with the header word in its colour beside it. Farming: T5-eq/h in
the T5 verdict's colour. (The user, 11-19: Levels/h stays grey, so no bar disagrees with its own tick, though in compact only
the header word carries colour.)

### Between crawls (the done card)

- Farming's "T5-eq/h" bar (today coloured with the 0.1 default) takes the last visit's farming verdict: green for more
  or usual, red for less, grey with none. The line under it is unchanged.
- Leveling's verdict line quotes the verdict's kills a minute: "24% slow: **3.1** kills a minute, your usual 4.1", the
  same number the Kill speed bar showed in the crawl.

### The states

- **Nothing yet** (no usual: "no usual yet"): dashed, empty grey bars, no ticks, as now.
- **Too little** (under 8 kills, "warming up"): "—" in Dim, grey bars, ticks shown, the note "The rate starts at 8
  kills: 3 so far.", as now.
- **Plenty:** colours as in the tables. A long history changes nothing.
- **Waiting / Wrong:** the overlay has none of its own.
- **Raid:** values, no ticks, every bar grey, as now.
- **Outside, can resume** ("outside · can resume"): the bars keep their verdict's colour.
- **The window:** no layout change; nothing new to fit at 150%.

No mock-up: the layout doesn't change, only which colour each bar takes and two numbers in lines that already exist.
Sketch of a slow leveling crawl, Standard:

```
Kedge Keep D2   +20% XP to level         34m · 24% slow     (header word red)
Levels/h     [==========|===    ]  2.1   grey bar, tick
T5-eq/h      [=======   |       ]   41   grey bar, gold value
Kill speed   [=======   |       ]  3.1   red bar, red value
XP per kill  [==========|=      ] 0.98%  green bar
Slow kills: 3.1 a minute, your usual here is 4.1. XP per kill is normal, so it's the killing.
```

### Help

In the card "Am I going at a proper speed?", a new paragraph after the words (`.pacewords`), in a plain `<p>` as the
card's other paragraphs:

> On the overlay, a bar is green or red only when a verdict judges its number: Kill speed against your usual, XP per
> kill against what the place gives, T5-eq/h against your usual when you farm. Green is on pace, usual or better; red is
> below by more than a run's normal swing. The other rows, Levels/h among them, stay grey: their tick still shows where your usual sits.

## Done when

- [x] Engine tests: each row's tone at both edges of its band (just inside is green, just outside is red), the grey
  rows grey in every verdict, and for every row a red tone only when its value over its tick is under 1 − band.
- [x] Leveling in a crawl: Kill speed and XP per kill coloured, Levels/h and T5-eq/h grey - seen in overlay-leveling-pace.png
- [x] The Kill speed value and the header word agree in colour - seen in overlay-leveling-pace.png
- [x] A slow crawl with a low XP per kill: Kill speed red with a red value, XP per kill red, the note's kills a minute
  the same as the bar's value (a new shot: the leveling snapshot with its pace set to slow and its XP low) - seen in overlay-leveling-slow.png
- [x] Farming in a crawl: T5-eq/h in its verdict's colour, Kills/min grey - seen in overlay-farming-less.png
- [x] Compact leveling: the Levels/h bar grey, the header word coloured - seen in overlay-leveling-pace-compact.png
- [x] Warming up: every bar grey, ticks shown - seen in overlay-early.png and overlay-early-farming.png
- [x] No amber on any bar, in any shot - seen in overlay-*.png
- [x] Between crawls, farming: the T5-eq/h bar in the last visit's verdict colour (a new shot, like done-leveling) - seen in overlay-done-farming.png
- [x] Help's new paragraph under the words (scrolled to the card if it's below the fold) - seen in main-help.png

Nothing tests the App's wiring of the tones: the forced shots (leveling-slow, leveling-pace, leveling-pace-compact, farming-less, done-farming) are its proof, because the real database has no usual to colour from.

## Reviews

- architect: no must fix. Should fix, all fixed in 544552f: the done card's farming bar was coloured by the farming verdict but drawn from the visit's own T5-eq/h (now the verdict's number, as the line under it); boxes cited shots that show grey bars (now the forced shots); a stale `Band` doc and the release note's "same call as the header" wording. It checked that each tone is its verdict's own comparison (`<` at both edges) and that `KillsPerMinute` is the run's kills over played minutes.
- designer: two must fix, both fixed in 544552f: no shot showed a green bar or compact leveling (new forced shots `leveling-pace`, `leveling-pace-compact`, `farming-less`); the done card's bar sat at "3" under "30% less" (the architect's first point). Should fix, fixed: the early shots now show "—" with ticks, as real play does; Help's "Green is on pace, usual or better; red is below by more than a run's normal swing." Help's "Fastest or Counting motes" line is already in docs/debt.md. The lead didn't run a second review: the engineer viewed every cited shot after the fix.
