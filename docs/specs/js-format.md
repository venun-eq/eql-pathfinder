# One layout for the JS and TS, kept by the gate

**Draft, 11-19** ([code-hygiene](code-hygiene.md)). **Tier:** format only, 150k. The architect reviews the new root
package, Prettier's config and the check. **Waits for:** format-only-gate. **Unblocks:** js-lint.

**Why:** the app's 15 page scripts (4.3k lines), the Worker, the site's build and the tools' scripts each have the
layout of whoever wrote them last, and nothing keeps it.
**Players:** see no change.

## Proposal

- A root `package.json` for dev tools only: Prettier now, ESLint in js-lint. Then the page scripts, `server/`, `site/`
  and `tools/` share one version and one config. `server/` and `site/` keep their own packages for their builds.
- A `.prettierrc.json`:
  - `embeddedLanguageFormatting: "off"`, because the page scripts build HTML in template strings and Prettier would
    otherwise rewrite them;
  - `endOfLine` to match `.gitattributes`;
  - a print width close to what the code uses today.
- A `.prettierignore` covering:
  - `node_modules/`, build output, `site/.cloudflare/` and generated files;
  - every `.html` file, because whitespace between inline elements shows on screen, and `site/privacy.html` is a
    risky path;
  - markdown;
  - `tools/feed.json` and `tools/signing.json`, which are release paths.
- One `prettier --write` commit, with that commit in `.git-blame-ignore-revs`. `tools/check.ps1` runs
  `prettier --check`, after an `npm ci` at the root in finish's clean copy.

## Done when

- [ ] `prettier --check` passes on the branch and fails on a sample with a stray space - step in `tools/check.ps1`
- [ ] Nothing that runs changed: format-only-gate's proof on the JS, TS and CSS
- [ ] `git blame` skips the reformat commit
- [ ] docs/engineering.md says how to run it

## Edge cases

- CSS (`app.css`, `site.css`) is on the designer's paths. The proof compares the minified CSS, so no designer is
  needed.
- `server/src/` is a risky path, and the proof is what lets it through.

## Not doing

- Lint rules and import order: js-lint.
- HTML and markdown.

## Where

- `package.json`, `package-lock.json`, `.prettierrc.json`, `.prettierignore` at the root; `.gitignore` gets
  `/node_modules`
- `tools/check.ps1`, docs/engineering.md
