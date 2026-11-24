# The website's screenshots, as the app is now

**Why:** The site's six shots were made 11-17, before 0.10.0 to 0.10.20. Home, Ask, History (Crawls is now Visits),
the overlay's verdict-coloured bars and the farmed difficulties judged in T5-eq/h all changed. The user also asked for
a shot that sells "Better together", and a draft for what the app lacks if no honest shot can.
**Players:** visitors to the website, none of whom has the app yet. Every shot is demo play ("Kestrel"), no real names,
no chat.

## What the player sees

Six shots in five sections, as before, and new words for Better together. Two renders of the demo logs, both from this worktree's build:
- **Full logs** (as site/README.md says): Ask and the meter.
- **Logs cut at 2026-11-18 17:12** (the main log's lines before the first `[Wed Nov 18 17:12`), with
  `EQLPATHFINDER_PREVIEW_BEFORE="2026-11-18 17:05"`: Home, Relevels, History and the overlay, one relevel in all four. Today the demo is at
  level 10 of a new relevel, where nearly every number is still "no usual yet".

| `site/img/` | From | Why this one |
|---|---|---|
| `home.png` | cut, `main-home-flipped` | A relevel with every part working: ahead of the best, route bar, splits, kill speed judged. Taken from `home-flipped` because snapshot Home opens on "Farming at 50" at level 42 (not this branch's: see Not doing) |
| `ask.png` | full, `main-ask-pool` | A four-place fastest route with solo, grouped and players-who-share side by side |
| `overlay.png` | cut, `overlay-leveling-at` | On pace, bars coloured only where the verdict judges, a tick from 4 runs |
| `meter.png` | full, `meter/meter-place-spell` | Unchanged choice, re-rendered: by ability, so no fighter's name |
| `relevels.png` | cut, `main-relevels` | The race with WAR/CLR/RNG at 43 dashed to the end: the same relevel as Home, History and the overlay |
| `history.png` | cut, `main-crawls` | Visits grouped by relevel, verdicts in both kinds (On pace, Usual T5-eq/h, 53% more T5-eq/h) |

`public/og.png` is `home.png` scaled to 1200 wide, top 630 px.

**Alt text, word for word:**
- home: "Pathfinder's Home page during a relevel: WAR / CLR / RNG as a Kerran at level 43 of 50 at 20% more XP to level, 38 minutes ahead of the best relevel at D4 with about 3.6 hours to go, the route as a bar from 1 to 50 with Ask's route from here dashed, kill speed at 83% of usual, and splits against the best at each level."
- ask: "The Ask page answering where to level from 11 to 34, fastest: about 2.3 hours from Befallen D1 through Estate of Unrest D1 and Castle of Mistmoore D0 to Kedge Keep D0, with solo, grouped and players-who-share answers side by side, and each place's time, minutes a level and T5-eq an hour."
- overlay: "The pace overlay in Kedge Keep D0 at level 42: on pace, with bars for levels per hour, T5-eq per hour, kill speed and XP per kill, each with a tick at the usual here from 4 runs, the two the verdict judges in green, and about 3.6 hours to 50."
- meter: "The fight meter for a Plane of Hate D0 crawl: 477 damage per second, 99% of the usual there, split by spell, each against the usual for that place."
- relevels: "The relevel race: six relevels, the best at 7.3 hours as a line against the typical 10.9 hours, WAR / CLR / RNG in progress at level 43 after 5.9 hours and dashed to about 9.5 hours, and below it every relevel as a bar coloured by zone."
- history: "History's visit list: 891 visits and 260 active hours, grouped by relevel, each row with trio, zone, levels, levels per hour, a verdict such as on pace, usual T5-eq/h or 53% more T5-eq/h, and the T5-eq looted."

**History's copy** follows Visits and farming verdicts (the "slower place" label is gone since 0.10.12): heading "Every visit, measured.", first sentence "Each crawl, raid and open-world hunt with its time, levels per hour, T5-eq, kills, deaths and a verdict against your usual there: on pace or slow while you level, more or less T5-eq an hour while you farm."

**Better together** stays a text section (kicker "Optional", heading unchanged). Its text, replacing the intro (today's says sharing is what fills Ask in, but players
who don't share read the same numbers):

> Where your own logs are thin, Ask answers from players who share: how fast they level and farm in places you haven't run, and how many players stand behind each number.
>
> Share your play and it fills in theirs. Only numbers go: levels, times, kills, motes and damage. Never names, chat or clock times. Sharing is off until you turn it on in Settings.

site/README.md: the recipe gains the cut-log render and the table above's mapping; step 5 says how to redo the sharing shot,
and why it isn't on the page yet.

## Done when
- [x] Home shows a relevel ahead of its best, with no real name - seen in home.png
- [x] History's rows carry verdicts, not "no usual yet" - seen in history.png
- [x] The overlay's bars are grey where no verdict judges them - seen in overlay.png
- [x] Better together has the new text, at phone width too - seen in a render of site/index.html
- [x] No character name, group member or chat in any shot - seen in all six PNGs

## Not doing
- Fixing Home: at level 42 in Kedge Keep D0 (and at 10 in New Sebilis Expedition D4) snapshot Home opens on
  "Farming at 50 · Auto: you're at 50". The words hard-code the cap (`home.js`, `modeSwitch`); the pick is the
  engine's `homeNow`. Goes to docs/debt.md; when fixed, `home.png` comes from `main-site-home` again.
- A Campaign shot (the user, 11-19: not now). It changed most of all, but the new planner is in beta on its own
  branch, so a shot of today's would soon be out of date.
- **A sharing shot, for now** (the user, 11-19: text only until it can sell sharing). The best real one, Ask's mote
  farm at 50, reads "3 players", "2 players". The user asked for one "of how the application is INTENDED to perform
  with the appropriate amount of data", so it was drawn from a sample pool (below), and it still leads with "Too
  little farming at 50 since the patch in your logs" and "none has enough players since the patch yet", with
  fragments/h 0.0 on every row. What the app lacks to sell sharing is the draft [sharing-value.md](sharing-value.md).
  site/README.md step 5 redraws it once that's built or real players farm at 50 since the patch; it goes on with a
  caption saying the numbers are a sample.

  **How the sample shot is drawn.** The live pool has 5 players, and the real farm at 50 reads "3 players", "2 players": a visitor sees a thin pool, not what sharing gets them. So the shot shows Ask as it reads with 32 players sharing. `site/shots/sample-pool.mjs` made `site/shots/sample-pool.json` from the live community file of 11-19 14:40: every player count multiplied by 32 ÷ 5 (capped at 32), crawls and hours by the same 6.4; every rate, minutes a level, place, level, difficulty, "since the patch" flag and last-played date exactly as the live file has them, and nothing added. `site/shots/sharing.ps1` runs the real window (`EQLPATHFINDER_SELFTEST=mock`) in an isolated copy over the demo logs with that file as its saved community copy, and `sharing.js` only clicks Ask → Mote farm at my level → 50 → Since the 11-10 patch (the page's default). The window is 1911 × 1137; the script keeps the top-left 1892 × 1116 (the scroll bar and the bottom edge go). No name or chat is on it: Ask names places only.

  Why this screen, of Ask's route ("new to you" legs, You against players who share), the farm at 50 and Home's farm card: it is the only one where the player's own logs say plainly they can't answer ("Too little farming at 50 since the patch in your logs to rank a place") and players who share answer right under it, with how many stand behind each row. The route's "Players who share" card is already `ask.png`'s; Home's farm card at 50 needs the cut logs, where the demo is mid-relevel.

  What the sample keeps from the real file, and a visitor may notice: the 50 band has no farming since the patch with enough players, so the label reads "Players who share · all time: none has enough players since the patch yet" and fragments/h is 0.0 on every row (fragments came with the patch). Making since-patch rows would invent numbers; rerun the script on a later live file once real players farm at 50 since the patch, and both go.

## Reviews
- designer: no must fix. Should fix: relevels.png came from the full logs and showed another relevel in progress (DRU/BRD/NEC at 10) than Home, History and the overlay; now from the cut render, alt and README to match. Ideas taken: History's copy said "slower place", a label gone since 0.10.12, and "crawl" where the page is Visits; the meter's alt says "by spell" as the app does. Left as the app draws them: the red "17% slow" beside "38 min ahead" on Home, the overlay's cut-off title and its log clock time, History's start times (all in the old shots too), full-app shots unreadable at phone width (the layout was so before).
