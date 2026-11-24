# One formula for each rate, everywhere

**Standard, in progress 11-18** ([calc-audit](calc-audit.md)). **Waits for:** nothing. **Unblocks:** engine-owns-numbers,
share-without-derived. **Tier:** standard, minus `src/EqlPathfinder.Engine/Community/` (the user, 11-18: touching it would
make this branch risky).

**Why:** the same number was worked out in several places, with different gates and denominators, and drifted at each rule
change (a 2-minute visit had a T5/h on Crawls and none on Home's session). **Players:** everyone, most of all a new
player, whose few short visits are all they have.

## The rule (the user, 11-18, over the draft's "6 active minutes")
A visit has a rate when it is **a finished crawl, or has at least 8 kills**. No time minimum ("a time minimum is a weird
tangential restriction"). Below it the rate is null and shows "—", never 0 and never a 1-minute floor. A stop, a leg, a
session and Ask's places follow the same rule over what they hold (a finished crawl in it, or 8 kills in all). It covers
levels an hour, T5-eq an hour, motes an hour, kills a minute and DPS. DPS divides by active time like the rest. Data
behind it: of 367 finished crawls 2 were under 6 active minutes; the junk rates came from unfinished in-and-outs (134
under 6 minutes, 19 with any kill). **Known gap:** kills at level 50 aren't counted until
[kills-at-cap](kills-at-cap.md) (803 of 848 cap crawl visits show 0 kills), so an unfinished visit at the cap shows "—"
until then; finished ones keep their rates. "Early" (`RunPace.Early` and its copies, the overlay's, TrackerService's) is
this same rule. The number 8 lives in `Rates.MinKills` and reaches every page as data (`rateFromKills`).

## Done when
- [x] One rule, `Rates.Has/PerHour/PerMinute`: a visit's rates are null under it, a zero over it stays 0 - `RateRuleTests`
- [x] DPS divides by active time - `RateRuleTests`
- [x] SessionStats, JourneyLeg, FarmPace, RouteAnalysis.Pace, RunPace.Early and the overlay call it - `RateRuleTests`, `RateRuleEverywhereTests`
- [x] One active-time function (`ActiveTime.Minutes`, `OpenWorldMinutes`), one `Valuation.T5`, one `RouteAnalysis.CostPerLevel` (engine; loot.js still re-sums raw pickups at view time for its filters)
- [x] Crawls, Home, the overlay show (Ask: left, see Reviews) "—" and the new words, read from the data - page tests, shots below
- [x] "Hide visits too short to judge" hides the rows with no rate except raids and cap visits; finished crawls stay - `RatePageTests`, `RateRuleEverywhereTests`

## Edge cases
- A finished crawl of 4 minutes has a rate. 8 kills in 1.5 minutes has one. 7 kills, unfinished: none.
- Ten minutes, 8 kills, no loot: T5/h is 0, a fact.
- Cap visits: kills not counted yet, so "—" until finished (the gap above).
- A session or stop with one finished crawl and short junk: a rate over all its time.
- Nothing yet: "—" with the words, never 0. Medians that make a usual skip visits with no rate.
- Which past visits make a usual (`RunPace.MinMinutes/MinKills`, `FarmPace`'s 6 minutes) is
  [one-usual](one-usual.md)'s question: left as it is, listed there.
- `CommunityBuilder` keeps its own active minutes and T5 valuation: moved to [share-without-derived](share-without-derived.md).

## Not doing
- Anything under Community/. Which past visits make a usual.

## Where
Tracking: `Rates.cs` (new), `ActiveTime.cs`, `VisitStats.cs`, `Records.cs`. Analysis: `RunPace`, `RouteAnalysis`,
`Journeys`, `FieldHunts`, `PlayFacts`, `Ask`. `Live/TrackerService.cs`, `Pages/AskPage.cs`, `OverlayWindow.xaml.cs`,
`web/js/crawls.js`, `home.js`, `ask.js`, `loot.js`. docs/engineering.md.

## What the player sees

The designer, 11-18, with the user's rule in place of 6 minutes. **No rate is "—"**, in `.dim` (the overlay's Dim brush)
in a slot that holds a number; in running text the rate and its unit are left out of the sentence; a ranking or bar never
draws a rate it doesn't have. Words, the number from the data: "The rate starts at 8 kills, or when the crawl is finished."

- **History → Crawls:** Lv/h, T5/h, K/min and DPS cells of a visit with no rate are "—" with the hover text "No rate yet:
  it starts at 8 kills, or when the crawl is finished." The headers carry a tip (Lv/h "Levels an active hour (time inside,
  idle taken out)."; T5/h; K/min; DPS "Damage a second of active time, idle taken out. The fight meter counts only seconds in
  a fight, so its DPS is higher."), each ending with the same rule. Sorting puts "—" last either way. The T5-eq per active
  hour tile follows the rule over the visits shown: "—" and "starts at 8 kills" when none has a rate (0 with no visits
  was a zero that read as a fact). "Hide visits too short to judge" (on by default) hides the rows with no rate, except raids (judged by their boss) and visits at the level cap, whose kills are not counted until [kills-at-cap](kills-at-cap.md) (the user, 11-18; the exception goes with that spec). The engine says per row (`tooShort`); the page never recomputes it. When it hides every visit the empty row says "N visits too short to judge are hidden: the rate starts at 8 kills, or when the crawl is finished." A visit with no rate shows "too short" (dim) in the verdict column only when the engine says `tooShort`, its tip "No rate: under 8 kills and not finished."; a visit at the level cap with no rate shows "—" (dim) there, tip "No rate: kills at level 50 aren't counted yet." (the level from the data).
- **Home:** the session card keeps "T5-eq so far this session" and now says "The rate starts at 8 kills, or when a crawl is
  finished." The place line under the relevel, when the last visit has no rate, reads "Too short for a rate yet: the rate
  starts at 8 kills, or when the crawl is finished." A route stop's line leaves out a side with no rate ("3.8 levels/h";
  both: "Too short for a rate yet"; no T5-eq/h when none), the T5-eq/h coming from the engine's one rate.
- **Overlay:** the live value of Levels/h, T5-eq/h, Kills/min with no rate is "—" in Dim, the usual beside it stays, and
  the note reads "The rate starts at 8 kills: 5 so far." (farming: "...: 1.2 T5-eq so far."). Recent rows with no rate show
  "—" and no bar.
- **Relevels and Ask:** a leg with no rate shows "—" for levels an hour (the bar is by levels, unchanged). A place whose
  crawls hold no rate isn't ranked and gets no bar. DPS reads higher with idle time (the release note says so).
- **States:** nothing yet, too little, plenty: "—" and the words, never 0. No layout change, so no mock-up.
- **Shots:** main-crawls-short.png (Crawls, sorted by T5/h, with visits too short), main-crawls-tile-short.png,
  overlay-early.png, main-relevels-splits.png when the data has a leg.

## Reviews

- Engineer, 11-18: **Ask** (not ranking a place whose crawls hold no rate) is not done: Ask's places are sums over visits per level (and the community's, which this branch must not touch), so the rule there belongs with one-usual. Shots not rendered (budget).
- architect: changes asked, 11-18. Must fix: tests for RouteAnalysis.Pace's and JourneyLeg's rule and for Home's "Too short for a rate yet" lines; crawls.js and loot.js copy Rates.Has (read each row's null instead); the overlay's farming note never reaches NoRateNote. Ask's place ranking moved to one-usual; loot.js's sums are already engine-owns-numbers'. Re-read of the fix round (11-18): the four must-fixes are fixed and proven (RateRuleEverywhereTests fail if a copy of the rule drifts); `TooShort` is the one place the box is decided; the two changed RatePageTests expectations are corrections to the designer's words and stricter (the exact tip, one "too short", the tile's rule), not a weakening. No must fix. Should fix: (1) `OverlayWindow.xaml.cs` farming note: at the level cap a crawl has 0 kills, so `NoRateNote` says "The rate starts at 8 kills" for as long as it runs; say what Crawls says (kills at the cap aren't counted yet) until kills-at-cap. (2) `VisitStats.From`: `TooShort` rewrites the raid test; build the record, then `s with { TooShort = Rates.Early(..) && !s.Raid && .. }`, so `Raid` stays the one home. (3) `loot.js` tile divides all instance T5 by all shown hours once any visit has a rate, while Crawls' tile uses only the rated visits: same words, two numbers when a short visit is shown.

- designer: changes asked, 11-18. Rendered from the branch's build into the scratchpad (shots1): main-crawls-short.png, main-crawls-short-sorted.png, main-crawls-tile-short.png, main-crawls-tile-none.png, overlay-early.png, main-relevels-splits.png (the Crawls ones are preview.html in headless Edge: box unticked, sorted by T5/h, or the history cut to six visits with no rate). Right: "—" in .dim in Lv/h, T5/h, K/min, DPS; a 0 stays 0; T5/h sorted down puts "—" after the zeros; the box's new words. **Must fix:** main-crawls-tile-none.png, a new player whose visits are all short sees "No visits match." with the box on by default, and nothing says six were hidden or why (the "Too little" state): when the box hides rows, the empty row says "6 visits too short to judge are hidden: the rate starts at 8 kills, or when the crawl is finished." **Should fix:** (1) the box now hides 748 of this player's 1246 instance visits (it hid 135): 379 raids, boss kills among them ("Master Yael killed" in main-crawls-short-sorted.png), and 222 cap visits of 10+ minutes with no rate only because kills at 50 aren't counted; "too short" is false for those (rule 2), a question for the user. (2) The tile in main-crawls-tile-none.png, "T5-eq an active hour, the rate starts at 8 kills", drops "or when the crawl is finished" and says "an" where the tile says "per". (3) Verdict "no usual yet" on a visit with no rate (Nov 18 02:38 Nagafen's Lair D4 in main-crawls-short.png; Castle of Mistmoore D4 under a relevel with a typical in main-crawls-tile-short.png): it's the visit's own rate that's missing; show "too short" in .dim with the rule as its tip. (4) The cell tip "No rate yet" on a past visit that will never get one: History's should say "No rate: under 8 kills and not finished." (5) overlay-early.png is a raid (no note by design): no leveling crawl in the data can show no rate, since PreviewAsCurrent takes only visits of 5+ minutes and 5+ kills, so "The rate starts at 8 kills: N so far." is unseen; add a DevSnapshots shot of a leveling crawl cut after its 5th kill. Idea: main-crawls-tile-short.png has six "—" rows over a tile of 9 T5-eq/h (11 kills in all): the spec's rule, but worth a tip on the tile.
- architect (fix round, 11-18): fixed. RateRuleEverywhereTests prove RouteAnalysis.Pace (7 kills early at 30 min, 8 not at 1.5), JourneyLeg (7 none, 8 across merged legs) and the box; HomePageTests has the null-rate last visit and stop; crawls.js and loot.js read each row's null (the tile is over the rows that have a rate); the overlay's farming note says NoRateNote first; docs name `Rates.MinKills`; the release note no longer mentions relevel lines; `SessionStats.From` sends null with no visits; `rateFromKills` is safe with no snapshot ("enough kills"). Test changed for the designer's asks (RatePageTests: the cell tip and tile words), in the same commit.
- designer (fix round, 11-18): fixed. The empty row says how many visits the box hides and why; the box keeps raids and cap visits (the engine's `tooShort`; noted here and in kills-at-cap.md); the tile reads "T5-eq per active hour: it starts at 8 kills, or when the crawl is finished."; a visit with no rate shows "too short" in .dim; the past-visit tip is "No rate: under 8 kills and not finished."; DevSnapshots has `early` and `early-farming` for overlay-early.png (path below in the report).
- architect (re-read fixes, 11-18): the three should-fix are fixed. The overlay says "No rate: kills at level N aren't counted yet." at the cap (`VisitStats.AtCap`, tested); `TooShort` is built from `Raid` and `AtCap` with `s with`; Loot's tile is the rate over the rated visits, as Crawls'. No DevSnapshots state for the cap note: the preview takes only crawls of 5+ kills, which none at the cap has.
