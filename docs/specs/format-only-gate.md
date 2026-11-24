# A reformat proves it changed nothing, and skips the tiers

**Draft, 11-19** ([code-hygiene](code-hygiene.md)). **Tier:** standard, 300k. It changes the gate's scripts (the
architect reviews) and the rules as prose (the user's go-ahead on the diff is the review). **Waits for:** nothing.
**Unblocks:** cs-format, js-format.

**Why:** the user, 11-19: "Perhaps we should be able to circumvent 'risky' tier when it's just lint/formatting. The
code shouldn't change, and as long as we have good unit test coverage, I would feel comfortable." Today the paths
set the tier, so reformatting `PathfinderStore.cs` or `server/src/` asks for qa, decisions before code, a failing
test and a release note, for a diff that does nothing.
**Players:** see no change. Nothing ships either: with no note waiting, `ship.ps1` doesn't release (line 89), and
the reformatted code goes out with the next feature's release.

## Proposal

- A spec marks its branch `**Format only.**` Then finish doesn't take it on trust. It builds the branch and the
  commit it left main from, and compares what they compile to:
  - **C#:** both built with `Deterministic`, `DebugType=none`, `IncludeSourceRevisionInInformationalVersion=false`
    and a `PathMap`, so neither the commit nor the folder ends up in the assembly. Every assembly is the same, byte
    for byte.
  - **JS, TS, CSS:** each changed file is minified on both sides, with whitespace and comments dropped, and gives the
    same bytes. Comparing parse trees without positions or comments would do as well.
  - **Anything else** under `src/`, `server/src/` or `site/` (HTML, XAML outside a build, images) fails the proof.
- When the proof holds: no Decisions, no designer, no failing test, no release note. The architect still reviews
  what isn't source: the formatter's config, the package files, the scripts.
- When it fails: finish refuses, names the files whose output changed, and the branch takes its paths' tier.
- The tool configs go on the architect's paths in `Get-RequiredRoles`: `.editorconfig`, `.gitattributes`,
  `.git-blame-ignore-revs`, a root `package.json`, `eslint.config.*` and `.prettierrc*`. Today none of them is on any
  path, so a change to one would need no review.

## Done when

- [ ] A sample C# file with only whitespace changed passes the proof, and one with a constant changed fails it and
  names the assembly - samples in `tools/check.ps1`
- [ ] The same for a JS file: a reflowed sample passes, one with a string changed fails - samples
- [ ] A format-only spec that touches `server/src/` asks for the architect only, with no decisions and no note -
  role-gate sample
- [ ] Each tool config asks for the architect - role-gate samples
- [ ] CLAUDE.md's tier table, docs/roles.md, the `feature` skill and docs/specs/README.md say what "format only"
  means and what proves it - the user's go-ahead on the diff

## Edge cases

- `[CallerLineNumber]` or `#line`: a moved line changes the IL, so the proof fails. That's the safe way to fail: the
  branch leaves that file out.
- A formatter that rewrites a string, such as Prettier formatting HTML in a tagged template, changes the minified
  output and fails. js-format turns that off.
- A branch that reformats and changes one line fails, as it should. Split the change off.
- The proof's second build runs only for a branch marked format only.

## Not doing

- Lint fixes that change code. They keep their paths' tier. The lint drafts baseline the warnings in risky paths
  rather than fix them there. Loosening that because of test coverage would need coverage measured first, and
  nothing measures it today.
- Running the proof on every branch automatically.

## Where

- `tools/pipeline-common.ps1`: `Get-RequiredRoles` (the configs), `Read-Spec` and `Get-RoleProblems` (the marker), a new
  function for the proof
- `tools/feature.ps1`: `finish` and `ready` run the proof; the release-note checks (lines 96 and 114) skip a branch
  it holds for
- `tools/check.ps1`: the samples
- CLAUDE.md, docs/roles.md, `.claude/skills/feature/SKILL.md`, docs/specs/README.md
