# A change to ship.ps1 is a risky one

**Why:** The risky list (`Test-NeedsDecision`) names `release.ps1` and `publish.ps1` but not `ship.ps1`, which runs
both every hour with nobody watching. A change's tier takes its floor from that list, so a change to the hourly
release could have gone through as a small one (the architect's review of lean-pipeline, 11-18).

## Done when
- [x] A branch that changes `tools/ship.ps1` is refused at `finish` without the architect's decisions - the role gate's samples, `tools\check.ps1 -SamplesOnly`

## Edge cases
- The pipeline itself (PowerShell 5.1, a clean copy): the list is one more name in a pattern that's already
  matched the same way, and the sample runs the function `finish` calls.
- A branch in flight that touches `ship.ps1`: there is none (`feature.ps1 list`), so nothing is newly refused.

## Not doing
- The gate's own scripts (`feature.ps1`, `pipeline-common.ps1`, `check.ps1`) stay off the list. The architect
  reviews every change to them with docs/engineering.md's three questions for the pipeline, and the user agreed
  to leave them there (11-18).

## Reviews
- architect: nothing to fix. The gate is the only caller of `Test-NeedsDecision`, `ship.ps1` loads the file every hour but calls neither function, and the sample runs what `finish` runs.
