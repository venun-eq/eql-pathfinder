# ESLint for the JS and TS, with import order and complexity at 25

**Draft, 11-19** ([code-hygiene](code-hygiene.md)). **Tier:** standard, 300k. It changes no code in a risky path:
violations there go in ESLint's suppressions file instead. **Waits for:** js-format.

**Why:** the page scripts, the Worker, the site's build and the tools' scripts have no lint. `tools/webcheck.mjs`
catches undeclared names in the page scripts and nothing else.
**Players:** see no change.

## Proposal

- ESLint, with one flat `eslint.config.js` at the root and its packages in the root `package.json` from js-format:
  - `@eslint/js` recommended everywhere;
  - `typescript-eslint` recommended for `server/` and `site/`, type-aware where a `tsconfig.json` exists;
  - browser globals for the page scripts and Node's for `tools/`;
  - `eslint-config-prettier`, so ESLint and Prettier don't fight.
- `complexity` at 25, the same as the C#.
- Import order with `eslint-plugin-simple-import-sort`, which fixes itself, wherever files use `import`. Check
  whether the page scripts are loaded by `<script>` tags. If they are, it doesn't apply to them.
- **Existing violations:** ESLint's bulk suppressions (`eslint --suppress-all` writes `eslint-suppressions.json`, and
  a count there can only go down). `server/src/` stays suppressed until a branch of its own touches it.
- Outside risky paths, fix what the autofix handles safely. The page scripts are on the designer's paths:
  `designer: not needed, <why>` where nothing on screen changes.
- `tools/check.ps1` runs `eslint .` with no warnings allowed.
- Keep `tools/webcheck.mjs`. It checks the names the C# side sends against what the pages declare, which ESLint
  can't see.

## Done when

- [ ] A sample with a new violation fails `tools/check.ps1`, and the branch passes - sample
- [ ] A function over 25 that isn't suppressed fails - sample
- [ ] Imports out of order fail, and `eslint --fix` sorts them - sample
- [ ] docs/engineering.md says how to run it and how to prune suppressions

## Not doing

- Fixing violations in `server/src/` or the release scripts.
- Lint for HTML or CSS.

## Where

- `eslint.config.js`, `eslint-suppressions.json`, the root `package.json`
- `tools/check.ps1`, docs/engineering.md
- It changes `src/` when it fixes the page scripts, so then it needs a release note that says players see no change
