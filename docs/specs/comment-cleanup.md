# Comment cleanup (Batch B)

**Built 11-23.** Strips internal development-session provenance, spec-path breadcrumbs, and pre-public version archaeology from source comments while preserving technical invariants and game-domain context.

## Decisions

- `server/src/connection.ts` updates only a JSDoc comment; no runtime logic, schema, rate limit, or API contract changes.
- No test: comment-only edits across `src/` and `server/src/` with zero executable code changes (verified via full `tools/check.ps1` including build, formatting, Biome lint, 946 engine/page tests, and 54 server tests).

## Done when

- [x] Zero `docs/specs/` breadcrumbs remain in `src/` and `server/src/` code comments - verified by comment audit scan
- [x] Internal session dates/quotes (`(the user, 11-17: ...)`, `report #N`, `Modeling N`, pre-public version archaeology) stripped and verbose XML doc essays tightened across `src/` and `server/src/` - verified by `git diff` & audit scan
- [x] Formatting, analyzers, engine/page tests, and server tests remain green - `tools/check.ps1`

## Reviews

- architect: reviewed comment-only diff across `src/` and `server/src/`; confirmed zero executable code or behavior changes, real game-domain notes (Nov 10 patch, compatibility schemas) retained, and `tools/check.ps1` passes cleanly.
- designer: not needed, comment-only cleanup across source files — nothing drawn or shown to players changed.
