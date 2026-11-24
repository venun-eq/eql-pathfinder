# eqlpathfinder.com

The website: a landing page with the download link, Privacy and What's new. Plain HTML and one stylesheet, no
scripts, served by a Cloudflare Worker with static assets only (`cloudflare.config.ts`), built by Vite. `www.` and
`http://` redirect to `https://eqlpathfinder.com` (rules in `infra/cloudflare`).

No framework on purpose: four static pages need no JavaScript. If the site grows into many pages (guides, docs), the
next step is Astro (static output, shared layouts, Markdown content), not a React framework.

| File | What |
|---|---|
| `index.html`, `privacy.html`, `changelog.html`, `404.html` | The pages. `privacy.html` must change whenever what the app sends changes (and its date) |
| `partials/` | The shared `<head>` lines, header and footer, filled in where a page says `<!-- head -->`, `<!-- header -->`, `<!-- footer -->`. A page's `<body data-page="…">` marks its header link as the current page |
| `build/site-plugin.ts` | The build step (TypeScript, typechecked): partials, What's new, responsive screenshots |
| `../docs/releases/*.md` | Every release's note, written by `tools/release.ps1`. `changelog.html`'s `<!-- changelog -->` becomes the stable ones, newest first |
| `img/*.png` | Screenshot sources. Each `<img src="img/x.png" sizes="…">` becomes WebP: the full width lossless, narrower widths at quality 90 when they save bytes (made into `gen/`, ignored by git) |
| `site.css` | The app's palette; no inline styles (the Content-Security-Policy forbids them) |
| `public/` | Copied as is: `_headers` (security headers, caching), favicon, `og.png` (link previews), robots.txt, the font's license |

## After a release

`tools/release.ps1` writes `docs/releases/<version>.md`. Commit it with the version bump, then:

```powershell
cd site
npm run deploy
```

## Working on it

Needs Node.js 22.18 or later; `npm install` once. `npm run preview` builds and serves the site at http://localhost:4173
the way Cloudflare will (headers, `/privacy` without `.html`, the 404 page). `npm run deploy` typechecks, builds and
publishes it with the `cf` CLI (`npx cf auth login` once).

## Screenshots

The screenshots come from the app's own snapshot tool run over a demo copy of a real log, so they show real play under a
made-up character name ("Kestrel"). To redo them:

1. Make the demo game folder: a folder with `dbstr_us.txt` and `spells_us.txt` copied from the game folder, and a `Logs`
   folder holding copies of the logs with the character's name replaced everywhere, in the file names too.
2. Run the snapshot tool over it twice, each time with a fresh database and its own output folder:

   - **Full logs:**

     ```powershell
     $env:EQLPATHFINDER_LOGS = "<demo folder>\Logs"
     $env:EQLPATHFINDER_PREVIEW_BEFORE = "2026-11-16 22:30"
     EQLPathfinder.exe --snapshot <full> <full>\pathfinder.db
     EQLPathfinder.exe --meter-snapshot <full>\meter <full>\fights.db "2026-11-17 01:40"
     ```

   - **Cut mid-relevel:** a second demo folder whose current log keeps only the lines before the first
     `[Wed Nov 18 17:12` (the dated log stays whole), so Home and History show a relevel at 43 with its usuals rather
     than one just started, where nearly every number reads "no usual yet". Point `EQLPATHFINDER_LOGS` at it, set
     `EQLPATHFINDER_PREVIEW_BEFORE = "2026-11-18 17:05"` (the overlay on pace in Kedge Keep at 42), and run
     `--snapshot <cut> <cut>\pathfinder.db`.

3. Copy from the cut render `main-home-flipped` → `img/home.png`, `main-relevels` → `relevels.png`, `main-crawls` →
   `history.png` and `overlay-leveling-at` → `overlay.png`, so all four show the same relevel; from the full render
   `main-ask-pool` → `ask.png` and `meter/meter-place-spell` → `meter.png`. Remake `public/og.png` (home.png scaled to 1200 wide,
   top 630 px). Home comes from `main-home-flipped` because at 42 `main-site-home` opens on Farming at 50
   (docs/debt.md); once that's fixed it comes from `main-site-home` again.
4. Read every PNG before it goes in: no real character name, no group member's name (the meter's shot is by ability
   for that reason), no chat.

5. **sharing.png**, for Better together, isn't on the page yet: even from a sample pool it leads with what the
   player's logs and the pool can't answer (docs/specs/site-screenshots.md, Not doing). It goes on once
   docs/specs/sharing-value.md is built or real players farm at 50 since the patch. It's drawn from a sample pool,
   not the live one. With a Debug build in your worktree and the demo folder from step 1:

   ```powershell
   node site/shots/sample-pool.mjs <live community.json> site/shots/sample-pool.json 32   # only to refresh the sample
   .\site\shots\sharing.ps1 -Demo "<demo folder>" -Out site\img\sharing.png
   ```

   `sharing.ps1` opens the real window in an isolated copy (never your settings or database) with the sample as its
   saved community file; `sharing.js` only clicks Ask → Mote farm at my level → 50. It takes about two minutes and
   saves 1892 × 1116. The live file is at the address in `ShareProtocol.DefaultCommunity` (the `community-pool`
   skill). Keep the caption under the shot that says the numbers are a sample.

Six screenshots in five sections is the budget: one job each. A new one replaces an old one rather than adding to the
pile.
