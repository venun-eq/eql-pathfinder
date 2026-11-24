# One layout for the C# code, kept by the gate

**Draft, 11-19** ([code-hygiene](code-hygiene.md)). **Tier:** format only, 100k. The architect reviews
`.editorconfig`, `.gitattributes` and the check, and the source needs no review. **Waits for:** format-only-gate.
**Unblocks:** cs-analyzers. Not alongside invariant-dates, since both change `PathfinderStore.cs` and `FightStore.cs`.

**Why:** 82 of 215 C# files don't match what the formatter would make of them (about 680 places), 22 `using`s are
unused and line endings are mixed. Each session's edits drift the layout further, and every diff carries layout
noise for its reviewer to read past.
**Players:** see no change.

## Proposal

- An `.editorconfig` at the root. Start from `dotnet new editorconfig` and set it to what most of the code already
  does: indent, braces, namespace style, `var`, with `System` usings first. Style rules stay suggestions here, and
  cs-analyzers makes the ones it wants into warnings.
- A `.gitattributes` with one line ending per kind of file. Pick what most files use today, but keep `*.sh` LF: the
  NAS runs `server/nas/*.sh`. Leave `tools/*.ps1` out of this branch. `ship.ps1` and `release.ps1` are risky paths and
  aren't compiled, so the proof can't cover them.
- `dotnet format` over the solution in a commit of its own, with that commit in `.git-blame-ignore-revs`.
- `tools/check.ps1` runs `dotnet format --verify-no-changes` before the build.
- Optional: a `global.json` that pins the SDK's feature band, so a new SDK can't change the formatter's output and turn
  the gate red on code nobody touched.

## Done when

- [ ] The format check passes on the branch and fails on a sample with a stray space - step in `tools/check.ps1`
- [ ] Nothing compiled changed - format-only-gate's proof
- [ ] `git blame` skips the reformat commit - `.git-blame-ignore-revs`, with `git config blame.ignoreRevsFile` in
  docs/engineering.md
- [ ] docs/engineering.md says how to run the formatter

## Not doing

- Analyzer rules and complexity: cs-analyzers.
- Formatting XAML.

## Where

- `.editorconfig`, `.gitattributes`, `.git-blame-ignore-revs`, maybe `global.json`, at the root
- `tools/check.ps1`, docs/engineering.md
