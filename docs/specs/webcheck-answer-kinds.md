# The page-script gate sends the six answers it skipped

**Why:** `tools/webcheck.mjs` never sends the answers `loot`, `ask`, `upgrade`, `shareStatus`, `sharePreview` or
`usagePreview`, the ones where core.js's handler writes names that other files declare. Rename one of those names and
miss core.js, and the gate passes: run today on a copy with `lootRows` renamed in core.js, it prints "15 scripts load"
and exits 0, though a `loot` answer then throws "lootRowsRenamed is not defined". It's docs/debt.md's second row.
Four places also say a 0.9.38 shipped with pages losing their data. There was no 0.9.38.
**Players:** nobody sees it. It protects everyone who uses Loot, Ask, Upgrades, the sharing card or Settings' usage
preview: the slip would leave that answer undrawn in a release.

## Done when
- [x] `node tools/webcheck.mjs` exits 1 when an answer of one of the six kinds reaches a name nothing declares, and its output has the kind in quotes and the name (`a "loot" answer: lootRowsRenamed is not defined`) - the "page scripts gate" sample in `tools/check.ps1`, eight names (`.\tools\check.ps1 -SamplesOnly`)
- [x] It still fails scripts that take an answer for the page data - the same sample, the copy without `if (e.data?.kind) return;`
- [x] The real scripts pass, and nothing under `src/` changed - the "page scripts" step of `.\tools\check.ps1`, and `git diff --name-only main...HEAD -- src` prints nothing
- [x] No page says a 0.9.38 shipped, and each says what did (the facts below) - `git grep -n "0\.9\.38" -- docs/debt.md docs/quality.md docs/roles.md tools/webcheck.mjs` prints nothing
- [x] The debt row is gone and the code map no longer says the kinds aren't sent - `git grep -n "never sends" -- docs/debt.md docs/code-map.md` prints nothing
- [x] An answer added later is sent with no list to add it to: webcheck reads the kinds from the scripts' text, and exits 1 on a copy where a new `raceTable` branch in core.js's handler writes a name nothing declares (`a "raceTable" answer: raceTableRows is not defined`) - the same sample, the copy with the new answer
- [x] It fails a script that doesn't parse, naming the file (`js/boot.js doesn't parse`), and that debt row is gone - the same sample, the copy with a stray `function (` at the end of boot.js

## The facts, for the rewording
Checked against git and by running today's webcheck on each release's scripts.

- **No 0.9.38.** v0.9.37 is c07dfb3, the next release is v0.10.0 (90b4e9c), and `docs/releases/` has no 0.9.38.
- **0.9.37's leftover name shipped.** On c07dfb3's scripts webcheck says "data message: planLocal is not defined".
  What the four places say about 0.9.37 is right.
- **Campaign's options never shipped broken.** The answer that replaced the page data came in 8f504fb and was fixed
  in a9453c7, both between 0.9.37 and 0.10.0.
- **What did ship:** from 0.9.9 to 0.9.37, core.js took any message whose kind it didn't know for the page data, and
  Help's `roadmap` and `feedbackSent` answers were two (both handlers are in v0.9.9, dec7d2e; `roadmap`'s came in
  860abf5). On 0.9.35's and 0.9.36's scripts (de30fb5) a `roadmap` answer replaces `data`. 0.10.0's note told players "opening Help could leave other pages without their data until the next update".
  Nobody reproduced what a player saw, so write "could", as the note does, and not "every page lost its data".

## Edge cases
- **A message that isn't the page data, and a name no script declares:** every kind the scripts test goes empty (`{ kind }`), after the
  data message, like the answers sent today. A handler may trip over the empty answer (`ask`, `upgrade` and
  `sharePreview` throw a TypeError in their render today): still fine. Only a ReferenceError fails.
- **The pipeline itself (a clean copy, PowerShell 5.1):** the sample copies `index.html` and `js` to the temp folder
  and removes them, writes nothing into the checkout, needs no `node_modules`, and runs node through cmd so stderr
  isn't an error under Stop. Seen failing under `powershell.exe` 5.1.
- **A second copy:** webcheck's folder argument is for the samples. With none it checks this checkout's scripts.
- **A branch moves out of core.js later:** the sample stops with "core.js no longer uses the name ..." and doesn't
  pass on a copy it didn't change.
- **Something leaving the PC:** nothing is sent, and the answers hold no name.

## Not doing
- Anything under `src/`, so no release note. A probe showed the real scripts take all six with no ReferenceError.
- What a handler does past the point where the empty answer trips it, and names it calls there (`syncShareCard`
  after `renderShare`). Real answers and what's drawn are the page-test harness, debt row 1, which stays.
- A ReferenceError hidden behind an earlier listener's TypeError (`dispatchHost` throws only the first error).
  core.js's listener runs first and takes all six, so nothing hides today.
- A kind the scripts test some other way than `data?.kind === "..."` (a `switch`, a lookup table): webcheck wouldn't
  read it. The eight-name sample fails if that happens to core.js's six.
- The fixture's character name, and moving page data into the engine: later slices.

## Where
- `tools/webcheck.mjs`: the answers loop (line 49) and the comment above it (line 46). The folder argument (line 10)
  is the stub the sample needed, as `tools/mapcheck.mjs` takes its map.
- `tools/check.ps1`: the "page scripts gate" sample, already written.
- `docs/debt.md`: the `tools/webcheck.mjs` row goes; the first row's cost (line 10) is reworded.
- `docs/quality.md:50`, `docs/roles.md:57`: reworded.
- `docs/code-map.md:114` and `:137`, `docs/pipeline.md:18` (check.ps1's row): what webcheck sends and what the
  samples now cover.

## Reviews
- architect: no must-fix, and the sample was confirmed failing without the change. It found Help's history starting too late (0.9.9, not 0.9.36), the hand-kept list of kinds letting a new answer through, and webcheck passing a script that doesn't parse. All three fixed: the five places reworded, the kinds read from the scripts' text, a parse failure reported, with a sample copy for each of the last two. One debt line logged (core.js's comment), one logged and paid here.
