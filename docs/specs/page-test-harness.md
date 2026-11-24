# A page's behaviour is tested in headless Edge, starting with Home

**Why:** No test runs a page's behaviour (docs/debt.md, row 1). 0.9.37 shipped with every page blank, and a UI
"Done when" line can only be proven by a screenshot. `AppPageTests` holds three behaviours as regular expressions
over template strings: they fail on a rewording and pass on a wrong result. This is the harness, with Home's two
moved onto it. The other pages are the next branch.
**Players:** nobody sees a change. Every later UI change is proven through this, so a harness that passes a broken
page, or stops `finish` with nobody watching, costs every player the next release.

## Done when
- [x] Home's farming card, drawn by the real scripts in headless Edge from the fixture's page data at the cap: two minutes into a session it shows the T5-eq so far and "The rate starts at 6 min.", and the rate is nowhere on Home; after a whole crawl it shows the rate in crawls as the big number and in "This session", and the wall-clock rate, a different number in the test's message, on its own line and nowhere else - `HomeShowsT5SoFarUntilTheSessionHasAFairRate`, `HomeShowsTheRateOnceTheSessionHasOne`
- [x] Home's farm list: one crawl at the cap reads "Too little at 50 since the Nov 10 patch to rank a place yet" and what a place needs; none reads "You haven't farmed at 50…" - `AFarmTooSmallToRankReadsAsTooLittleOnHome`, `NoFarmingAtTheCapReadsAsNotFarmedOnHome`
- [x] A test reads what a player can read (a label in capitals as drawn, a hidden card as nothing, an id the page lacks fails by name) and what the page posted to the window (`seen`, once) - `AHiddenCardReadsAsNothingAndAnIdThePageLacksFailsByName`, `OpeningHomeTellsTheWindowWhichPageWasSeen`
- [x] It catches a broken page, on copies of the scripts with one slip each, all four of which `node tools/webcheck.mjs` passes. A draw that throws fails with the error and the line of home.js that threw it, not core.js's that passed it on. A script that throws while loading (0.9.37's kind) fails with the error and its line, never "Script error.", though Home's hero still draws. Home forgetting to wait fails Home's own test. A page that hears its data and drops it fails and says it never took it - `APageWhoseDrawThrowsFailsItsTestWithTheErrorAndWhereItIs`, `AScriptThatThrowsWhileLoadingFailsTheTestWithTheErrorAndItsLine`, `APageThatDrawsTheWrongThingFailsHomesTest`, `APageThatNeverTakesItsDataFailsTheTestAndSaysSo`
- [x] Without Edge a page test fails and says where it looked: never skipped, never passed. A browser that gives nothing back fails as the browser's, with its exit code and what it wrote. A page given back without the harness's reading fails as the harness's. A message that isn't JSON fails as the test's, and no browser is started for it - `WithoutEdgeAPageTestFailsAndSaysWhereItLooked`, `ABrowserThatGivesNothingBackFailsAsTheBrowserNotAsThePage`, `APageGivenBackWithoutItsReadingFailsAsTheHarnessNotAsThePage`, `AMessageThatIsNotJsonFailsAsTheTestsBeforeAnyBrowserStarts`
- [x] A browser that hangs is stopped after its time, with everything it started, and the test fails - `ABrowserThatHangsIsStoppedWithEverythingItStartedAndFailsTheTest`
- [x] Two runs at once leave nothing in the folder they were given (the page's copy and the profile are gone, which Edge only allows once no part of it runs), and the page's own files are untouched - `APageTestLeavesNothingBehindAndWritesNothingBesideThePage`
- [x] The preview's stand-in for the window and the harness's are one text, which the page tests run every time - `ThePreviewAndThePageTestsUseOneStandInForTheWindow` (source text: the test project can't load the app)
- [x] `--snapshot` still draws from that stand-in's messages: `EQLPathfinder.exe --snapshot <out> <out>\pathfinder.db` writes every `main-*.png` with data in it and `main-js-errors.txt` is `[]`, and `<out>\preview.html`, served on 127.0.0.1, draws Home with data in headless Edge - that run, in the engineer's report
- [x] Home's two source-text tests are gone: `HomeWaitsForTheSessionRate` whole, and from `AFarmTooSmallToRankIsNotNoFarming` everything but ask.js's two lines - `git grep -n "HomeWaitsForTheSessionRate\|haven't farmed\|so far this session" -- tests/EqlPathfinder.Engine.Tests/AppPageTests.cs` prints nothing
- [x] The gate runs them where it runs the engine tests, in a worktree and in `finish`'s clean copy, and they add no more than 5 s to that step on this PC - `.\tools\check.ps1`, with the "engine tests" step's time on main and on the branch in the engineer's report
- [x] docs/quality.md's "What a page does with it" row says how a page's behaviour is tested, the command that runs only that, and that Edge must be installed; docs/debt.md's first row says what's left; docs/code-map.md names the harness; `tools\check.ps1`'s description says the engine tests need Edge; docs/pipeline.md says, beside `check.ps1`, that `tools\release.ps1` runs these tests in every release, so no Edge or a failing page test stops that release - `.\tools\check.ps1` (mapcheck), `git grep -n "No test runs a page's behaviour yet" -- docs/quality.md` prints nothing, and `git grep -n "Edge" -- docs/pipeline.md` prints the line

## Edge cases
- **Nothing yet:** a page opened with no message stays as index.html leaves it ("Reading your history…") and that
  isn't a failure: Help needs no data. A new player's Home is the next branch's.
- **A second copy:** every run has its own folder and profile, never Edge's own, so two runs at once, two worktrees
  checking at once and the user's open Edge don't meet. No fixed port, if a port is used at all.
- **Starting and stopping:** a test host killed mid-run can leave a folder in `%TEMP%`. Names are unique, so the
  next run doesn't trip on it. A run that fails or times out cleans up as a good one does. Tidying fails closed: a
  run's folder that still can't be removed fails the run and names the folder, since part of Edge is still running.
  No test: a stand-in browser can only outlive its run inside that folder by knowing the harness's own switches.
- **The rules change:** Edge updates itself. A switch it stops knowing, or a crash, reads as the browser's failure.
- **Any error fails the test,** even when the text it wants is there: an uncaught error (while a script loads or
  in a listener), an unhandled rejection and `console.error`. The pages' own ways into the last two are click
  handlers in report.js and share.js, which come with clicks in the next branch; here a copy of core.js does both
  as it takes its data - `WhatAPageLogsAsAnErrorOrRejectsUnheardFailsTheTestThoughHomeDrew`.
- **The reading isn't whole:** the harness's own script stopped part-way through reading the page. That fails as
  the harness's, not the page's.
  No test: it takes the harness's own script throwing, and no page or stand-in browser can make it.
- **Two numbers that happen to be equal:** the fixture's session has one rate in crawls and by the clock (76.9),
  so a page that drew either in the other's place read the same, and the first tests passed it. A test that says
  which number is where sets them apart in its message first.
- **A message that isn't the page data:** `Open` sends its messages in order, as the window does, so an answer
  (it has a `kind`) follows the data. "Never took the page data" is only about a message without a kind, and
  taken means core's `data` is the very message sent: a listener that was called and dropped it hasn't taken it.
- **WebView2 isn't Chrome:** nor is Edge WebView2. The datalist and Ctrl+Shift+R invariants stay in `AppPageTests`
  and the mock self-test. The stand-in steps in only where `chrome.webview` is missing, so in the app it does nothing.
- **Locale and clock formats:** Edge is started in en-US. Seen by hand: with `--lang=de-DE` the same page draws
  "76,9". The message's times carry no zone, so the PC's time zone moves no date.
  No test: this PC is en-US, so a missing `--lang` shows only on another PC.
- **Window size and scaling:** one fixed window (the `--snapshot` pages' 1274×757) at scale 1, so what counts as
  drawn doesn't depend on this PC's display.
- **Something leaving the PC:** the page is a local file about the made-up Standin, no script under `web/` fetches
  anything, the stand-in keeps what the page posts, and Edge starts without first-run, sync or background
  networking. A failure's text carries what was drawn into the test output, which stays here. The message is built
  on each run and never checked in (docs/quality.md, rule 6).
- **A message no player gets:** the fixture stops at level 23, so the tests set values in the built message
  (`PageMessage.With`). Only at a path the engine wrote, and only to the same kind of value: a field the engine
  renames fails the test and doesn't leave it drawing from fiction.
- **The harness's own script on the page:** the page's scripts share one global scope. A `const s` in the scratch
  run's script collided with theirs, the whole script was dropped without an error, and Home sat at "Reading your
  history…". It declares no global name, and a `<` in the message can't end its script.
- **The pipeline itself:** no new step and nothing PowerShell-specific: the tests find `web/` from their own source
  path, as `AppPageTests` does, so the clean copy works. The browser is started without the shell.

## Not doing
- The other pages, Home's other states (new player, leveling, releveling, between relevels), the counts-nudge and
  Upgrades-box source tests, and Ask's half of `AFarmTooSmallToRankIsNotNoFarming`: the next branch.
- Clicks, typing, the canvas and pixels. A test reads text and posts. The designer still checks screenshots.
- A checked-in log that reaches the cap.
- Retrying a run that failed or timed out: a gate that passes on the second try hides the first.
- A step or a sample of its own in `tools\check.ps1`: the copies with a slip are tests, beside the harness.
- Anything under `src/EqlPathfinder.App/web`. core.js's comment row in docs/debt.md waits for a change to core.js.

## Where
- `tests/EqlPathfinder.Engine.Tests/PageHarness.cs`: the stub. `PageHarness` (`Web`, `Browsers`, `Timeout`,
  `Scratch`, `Open(page, messages…)`), `PageRun` (`Page`, `Text(id)`, `Posted`), `PageFailed`. The names and the
  failures' wording can change, with the tests.
- `tests/EqlPathfinder.Engine.Tests/PageMessage.cs`: the fixture's message and `With`. Written, not a stub.
- `tests/EqlPathfinder.Engine.Tests/HomePageTests.cs` (`HomePages` draws both states at once, one browser each),
  `PageHarnessTests.cs`, and `AppPageTests.cs` for the new source test and the two to take out.
- `src/EqlPathfinder.Engine/Pages/WindowStandIn.cs`: a stub, `Script = ""`. `src/EqlPathfinder.App/DevSnapshots.cs`,
  `Run`: the stand-in before `coreScript` and the closing script that hands it `previewMessages`.
- `docs/releases/next/page-test-harness.md`: nothing changes for players.
- What a scratch run showed (not checked in): `msedge --headless=new --disable-gpu --no-first-run
  --user-data-dir=<run> --window-size=1274,757 --lang=en-US --dump-dom file:///<copy>/<page>.html` on a copy of
  `web/` with the stand-in before core.js; the result left the page as JSON in a `<script type="application/json">`,
  read from the dump; text was `innerText` of each `[id]` that `checkVisibility()`. 1.2 s the first run, 0.5 to
  0.6 s after, and the profile folder could be removed as soon as Edge exited. The tests' messages drew every
  string they expect, and the four slips behaved as their tests say.

## Decisions
The architect's, before code (11-18). Nothing new is stored, nothing new leaves the PC, no update arrives another
way, and a revert takes all of it back. The numbers are scratch runs on this PC with Edge 157, not checked in.

- **The stand-in stays in the engine, as stubbed.** Its text ships today (it is `DevSnapshots.Run`'s), and
  `DevSnapshots.cs` changes wherever the one text lives, so the release note is owed either way. The engine is the
  one project the app and the tests both reference, so the compiler holds them to one text. A file both read would
  give the app something to find at run time, which only a `--snapshot` run notices missing, and the tests a path.
  It is `chrome.webview` and nothing else: the reading, the error list and the sending stay in the tests, and the
  preview's messages in `DevSnapshots`. `reread: false`, `job: false`: the command-line tool carries the constant
  and never reads it.
- **What the stand-in is.** Where there's no window, `chrome.webview` is a real `EventTarget` whose `postMessage`
  keeps a copy of each thing posted (through JSON, as the window gets it), in order, in a list on itself. A message
  is sent with `dispatchEvent(new MessageEvent("message", { data }))`. A listener that throws then doesn't stop the
  next and reaches `window`'s `error` event, as in WebView2 (the app-screenshots skill), and a listener for a
  misspelt event hears nothing. That list is also how the preview's closing script and the harness know it stepped
  in: in WebView2 neither sends anything. `previewListeners` goes, and no other global comes.
- **`PageHarness`, `PageRun` and `PageFailed` stay as stubbed, and a run is a batch.** Everything is decided before
  the browser starts: the page is put together (a copy of `Web`, the stand-in before core.js, the harness's script
  last in the body), drawn once, and read once. No test talks to a live page. A click arrives in the next branch as
  a step in that batch (send, click, answer what the page posts), run in order by the same script, with
  `Open(page, messages…)` the case where every step is a message. None of that is built now.
- **Which page.** The harness writes `page` where core reads it (`localStorage`, which works under `file://`: seen)
  before core.js runs, as a player comes back to the page they left, so `seen` is posted once. `PageRun.Page` is
  core's `page`, and `Open` fails when it isn't the page asked for.
- **What is read.** Every element with an id, the hidden ones as nothing, taken by the harness's script in the same
  turn as the last message: no timer of the page's runs in between, so when Edge writes the dump doesn't matter.
  `Text` is the only way in (`Shown` isn't the tests' to read). "Can read" means laid out: `display: none` and
  `visibility: hidden` read as nothing (seen: `innerText` of a `display: none` element is its whole text, so
  `checkVisibility()` is needed), and a card under another or faded out still reads. One row of a list is asserted
  on its container.
- **"Took the page data"** is that core's `data` is the last message without a kind that was sent, the same object.
  A listener that was called and dropped it has not taken it.
- **Any error fails `Open`,** even when the text the test wants is there: `window`'s `error` event (listened to
  on the way down, so a script that doesn't load counts), an unhandled rejection, and `console.error` (core.js logs
  a held message's throw that way).
- **Edge gets `--allow-file-access-from-files`.** Under `file://` each file is its own origin, and an uncaught
  error in a page script reaches `window` as "Script error." with no file, line or stack (seen, for a throw while a
  script loads and for a listener's). With the switch it carries all three, as the app's one origin
  (`https://pathfinder.local`) does. A failure's text has the error and the top of its stack as file and line:
  `dispatchHost` throws the first error on, so the event's own line is core.js's. A try/catch round a direct call
  to the listener would pass `APageWhoseDrawThrows…` without the switch and leave a throw at load, 0.9.37's kind,
  nameless.
- **The failures' wording stays as the tests pin it.** Each failure starts with whose it is. The browser's: not
  found, with every place looked; gave no page back, with the exit code and what it wrote; took longer, and was
  stopped. The page's: the error and where; never took the page data; opened another page. The test's: an id the
  page lacks. A page's failure ends with what the page asked for had drawn, cut short. One more that no test pins:
  Edge gave a page back without the reading (the harness's script was dropped, or the page went elsewhere). That is
  the harness's failure and says so, not "gave no page back".
- **Stopping and tidying.** On the timeout, `Kill(entireProcessTree: true)`. After every run the run's folder is
  removed, trying for a second or two, and a folder that still can't be removed fails the run by name: it's the
  only sign that part of Edge is still running. `Timeout` stays 20 s. It's a fuse: the slowest of 16 starts at once
  took 3 s. No retry.
- **`file://`, `--dump-dom` and one start per state: yes, with a ceiling.** One start alone takes 0.55 s. At once,
  4 take 0.9 s, 8 take 1.5 s and 16 take 3.0 s, plus 0.6 s to remove 16 profiles: beyond the first, a start costs
  about 0.2 s however many cores there are (24 here). This branch makes seven real starts (five when this was
  written: the throw while loading and the logged error each added a copy with a slip). The next branch's four
  pages at three or four states each add about 3 to 4 s, and only when a page's states open at once in its
  fixture, as `HomePages` does: one after another they cost 0.55 s each. So at about 20 starts the page tests cost
  what the user accepted (5 s), and past that the browser is started once for the run, in a branch of its own. For
  that, the harness keeps three parts apart: putting the page together, running a browser on it (the only part
  that knows Edge's switches, the dump and the process), and turning the reading into a `PageRun` or a
  `PageFailed`. No page test names any of the three.
- **Timers are the next branch's, and the way is known.** upgrade.js posts after a timer, Campaign redraws after
  4.1 s, and Ask and Upgrades draw from answers. With `--virtual-time-budget`, 4.7 s of timers, a click's handler
  and a 120 ms debounce ran inside the same 0.5 s start, and the dump waited for them (seen). An animation frame
  asked for after time had moved never came (seen once), so report.js's picture stays with the mock self-test.
  Home needs none of it.
- **Who else runs it.** `tools\release.ps1` runs the engine tests in every release, as the user in their own
  session, like a check. A failing page test, or no Edge, stops that release and the alert says so: closed.
  docs/pipeline.md says it beside `tools\check.ps1`'s description.
- **Nothing about a player leaves.** The message is the stand-in's, the page is a local file and the stand-in
  keeps what's posted. Edge's own start-up traffic is turned down by switches and not proven silent; it carries
  nothing of the page. `site/privacy.html` doesn't change: the app sends nothing new.
- **Setting values in the built message is sound for what these tests are about, and not a player's message.**
  `OneCrawl` has `reinc.home.farm.crawls` 1 beside `reinc.farm.crawls` 0, `usual` null and `reinc.home.farm.at`
  23, and `With` can't set `usual` (null to a number). Home's two farm lines read only `home.farm.crawls`,
  `minCrawls`, `minHours` and `farm.patch`, so they are proven; what Home does with a whole message from the cap
  waits for the cap's log, which Home's other states in the next branch will want anyway.
- **Ask's half stays a source-text test** until Ask is on the harness: no worse than today, and docs/debt.md's
  first row keeps saying so.

## Reviews
- architect: the build is sound, safe in every release (about 200 runs with no Edge start failing and nothing left behind), and nothing has to be redone for the next pages. One must-fix: Home's two rates were the same number in the fixture, so swapping them in home.js passed every page test; the message now gives them different numbers and each is asserted where it belongs, seen failing on both swaps. Also fixed: the hung-browser test's 2 s fuse, which failed 9 of 24 runs under heavy load (now 5 s); `PageRun.Shown` closed so `Text` is the only way in; a test for a message that isn't JSON. docs/quality.md gains the rule that a page test saying something is absent also says what is there. Two debt lines logged (Home deciding its state in the page; two more stand-ins for the window). Unproven until this branch's own release: the page tests inside the scheduled task's session.
