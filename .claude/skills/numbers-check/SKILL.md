---
name: numbers-check
description: Prove History's numbers against real play. Recompute every visit's kills, T5-eq, times, rates, usual, band, verdict and kill speed from the database's rows and the raw log by the docs' definitions, independent of the engine's code, and work one visit out by hand. Use after any change to parsing or to a number on screen, before a release that re-reads logs ships, or for "are the numbers right", "check the math against my log".
---

# The real-data numbers check

The scripts in `tools/analysis/numbers-check/` work each number out from its definition in the docs (docs/pace.md,
docs/log-formats.md, the specs they name), from the database's kill and loot rows and the raw log. They stand apart from
the engine: keep it that way, since that's what makes the check a proof. It changes nothing and needs Node 22.5 or later (`node:sqlite`).

## 1. A copy and a dump

Work from a copy of the database while the app runs. Copy it whole, in one consistent read, into the scratchpad:

```sh
node tools/analysis/numbers-check/copydb.mjs "$LOCALAPPDATA/EQL Pathfinder/pathfinder.db" <scratch>/pathfinder.db
```

Then let the command-line tool (the game-data skill) dump History from the copy. **Run it from a worktree, or from a `git archive` of main unpacked in the scratchpad**: `dotnet run` builds into whichever checkout runs it, and the main checkout is the release lane, read-only for sessions.

```powershell
dotnet run -c Release --project src\EqlPathfinder.Cli -- import --db <scratch>\pathfinder.db --dump-history <scratch>\history.json
```

- **`import`** re-reads every log with the code you have, which is what a change to parsing needs. **`status`** resumes
  from the copy's checkpoint with the parse it already has: quicker, and enough for a change to a number that's worked
  out from stored rows.
- It prints the fragment value the dump was made at. Every script below takes **that** value. It's the recommended one
  (`Potential.RecommendedFragmentValue`) unless you pass `--frag`; pass the player's own value when their settings have
  `FragmentValueRecommended: false`. settings.json's `FragmentValue` means nothing while that's true.

## 2. Every visit

Run from `tools/analysis/numbers-check/`:

```sh
node recompute.mjs <db> <history.json> <fragment value> <recomputed.json>   # kills, T5-eq, chest, above-T5, times, idle, rates, DPS, kill XP
node usual.mjs <history.json> <recomputed.json>                             # farming and grouped: usual, runs, band, pct, verdict
node killspeed.mjs <history.json>                                           # leveling: kill speed and its word
node fullcheck.mjs <db> <history.json> <fragment value> <fullcheck.json> <log> [<log> ...]   # kills and loot from the raw log
```

- Each prints `<field>: N match, M mismatch` and the first mismatches. Everything should match. A mismatch is the
  engine or the docs being wrong: find which before changing either.
- `killspeed.mjs` tries three ways of choosing the earlier runs. **`levelingOnly` is the rule** (one-usual, 0.10.15);
  the other two show what a different rule would give.
- **The farm band comes from the docs.** docs/pace.md ("The farm band") gives it: 0.25 by default, held to 0.15-0.6, from
  the log spread of the runs once there are 5. `usual.mjs` copies those numbers from the doc, and the code (`FarmPace.For`)
  was checked to say the same on 11-19, so its match on band, pct and verdict holds the engine to the doc.
- The scripts take the character from the database's kills: one character a database, as the app keeps it.
- `fullcheck.mjs` reads the character's logs oldest first: the `.bak`, the dated splits, then the live log (the
  game-data skill says where; `head -1` gives each one's first time). About 10 seconds for 1.4 GB, and `import` about
  as long.
- **What it can't prove yet: kills by a charmed pet.** `fullcheck.mjs` follows charm only in part (its comment says
  which part; docs/fight-meter.md has the whole rule), and `analyze.mjs` not at all. On 11-19 the raw-log kills
  differed on 69 of 1,337 visits: the app counted more on 61 and fewer on 8, net 204 more of 86,163 in all. The ones looked at had a
  charmed pet (Befallen, Kedge Keep). `killdiff.mjs` lists a visit's slain lines and who killed each (docs/debt.md).
- On 11-19 everything else matched on 1,337 visits, apart from what [numbers-audit](../../../docs/specs/numbers-audit.md) lists.

## 3. One visit by hand

```sh
node row.mjs <history.json> <visit id>                                       # the app's numbers for it
node extract.mjs <log> "2026-11-18T22:40:00" "2026-11-18T23:15:00" <window.txt>   # the log lines in a window
node analyze.mjs <window.txt> "<zone as entered>" <zone-in ISO> <character> <fragment value> [--list]
node killdiff.mjs <db> <visit id> <window.txt>                               # slain lines against the kills table
```

- A visit's id is `v-<zone-in as yyyyMMddHHmmss>`, and its zone-in, zone-out and trips outside are in the `visits`
  table's `json`. Take a window a few minutes wider than the visit.
- Pick visits that differ: leveling, a farm, one at 50, a raid, an unfinished one, one with a trip outside, a death,
  kills after "completed", the latest.
- Windows of log lines hold chat and names: they stay in the scratchpad, outside the repository.
- **What it can't prove yet: place words.** A crawl's "<place> levels faster" and Fading (the place check, docs/pace.md) have
  no script: a faster place needs at least 2 visits at the level and your own place at least 1, weighed from earlier
  visits by their start, and none of that is recomputed from the log. They are held by `RunPaceTests` and `VerdictsTests`
  on built visits only (docs/debt.md has the row).
- **Checking the live log:** `status` keeps reading while you check, so the log grows after the dump. Cut it at the dump's
  last time first (the last kill's time in the copy's `kills` table) with `extract.mjs <log> <first time> <that time>
  <cut.txt>`, and give `fullcheck.mjs` the cut file in place of the live log.
