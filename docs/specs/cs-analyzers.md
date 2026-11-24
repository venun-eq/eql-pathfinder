# Analyzers in the build, complexity at 25, and a baseline that only goes down

**Draft, 11-19** ([code-hygiene](code-hygiene.md)). **Tier:** standard, 300k. It changes no code in a risky path:
warnings there go in the baseline instead. **Waits for:** cs-format.

**Why:** the build hides about 215 analyzer warnings, and some of them were real bugs
([invariant-dates](invariant-dates.md)). 23 methods have a cyclomatic complexity over 25, and nothing stops a 24th.
**Players:** see no change. The culture rules guard numbers and dates that would read differently on a PC set to
another language.

## Proposal

- A `Directory.Build.props` at the root with:
  - `AnalysisLevel` pinned to `10.0-recommended`, not `latest`, so a new SDK can't add rules without anyone choosing
    them;
  - `EnforceCodeStyleInBuild`;
  - `GenerateDocumentationFile`, with the doc-comment warnings (CS1591 and its family) off. The build needs it to
    report unused usings (IDE0005).
- CA1502 on at 25, set in a `CodeMetricsConfig.txt` (`CA1502: 25`).
- CA1707 off under `tests/`, because test names are sentences.
- **A baseline rather than a clean-up:** a checked-in file of (file, rule, count) from the build. The gate fails when
  a pair is new or a count goes up, and asks for the file to be lowered when one goes down. Warnings in risky paths
  (`Storage/`, `Sharing/`, `Community/`, `FightStore.cs` and the rest) stay in it until a branch of their own
  touches those files. The 23 complex methods stay in it until a refactoring job takes one.
- Outside risky paths, fix what's mechanical: unused usings, static local functions, CA1859, CA1822, CA1861, CA1869
  and CA1854. Culture in code that displays something: make `CultureInfo.CurrentCulture` explicit, so nothing a
  player sees changes. Culture in data goes to invariant-dates, or into the baseline.
- Warnings fail only in `tools/check.ps1` (`-warnaserror` there), never in the build `ship.ps1` runs, so an SDK
  update can't stop an unattended release.
- The eight nullable warnings the build gives today: fix them or add them to the baseline.

## Done when

- [ ] A sample with a new analyzer warning fails `tools/check.ps1`, and the clean branch passes - sample
- [ ] A method over 25 that isn't in the baseline fails the gate - sample
- [ ] A count that goes down passes and says to lower the baseline. A count that goes up fails - samples
- [ ] `ship.ps1` and `release.ps1` aren't in the diff, and the Release build only warns
- [ ] docs/engineering.md says how to read the baseline, and that refactoring a method takes it out

## Edge cases

- The WPF project compiles twice (the `_wpftmp` pass). The baseline counts each warning once.
- `MeterWindow.cs` and `OverlayWindow.xaml.cs` are on the designer's paths:
  `designer: not needed, the culture is now explicit and the output is the same`.

## Not doing

- Refactoring the 23 complex methods. Targeted jobs do that later (the user, 11-19).
- Sonar's analyzers. Cognitive complexity measures this better than cyclomatic, but it comes with 400+ rules and
  another package.
- A limit stricter than 25.

## Where

- `Directory.Build.props`, `CodeMetricsConfig.txt`, `.editorconfig`, the baseline file (beside `tools/check.ps1`)
- `tools/check.ps1`, docs/engineering.md, the files with the mechanical fixes
- It changes `src/`, so it needs a release note, which says players see no change
