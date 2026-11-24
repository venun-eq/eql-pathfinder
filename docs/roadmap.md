# Roadmap

Friends see the short version in the app (Help → What's coming), with votes. It's served from the `roadmap` table in D1,
edited by hand. This file is the long version, with notes. Keep the two in step: same ids, same status.

Statuses: **building** (in progress), **next** (decided, not started), **maybe** (being considered, votes decide),
**shipped** (with the version).

## Building

Nothing here: what to build and in what order is docs/queue.md.

## Next

### kunark-cap: Ready for Kunark's level 55
**For players:** on 12-08 the cap becomes 55. Unless the app is ready, it goes on treating 50 as the end: Home says
you're farming at the cap while you still have five levels to go, the farm rate counts your play at 50 as farming, and
Campaign marks runs done five levels early. After, everything that asks "at the cap?" says 55 from launch day, and
what you finished at 50 before then stays finished.

```text
On 12-09, a sketch
Home, your main at 50     today ⟦Farming · at the cap⟧    after ⟦below the cap: 50 / 55⟧ (the designer's words)
Relevels, one that hit 50 on 12-05                        stays ⟦done at 50⟧, never ⟦50 / 55⟧
Campaign, a run back at 50 after 12-08   today ⟦done⟧    after ⟦leveling, 50 / 55⟧
```

Kunark launches on 2026-12-08: the level cap goes from 50 to 55, and the reincarnation token's 8 crawls change (see
reinc-token). The engine's cap is a fixed 50 today (`LevelCap` in `Tracking/EngineState.cs`; nothing sets it), so
everything that asks "at the cap?" moves at once. The user, 11-18: it can wait a week or two; start by about 12-01.
- What moves: Home's state, where a relevel ends (`Journeys`), farming at the cap and the farm rate, Campaign's
  "done", the field hunts below the cap, and the community numbers that group play at the cap.
- The cap belongs to the day it was played: a relevel that reached 50 before 12-08 stays done and doesn't reopen as
  "50 / 55". Farming at 50 before the launch isn't farming at the cap after it.
- To decide: how the app learns the cap (a date in the code, since releases are hourly, with a level past 50 in the
  log as the fallback), and what the community numbers do with play at 50 after the launch.
- Likely risky: the shared numbers group play by the cap.
- To decide, the user 11-20: on launch day there's no data for the new zones. Either level data from outside sources,
  such as eqlwiki, gives a first guess, or the app says plainly that there's no route there yet.
- Built for the next one too (the user, 11-20: "This won't be the last expansion that raises the level cap"): the
  cap is a list of expansions with the day each starts and its cap, so the next raise is one more entry and its
  test. docs/engineering.md rule 13.

### rank-check: Measure the XP penalty past rank 3
**For players:** once a trio's most-reincarnated class is past rank 3, the app assumes +30% XP to level, so that
trio's later relevels look slower than they are. After, it measures the real penalty from your own kills and says
it's measured.

```text
Relevels, a later relevel of a rank-3 trio                       made-up numbers
today  at ⟦+30% XP to level⟧ · pace ⟦behind your usual⟧
after  at ⟦+50% XP to level, measured⟧ · pace ⟦on your usual⟧
```

The reincarnation lines are AA rank-ups, and each AA line has 3 ranks, so the log shows ranks only up to 3. The app
counts to 3 from the lines and doesn't try to go further. A low /who can't say which loadout reincarnated, because a
loadout shows its lowest member's level (decided 11-17). Past 3 the only thing that changes is the XP a level needs:
+10% per reincarnation of the trio's most-reincarnated class, up to +300%. So measure the penalty instead of counting.
- When a trio's top class is at rank 3, compare its XP % per kill with matched kills at a known rank (solo, same mob,
  level, zone and difficulty) and infer the penalty: +40% up to +300% (1.4× to 4.0×). Same method that confirmed 1.20×
  on 11-16 (median of 56 kills) and 1.100 on 11-13 (128 pairs). Pre-11-10 kills are the cleanest base (rank 0).
- Use the measured multiplier for that relevel's pace instead of 1.3×, and show it as a penalty ("XP penalty +50%,
  measured"), not as a rank. Until this exists, a rank-3 trio's later relevels assume +30% and look slower than they
  are.
- Steps are small at the top: 1.3× → 1.4× is about 7% less XP per kill. Measure the spread of matched kills first to
  see how many kills a reading takes, and round to the nearest 10%.
- Facts stay raw: the kills are already shared; the multiplier is worked out in the view, never stored or sent.
- Limits: solo kills only, unless the party split is modelled. Needs matched kills from an earlier relevel. Any XP
  modifier added later would confuse it.

## Maybe (votes decide)

### sky-quests: Sky quests and class unlocks
Plane of Sky quest items you hold (inventory export) and have turned in (log lines), checked against the achievement
export: what each class unlock still needs. Also multi-step turn-ins on upgrade paths for important items.
- Data: `/outputfile inventory` and the achievement export, both already read elsewhere; turn-in lines from the log.
- Risk: quest chains and item names need a table we keep by hand.

### hunter: Hunter achievements
Which Hunter kills you still need, where those mobs are, and how close each achievement is.
- Data: the achievement export (what's done) and slain lines (what you killed).
- Needs a table of mob → zone that we keep by hand, or that is built from the shared pool's kills.

### banestrike: Banestrike progress and routes
Your Banestrike progress, and the quickest zones to clear for the mobs and named you still need. Pairs naturally with
Hunter: "go to X, kill these N mobs and these named". The route part can reuse Ask's routing.

### char-sheet: Character sheet from your gear
Your stats, mods and caps worked out from what you actually wear, like eqlegendstools.com's char sheet.
See "The character sheet question" below before starting.

### group-meter: Group fight meter
Groupmates' Pathfinders compare notes after each fight: everyone's real damage, every pet assigned, nothing lost to
range. Step 1 costs no networking: merge one groupmate's log with yours offline and measure what the meter misses.
Build the room (a Durable Object per group code) only if the gap is worth it.

### reinc-token: Reincarnation token progress
The game gives a free reincarnation token for finishing 5 of 8 listed Dungeon Crawls (Permafrost, Guk, Hole, Fear,
Sky, Hate, Kedge, Nagafen's Lair; the list changes with Kunark on 12-08), and you can hold only one. Players route a
relevel through those places so each relevel pays for the next, and reincarnate a parked loadout to spend a token
before they earn another. Show "Token: 3 of 5 places" during a relevel, and let Ask's route favour the places still
needed. Asked for in feedback report 6 (11-18).
- The game: the first listed crawl you finish gives the quest "Dungeon Crawler's Reincarnation Token" (Quest Log,
  Alt-Q), with the 8 as optional steps, each marked Done as you finish it. At 5 the quest completes and the token goes
  into your inventory.
- Data (checked 11-20, docs/log-formats.md "Token quest"): the log says both. A crawl that counts writes
  `Your task 'Dungeon Crawler's Reincarnation Token' has been updated.` in the same second as the crawl's end, the
  first of a round also `You have been assigned the task …`, and the 5th `You have been given: Dungeon Crawlers
  Reincarnation Token`. A repeat place writes nothing. So the app reads the game's own count and needs no list of
  its own to count with: the list is only for routing to the places still missing. What the log can't say: a round
  started before /log was on, and a token spent (no reincarnation line names the token it used).
- Before building: does a crawl finished while you hold a token count? Venun's log can't say: no listed crawl came
  between a token and the next reincarnation. Does every reincarnation spend one? The one on 11-18 11:08 came with no
  token earned or bought since the one before. Kunark's list.
- The user decided on 11-20 to build routes-you-can-shape (docs/queue.md) before Kunark, with the 8 crawls as
  configuration, swapped on 12-08; the 5-of-8 rule stays. This row's D1 status stays `maybe` for now.

## Shipped

| id | What | Version |
|---|---|---|
| open-world-pool | More open-world hunting in the shared numbers: each hunt is sent with its trio and rank, so the nightly job keeps 84 of 129 hunts instead of 59 | 0.9.30 |
| charm-dps | Charmed pets counted right (only while the charm holds, only on its own targets; hits on yourself aren't damage) | 0.9.9 |
| feedback | Feedback and votes in the app | 0.9.9 |
| fight-meter | Fight meter (pets count as you; active or kill time) | 0.9.8 |
| overlays | Overlays that work the same way | 0.9.8 |
| community | Numbers from players who share | 0.9.4 |
| ask | Ask | 0.9.0 |

## Dropped

**timers** (timers and alerts, 11-20): dropped by the user, who wants to run Pathfinder alone, not Pathfinder +
Companion. It can come back.

## Feedback and triage

The `app-feedback` skill reads the reports, and docs/queue.md is where each one goes.

## The character sheet question

What eqlegendstools.com's char sheet does depends on three things: item stats, the stat and cap rules, and your gear.
Pathfinder has only the last one (the inventory export). The question is which of the other two to own.

- **Item stats** aren't on your PC. The game files have spells (`spells_us.txt`) and skill caps (`skillcaps.txt`),
  and Pathfinder reads both, so they stay right after every patch. Items come from the server. Owning item stats
  means scraping or licensing someone's database and keeping it current, which is the expensive part.
- **Stat and cap rules** (caps that move with reincarnation ranks and Legends changes, focus and mod stacking) are
  EQL-specific and mostly undocumented. Owning them means checking them by hand after each patch. Get one wrong and
  the sheet is silently wrong, which is worse than having none.
- **Drift:** every patch that adds items or moves caps needs a data update. Parts derived from the game files drift
  by themselves; tables we keep by hand drift until someone notices.

Recommendation: don't clone the sheet. First, see what the site accepts (a paste or import of an inventory) and
whether its maker shares data or would welcome a hand-off. Then:
- **Cheap and right:** send your real inventory to the site (copy or deep link), and own only the layer Pathfinder
  is uniquely placed for: which upgrade is worth your motes (Upgrades already does this) and what your reincarnation
  passives add (Roster knows your ranks).
- **Only if votes demand it:** own a small sheet limited to stats whose rules come from game files, and show
  "unknown" rather than guessing.
