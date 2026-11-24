# The last source-text behaviour tests run the pages: counts, Ask's farm, the Upgrades box

**Why:** `AppPageTests` still proves three behaviours by reading scripts as text: the counts banner on Home,
Campaign, Ask and Roster with its click, Ask's "Too little farming", and the Upgrades item box. Such a test fails on
a rewording and passes on a wrong result (docs/debt.md, row 1). The harness can draw a page but can't click, type or
read twice, and the Upgrades page's answer is put together in the window, where no test can build it.
**Players:** nobody sees a change. The banner is for the player whose log starts years into their play; the box is
for anyone raising gear. Every later change to these pages is proven through this.

## Done when
- [x] While the log has seen no reincarnation and no count is set, Home, Campaign (with a plan and before one), Roster and a route on Ask each say so and offer Set counts; Campaign says "from no reincarnations" and Ask that its times assume none - `HomeAsksForCountsWhileTheLogHasSeenNoReincarnation`, `CampaignSaysItPlansFromNoReincarnationsAndAsksForCounts`, `RosterAsksForCountsWhileTheLogHasSeenNoReincarnation`, `AskSaysItsTimesAssumeNoReincarnationsAndAsksForCounts`
- [x] Once a count is set the banner is gone from all four, and each shows what it drew from that message (Home "1 / 16 reincarnated", Campaign "2 of 48 ranks you want" and "runs from here", Ask "+20% for 2 reincarnations"); on Roster it is also gone once the log has seen one - `HomeStopsAskingOnceACountIsSet`, `CampaignStopsAskingOnceACountIsSet`, `RosterStopsAskingOnceACountIsSetOrTheLogSeesAReincarnation`, `AskSaysWhatXpItsTimesAreForOnceACountIsSet`
- [x] A click on Set counts, on any of the four, opens Roster with its count entry ready, and posts nothing but the page seen - `SetCountsOnHomeOpensRostersCountEntry`, `SetCountsOnCampaignOpensRostersCountEntry`, `SetCountsOnRosterOpensItsCountEntry`, `SetCountsOnAskOpensRostersCountEntry`
- [x] Ask posts each question the player picks (a tab, "1–20", the level stepper), and the answers the tests send are the engine's to those questions. No crawl at the level reads "No farming at levels 22–24…"; one crawl where a place needs two reads "Too little farming… to rank a place" with what a place needs - `AskSendsTheWindowEachQuestionThePlayerPicks`, `NoFarmingAtALevelReadsAsNoFarmingOnAsk`, `AFarmTooSmallToRankReadsAsTooLittleOnAsk`
- [x] The Upgrades item box: clicking in lists every piece of gear in the export once, with its copies and where they are; typing keeps the names with every typed word, the ones that start with it first, or says none matches; picking closes the list, lists the item's copies, and 120 ms later posts the request with the other copy to merge - `TheUpgradesItemBoxListsYourGearWhenYouClickIntoIt`, `TheUpgradesItemBoxFiltersYourGearAsYouType`, `PickingGearFromTheListShowsItsCopiesAndAsksTheWindowForThePlan`
- [x] The engine builds the Upgrades answer (`UpgradePage.Build`), with every field under the name the window sent it, with and without an inventory export, for gear and for a spell; the window and the preview send that and put none together themselves - `TheUpgradesAnswerHasYourGearYourBankAndThePlanYouAskedFor`, `WithNoInventoryExportTheUpgradesAnswerSaysSoAndHasNoGearOrPlan`, `ASpellsPlanLeavesCopiesOut`, `TheWindowAndThePreviewSendTheUpgradesAnswerTheEngineBuilds` (source text: the test project can't load the app)
- [x] A run is a list of steps done in order: the window sends, the player clicks or types, the test reads the page under a name. A reading no step took fails as the test's, by name. A click on nothing the page shows fails as the test's and names the step. An error, a `console.error` or an unheard rejection from a click's handler fails as the page's, with the script and line - `AReadingTheRunNeverTookFailsByName`, `AClickOnSomethingThePageDoesNotShowFailsAsTheTestsAndNamesTheStep`, `AnErrorInAClicksHandlerFailsTheTestWithTheErrorAndWhereItIs`
- [x] `--snapshot` still draws Upgrades from the engine's answer: `EQLPathfinder.exe --snapshot <out> <out>\pathfinder.db` (always with the database) writes `main-upgrade.png` and `main-upgrade-copies.png` with a plan in them, and `main-js-errors.txt` is `[]` - that run, in the engineer's report
- [x] The two source-text tests are gone from `AppPageTests`: `EveryPageThatPlansFromYourRanksAsksForCountsWhileTheLogHasSeenNone` and `AFarmTooSmallToRankIsNotNoFarming` - `git grep -n "ranksUnconfirmed\|countsNudge\|Too little farming" -- tests/EqlPathfinder.Engine.Tests/AppPageTests.cs` prints nothing
- [x] Every page's fixture opens its runs through `PageRuns.AtOnce`, and the page tests start Edge 15 times in all (7 before); the "engine tests" step stays within the 5 s accepted for the page tests - `git grep -n "Task.WhenAll" -- "tests/EqlPathfinder.Engine.Tests/*PageTests.cs"` prints nothing, and `.\tools\check.ps1` with that step's time on main and on the branch in the engineer's report
- [x] docs/quality.md's "What a page does with it" row says a run is steps (send, click, type, read) and no longer "No clicks or typing yet" or "only Home's farming card and farm list", and its "What a page is told" row names `UpgradePage`; docs/debt.md's first row says only what is left (Home's other states, and the pages no test opens); docs/code-map.md names `UpgradePage`, the steps, `PageRuns` and the new test files, and no longer lists the counts banner and "Too little farming" under `AppPageTests` - `.\tools\check.ps1` (mapcheck), and `git grep -n "No clicks or typing yet" -- docs/quality.md` and `git grep -n "still asserts on source text" -- docs/debt.md` print nothing

## Edge cases
- **Nothing yet:** before any plan Campaign shows its ready-made goals, and the banner is there too. With no
  inventory export the Upgrades answer says so and has no gear; the box then has nothing to list and stays shut
  (the engine's half is tested, the page's isn't).
- **Too little:** one crawl in the band where a place needs `Ask.MinCrawls`: "Too little farming", never "No farming".
- **Play the log never saw:** the banner itself. Both ways out are read: a count the player set (the engine builds
  that message from the same log with the count entered) and a reincarnation the log saw.
- **A second copy:** each run still has its own folder and profile. Up to six fixtures now open at once beside the
  harness's own tests; six at once took 1.3 s in a scratch run.
- **Starting and stopping:** steps run in order and a step that can't be done ends the run there. That run fails
  and tidies up as any other. New page data re-asks Ask's open question, as in the app, and the tests expect it.
- **Same name, different thing:** two copies of one item are one line in the list, "2 copies". A selector that
  finds several things clicks the first one the player can see; a hidden match is never clicked.
- **A post behind a timer:** Upgrades posts its request 120 ms after the last change. The run gives the page that
  time after each step, so `Posted` has it; the test reads the last request, not how many came before.
- **A message that isn't the page data:** answers (`ask`, `upgrade`) follow the data in a run, and data may come
  again mid-run. "Took the page data" stays about the last message without a kind.
- **WebView2 isn't Chrome:** the box's filter is proven in Edge, not WebView2: the page draws the list itself, so
  the browser's popup isn't in it. The datalist invariant and the box's ARIA markup stay as source tests.
- **A tab named like its group:** Set counts on Campaign lands on Roster, the other tab of the same group.
- **Locale and clock formats:** "since Nov 14" comes from core.js `dt` in en-US; the tests build it from the
  message's `logStart`. Upgrades shows the export file's clock time; the test doesn't read it and nothing keeps it.
- **Window size and scaling:** a click is on the element, not a point, so it doesn't depend on the layout.
- **Something leaving the PC:** nothing new. The inventory export is made up, written to a temp folder and removed.
- **A message no player gets:** only Roster's "the log saw one" sets values (`logged` and `rank` of one race, both
  at paths the engine wrote). Everything else is as the engine built it, the Upgrades answer included.
- **Errors from clicks:** the pages' own handlers that wait (report.js's Report a problem, share.js's Copy) sit
  behind the rail's button and Settings' share card. No click here reaches them, so a copy of roster.js with a slip
  stands in. No test: those two handlers, until a page test clicks them.

## Not doing
- Home's other states, and anything that says which state Home is in: docs/debt.md's second row. Home's counts
  tests read the fixture's message as built and the whole of `page-home`.
- The pages no test opens: Crawls, Loot, Relevels, Settings, Help, Report a problem, sharing.
- Keys (arrows, Enter, Escape in the box), Campaign's "click again to clear" after 4.1 s, pixels and report.js's
  picture. Tapping a tile in Roster's count entry.
- Changing what the Upgrades answer holds. It moves as it is.
- Starting Edge once for the whole run: 15 starts is under the ceiling of about 20.
- A checked-in log that reaches the cap or has a reincarnation in it.

## Where
- `tests/EqlPathfinder.Engine.Tests/PageHarness.cs`: stubs. `PageHarness.Run(page, steps…)` sends the messages and
  skips every other step, and `PageRun.At(name)` gives the end of the run. `PageStep.Send/Click/Type/Read` are the
  steps. `HeadlessEdge.Dump` takes `--virtual-time-budget`, and the `pagetest` handler becomes the list of steps
  with the reading after the last. A click is mouse down, focus, mouse up, click (the list picks on mouse down);
  typing focuses the box, replaces its text and fires `input`. Names and wording can change, with the tests.
- `src/EqlPathfinder.Engine/Pages/UpgradePage.cs`: a stub that returns an empty answer. `MainWindow.UpgradeMessage`
  moves here as it is; `MainWindow.SendUpgrade` and `DevSnapshots.Run` (`UpgradeDemo`) call it.
- Written, not stubs: `PageMessage.cs` (`WithCountsSet`, `FromFixture(plan: false)`, `Answer`, `UpgradesAnswer`),
  `PageRuns.cs` (`AtOnce`, which `HomePages` now uses, and `IsRosterWithItsCountsOpen`), and the tests in
  `HomePageTests.cs`, `CampaignPageTests.cs`, `RosterPageTests.cs`, `AskPageTests.cs`, `UpgradePageTests.cs`,
  `UpgradesAnswerTests.cs`, `PageHarnessTests.cs` and `AppPageTests.cs`.
- `AppPageTests.cs` keeps: the datalist invariant, the box's markup, Report a problem's markup and shortcut, the
  clock times, core as the only listener, and the four that read the app's C#.
- `docs/releases/next/page-tests-other-pages.md`: nothing changes for players (`src/` changes, so `finish` asks).
- What a scratch run showed (not checked in): with `--virtual-time-budget=10000`, an async loop over the steps with
  a `setTimeout` between them, and a snapshot of every `[id]` at each read, one start took 0.6 s and six at once
  1.3 s. Focus, `input`, mouse down on the list and the 120 ms post all worked in headless Edge, and every string
  these tests expect was read from the real pages with the tests' own steps. With `MainWindow.UpgradeMessage`'s
  body in `UpgradePage.Build` for a moment, the three answer tests passed.

## Reviews
- architect: nothing to fix, from reading the diff. The Upgrades answer in the engine is the window's old code line for line; readings taken mid-run are still one batch fixed before Edge starts; debt row 1 as rewritten matches what the tests now open; qa's tests kept their meaning; a trial merge with main drops the already-released page-test-harness note. Two debt lines logged: "took the page data" is judged after the last data message and not again at the end of the run (webcheck still holds that rule), and later states in a run are drawn over earlier ones.
