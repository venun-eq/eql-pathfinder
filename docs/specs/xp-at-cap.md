# XP at the cap, and one XP per kill

**Built 11-19, reread 13** ([numbers-audit](numbers-audit.md)). **Tier:** standard. Paid the two docs/debt.md rows kills-at-cap
left (XP per kill over every kill; Lv/h at 50). One part stays debt: ask.js's "Both" explanation.

**Why:**
- **XP at the cap became levels.** At 50 the game still writes `(x%)` XP lines (the bar fills beside the AA-cap line,
  docs/log-formats.md "Kill XP"), but no level follows. `Tracker` added them to `KillPercent`, so a 50→50 visit showed
  Gained 0.22 and Lv/h up to 2.54.
- **XP per kill was worked out in five places** with different divisors and gates (RunPace, the overlay's card, the
  overlay's equation line, AskPage's map bars, ask.js). The overlay's equation line also left out ×60/100.

**Answers found:** an XP line at 50 is the bar filling with no level to come (the fact stays stored: `KillRecord.Percent`
and the new `Visit.CapPercent` keep it). The view says levels gained at the cap is 0. A 49→50 visit splits at the ding:
an XP line counts as levels while the level read is below the cap, and the ding follows its kill's XP line in the log.

## What the player sees

The engine's number at the cap is 0; the page doesn't print it. At 50 the log still says "You gain experience! (1.1%)"
after every kill, so "0.00 gained" reads as "this crawl gave me nothing", which the player can see is false (design.md
"never a zero that reads as a fact"). Levels/h doesn't apply at the cap, so it's left out, the way Home and the overlay
already leave it out there.

**History → Visits**, reusing the table, `.dim` and the footer note (`crawls.js`):
- A visit that starts and ends at the cap (`50→50`, raids at the cap too): the **Gained** and **Lv/h** cells are empty,
  with no `~` or `+R?` mark. Sorting by either puts them with the visits that have no rate.
- A visit that dings the cap (`49→50`): as below the cap, the levels before the ding (e.g. `0.26`).
- The footer note gains one sentence at its end: "At 50 the XP bar still fills, but no level follows, so those visits
  have no Gained or Lv/h."
- The "levels gained" tile above the table sums only levels below the cap (unchanged words).

**The overlay, a leveling crawl just done** (Standard and Detailed, `DrawDone`), when every kill's XP is known:
- The line: "2.8 levels/h from kills = 4.1 kills/min × 60 × 1.14% of a level". No "÷ 100": the % sign already
  divides by 100, so with it the line reads as 0.03.
- When the crawl's completion XP is sized (`RewardLevels > 0`), the dim line under it gives the crawl's whole rate, the
  same number History's Lv/h and the overlay's "Last crawls" show: "3.4 levels/h with the crawl's completion XP".
- When a kill's XP isn't known, the Levels/h row as before. At the cap, no levels line at all (T5-eq, as before).

**The overlay during a crawl:** unchanged. XP per kill shows only with a number; at the cap the row isn't there.

## Done when
- [x] A 50→50 visit's `(x%)` kills gain no levels: Gained 0, Lv/h 0, the percent still kept - `XpLinesAtTheCapAreNotLevels`
- [x] A 49→50 visit counts the kills before the ding and not the ones after - `AVisitThatDingsTheCapCountsOnlyTheKillsBeforeTheDing`
- [x] Kills at the cap stay out of XP per kill's divisor - `CapKillsAreNotInXpPerKillsDivisor`
- [x] One `Rates.XpPerKill(killLevels, xpKills)`: null with no levels or no kills whose XP is known, else 100 × levels / kills - `XpPerKillIsOneFunction`
- [x] RunPace, the overlay card, the overlay equation and Ask's map bars use it (`VisitStats.XpPerKill`, `RunPace.XpPerKill`)
- [x] The overlay's equation is true: levels/h = kills/min × 60 × % a kill / 100, shown only when every kill's XP is known
- [x] History: a 50→50 visit's Gained and Lv/h are empty with no `~`, a 49→50 visit keeps its number, and the footer has the at-cap sentence - seen in main-crawls-cap.png (a new shot: History scrolled to the "At 50" group)
- [x] The overlay's done card reads "… levels/h from kills = … kills/min × 60 × …% of a level", with no "÷ 100" - seen in overlay-done-leveling.png (a new shot: the Detailed overlay after a leveling crawl ends)
- [x] With completion XP sized, the dim line reads "… levels/h with the crawl's completion XP", the same number as History's Lv/h for that crawl - seen in overlay-done-leveling.png, or a test if the shot's crawl has no sized reward
- [x] The release note says a visit at 50 shows no levels gained or Lv/h, not "0"

## Edge cases
- Nothing yet / no kill with a known XP: XP per kill is null (the card shows nothing), never 0.
- Too little: RunPace's verdict keeps its 8-kill sample gate (a judging rule); the number itself has none.
- Unknown-XP kills (no `(x%)`) stay out of the divisor; no-XP mobs count as 0 (Modeling 11, unchanged).
- Rewards (a hand-in) at the cap: not changed, they have no kill; rewards at 50 are rare and sized from the bar.
- Old shared/community rows: unchanged, the percent is stored raw. A re-read moves nothing else.
- Completion XP is never in XP per kill (it is `RewardLevels`).

## Not doing
- ask.js's "Both" explanation divides levels per hour by kills a minute from Ask's windows, which hold no XP-kill
  counts (community windows neither). Left in docs/debt.md.

## Where
- `Tracking/Tracker.cs` (`EmitKill`), `Tracking/Records.cs` (`CapPercent`, `CapKills`, `XpKills`), `Tracking/Rates.cs`
- `Tracking/VisitStats.cs`, `Analysis/RunPace.cs`, `Pages/AskPage.cs`, `App/OverlayWindow.xaml.cs`

## Reviews

- architect: two must fix, both fixed: the overlay turned no XP per kill into "0.00%", and the equation line's maths sat in the App untested (now `VisitStats.KillLevelsPerHour`, with tests). Should fix, fixed: the Lv/h test passed either way (now 8 kills, no `??`); XP per kill is 0, not blank, for kills of no-XP mobs (the lead's call, Modeling 11). The community job still counting kills at 50 as levels went to community-same-rules. Debt: a hand-in's XP at the cap still counts as levels.
- designer: three must fix, all fixed and seen in main-crawls-cap.png and overlay-done-leveling.png: visits at 50 read 0.00 and 0.0 (now empty, with a footer sentence; empty cells were the lead's pick over "at cap"), the equation divided by 100 twice, and the card lost the crawl's whole rate. Should fix, fixed: the two shots are in DevSnapshots. Debt: a 49→50 visit's Lv/h counts the minutes after the ding; the done card's "+0.90 levels" against History's "+R?". The lead read the fixes instead of a second review.
