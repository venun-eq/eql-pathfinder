# Engineering: how this code is modeled, written and reviewed

The `engineer` and `architect` agents' page (docs/roles.md). The engineer builds by it. The architect decides by it
before code and reviews by it afterwards. docs/code-map.md says where things are, and docs/debt.md what's owed.

## Modeling

1. **Store and share facts. Work numbers out when they're shown.** A crawl's kills, rewards, pauses and end are
   facts. Its levels an hour is worked out. The community job once took each app's worked-out numbers, so a math fix
   reached only crawls that were sent again. A shared unit's hash is its facts and schema alone, or every math
   change re-sends every history. docs/pace.md has the rest.
2. **A number has a source and a smallest sample.** Below the sample, say what there is and when the number starts
   (`Rates.MinKills`, "3 farming crawls"). The page says where a number comes from.
3. **Don't compose a prediction from parts that don't compose.** A trio's pace isn't the sum of its classes'. That
   forecast was removed in 0.9.37, and it stays out.
4. **The engine decides, the page draws.** A threshold, a choice of words, or any "if" about the player's data
   belongs in the engine, where tests reach. Page scripts format and lay out what they're told. Only what a
   page test opens is tested as drawn (docs/quality.md says which, docs/debt.md what's left), so logic anywhere else
   in a page script ships unproven.
5. **One writer per database.** A copy on its own data folder never acts as the real install: it sends no usage
   counts and uploads no shares.
6. **Old logs are read again when what's read from them changes:** raise `EngineState.CurrentVersion` (the fight
   meter has its own), and the release note says `reread: true`. A test holds it: `RereadGuardTests` keeps each
   version beside a hash of what its reader makes of the fixture, and fails when one moves without the other.
7. **A limit is a fuse.** It has its reason written beside it, and it doesn't count what costs nothing: the daily
   upload limit skips identical re-sends.
8. **One quantity, one formula.** Active time, a rate an hour, T5-eq and "too early to say" each have one function, and
   the pages, the overlay and the community job all call it. A second copy, in C# or in a page script, drifts at the
   next rule change. The calculation audit (11-18) found five copies of the 6-minute gate. Not yet true everywhere:
   docs/specs/calc-audit.md lists what's owed.
9. **Every rate on a visit divides by its active time,** DPS included (the user, 11-18). Owed: docs/specs/one-rate.md.
10. **A verdict compares with what came before it.** A visit's usual is made of earlier visits only, so its verdict
    doesn't change once it's over (the user, 11-18, over "every other visit"). Owed: docs/specs/usual-from-earlier.md.
11. **Nothing seen while playing is hidden from a rate. Unknown isn't zero** (the user, 11-18). A kill is its slain
    line. One that gave no XP is a 0 in XP per kill, as it is in kills and K/min, so XP per kill × kills an hour stays
    XP an hour. One whose XP the log doesn't give (an XP line with no `(x%)`) is unknown: it's left out of XP per kill,
    the app's and the community job's, and counts as a kill everywhere else. docs/specs/kills-at-cap.md.
12. **What the player is on now is one state the whole app reads.** The loadout on (its trio, race and level, and how
    each is known) is the engine's, once. Home, the fight meter, the overlays, Ask and the tracker read it; none works
    it out for itself, and a feature never keeps its own copy (the user, 11-19: "I would rather the entire
    application's state knew the current loadout, as other features could use it in the future"). Owed:
    docs/specs/current-loadout.md.
13. **What an expansion changes is data with the day it starts.** The level cap, the token's list of crawls and any
    other game limit an expansion moves live in one list of (from this day, this value), and everything reads it by
    the day the play happened. The next expansion is then one more entry and a test, not a branch (the user, 11-20:
    "This won't be the last expansion that raises the level cap").

## Decisions the architect takes before code

For each that the change touches, the spec's "## Decisions" gets a line or two with the reason.

| Question | What to settle |
|---|---|
| **What's stored** | A new column or table: `CREATE TABLE IF NOT EXISTS` never changes an existing database (docs/code-map.md, recipe 7). Does it need a re-read? |
| **What's sent** | Does anything new leave the PC? Never a name, chat or a clock time. `site/privacy.html` says it in the same branch. Is it a fact or a worked-out number (rule 1)? |
| **Who else reads it** | Older apps still upload and still read the community file, and the NAS job reads every shared unit. Add fields, never change what one means. The release note's `job: true` when the job needs the new build |
| **The server** | Migration first, then the Worker; staging first; both by hand after the user says so (CLAUDE.md). What does the new Worker do with rows from before the migration? |
| **How updates arrive** | A broken updater strands every player on that version. What happens to a copy that's mid-update, offline or two versions behind? |
| **Limits and cost** | What does a runaway copy, a re-send or ten times the players do to it (rule 7)? |
| **Taking it back** | After it's released, how would it be undone? If it can't be, the user decides, not the architect |

## Writing it

- **The smallest change that meets the spec,** in the style of the code beside it. A refactor nobody asked for goes
  to docs/debt.md, not into the change.
- **Page scripts share one global scope across files.** Declare state in the file that owns it. Only core.js listens
  to the window; other scripts use `onHost`. A message with a `kind` is an answer and never the page data. Every
  time shown goes through `dt` or `clockShown`, so a report's screenshot can hide it.
- **A rate is worked out once, in the engine** (`Tracking/Rates.cs`: `Has`, `PerHour`, `PerMinute`, `PerSecond`; active time in
  `ActiveTime`, T5-eq in `Valuation`). It is null when a visit is not a finished crawl and has under `Rates.MinKills` kills, and
  null is "—", never 0. A page draws the number it is sent and divides nothing; a new rate calls `Rates`, and a test in
  `RateRuleTests` says so (docs/specs/one-rate.md).
- **Comments say why,** with the case that taught it: "0.9.37 shipped core.js setting `planLocal` after plan.js was
  gone".
- **Test names are sentences** about what the player gets.
- **PowerShell here is Windows PowerShell 5.1:** `-match` overwrites `$Matches` (use `[regex]::Match`), piped text
  gets a byte-order mark, and a native command's stderr is an error under `Stop` (the `git` wrapper in
  `tools/pipeline-common.ps1`). No `&&`, `??` or ternary.
- **Keep the pages true:** a renamed file, message or cross-file global changes its line in docs/code-map.md
  (`tools/mapcheck.mjs` fails when a name on that page is gone from the code), and how something works goes on its
  docs page.

## What the architect's review looks for

In this order. The first three are where the bugs have come from.

1. **Tests that prove it.** For each "Done when" line: would its test fail if the feature broke? A test that reads
   source text, or checks what a stub returns, wouldn't. Were tests changed after the commit that added them, and
   did they keep their meaning? On a small or standard change one hand wrote the test and the code, so this is the
   question that matters most.
2. **The spec's edge cases,** each followed through the code, and any in docs/quality.md that the spec missed.
3. **The modeling rules above,** most of all a worked-out number stored or sent, a guess shown as a fact, and logic
   in a page script that the engine should own.
4. **Two things at once:** the tracker's loop and a query, two processes on one database, start-up and shut-down in
   the middle of a read.
5. **Everyone else:** older apps, the NAS job, an isolated copy, a player with no data.
6. **Privacy:** anything newly sent, logged or captured.
7. **A simpler way** to do the same thing, and whether the change made an item in docs/debt.md worse.

For a change to the pipeline itself (`tools/`, the hooks, the settings), three more. They found most of what was
wrong with the roles' own first version. (The rules as prose, which are CLAUDE.md, the agents and the `feature`
skill, are read by the user and not by the architect.)

A review is of this change and no more: what it didn't cause is a line in docs/debt.md, and the limits on what a
review reads and runs are in `.claude/agents/architect.md`.

- **Does it fail open or closed?** When a check can't run or can't read its input, does the branch get through?
  A guard on an edit should let work go on. A gate before main shouldn't.
- **Does the sample run what really runs?** A sample that calls a script directly passes with a mistyped name in
  the settings that call it.
- **Who else runs this?** Worktrees already in flight have the old scripts, `ship.ps1` loads
  `pipeline-common.ps1` every hour, and `finish` checks in a clean copy with nothing built.

A claim about Claude Code itself (a settings key, a front-matter field, what a hook is given) is checked against
its documentation (the `claude-code-guide` agent) or a run with `claude --debug`, never taken from memory.
