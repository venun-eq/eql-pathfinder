---
name: queue-board
description: Draw the work queue as a board for the user and read back their moves. Use after a triage, after a finish that removed a queue row, or when the user asks to see the board or "the queue".
---

# The queue board

[docs/queue.md](../../../docs/queue.md) is the queue. The board is its picture, with Save for the user's moves, and
`tools/queue-board.mjs` draws it. The queue's order, rows and **You** entries live in that file only.

## Show it

1. Render to the scratchpad: `node tools/queue-board.mjs <scratchpad>\queue-board.html` (Node is in
   `C:\Program Files\nodejs`). It reads docs/queue.md, docs/releases/next and the last 3 docs/releases, so run it in a
   checkout whose docs/queue.md is the one to show (main, after the finish).
2. Publish with the Artifact tool to the existing queue board URL (keep the URL outside the repository, e.g. in your
   local notes, and omit `capabilities` and `icon` so it keeps its db).
3. Give the user the link.

## Read their moves

1. ArtifactData, action `get`, collection `board`, doc `overrides`, with `out_dir` in the scratchpad.
2. Its `text` field lists each column in order, with what they moved (`you`) and their notes. Its `source` is a short hash of
   the docs/queue.md text the board was drawn from: when it isn't the current text's, the page shows it as old text and
   the moves are for the lead to weigh by hand.
3. Apply them to docs/queue.md as **You** entries, in the branch doing the work (CLAUDE.md, "The funnel"). A moved
   card gets its new place and a You note saying so; product's notes go beside it, never over it.
   Once the moves are in docs/queue.md, delete `board/overrides` with ArtifactData (`delete`, with the `if_version`
   from the read), or the "older queue" banner shows on every republish.

## When it fails

`node tools/queue-board.mjs --check` names the lane, row and problem; `tools\check.ps1` runs it, so a queue.md edit
that breaks the board is red before it merges.
