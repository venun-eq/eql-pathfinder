# Ask's "Both" reason compares XP per kill fairly

**Draft, 11-19, not started** ([numbers-audit](numbers-audit.md), from [page-math-to-engine](page-math-to-engine.md)'s
"Not doing"). **Tier:** risky, 600k (`Engine/Community/` is on the risky list), `job: true`. **Waits for:** nothing.
Pays docs/debt.md's `Ask.Versus.PerKillGap` line.

**Why:** in Ask, "You against players who share" says why you're slower at a place. When it's both reasons, the
second half ("each kill fills N% less of a level") is off today: it counts a crawl's finishing bonus as if kills
earned it, and divides by kills whose XP the log never showed. After, it compares only kills whose XP the log shows,
yours and theirs, so "N% less" is what a kill is really worth.
**Players:** anyone who reads Ask with players who share. Few see a wrong split today: below the cap, only a visit
that mixes both kinds of XP line reaches it.

```text
Ask → a leveling answer → You against players who share (i)            made-up numbers
┌─────────────────────────────────────────────────────────────────────┐
│ You take 18% more time a level than players who share at the        │
│ 3 places you've both played at these levels. Slower at Guk D2.      │
│                                                                     │
│ Guk D2 · 25% more time                                              │
│   Both: you kill 9% slower (3.1 kills a minute against their 3.4),  │
│   and each kill fills ⟦12%⟧ less of a level.                        │
└─────────────────────────────────────────────────────────────────────┘
Today  ⟦12%⟧  the crawl's finishing bonus counted as kill XP, over every kill
After  ⟦7%⟧   over the kills whose XP the log shows. The two halves (9% and 7%) no
              longer add up to the 25%: the designer says how "Both" reads then
```

## The cause

docs/engineering.md, Modeling 11: XP per kill divides by the kills whose XP the log sizes. Ask's "Both"
explanation doesn't: `Ask.Versus.PerKillGap` (Ask.cs:487) is 1 − (theirs × their kills a minute) / (mine × my kills a
minute), levels a kill with completion XP in, over every kill.

## What the windows hold (as of 05405d9)

`LevelWindow` (:150) and `LevelExtras` (:142) hold minutes, T5, hours, high tier, fragments, deaths, kills a minute and
DPS: no kill levels and no XP-kill counts. `CommunityPlace` (CommunityBuilder.cs:32) holds none either.

## Proposal

- `Ask.Levels` (:255-297) adds up kill levels (at rank 0) and XP kills per level from each segment's
  `KillLevels`/`XpKills` (:40, :73); `LevelExtras` and the window carry `Rates.XpPerKill`; `Versus` carries both sides.
- The community file carries the same per place, built from `KnownXpKills` (CommunityBuilder.cs:209) and read in
  CommunityData.cs (:31, :142). An older file has no such field: Ask then words "Both" as today.
- Numbers move on purpose: `perKillPercent` and the "why" can change, and the gap no longer splits exactly into its two
  parts. The designer says how "Both" reads when the parts don't add up.
- What players upload doesn't change: shares already carry each kill's XP.

## Where

- `src/EqlPathfinder.Engine/Analysis/Ask.cs` (`Levels`, `LevelExtras`, `LevelWindow`, `Versus`)
- `src/EqlPathfinder.Engine/Community/CommunityBuilder.cs`, `CommunityData.cs`
- `src/EqlPathfinder.App/web/js/ask.js` (:101)
- Tests: `AskTests` (`TheGapAgainstThePlayers...`, `BothCausesAreNamed...`), `PageMathTests`
  (`AsksVersusPanelDrawsTheEnginesGapAndItsSplit`), the community builder's tests
