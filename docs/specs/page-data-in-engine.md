# The page data is built in the engine

**Why:** No test can build what a page is told: the data message is put together in `MainWindow.Refresh`, with
`App.SettingsForPage` and `MainWindow.Json`, in the project the tests can't load. So the page-test harness
(docs/debt.md, row 1) has nothing real to send. The `--snapshot` preview keeps its own copy, which has lost six
settings (`shareIntroSeen`, `beta`, `onBetaBuild`, `shareEnabled`, `usage`, `noticesSeen`) and sets `data` and calls
`render()` itself, past the message handler. `AskQueryTests` reads a question with its own copy of the window's
options, and the copy differs. This pays those two debt rows and is the first of three slices left on row 1.
The review found that `--snapshot` runs the app's whole start-up on the real settings and database (docs/debt.md,
11-18). This branch's own proofs need that run, so it takes the fix on.
**Players:** nobody sees a change. `Refresh` feeds every page, so a slip here reaches everyone; the new player's
message (no snapshot, no history) is the one least looked at.

## Done when
- [x] The engine builds the data message, and from the fixture, with a snapshot and a whole campaign, it is the one main's window sent: the same names in the same order, the same kinds and values all the way down, only `generated` apart. `data.settings` has main's 25 names with `pendingUpdate: null` written - `EveryPageGetsTheDataMessageItGotBefore`
- [x] The same for a log turned on today, with no snapshot yet: all ten fields, `snapshot: null`, empty lists - `ANewPlayersPagesGetEveryFieldToo`
- [x] The engine's options write a message as main's did (camelCase names, an enum by its name, NaN and Infinity as words, null written, a dictionary's keys as they are) and read what a page posts in any case - `AMessageIsWrittenTheWayThePagesReadIt`, `WhatAPagePostsIsReadWhateverTheCaseOfItsNames`
- [x] The window reads a page's question with the engine's options, and the test uses those, not a copy - `EveryFieldThePagePostsReachesTheQuery`
- [x] A setting the window forgets to fill doesn't compile - `ASettingTheWindowForgetsToFillDoesNotCompile`
- [x] `MainWindow.Refresh` and `DevSnapshots.Run` both send what `PageData.Build` returns, the window keeps no JSON options of its own, and the preview no longer sets `data` and calls `render()` - `TheWindowAndThePreviewSendThePageDataTheEngineBuilds` (source text: nothing else reaches the app's code)
- [x] Rendering never starts the app. With the installed app open, `--snapshot <out> <out>\pathfinder.db` raises no dialog, writes every shot it wrote before (`overlay-*`, `titlebar`, `main-*`, and `welcome-*.png` last), exits by itself, and the real `%LOCALAPPDATA%\EQL Pathfinder\error.log` gains no line from it. `--meter-snapshot` the same, to `meter-*.png`. docs/debt.md's `new App()` row is gone - that run, which is also the next box's, and `RenderingScreenshotsNeverStartsTheAppOnThePlayersData` (source text: the test project can't load the app)
- [x] The preview's pages draw from a delivered message: `--snapshot` writes every `main-*.png` with data in it and `main-js-errors.txt` is `[]`. With `throw new Error("x")` put first in core.js's data listener, the shots are pages without data, where main's preview draws them all, and `main-js-errors.txt` says the page data never arrived. The run writes that line itself when its wait for `data` runs out, because nobody has seen whether WebView2 hands a listener's throw to `window`'s `error` event: the thrown error is in the file only if it does. The engineer's report says which, and the `app-screenshots` skill keeps it. Seen once, then put back - `EQLPathfinder.exe --snapshot <out> <out>\pathfinder.db`
- [x] The preview is a new player who took the tour: no Home shot carries the one-time usage notice (`main-home.png`, `main-home-flipped.png`, `main-home-share-code.png`, `main-site-home.png`), and one new shot shows it on purpose - seen in those four and in `main-home-notice.png`
- [x] In `main-settings-share-on.png` the "Your data stays on this PC" row agrees with the Sharing card below it: "Sharing sends only what Settings → Sharing shows." comes before the usage sentence. `main-settings-share-off.png` and `main-site-home.png`, taken after it, are as with sharing off - seen in those three shots
- [x] The real window sends the same settings and the same field names after as before - the mock self-test under "Where", run on the commit before the change and on the finished branch, the two PNGs side by side
- [x] docs/debt.md's `DevSnapshots.cs` and `AskQueryTests.cs` rows are gone, docs/code-map.md names where the data is built now, and the `app-screenshots` skill no longer says the preview calls `render()` directly - `.\tools\check.ps1` (mapcheck), `git grep -n "its own copy" -- docs/debt.md` prints nothing (the review's two newer `DevSnapshots.cs` rows are another matter)

## Edge cases
- **Nothing yet:** the second test. `snapshot` is null, not left out; nothing throws on an empty history.
- **A second copy:** a render is never the real install (docs/engineering.md, rule 5). It opens only the database
  it was given and makes none of the app's own tracker, tray icon, window, updater, usage or sharing, whether the
  installed app is open or closed. `new MainWindow(app)` then gets an app that never started: the title bar is
  still drawn.
- **The preview's player:** a new player's settings as the tour leaves them (`WelcomeSeen`, the notices the tour
  tells, `ShareIntroSeen`), set by the code `WelcomeWindow.Finish` runs and not by a second list in `DevSnapshots`,
  then through the window's own mapping with its stand-ins as now (`version` "0.4.0", `updatesEnabled` and
  `gameRunning` true, `logsDir`, `character`). The website's screenshots come from `--snapshot`: the engineer's
  report names each shot that changed and why.
- **A shot that changes a setting puts it back:** `shareEnabled`, set with `shareStatus.enabled` for the share-on
  shot, is off again before the shots after it. Home's sharing card and Settings' privacy row both read it.
- **Starting and stopping:** as now. The settings are read on the UI thread before the query, `Build` runs on the
  tracker's turn inside `svc.Query`, and the JSON is written off both. A preview whose page never takes its data
  goes on after its wait and says so in `main-js-errors.txt`: it never hangs, and never passes in silence.
- **A message that isn't the page data:** the page data has no `kind` (both message tests assert it). Loot rows are
  an answer: the preview hands them over as it hands Ask's and Upgrades', not as `data.lootRows`.
- **WebView2 isn't Chrome:** `preview.html` is also opened in Chrome (the `app-screenshots` skill), where
  `chrome.webview` doesn't exist. The message must still arrive there, or the skill's recipe changes with it.
- **Locale and clock formats:** dates in the message stay as System.Text.Json writes them; the message tests compare
  them as text.
- **Something leaving the PC:** nothing new is sent. The page data holds the name and clock times, so the tests build
  it on each run and nothing built from the fixture is checked in (docs/quality.md, rule 6, added here).
- **The pipeline itself:** mapcheck fails until docs/code-map.md's names follow (`MainWindow.Json`,
  `App.SettingsForPage`, section 7's items 1 and 3).

## Not doing
- The page-test harness in headless Edge, and replacing the source-text tests: the next two branches.
- Renaming or re-scrubbing the fixture (its file name and 85 lines hold a character's name).
- The window's other answers (`UpgradeMessage`, `SharePreviewMessage`, loot, campaign options): they stay where they
  are, written with the engine's options.
- core.js and every other file under `web/`. Its `data.lootRows` line is left without a caller; it goes with the
  next change to core.js, as its comment's debt row does.
- `AppSettings` stays in the app, and its defaults aren't copied into the engine.
- `--snapshot` without a database still falls back to the real one: its own debt row (11-18).
- The farm rate's side effect in `ReincarnationData`, and the double refresh: their own debt rows.

## Where
- `src/EqlPathfinder.Engine/Pages/PageData.cs`: stubs for `PageJson.Options`, `PageJson.Query`, `PageSettings`,
  `PageData.Build`. The names can change, with the tests.
- `src/EqlPathfinder.App/MainWindow.xaml.cs`: `Json`, `QueryJson`, `Refresh`. `MainWindow.Json` is also read in
  `App.xaml.cs` (the self-tests), `WelcomeWindow.xaml.cs` and `DevSnapshots.cs`.
- `src/EqlPathfinder.App/App.xaml.cs`: `SettingsForPage`.
- `src/EqlPathfinder.App/DevSnapshots.cs`: `Run`, from `var defaults` to the `html.Replace("</body>", …)` script.
  After the review: `new App()` in `Run` and `RunMeter` (the architect's fix: a private sealed `RenderOnlyApp : App`
  whose `OnStartup` is empty; the test reads for that, so the fallback, a flag in `App.OnStartup`, comes back to qa
  first); the wait after `PostWebMessageAsJson`; `pageSettings`; the `settings-share-on` shot; the notice's shot.
- `src/EqlPathfinder.App/WelcomeWindow.xaml.cs`: `Finish` and `ToldByTour`, for the preview's player.
- `tests/EqlPathfinder.Engine.Tests/PageDataTests.cs`: `Before` and `SettingsOnMain` are main's message, frozen. They
  stay as the list of what a page is sent.
- `docs/releases/next/page-data-in-engine.md`: a note that nothing changes for players.
- The mock self-test (the `app-screenshots` skill: an isolated copy, `EQLPATHFINDER_SELFTEST=mock`,
  `EQLPATHFINDER_MOCK_WAIT=5000`). Not run by qa: it opens a real window, and the game was running. The PNG shows
  the logs folder and the character, so it stays in the scratchpad.
  ```js
  (function wait() {
    if (!(typeof data !== "undefined" && data?.reinc)) return setTimeout(wait, 1000);
    const pre = document.createElement("pre");
    pre.style.cssText = "position:fixed;inset:0;z-index:9999;margin:0;padding:8px;background:#fff;color:#000;font:11px Consolas;white-space:pre-wrap;word-break:break-all";
    pre.textContent = [Object.keys(data), Object.keys(data.reinc), Object.keys(data.snapshot ?? {})].join("\n") + "\n" + JSON.stringify(data.settings);
    document.body.append(pre);
  })();
  ```

## Decisions
The architect's, before code (11-17). Nothing new is stored, nothing new leaves the PC, the pages are the message's
only reader and ship in the same build, and a revert takes all of it back. These settle what qa left open.

- **The stubs' names and shape stay.** `PageData.Build(s, snapshot, campaign, settings)` returns the object and not
  its text: main's ten fields moved as they are, built on the tracker's turn and written off it, as `Refresh` does.
  The message gets no type of its own: the serializer is its only reader in C#, and `PageDataTests` is the list of
  its fields. `generated` stays the clock's, since no page reads it. The next branch's harness writes its message as
  these tests do: `JsonSerializer.Serialize(PageData.Build(…), PageJson.Options)` inside `svc.Query`.
- **Two sets of options, each exactly main's.** `PageJson.Options` is `MainWindow.Json`. `PageJson.Query` is the
  window's `QueryJson` (`new(Options) { NumberHandling = AllowReadingFromString }`), not the test's old copy: a
  change nobody should see doesn't change what the window reads. A question holds only text and whole numbers, so the
  copy's difference never showed. `Query` is declared after `Options` (static fields are filled top to bottom).
  `MainWindow.Json` goes without an alias: one name for one thing, and docs/code-map.md follows.
- **`PageSettings` is a record of `required` init properties in main's order, not a constructor's parameters.**
  The JSON's order is the order they're declared in, and the two the engine works out (`fragmentRecommendedValue`,
  `fragmentChoices`) sit third and fifth as properties with no setter; a positional record would write them last.
  Four neighbours are all `bool` (`beta`, `onBetaBuild`, `shareEnabled`, `usage`), and positional arguments swap
  without an error where named ones can't. And the harness changes one setting with `with`. An overlay's row is its
  own small record, every field required as well.
- **One mapping in the app.** `App.SettingsForPage` becomes one static function of an `AppSettings` and what isn't in
  it (`overlaysLocked`, `arranging`, `version`, `pendingUpdate`, `updatesEnabled`, `gameRunning`, `onBetaBuild`),
  as parameters with no defaults. The window passes the live values; the preview passes its stand-ins and then
  `with { LogsDir = …, Character = … }`. A second initializer in `DevSnapshots` would compile, and would be the
  second mapping this branch is here to remove.
- **The preview's message: the window's own call in WebView2, a stand-in where there is no window.**
  - `--snapshot` posts it with `core.PostWebMessageAsJson(json)` once the page has loaded: the call `Refresh` makes,
    so it crosses WebView2 as a player's does and core.js's own listener takes it. Then it waits for `data` (by name,
    a few seconds at most) and goes on either way: a listener that throws leaves `main-js-errors.txt` and pages
    without data, never a run that hangs.
  - `preview.html` gets a few lines before `js/core.js`, written by `DevSnapshots` (nothing under `web/` changes):
    only where `window.chrome?.webview` is missing, a `chrome.webview` that keeps the listeners and ignores what the
    page posts, as `tools/webcheck.mjs` and the skill's `stub.js` do. The closing script keeps the message inlined
    and, only when that stand-in is in place, hands it to the listeners as `{ data: … }` without catching. One file
    works in both, and the message never arrives twice.
  - Not one path for both (replacing `chrome.webview` inside WebView2, or `dispatchEvent` on it): whether the host's
    object allows either isn't known, and the real call is the better proof of what a player's page gets.
  - Loot rows go the same two ways, as the window's own answer `{ kind: "loot", rows }` after the page data. Ask's
    and Upgrades' demos stay as they are.
  - The Chrome side has no "Done when" line. The engineer's report shows it once: `preview.html` served on 127.0.0.1
    and drawn by headless Edge or Chrome, with the data in it. The next branch's harness takes this stand-in over and
    proves it on every run.

## Reviews
- architect: the code that moved is sound and qa's tests kept their meaning. Two must-fix: `--snapshot` and `--meter-snapshot` ran the app's whole start-up on the player's real settings and database (it predates the branch, and raised a dialog on the user's desktop three times), and two boxes were unproven. Fixed with a render-only app whose start-up does nothing, held by `RenderingScreenshotsNeverStartsTheAppOnThePlayersData`, then proven by runs beside the open installed app: no dialog, the real error.log unchanged, and the mock self-test of the real window identical before and after. Also done: the preview's settings are a player who took the tour, the release note's doubled "No re-read" line is gone, and the core.js debt row names the dead `data.lootRows` line. Three debt lines logged, one of them paid here.
- designer: nothing a player sees changes. First look found the one-time usage notice in the website's Home picture and a false privacy line in main-settings-share-on (the preview set one sharing flag and not the other); both fixed. Second look (11-18, from PNGs): nothing to fix. Against main's shots, main-site-home and main-home-share-code are byte-identical; main-home and main-home-flipped lose only the one-time sharing card (the preview's player took the tour), which main-home-share-code still shows; main-home-notice shows the usage notice on purpose; the real window's mock self-test is byte-identical before and after. Ask and Upgrades shots differ by live data that moved between runs (the community file, loot since the demo bank's entry time), not by this branch.
