# A board of the work queue

**Why:** The user wants one picture of all the work. The "Pathfinder Work Queue" artifact was a one-off from the 11-20
triage and still shows answered questions. docs/queue.md stays the single source of truth (agents read it); the board
is a view of it plus a way for the user to override, never a second copy.
**Players:** nobody. This is a tool for the user: no change under `src/`, so no release note.

## Done when
- [x] `node tools/queue-board.mjs --check` fails on a malformed sample (missing lane, wrong cell count, no name) and says which - `tools\check.ps1` runs it against three samples
- [x] `--check` passes on today's docs/queue.md - `tools\check.ps1`
- [x] The page renders every row of queue.md in its column, with the right count - checked by rendering and comparing counts (queue-board.mjs `--check` also asserts it on the generated data)
- [x] `tools\check.ps1` runs the check next to the code map's
- [x] The rendered page's script passes `new Function` - `--check` does it on the inline script

## Edge cases
- "Empty." under a lane heading: no rows. Parked and Dropped may be bullet lists when they have no table: then a bullet is a row. Elsewhere a bullet is a note.
- A lane heading missing, a lane with neither a table nor "Empty.", a table under a heading that isn't a lane, a row with the wrong cell count, a row with no name, two rows with one name: named in the error.
- A row name that is a link: the name and the spec link both kept.
- Saved overrides are read only when their source hash matches the page's; older ones are shown as text only.
- No db (Save unavailable): "Copy as text" only.
- No waiting rows: no Needs you column. Inbox and Dropped show only when they have rows.

## Where
- `tools/queue-board.mjs`, `tools/queue-board/template.html`, `tools/check.ps1`
- `.claude/skills/queue-board/SKILL.md`; one line each in docs/queue.md, the feature skill, the designer agent.

## Reviews
- architect: two must fixes. (1) check.ps1's "cells" and "name" samples hold only `## Now`, so they fail on the seven missing lanes and the cell-count and no-name checks are unproven (output goes to nul, so "says which" isn't checked either): give each sample all eight lanes and one fault, and match its message. (2) parseQueue drops or invents rows with the gate green: a bullet list in place of a table outside Parked/Dropped gives 0 rows, a note bullet under Parked's or Dropped's table (the convention Now and Next use) becomes a card, and a table under a new `## ` heading is dropped; bullets count as rows only in a Parked/Dropped lane with no table, a lane with neither a table nor "Empty." is a problem, and so is a table under an unknown heading, with a sample for one. Should fix: hash queue.md's text for `source` instead of the last commit (the uncommitted-edit case is harmless today, since applySaved skips unknown names and puts unseen ones home, but a content hash removes it); the skill clears `board/overrides` after the lead applies them, or the "older queue" banner shows on every republish. Sound: pipes (escaped or not, the latter red naming the row, as GFM splits them too), links, backticks, the step's speed, failing closed, and the clean copy without git.
- engineer: all four fixed. The gate's samples are whole queues (eight lanes) with one fault each (missing lane, 7 cells, no name, bullets where a table belongs, a table under `## Blocked`) and each must fail with its own message. parseQueue: bullets are rows only in a Parked or Dropped lane with no table; a lane with no table and no "Empty." and a table under an unknown heading are named problems. `source` is a hash of queue.md's text (commit and date kept in the header). The skill deletes `board/overrides` after the moves are applied.
