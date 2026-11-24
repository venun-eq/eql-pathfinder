# The real-data numbers check, kept and run again

**Built 11-19** ([numbers-audit](numbers-audit.md)). **Tier:** small, 60k. Scripts under `tools/` get the architect's
review. **Unblocks:** every later math change can be checked against real play.

**Why:** on 11-19, an independent check recomputed every History number for 1,333 visits. It used the database's kill
and loot rows and the rules as the docs state them, not the engine's code. It also worked out 10 visits by hand from the
raw log. Everything matched apart from the three small issues in [numbers-audit](numbers-audit.md). It's the
strongest proof the math is right, and it couldn't be run again from the repository: its scripts were in a scratch
folder, and the History dump needed a line patched into the CLI by hand.

## Done when

- [x] The scripts are in `tools/analysis/numbers-check/` (`copydb`, `recompute`, `usual`, `killspeed`, `extract`, `analyze`,
  `row`, `fullcheck`, and `killdiff`, which the audit used for kills) and none calls the engine - read: they use
  `node:sqlite`, `fs` and `readline` only
- [x] Nothing personal or stale is written into them: the character comes from the database or an argument, the
  fragment value is an argument everywhere, with no default - `node recompute.mjs` with no arguments prints its usage
- [x] `import` and `status` take `--dump-history <file>` and print the fragment value they dumped at - on a copy of the
  live database (11-19, 1,335 visits): "history dumped to â€¦ at fragment value 3.56"
- [x] Run again from the repository, the check reproduces the audit - on that dump: `recompute.mjs` 1,335 of 1,335 on
  all 17 fields; `usual.mjs` 476 of 476 on verdict, usual, runs, band and pct; `killspeed.mjs` 53 of 53 by the
  leveling-only rule; `fullcheck.mjs` T5, chest and above-T5 1,335 of 1,335 except the Nagafen's Lair visit
  visit-boundaries is about
- [x] The skill says when to run it, and its steps - `.claude/skills/numbers-check/SKILL.md`

No test: the CLI has no test project, and the flag only writes what `TrackerService.History` already returns; its
proof is the check run on its dump (above).

## What running it again found

- **The raw-log kills pass didn't follow pets the app's way.** It learned only one-word pet names, so a charmed mob
  ("a stiletto fang piranha", Kedge Keep D2 on 11-11) wasn't yours. On 11-19 that left 116 visits apart. It now
  follows charm in part (docs/fight-meter.md: from "Attacking â€¦ Master." until it breaks, back within 2 minutes),
  which leaves 68: the app counts more on 61 and fewer on 7, 207 more of 85,529 kills in all. The one looked at after
  that (Befallen D4 on 08-02) charmed several mobs in turn, most landing as "has been charmed." with no "Attacking â€¦
  Master." line, which this pass doesn't take. So kills on charm visits aren't independently proven yet
  (docs/debt.md).
- The audit's "kills on 1,333 of 1,333" is the database's rows (`recompute.mjs`), not the raw log. The index says so
  now.
- **`status` alone isn't enough after a change to parsing:** it resumes with the database's parse. `import` re-reads
  (11 seconds for 1.4 GB of logs).

## Edge cases

- **The live database while the app writes to it:** `copydb.mjs` copies it with `VACUUM INTO`, one consistent read,
  never the file and its WAL by hand.
- **A player who set their own fragment value:** the dump is made at the CLI's value (the recommended one, or
  `--frag`), and prints it, so the scripts are given the value History was made at, not settings.json's.
- **Log windows hold chat and names:** the skill keeps them in the scratchpad.

## Not doing

- The whole charm rule (charms that land without "Attacking", one-word charms, "only its own targets count") in
  `fullcheck.mjs`: that's a second implementation of `LineParser`'s charm state, worth its own branch if charm visits
  need proof (docs/debt.md).
- Running the check from `tools/check.ps1`: it needs the player's database and logs.

## Where

- `tools/analysis/numbers-check/*.mjs`, `.claude/skills/numbers-check/SKILL.md`
- `src/EqlPathfinder.Cli/Program.cs` (`import`/`status`: `--dump-history`)

## Reviews

- architect: on Sonnet, no must fix; the scripts are independent of the engine, read-only, and nothing personal is left
  in them. Should fix: `usual.mjs`'s band constants come from `FarmPace.For`, not the docs, so that match proves less
  than it seems: the skill says so, and calc-docs is to put the band in docs/pace.md. The new debt row sat below the
  table: moved into it. One character a database: a line in the skill.
