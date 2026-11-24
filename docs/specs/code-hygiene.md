# Code hygiene: formatting, lint and complexity checks for the C# and the JS/TS

**Review, 11-19 (v0.10.20). Not a change of its own:** this page is the index of the drafts it led to. Each draft is
solved in its own branch and session, in the order below, by the tier it names.

**Why:** the user, 11-19: "quality of life code hygiene updates to the codebase. I'm thinking linting, formatting,
cyclo complexity warnings/errors, import sorting, etc.. Whatever the best practices for modern C# development looks
like." Players see no change from the hygiene drafts themselves. Every session stops reshaping the last one's
layout, and the gate starts catching a kind of bug no test looks for today: [invariant-dates](invariant-dates.md)
is one.

## What the review found

Measured on 11-19 on a copy of `src/` and `tests/`, with Microsoft's recommended analyzers and the code-style rules
on in the build. Nothing in the repo was changed. Whoever builds a draft measures again first.

- **Nothing checks style today.** There's no `.editorconfig`, `Directory.Build.props`, analyzer beyond the
  compiler's, ESLint or Prettier, and `tools/check.ps1` has no style step. Line endings are mixed: of 215 `.cs` files,
  188 are CRLF, 27 LF, and 15 start with a BOM.
- **The formatter** (`dotnet format`) would change 82 of the 215 files, in about 680 places. 22 `using`s are unused.
- **About 215 analyzer warnings in 14 rules.** The biggest: culture (CA1305, CA1310) 72; underscores in names
  (CA1707) 32, every one a test's name; a narrower type would do (CA1859) 28; local functions that could be static
  (IDE0062) 18. Eight are nullable warnings the build already gives today.
- **Cyclomatic complexity (CA1502):** 120 methods are over 10, 23 over 25 and 13 over 40. The highest: the
  command-line tool's top level (166), `MeterViews.Build` (93), `AskPage.Build` (82), `OverlayWindow.DrawDone` and
  `DrawCrawl` (63, 61), `MainWindow.OnMessage` (58), `Campaign.Evaluate` (47), `Ask.Chain` (45).
- **Some culture warnings are bugs.** Times written for the machine follow the PC's regional settings. On a Finnish
  or Indonesian PC the stores write `14.03.22` and can't read it back, and on a Thai PC a share's day goes out as
  2569: [invariant-dates](invariant-dates.md).
- **JS and TS:** the app's 15 page scripts (4.3k lines), the Worker (9 files, 788 lines, 39 tests), the site's build
  and the tools' `.mjs` files. They share no config. `tools/webcheck.mjs` checks the page scripts for undeclared
  names only.

## The user's decisions (11-19)

- Draft it now and build it later, one branch per draft.
- ESLint and Prettier for the JS and TS too.
- Complexity fails at 25 for new code. Stricter limits come later, through targeted refactoring jobs.
- The culture bug is its own risky branch.
- A change that only lays code out differently shouldn't need the risky tier: "The code shouldn't change, and as
  long as we have good unit test coverage, I would feel comfortable." [format-only-gate](format-only-gate.md) makes
  finish prove the code didn't change.

## The drafts, and what each waits for

| Order | Draft | Tier, budget | Waits for |
|---|---|---|---|
| 1 | [format-only-gate](format-only-gate.md): finish proves a reformat compiles to the same thing, and then needs only the architect's look at the tool setup | Standard, 300k | nothing |
| 1 | [invariant-dates](invariant-dates.md): times and days written for the machine are the same on every PC, and the ones already written still read | Risky, 600k | nothing; not alongside cs-format (both change `PathfinderStore.cs` and `FightStore.cs`) |
| 2 | [cs-format](cs-format.md): `.editorconfig`, line endings, one reformat, the format check | Format only, 100k | format-only-gate |
| 2 | [js-format](js-format.md): Prettier for the JS, TS, CSS and JSON, and its check | Format only, 150k | format-only-gate |
| 3 | [cs-analyzers](cs-analyzers.md): the recommended analyzers, complexity at 25, a baseline that only goes down | Standard, 300k | cs-format |
| 3 | [js-lint](js-lint.md): ESLint with import order and complexity at 25, the same kind of baseline | Standard, 300k | js-format |

```
format-only-gate ─┬─► cs-format ──► cs-analyzers
                  └─► js-format ──► js-lint
invariant-dates   (any time, but not while cs-format is open)
```

A draft in one row may start while another in the same row is open. One that waits starts after what it waits for
is on main. cs-format and js-format both add a step to `tools/check.ps1`: the second to finish merges the first's.

**When to land the reformats:** each touches most files of its language, so a branch in flight with changes to the
same files conflicts. Land one when `feature.ps1 list` shows no branch ahead with changes in that language.
Otherwise the branch in flight merges main and runs the formatter on its own files.
