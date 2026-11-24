---
name: app-screenshots
description: See the app without opening a window. Render every page, the overlays and the fight meter to PNGs from real or demo logs, draw a mock-up, or run a self-test in an isolated copy. Or drive a page in Chrome (typing, keys, clicks) with a stand-in for the window. Use to check a UI change, find a visual bug, test how a page behaves, or make website screenshots.
---

# Rendering the app

Build in your worktree with `dotnet build src/EqlPathfinder.App -c Debug`. The exe is
`src/EqlPathfinder.App/bin/Debug/net10.0-windows/EQLPathfinder.exe`. Every mode runs off-screen and exits. Put
output in the scratchpad, outside the repo.

| Command | What you get |
|---|---|
| `EQLPathfinder.exe --snapshot <out> <out>\pathfinder.db` | `main-<page>.png` for every page and its main states (Ask answers, Plan, Campaign, History tabs, Help, Settings, sharing), `overlay-*.png`, `welcome-*.png`, plus `preview.html` with the data inlined. A fresh database reads the whole history first (about a minute). Always pass the database, a copy: without it, snapshot mode opens the real one, and fails while the app is running (one writer per database) |
| `EQLPathfinder.exe --meter-snapshot <out> <fights.db> "2026-11-17 01:40"` | `meter-*.png`: the fight meter for the stint before that time (it builds the fight store if needed) |
| `EQLPathfinder.exe --updater-test <report>` | Checks the beta-channel update rules |

The sequence of shots is `src/EqlPathfinder.App/DevSnapshots.cs`. To show a new state, add a shot there.

**A render never starts the app.** `--snapshot` and `--meter-snapshot` make an app of their own whose start-up does
nothing (`RenderOnlyApp` in DevSnapshots.cs), so they open only the database you pass. Both ran beside the open
installed app on 11-18: no dialog, every shot, an exit by itself, nothing new in the real `error.log`. A build from
before that fix
(main until 11-18, and any branch started earlier) ran the app's whole start-up inside the render, on the real
`settings.json` and `pathfinder.db`: with the installed app open the player got "EQL Pathfinder couldn't open its
database" on their desktop, the real `error.log` got an "opening database" entry, and the run wrote its `main-*.png`
and then never exited, with no `welcome-*.png`. If you ever see that again, end only your own process, and render
with that build only while the installed app is closed.

`main-js-errors.txt` is `"[]"` when the pages took their data and no script failed. The preview posts the page data
as the window does and waits five seconds for core.js's listener to take it. When it never does, the shots are pages
without data and the run adds its own line to that file ("--snapshot: the page data never arrived…"), so read the
file before the shots. WebView2 does hand a listener's throw to `window`'s `error` event (seen 11-18, with
`throw new Error("x")` first in core.js's listener): the file then also has "Uncaught Error: x @ core.js:106", once
for each message posted, with the line where `dispatchHost` throws it on and not the listener's own. Ask's shots
still draw in such a run, because each shot hands Ask its answer directly.

To try a broken page script, copy the build folder to the scratchpad and change the copy's `web\js`: the snapshot
reads the scripts beside its exe, so the source is never touched and nothing needs putting back.

**Environment variables:**

| Variable | Effect |
|---|---|
| `EQLPATHFINDER_LOGS=<folder>` | Read these logs, not the game's. The folder above it must hold `dbstr_us.txt` and `spells_us.txt`; copy them from the game folder |
| `EQLPATHFINDER_PREVIEW_BEFORE="yyyy-MM-dd HH:mm"` | Adds `overlay-leveling-at`: the overlay during the last crawl that started by then |
| `EQLPATHFINDER_DATA=<folder>` | An isolated copy with its own settings and database. It never touches the real ones, never sends usage counts, and never uploads shares (only `SELFTEST=share` does), even with a copy of real settings. Point it at a folder of its own: pointed at the real `%LocalAppData%\EQL Pathfinder`, it ran a second tracker beside the app on 11-17 and wrote kills twice (it now fails to open the database instead) |
| `EQLPATHFINDER_SELFTEST=1`, `share`, `community`, `overlays`, `help`, `usage` | Self-tests in an isolated copy (App.xaml.cs, the `SelfTest*Async` methods) |
| `EQLPATHFINDER_SELFTEST=mock` with `EQLPATHFINDER_MOCK=<script.js>`, `EQLPATHFINDER_MOCK_OUT=<png>`, `EQLPATHFINDER_MOCK_WAIT=<ms>` | Opens the main window in an isolated copy, runs the script on the page (it can draw anything), saves a PNG and exits |

An empty `EQLPATHFINDER_DATA` folder stops at first-run setup, so `mock` waits for minutes and saves nothing. Copy
`settings.json` and `pathfinder.db` from `%LOCALAPPDATA%\EQL Pathfinder` into it first. A screenshot shows no
hover or focus states, such as a number box's spin arrows.

**Mock scripts, from experience:**
- The copy reads the logs before the first data message, so the script runs on an empty page. Poll before acting:
  `(function wait() { if (!(typeof data !== 'undefined' && data?.reinc)) return setTimeout(wait, 1000); … })()`.
  `data` and the pages' state are `let` globals, so they live outside `window`: reach them by name.
- Clicks go through the real window: Swap…'s `campaignOptions`, Ask, Upgrades get real answers. `--snapshot`'s
  `preview.html` can't do that (no window behind it): it is sent the page data and Loot's rows as messages, through
  core.js's own listener, and nothing else, and what the page posts goes nowhere. Test anything that talks to the
  window here.
- To see what happened, draw it: append a fixed `<pre>` to the page and log `window` `error` events into it. Wrapping a
  page function (`campOpenPop = function (…) { …; return orig(…); }`) shows whether it ran.
- `node tools/webcheck.mjs` (part of `check.ps1`) catches undeclared names and answers that replace the page data
  without opening a window.
- Home's Next relevel / Farming pick lives in the page (the copy's WebView2 profile), not in `settings.json`, so a copy
  opens on auto. To see the farming card, click it: `document.querySelector('[data-home-mode="farming"]').click()`.
  `[data-ask-q="farm"]` on that card then opens Ask's farm answer.
- Use a new folder for each run. The last run's WebView2 profile stays locked for a few seconds after the app exits,
  so clearing the same folder straight away fails part way. Runs at the same time need a folder each too (one writer
  per database).
- An (i) (`infoTip` in core.js) opens on a plain `button.click()`; check `#infoPop`. Scrolling `#main` closes it, and a
  scroll's event lands a frame later: after `scrollIntoView`, wait ~300 ms before clicking, or the popover shuts at once.
- In Windows PowerShell, read a mock script with `Get-Content -Raw -Encoding UTF8`: without it, ✕, → and × arrive garbled.

**Demo data** (website screenshots, anything shown publicly): copy the logs with the character's name replaced
everywhere, in the file names too. site/README.md has the full recipe; only the demo name may appear.
Read the PNGs with the Read tool to look at them.

## Trying a page in Chrome

What a page draws from its data is a test's to prove, not Chrome's: `HomePageTests` opens the real page in headless
Edge and reads it (docs/quality.md, "What a page does with it"). Those tests don't click or type yet.

Some behaviour a PNG can't show: typing, keys, a list that opens. For that, run the page's own files in Chrome with a
stand-in for the window.

1. Copy `src/EqlPathfinder.App/web` to the scratchpad. In the copy's `index.html`, add
   `<script src="stub.js"></script>` before `js/core.js`. Then write `stub.js`:

   ```js
   (() => {
     const handlers = [];   // keep every one: several page scripts listen, and core.js is only the first
     const answer = { kind: "upgrade", inventory: { exists: true, file: "Demo-Inventory.txt", time: "2026-11-17T10:00:00" },
       items: [{ name: "Ring of Demo", tier: 3, place: "Ring 1", count: 1 }], bank: null, plan: null };
     window.chrome = window.chrome || {};
     window.chrome.webview = {
       addEventListener: (t, h) => { if (t === "message") handlers.push(h); },
       postMessage: m => { if (m.type === "upgrade") setTimeout(() => handlers.forEach(h => h({ data: structuredClone(answer) })), 60); },
     };
     localStorage.setItem("page", "upgrade");
   })();
   ```

   Shape each answer the way `MainWindow.xaml.cs` sends it (here what the engine's `UpgradePage.Build` returns).
2. Serve the copy on 127.0.0.1 with a few lines of Node `http`, open it with the claude-in-chrome tools, and use real
   clicks and keys.
3. The stand-in never sends the full `data` message, so a page that asks for its data when that message arrives
   won't ask. Call the request yourself, for example `requestUpgrade(true)`.

For keys on a page that only needs the page data (Settings, Home, Help, the (i) popovers), skip the stand-in: serve a
`--snapshot` output folder instead. Its `preview.html` has the page data inlined and the page's own `app.css` and `js/`
beside it. Where `chrome.webview` is missing, as in Chrome, it puts the engine's stand-in there (`WindowStandIn.Script`,
the one the page tests run) and sends the page data and Loot's rows to core.js's listener as messages, so the pages
draw as they load. What the page posts is kept in `chrome.webview.posted`. To send it another message:
`chrome.webview.dispatchEvent(new MessageEvent("message", { data: { kind: "loot", rows: [] } }))`. Call `show("settings")` from the javascript tool, then press real Tab, Enter, Space and Escape: a script's
synthetic keys never press a button, so only this tests keyboard use.

This is Chrome, not WebView2. Native popups, such as a datalist or a `<select>`'s list, don't show in screenshots and
behave differently in WebView2, so test only what the page draws itself. To check something in WebView2,
`EQLPATHFINDER_SELFTEST=mock` runs a script in the real window.
