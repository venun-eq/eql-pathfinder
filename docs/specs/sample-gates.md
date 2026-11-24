# One named smallest sample for each purpose

**Built 11-19** (in progress, [numbers-audit](numbers-audit.md)). **Tier:** standard. Ask's gate changes, so the community file moves: `job: true`.
Fix round 11-19: farm Left out counts crawls only, leveling Left out its own range, named fit floors (`Samples.MinFitMinutes`), Settings hides the "Also used until" line once met.
Built: `Samples` (Rates.cs) names the 3 active minutes and the 3 farming crawls; the farm rate's crawl count, Ask's bands, sources and Left out count visits with a rate.

**Why:** docs/engineering.md, Modeling 2: "A number has a source and a smallest sample." Today, which visits count is
set in many places, on two clocks:
- `Rates.Has` (a finished crawl or 8 kills);
- Ask: 3 active minutes (`Ask.cs`, line 215);
- `Ask.Falls`: 5 minutes played and a kill;
- `PaceDrift`: 5; `RelevelPaces`: 5; `Journeys`: 3; `ask.js` (line 333): 5;
- `RouteAnalysis.Usable`: 300 s **inside** and 5 kills, then 3 active;
- `SharePayload`: 1 minute.

So "your typical farm" (the farm rate) and the farming card's usual use different sets of visits. The farm rate's
count includes visits with no rate, though its median leaves them out. Separately, `Ask.cs` (line 225) counts
left-out visits only below 50 (`LevelStart is < 50`), so the "Left out" count says nothing at the cap.

## Proposal

- Name the samples by purpose. At most:
  - one "counts toward a usual or a table" (`Rates.Has`, on active time);
  - one fuse where a fit needs more (the slope's runs).

  Each has its reason beside it (Modeling 7). Every place above calls one of them.
- A count shown beside a number ("over 12 visits") counts the same visits the number used.
- The left-out count includes the cap.

## Where

- `src/EqlPathfinder.Engine/Analysis/Ask.cs`, `RouteAnalysis.cs` (`Usable`, `FarmRates`), `PaceDrift.cs`, `Journeys.cs`,
  `RelevelPaces`, `Analysis/Verdicts.cs` (after verdicts-out), `Sharing/SharePayload.cs`; `web/js/ask.js`

## What the player sees

The designer, 11-19, from the code and docs (no render: nothing new is laid out). Nothing new is drawn and no class
is added: every change is a count that moves, or words on an existing line (`.ak`, `.dim`, `.chip`). What players
read about the rule stays the one sentence they already read everywhere (`rateRule()`, `noRateWhy()`): **a visit
counts once it has a rate: 8 kills, or a finished crawl.** If a fit needs a floor beyond that (the slope's fuse), it
is never said on a page as a second rule; only the "Why levels/h falls" panel uses it, and it already says "Under 2
runs here to show it."

**The one rule for every count.** A count printed beside a number is the visits that number was made from, nothing
more. Visits with no rate are never in a count beside a number; where an answer leaves some out, it says so on its
own "Left out" line. A minimum said in words ("it takes 3", "2 or more visits") comes from the engine's named sample,
never a literal in the page (today `core.js` and `settings.js` write "3 farming crawls" by hand).

### Counts that move (same words, new number)

| Where | Line | Today it counts | After |
|---|---|---|---|
| Every page with the farm rate (Ask motes, Upgrades, Every place, Settings) | "your typical farm (the middle of your 14 farming crawls at 50 since the 11-10 patch)" | every crawl with 300 s inside and 5 kills or loot, though the middle leaves the ones with no rate out | the crawls with a rate: the ones the middle is taken from |
| Same | "your best farm, Plane of Hate D4 (5 crawls ...)" | the same mixed set | crawls with a rate at that place |
| Same, `farmLine` | "typical 312 over 14 crawls, best ...", "Before it: typical ..." | same | same as above |
| Home, farming card | "Your typical farm at 50 since the patch: 312 · 14 crawls" | `r.farm.crawls` (mixed) | crawls with a rate |
| Ask, Best mote farm | each row "9 crawls · 4.1 h"; sources "31 crawls at 50 since the patch." | every visit of 3 active minutes, with or without a rate (debt.md, one-usual) | crawls with a rate, and only their hours |
| Ask, Leveling | "Behind every answer: 120 instance visits and 30 open-world hunts, 12 of them grouped." | every visit of 3 active minutes | visits and hunts with a rate |
| Ask tables and route | "up to 4 crawls a level", tip "· 3 visits", "faint = under 3 visits", "(hint)" | ranked visits (already with a rate) | same kind; a number moves only where the 3-minute floor differed |
| History, Home's chip | "no usual yet", "kill speed 92% of your usual over 6 runs" | the usual's own runs already | same; a run counts on active time, so a count can move by one |

Numbers that move with no count beside them: Ask's "You've been getting faster, about 3% less time a level each
month" (PaceDrift), relevel paces (×1.04) and Campaign's hours a level (Journeys) can change a little as their visits
follow the one rule. No layout moves.

### Words that change

- **Farm rate, too little** (`farmFrom`, kind `default`): today "the default: your logs have only 2 farming crawls at
  50 since the 11-10 patch, and it takes 3". A player who farmed four short crawls reads "only 2" as wrong. After:
  "the default: your logs have only 2 farming crawls with a rate at 50 since the 11-10 patch, and it takes 3".
  None rated but some played: "the default: none of your 4 farming crawls at 50 since the 11-10 patch has a rate yet
  (8 kills, or a finished crawl), and it takes 3". None at all, as today: "the default: your logs have no farming at
  50 since the 11-10 patch". This needs the engine to send the unrated count beside the rated one.
- **Farm rate, set, as the fallback**: "the rate you set in Settings, used until your logs have 3 farming crawls with
  a rate at 50 since the 11-10 patch".
- **Settings**, under Your farm rate: "Also used until your logs have 3 farming crawls with a rate since the patch."
- **(i) farmRate**: "It's your best or typical farm since the 11-10 patch, once 3 of your crawls at the cap have a
  rate (8 kills, or a finished crawl). Until then it's the rate you set in Settings, or 50."
- **Ask, Left out** (`noRateLeftOut`): counts the cap too; the words stay: "Left out: 3 visits with no rate (an
  instance you didn't finish, under 8 kills). History shows them as they happened." It now shows on the Best mote
  farm answer too (hidden there today), counting only that answer's level and scope: "Left out: 4 crawls at 50 since
  the patch with no rate (a crawl you didn't finish, under 8 kills). History shows them as they happened." Not on
  "Is my damage normal".
- **Ask, Best mote farm, crawls played but none ranked**: today a player whose only crawls at 50 have no rate reads
  "No farming at 50 since the patch in your logs", a zero that reads as a fact (design.md, Too little). After:
  heading "Too little farming at 50 since the patch in your logs to rank a place"; body "Your logs have 4 crawls
  there, none with a rate yet: a rate starts at 8 kills, or when the crawl is finished." The Left out line follows.
  "No farming" stays only when there was none.

All "8" above is `rateFromKills()`, all "3" the engine's sample: neither is typed in a page.

### By player

- **The regular:** counts drop by their unfinished short crawls. The farm rate itself stays (its middle already left
  them out); the farm answer's rates rise a little where short unfinished crawls dragged them. Left out now names them
  at 50 too.
- **New, log just turned on:** first crawls at 50 under 8 kills, unfinished: the farm rate says "none of your 2
  farming crawls ... has a rate yet", the farm answer says "Too little farming", never "No farming".
- **Not sharing, and among few sharers:** the community numbers follow the same rule (below), so "Players who share,
  here" and "A number shows once 3 players and 6 visits stand behind it" count visits with a rate.
- **Waiting, Wrong, the window:** unchanged.

### The community file and sharing

- Ask's gate changes, so the community file moves: the release note has `job: true`.
- What the app sends doesn't change: `SharePayload` keeps sending every crawl with a kill or a minute (facts are
  shared raw; the job applies the rule when it builds the numbers, CLAUDE.md). Changing what's sent is the risky
  tier, and this branch is standard.

### Release note

`docs/releases/next/sample-gates.md`, `reread: false`, `job: true`:

> Every count beside a number now counts the visits behind that number. Crawls with no rate yet (under 8 kills and not
> finished) were counted in "your typical farm (the middle of your 14 farming crawls)" and in Ask's mote farm list
> though they weren't in the rate; now they're left out of both, and Ask's "Left out" line counts them at 50 too. The
> farm rate starts once 3 crawls have a rate. The numbers from players who share are rebuilt the same way. No re-read.

## Done when

- [x] The farm rate's "the middle of your N farming crawls" is the count of crawls its median used - seen in main-ask-motes.png
- [x] Home's farming card's "Your typical farm at 50 since the patch" shows the same N - seen in home-farming.png (Home "3.3 · 6 crawls")
- [x] Ask's Best mote farm rows count crawls with a rate, and its Left out line counts those at 50 without one - seen in main-ask-farm50.png
- [x] Ask's Leveling "Behind every answer" counts visits with a rate, and Left out includes the cap - seen in main-ask-level.png
- [x] Settings says "3 farming crawls with a rate", the 3 from the engine - seen in set.png (Settings under Set it)
- [x] A farm answer whose crawls all lack a rate reads "Too little farming", not "No farming" - seen in a page test (no shot has that state)

## Edge cases
- Crawls at 50 all without a rate: the farm rate says "none of your N farming crawls ... has a rate yet", Ask says "Too little farming", never "No farming".
- A zone with 3 crawls but only 2 with a rate is not "best" (a best needs 3 rated crawls).
- Left out for the farm answer counts the answer's own level and scope (since the patch, or all time); below the cap, levels within one of it.
- Level 50, no rate: counted in Left out as at any level (the hold from one-usual is gone; kills at the cap are known since 0.10.17).
- Community builder: the same `AskData.From`, so its numbers follow the rule; `SharePayload` still sends every crawl (facts shared raw).

## Not doing
- `PaceDrift`, `RelevelPaces`, `Journeys`, `Falls`/`ask.js` 5-minute floors and `RouteAnalysis.Usable`'s 300 s: separate clocks, left as they are (debt).
- `SharePayload`'s 1-minute gate: the risky tier; the job applies the rule.

## Reviews

- architect: no must fix. Should fix, all fixed in e701b1c: the fit floors named (`Samples.MinFitMinutes` in PaceDrift and RelevelPaces, `Samples.MinActiveMinutes` in Journeys), with `Ask.Falls` and ask.js's copy left in docs/debt.md; core.js's literal `?? 3` dropped, and the test that only checked a default parameter deleted; AskPage's ±1 level read from `AskData.WindowLevels`. It checked on a copy of the real database that the farm rate's value didn't move (3.34 on main and here, the count 7 → 6), and logged the farm rate at the cap (D0 crawls, the patch's hour) as debt's top row. The user then gave the patch's real hour (2:50 PM EST), written into that row.
- designer: must fix, fixed: two boxes pointed at shots that don't show the thing, and now point at its renders (`rv\home-farming.png`, `rv\set.png`). Should fix, fixed in e701b1c: the farm answer's "Your logs have 6 crawls with a rate there, but no place has 2 of them ..."; its Left out counts crawls, not hunts and other instances; the leveling reason names the open world; Settings hides "Also used until ..." once met. The user, 11-19: a leveling answer's Left out counts only its own range (done, `ALevelingAnswerLeftOutCountsOnlyItsOwnLevelRange`). Two rows for debt, added. The lead re-rendered Ask's farm at 50 and 5–15 answers after the fix (`sg2\main-ask-farm50.png`, `sg2\main-ask-level.png`, scripts without errors) instead of a second review, and ran the two new tests against the engine before the fix: both fail.
