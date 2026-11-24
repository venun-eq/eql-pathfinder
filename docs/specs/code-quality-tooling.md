# Code quality tooling (Batch A)

**Built 11-23.** Adds repository-wide linting, formatting, import/using autosort, CI enforcement, and an opt-in lightweight pre-commit hook.

## Decisions

- `server/src/sharing.ts` only adds a `biome-ignore` comment above `CONTROL_CHARS` and formatting/import ordering; no stored schema, request/response contract, rate limit, or privacy behavior changes.
- Roslyn `AnalysisLevel=latest-recommended` and `EnforceCodeStyleInBuild=true` are enabled in `Directory.Build.props`, with noisy globalization/micro-perf rules disabled in `.editorconfig` so the Release build stays at 0 warnings while keeping nullability, reliability, security, and xUnit diagnostics active.
- Biome (`@biomejs/biome` 2.6.8) formats and lints `server/`, `site/`, `tools/*.mjs`, and root/package JSON files with `lineEnding: "auto"` (so Windows CRLF and Linux LF checkouts both pass without spurious diffs), and lints `src/EqlPathfinder.App/web/js/*.js` in script-compatible mode without reformatting it (preserving `tools/check.ps1` literal guards).
- `.githooks/pre-commit` is opt-in (`git config core.hooksPath .githooks`) so local WIP commits stay fast while full verification runs in `tools/check.ps1` and `.github/workflows/ci.yml`.

## Done when

- [x] Root `.editorconfig` and `Directory.Build.props` configure C# formatting, `using` autosort, and Roslyn analyzers with 0 Release build warnings - `dotnet build -c Release` and `dotnet format --no-restore --verify-no-changes`
- [x] Root `package.json` and `biome.json` lint, format, and autosort imports across `server/`, `site/`, `tools/*.mjs`, and `web/js/` with 0 errors and 0 warnings - `npm run lint`
- [x] Real compiler/analyzer warnings fixed (`TrackerService.CampaignOptions` nullability, `CommunityData.For` nullable parameters, `EdgeSlotsTests` async wait, `PageLimits` OS guard, test nullability) - `dotnet test` (all 946 engine & page tests pass)
- [x] `.github/workflows/ci.yml` and `tools/check.ps1` enforce `dotnet format --verify-no-changes` and `npm run lint` - `tools/check.ps1 -All`
- [x] Opt-in `.githooks/pre-commit` added and documented in `CONTRIBUTING.md` - verified hook syntax and `.gitattributes` LF rule

## Reviews

- architect: reviewed `.editorconfig`, `Directory.Build.props`, `biome.json`, `tools/check.ps1`, `.github/workflows/ci.yml`, `.githooks/pre-commit`, and the 6 compiler-warning fixes; no runtime behavior changes, no privacy/server contract changes, and full `tools/check.ps1 -All` passes cleanly.
- designer: not needed, formatting, static analysis, and tooling configuration only — nothing drawn or shown to players changed.
