# A draft starts under its own name

**Why:** `feature.ps1 start` refused any name whose spec is on main, to keep a finished change's record its own. The
calc-audit's ten drafts (11-18) are on main before anyone builds them, so `start reread-guard` said "A finished change
was called reread-guard", and every draft would have said the same.
**Players:** none. Every session that builds a draft.

## Done when

- [x] A spec whose first line after the title starts `**Draft` is a draft, and nothing else is - the samples in
  `tools/check.ps1` (`Test-DraftSpec`); they fail before it exists. Real files: `reread-guard.md` reads as a draft and
  `calc-audit.md` (an index, "Audit") does not
- [x] `start` takes a draft's name, still refuses a finished change's, and says to replace the "Draft" line with what
  was built - `start reread-guard` once this is on main
- [x] docs/specs/README.md says a draft is built under its name

## Edge cases

- **Same name, different thing:** a finished spec that mentions a draft further down (its Why, its notes) is not a
  draft: only the first line after the title counts (a sample). A file with no title isn't one either.
- **A built draft that still says "Draft":** `start` would take its name again. The README and `start`'s message say to
  replace that line; `finish` doesn't check it, because a trivial branch (tests, docs) has no spec check to hang it on.
- `finish` already handles a spec that's on main: it reads the reviews main has as the last change's, so a draft's
  reviews (it has none) can't stand in for this branch's.

## Reviews

- architect: no must fix. Fails closed (an unreadable or empty spec is not a draft, so `start` refuses); the samples call `Test-DraftSpec` directly and would fail if it were removed or loosened, `start`s wiring is proved only by running it. Should fix: the comment above `Get-MainSpec` in feature.ps1 still says "empty unless an earlier change with this name was finished"; a draft is on main too. Fixed by the lead.
  `start reread-guard` run from this branch's script: it started the worktree and said to replace the "Draft" line;
  `start calc-audit` was still refused.
