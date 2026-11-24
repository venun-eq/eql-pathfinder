# Contributing to EQL Pathfinder

Thanks for taking a look at EQL Pathfinder. Everything builds and tests locally with no accounts, logins, or private
settings.

## Prerequisites

- **Windows 10 or 11** (for the WPF desktop app and headless Edge page tests)
- **.NET 10 SDK**
- **Node.js 22.18+** (for `tools/webcheck.mjs`, `server/`, and `site/`)

## Build & Test Locally

```powershell
# Build the whole solution (Engine, App, Cli, Tests)
dotnet build EQLPathfinder.slnx -c Release

# Run engine, golden-log, and headless-Edge page tests
dotnet test tests/EqlPathfinder.Engine.Tests -c Release --no-build

# Verify page scripts
node tools/webcheck.mjs

# Run the full local gate (self-tests, privacy guard, build, tests, webcheck)
.\tools\check.ps1
```

To run your development build of the app alongside an installed copy without touching its data:

```powershell
$env:EQLPATHFINDER_DATA = "$env:TEMP\pathfinder-dev"
dotnet run --project src/EqlPathfinder.App
```

### Community API (`server/`) and Website (`site/`)

Neither requires Cloudflare credentials or database IDs for local development or tests:

```powershell
cd server; npm ci; npm run typecheck; npm test
cd site;   npm ci; npm run build
```

### Linting & Formatting

- **C#** uses `.editorconfig` and `Directory.Build.props` (Roslyn analyzers + sorted `using` directives):
  ```powershell
  dotnet format EQLPathfinder.slnx
  dotnet format EQLPathfinder.slnx --verify-no-changes
  ```
- **TypeScript, JavaScript, and JSON** use [Biome](https://biomejs.dev/) (`biome.json`):
  ```powershell
  npm ci
  npm run lint     # check formatting, lint rules, and sorted imports
  npm run format   # auto-format and organize imports
  ```
- **Optional pre-commit hook** (fast checks on staged files + privacy guard):
  ```powershell
  git config core.hooksPath .githooks
  ```

## Making Changes

- **Open an issue before a pull request.** This repository is a copy of the working one, and changes are carried over
  by hand, so a pull request can't be merged as it is. Issues are welcome:
  [GitHub issues](https://eqlpathfinder.com/github/issues).

- **Keep it private-safe:** Never commit personal game logs, real character names, local paths (`C:\Users\<you>`), or
  private addresses. `tools\check.ps1` checks tracked files for home paths and LAN addresses automatically.
- **Add tests for behavior changes:** Engine changes go in `tests/EqlPathfinder.Engine.Tests`; Worker changes go in
  `server/test/`.
- **Keep images out of git:** Put review screenshots and visual mockups in your Pull Request description rather than
  committing large PNGs to `docs/specs/`.
- **Privacy promise:** Any change affecting what leaves the PC or what `server/` stores must keep
  [site/privacy.html](site/privacy.html) in exact step with the code.

See [docs/README.md](docs/README.md) for architecture and math notes, and [SECURITY.md](SECURITY.md) for reporting
security issues.
