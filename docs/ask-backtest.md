# Ask backtest: 10-level bands or level by level?

Ask uses two tables today:
- **Level from A to B** chains places level by level (`PlaceLevels`).
- **Every place, Mote farm and "Is my damage normal"** rank 10-level band rows (`BandStat`). Each visit is filed under
  the band of the level it started at.

They disagree. Every place gave Estate of Unrest D4 the ★ for 21–30, but it was only ever played at 20–25, and the
route leaves it at 26.

This backtest decides which way Ask's one table should work. Script: `tools/analysis/ask-backtest.cs`.

## Method

Every leveling visit is predicted from only the visits before it, using Ask's own tables as they stood then (the same
30-day fading). A leveling visit means 1–49, at least 0.3 levels and 5 active minutes, open past any zone gate. The
methods:

| Method | The prediction for a visit's levels |
|---|---|
| band | Its place's row in the band of its starting level (today's Every place and Mote farm) |
| exact | Its place's numbers at exactly those levels |
| win1, win2, win3 | Its place's numbers at those levels ±1, ±2 or ±3; nearer and better-covered levels count more |
| trend3 | ±3 with a straight-line trend across the window |
| place | Its place's average at any level |

## Results (11-16, Venun's logs)

Data: 723 segments from 07-31 to 11-16, 188 leveling visits predicted (153 crawls).

**Predicting a visit** (the 111 visits every method can answer; "miss" is the typical error in minutes a level):

| Method | Answers | Minutes a level: miss | Bias | Motes/h: miss (T5-eq/h) |
|---|---|---|---|---|
| band | 75% | 19% | +6% | 7.7 |
| exact | 61% | 19% | +7% | 8.1 |
| **win1** | **75%** | **17%** | +6% | 7.8 |
| win2 | 76% | 20% | +6% | 8.0 |
| win3 | 77% | 20% | +9% | 7.7 |
| place | 82% | 32% | +13% | 9.4 |

**Ranking two places** (crawls at different places at most 2 levels apart, with results at least 15% apart: did the
method, beforehand, put them in the order they came out?):

| Method | Minutes a level: pairs, right | Motes/h: pairs, right |
|---|---|---|
| band | 327, 86% | 347, 90% |
| exact | 184, 96% | 184, 90% |
| **win1** | **345, 94%** | **361, 93%** |
| win2 | 361, 91% | 379, 93% |
| win1, else win2 | 361, 92% | 379, 93% |

## Findings

- **Level matters.** Ignoring it (place) misses by 32% against 17–20%. Inside one place, minutes a level rise
  **6.3% per level**, so data 5 levels apart differs by 36% from level alone.
- **Bands predict one visit about as well as levels**, because a place gets replayed at the same few levels. That's
  why the problem didn't show as error.
- **Bands rank badly.** A band compares one place's data at 21–25 with another's at 26–30. They put two places in the
  wrong order 14% of the time; ±1 levels does so 6% of the time, answering as many pairs. 18 of the 35 band rows Ask
  ranks today were played at under half of their band's levels.
- **Every method runs about 6% slow.** Visits come out faster than the history before them: gear, upgrades and
  practice. The 30-day fading already leans toward recent visits.

## Decision (built 11-16)

One table, level by level, read through a **±1-level window** (nearer and better-covered levels counting more):
`PlaceLevels.Window`, with `AskData.WindowLevels = 1`. The backtest's `engine` method runs it and matches `win1` exactly.

- **The route** reads each level through the window. Before, it borrowed the next or previous level when one was
  missing. Routes on Venun's logs kept the same places, within 2% of the time.
- **Mote farm and "Is my damage normal"** below the cap rank `AskData.Near(level)`: every place open at the level with
  play within a level of it (`Ask.RowsAt`). At the cap they still use the level-50 rows.
- **Every place** is level by level: a strip of the ★ place at each level above the map (a place played at the level
  itself beats one only played beside it, as in the route), and every place at a level you pick, with "near" on rows
  that only rest on the levels beside it.
- **The overlay's NEXT picks** read places the same way.
- **The shared file** carries per-level hours, above-T5, fragments, deaths, kills a minute and DPS (`CommunityPlace`
  extras). Older files leave them out and still answer minutes and motes. Band rows stay in the file for older apps,
  but no answer ranks by them.

## Re-run

```
dotnet run tools/analysis/ask-backtest.cs -- <copy of pathfinder.db> <out.csv>
```

It reads Ask's segments through reflection (`TrackerService.AskSegments`), so run it on a copy of the database.
Re-run it on the shared pool once about 5 players share. Other players' visits at levels you never played are where
windows and bands could differ most.

## Pace drift (11-16)

A check of one relevel (SHD/ROG/BER at 1.2× XP) against Ask's predictions showed it took 84 minutes where Ask predicted
164. The predictions came from visits months old: Mistmoore D0 at 23–27 last played on 08-14, Befallen D2 at 15 on
08-03. The player keeps getting faster (gear, upgrades, AAs, practice). Within each place-and-level cell, August visits
ran 25% slower than the cell's average and November's 18% faster. The fit is about 24% less time a level per 30 days.

`PaceDrift` measures this from the player's own history: the same place and levels on dates at least a week apart, at
rank 0, a pooled slope within cells. It needs 5 cells and 20 visits. The trend is held to at most 40% faster or 25%
slower a month, and a visit's scaling to 3× either way. `AskData.From` scales each visit's time to the pace at "now",
and the hours shown and the evidence minimums still use the time actually played. Every Ask answer says how much in one
line. The shared numbers get it per player (each friend's history at their own current pace) once the nightly job is
rebuilt.

Backtest (203 leveling visits, each predicted from the visits before it, the drift fitted on those too):

| | Minutes a level: typical miss | Bias | Motes/h: miss (T5-eq/h) | Ranking two places right |
|---|---|---|---|---|
| without drift | 21% | +9% | 7.8 | 84% |
| **with drift** | **17%** | **−2%** | **6.4** | **88%** |

The SHD/ROG/BER relevel: 84 minutes against 89 predicted (−6%), instead of 164 (−49%).

**Capped at 1.5× (11-16, same day).** At up to 3×, the 1–50 route took Befallen D0 at 41–44 from a single 11-minute
visit on 08-14 (scaled to 40% of its time), and Ruins of Old Guk D0 and Old Paineel D0 legs from August too. A route
takes the fastest-looking place at every level, so it picks whatever is estimated too fast, and old one-off visits
scaled hard are exactly that. Held to 1.5× (about six weeks of a −26%-a-month player), the route is Befallen D1, Estate
of Unrest D1, Mistmoore D0, Kedge D0, Old Guk D1, Kedge D2 and Plane of Hate D2 (7.7 h), close to how the player was
actually leveling that night. The backtest barely moves: 18% typical miss and +2% bias (3×: 17%, −2%; no drift: 22%,
+10%).

## Your times and the players' together (built 11-17)

The user, 11-17: Ask's leveling hours should use "my time to level in those zones, and the community. Weighing more
towards mine when I have it, and more towards the community when I don't." Until then the route came from your logs
alone, with the players' route beside it, never mixed in.

**The rule** (`Ask.PoolVisits`, `Ask.Chain` with a pool): where both have a place at a level, its minutes are yours and
theirs together, yours counting by your visits there and theirs as one visit. One visit of yours is half the time, four
are 80%. A place only they have played joins the route at their time (marked "new to you"); a number they publish
already rests on enough visits, so it isn't counted as thin. Motes per hour blend the same way. The solo and grouped
routes blend with the players' solo and grouped numbers when the file has them apart. Each leg shows your time, theirs,
and how much of the time is yours. Mote farm, the difficulties, damage and Every place still answer from your logs,
with the players' beside them.

**Probe** (`tools/analysis/ask-blend.cs`, one player's logs and the community file of 11-17: 4 players, 1,376 crawls):

| | |
|---|---|
| Place-levels both have | 56, at 7 places |
| Your minutes a level ÷ theirs | median 0.86 (14% less time), spread ±15% |
| Predicting each of your visits from your visits before it: yours alone | misses by 15%, 3% fast |
| The same, theirs counted as ¼, ½, 1, 2 or 4 visits | misses by 14–16%, bias −2% to +2% (1 visit: 15%, +1%) |
| Theirs alone | misses by 20%, 5% slow |

Blending never predicted worse than your logs alone, and their number alone clearly did, so it fills in rather than
overrides. Every weight from ¼ to 4 visits came out within a point; 1 visit is the easiest to say. The community file
includes your own shared runs (no player counts for more than the file's share rule allows), which flatters "theirs"
here a little. With more sharers, rerun the probe before changing the weight.

**Not changed:** the overlay's NEXT picks and Home's next place still read your logs alone.

## Each sharer against everyone else (11-17)

`tools/analysis/sharers.cs` rebuilds the community numbers from the shared units once per player, with that player left
out, then compares them, replays their visits with the blend, and counts how much of their leveling the others cover.
Players are letters, most crawls first (A is the user). 4 players, 1,842 visits and 242 open-world hunts.

| | A | B | C | D |
|---|---|---|---|---|
| Leveling time shared | 151 h | 64 h | 23 h | 13 h |
| Leveling time at D4 / D0–D2 / open world | 62 / 20 / 18% | 42 / 29 / 26% | 53 / 29 / 11% | 100 / 0 / 0% |
| Of their leveling, the others' numbers cover | 1% | 16% | 17% | 91% |
| Against the others, overall (place by place) | −32% (1 place) | +9% (3 places) | −11% (2 places) | +19% (5 places) |
| A place new to them, from the others alone: miss, bias | 44% (n 2) | 36%, −14% (n 12) | 9%, +1% (n 8) | 24%, −15% (n 20) |

- **Most published numbers lean on one player.** With 5 or fewer players every player counts equally, and of the 57
  published place-levels (11-17), 20 rest on 2 players and 28 on 3. Without A, the others publish one place. So for A,
  "players who share" is half A's own play wherever 2 players stand behind a number: You against players who share
  understates A's gap (Estate of Unrest D4: −26% against everyone, A included; −32% against the others alone), and
  the blend counts A twice. Fixed the same day: see Leaving yourself out, below.
- **The blend weight can't be tested this way yet.** Places both a player and the others have: 7 visits across all four.
  `Ask.PoolVisits` = 1 stays on the 11-17 probe alone.
- **For a new player the others' numbers are a fair first guess** (misses 9–36%), and they run fast: B and D take longer
  than the others where they're new (bias −14%, −15%).
- **The community only knows the D4 route** (Befallen → Unrest → Mistmoore → Kedge → Hate). It covers 91% of D, who plays
  only that, and 1–17% of everyone else. Nobody else levels where A is fastest (D0/D1: A's own route 1–50 is 5.9 h, the
  others' D4 route 9.7 h), so those places stay unpublished. Levels 1–8 have no shared numbers at all.
- **Where players are behind, it's kill speed.** B at Kedge Keep D4 36–45: +37%, 1.4 kills a minute against 2.0. D at
  Unrest D4: +74%, 2.4 against 3.6; at Mistmoore D4: +37%, 2.9 against 3.5. XP per kill was within ±13% everywhere
  but one place.
- **Blended routes do what they should:** C's 1–50 drops from 9.9 h to 8.6 h through Mistmoore D4 and Kedge D4 (new to
  C), D's from 11.8 h to 11.1 h through Old Guk D4.

## Leaving yourself out (built 11-17)

With a handful of sharers, a player who shares is in most of the numbers they read: up to 5 players count equally, and
on 11-17 20 of the 57 published place-levels rested on 2 players (half of each was the reader) and 28 on 3. So
"players who share" was partly you, You against players who share understated your gap (Estate of Unrest D4: −26%
against everyone, −32% against the others), and the route counted you twice.

Now a copy whose play is on the server (sharing on or paused, and the server holds its crawls or hunts) reads every
slice of the file without itself (`CommunityData.LeavingOut`, `CommunityView.Without`): its own tables, built the way
the community job builds a player's (one difficulty is enough, as of the file's time, from `ShareFrom` on), come out of
each number it's one of the players behind. Your weight is 1/n up to 1 ÷ MaxShare players (the job's `CappedWeights`),
past that your crawls' share, capped. Where that leaves fewer players or visits than the file publishes with, the
number goes, and the Community note names the places: what's left would be one player's play. On 11-17 that left one
place for the user (Estate of Unrest D4 22–23, 3 players), which is the truth of it with 4 sharers.

Limits: up to 30 minutes after a first upload the file doesn't hold your play yet but is read as if it did; a player
linked across two PCs is taken out only for this PC's play; and your local numbers stand in for what you shared, which
can differ a little (visits the server left out).
