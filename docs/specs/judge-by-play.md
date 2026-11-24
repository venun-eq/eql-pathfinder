# Judge by what was played

## Why
Report #9 (0.10.10, History → Crawls): a D4 relevel's rows read "36% fast · Slower place", the amber read as "slower pace", while Home said faster. The place verdict compares levels/h with any place at that level, so D4 always loses to D0. The user: "D3 and D4 are for motes. Everything else is leveling", "the only thing I enjoy tracking is my motes per hour", and of Home's Fastest | Counting motes switch: "scrap it... inferred by the behavior of the character through logs. Ask should be the only place where someone can specify what they're looking for."
**Players:** the regular most (Crawls is long, D4 relevels); new players see "no usual yet" in more places; grouped players get a verdict for the first time.

## What the player sees
Mock-up of today's Home (every D4 leg "Slower place" against D0–D2, the switch to go): `mock-judge-home-today.png`, path in the designer's report. Everything new reuses `.chip`, `.good`/`.warn`/`.gold`/`.dim`, `.setrow`, `.seggroup`, `.stale`.

**How a visit is read** (the engine names each visit's kind; docs/log-formats.md, "What kind of instance"):
- **Raid** (`- Solo`/`- Group` in the name): `Lord Nagafen killed` (good) or `No boss kill` (dim). No pace.
- **Grouped** (most kills gave party experience), **farming** (a difficulty you farm, default D3 and D4; an instance that ended without "completed"; any visit at the cap): T5-eq per active hour against your usual of the *same kind* at that place and difficulty: `31% more T5-eq/h` (good), `Usual T5-eq/h` (good), `24% less T5-eq/h` (warn), `no usual yet` (dim). Grouped adds `Grouped · ` in front. Tip: `T5-eq per active hour against your usual at Kedge Keep D4: crawls you finished, solo` (or `instances you didn't finish`, `grouped`). The band is a visit's normal swing of T5-eq/h measured from the player's own visits (into docs/pace.md), not kill speed's ±15% by assumption. No kill-speed or place words.
- **Leveling** (the rest below the cap: crawls at other difficulties, open world): kill speed as today. The place verdict compares only with leveling at the same difficulty (open world with open world). "Slower place" becomes `Befallen D1 levels faster` (gold); "Fading" becomes `Fading · Befallen D1 levels faster`. Live, an unfarmed instance is leveling until it ends without "completed".

**History → Crawls:** the Verdict column as above (its tip rewritten to match). Relevel header after `· now`: `· mostly D4 · 52 T5-eq/h`, plus `(your D4 relevels: 48 typical)` with 2 or more finished of that kind. Levels/h and kill-speed parts stay.

**Home:** the Fastest | Counting motes switch and `Your goal: …` go. Under the strip: `dashed = Ask's route from here, counting motes · mostly D4 (i)`, then dim: `Counting motes: this relevel is mostly D4, a difficulty you farm. Ask it another way →` (opens Ask's level answer with that pick). Otherwise `Fastest: this relevel is mostly D1.`; between relevels `… your last relevel, WAR/CLR/RNG, was mostly D4.`; no relevel yet `Ask's route is the fastest: no relevel of yours to go by yet.` The kind is the relevel on now's once it has crawl hours, else the latest finished relevel's; mixed counts motes when farmed difficulties hold over half its hours. To beat, the pace line, splits, race and Your relevels keep goal-on-home's like for like, keyed on that kind (`Mostly D4, like this relevel` / `like your last relevel`). The XP-per-kill tile and place-by-place chips use the visit words above; a farming leg's note is `3.7 → 2.4 levels/h · 52 T5-eq/h`.

**Overlay:** rows by kind: farming `T5-eq/h, kill speed`; leveling `levels/h, T5-eq/h, kill speed, XP per kill`. NEXT ranks by T5-eq/h while farming, levels/h while leveling. Place line: `Befallen D1 levels faster: 4.2 levels/h at 32`, leveling only.

**Settings:** "What matters to you" becomes **How you play**: `Which difficulties do you farm for motes?` with a `.seggroup` of five toggles, on ones `✓ D3` `✓ D4`. Hint: `Pathfinder judges a visit at these by T5-eq per hour against your usual there, and the rest as leveling, by levels per hour. Grouped play, instances you never finished and play at 50 are always judged by T5-eq per hour. To rank places your own way, use Ask.` None on is allowed. After a click, Home's and Crawls' verdicts are `.stale` until the next data message.

**Ask:** Rank by and What counts stay and are remembered for Ask only; nothing else reads them. Fading marks only leveling places, against places at the same difficulty. **Help** `help-farm`: "Your goal (Settings) decides..." becomes the Settings hint's first two sentences. **Welcome:** the "Your goal" step goes; "Where to go next" reads `The best zone for your level, from your own crawls.`

## Done when
- [x] A visit's kind (raid, grouped, farming, leveling) follows the farmed difficulties, party experience, "completed" and the cap - `JudgeByPlayTests` (`ADifficultyYouFarm...`, `GroupedAnInstance...`, `AnInstanceStillGoing...`, `ARaidIs...`)
- [x] Farming and grouped visits are judged by T5-eq/h against your same kind at that place, with a band from your own visits - `FarmingIsJudged...`, `ABandComes...`, `YourUsualIsOnly...`, `FewerThanThree...`
- [x] The place check compares only the same difficulty (report #9) - `ThePlaceCheckComparesOnlyPlacesAtTheSameDifficulty`
- [x] A relevel is farmed when farmed difficulties hold over half its hours - `AFarmedDifficultyHolding...`
- [x] Crawls: D4 rows say `% more/less T5-eq/h` or `Usual T5-eq/h`, no "Slower place"; a D1 row says `<place> levels faster`; a raid row names its boss or `No boss kill` - seen in main-history.png
- [x] Crawls relevel header: `mostly D4 · … T5-eq/h (your D4 relevels: … typical)` - seen in main-history.png
- [x] Home on a D4 relevel: no switch, `Counting motes: this relevel is mostly D4…`, To beat at D4, place chips in T5-eq/h words - seen in main-home.png
- [x] Home with no relevel: `Ask's route is the fastest: no relevel of yours to go by yet.` - seen in a new main-home-norelevel.png
- [x] Settings: How you play with `✓ D3` `✓ D4` and no Your goal - seen in main-settings.png
- [x] Overlay at D4 leads with T5-eq/h and has no place line - seen in overlay-crawl shots
- [x] Welcome has no goal step - seen in welcome-*.png

## Edge cases
- No farmed difficulty picked: every visit is leveling except grouped, unfinished, at the cap and raids.
- Fewer than 3 earlier visits of the same kind at the place, or under 6 minutes in this one: no verdict (`no usual yet`, or early).
- A visit in progress is leveling until it ends without "completed"; a raid never has a pace.
- Open world is leveling, compared only with open world; History has no open-world rows, so only the overlay shows it.
- Raid boss: the tracker keeps the name of a known boss slain inside a raid instance (`Visit.BossKilled`, optional in stored JSON, so old visits read `No boss kill`).
- A relevel with no crawl hours has no kind; Home says `Ask's route is the fastest: no relevel of yours to go by yet.`

## Not doing
- Zoning out and back splitting a visit (separate branch). Telling respawning instances from given-up crawls (the log can't).

## Where
- Engine: `Analysis/VisitKinds.cs`, `FarmPace` (RunPace.cs), `PlaceCheck`, `DifficultyMix.Farmed`, `Journey.Farmed`, `TrackerOptions.FarmDifficulties`, `VisitStats` (kind, farm verdict), `TrackerService` (`Judged`, `Judge`, `homeKind`, Home's route goal)
- App: `AppSettings.FarmDifficulties`, overlay rows by kind, Settings/Welcome/Help/Home/Crawls scripts
- `_settings.Goal` stays Ask's own pick (Usage.cs and privacy.html untouched).

## Reviews
- designer: 2 must fix (Home's place chips judged each stop by its first visit; Home's place tile not in T5-eq/h words on a farmed relevel), 4 should fix (cap and farmed difficulties written into text, no fade after a Settings change, raid chip wording, Crawls header cut off), all fixed in 16d4aa1; D4 rows no longer say "Slower place" (report #9).
- architect: first review, 3 must fix (homeKind without crawl hours, raid boss needing a re-read and a test, Crawls' typical decided in the page) and 3 should fix (NEXT's kind fallback, Crawls not stale, raid overlay rows), all fixed in 16d4aa1; second review, the fixes are right, one must fix (the stop grouping and raid kind untested) and two should fix ("early" chip wording, legs mixing difficulties), fixed in the commit after 16d4aa1: `ConsecutiveFarmedVisitsAtOnePlaceAreJudgedAsOneStop` (it also showed History grouped stops by place alone, so a stop now ends at another difficulty); Home's "early" chip reads "Too short to judge yet"; its legs split by difficulty.

## Review fixes (11-18)
A stop is judged whole (`FarmPace.ForStop`, `VisitStats.StopFarm`) and Home's tile and chips use it; Home's kind is picked by `DifficultyMix.HomeKind` (crawl minutes, none for open world alone); `EngineState.CurrentVersion` is 11 for the raid boss; Crawls' header reads `Journey.T5PerHour` and `TypicalOfKind`. Tests: `JudgeByPlayReviewTests`.
