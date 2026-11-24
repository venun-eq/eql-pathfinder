# Quality: tests first, and the cases that have bitten

The page of whoever writes the tests: the lead on a small change, the `engineer` on a standard one, `qa` on a risky
one (docs/roles.md). The spec and the failing tests come before any code (docs/specs/README.md).
When a bug reaches players, its case goes into the list below in the same branch as the fix, so the next spec asks
about it.

## Where each kind of thing is tested

| What changes | Its proof | Run only that |
|---|---|---|
| The engine: parsing, tracking, Ask, planning, upgrades, the fight meter, community numbers, sharing | `tests/EqlPathfinder.Engine.Tests/<Area>Tests.cs`: real log lines in, facts out. docs/code-map.md says which file covers what | `dotnet test tests/EqlPathfinder.Engine.Tests --filter "FullyQualifiedName~CampaignTests"` |
| What a page is told: the data message, an answer | The same tests, on the type that builds it (`PageData`, `TrackerService`, `AskPage`, `UpgradePage`), written with `PageJson.Options` as the window writes it. `PageDataTests` lists every field of the data message: a new one is added there too | As above |
| What a page does with it: the page scripts | `tests/EqlPathfinder.Engine.Tests/<Page>PageTests.cs`: the real page in headless Microsoft Edge (`PageHarness`), read as a player reads it. A run is steps, done in order (`PageHarness.Run`): `PageStep.Send` is the window sending a message (the fixture's page data or an answer the engine built, from `PageMessage`, with the few values the test is about set by `With`), `Click` and `Type` are the player, on the first element a selector finds that they can see, and `Read` keeps the page as it is just then under a name. After each step the page gets 200 ms of its own time, so a post behind a short timer has been made. The test reads `run.At(name)` or the end of the run: `Text(id)` for what's drawn in an element (a hidden one reads as nothing), `Posted` for what the page had told the window, `Page` for the page that's open. Any script error fails the test, from a click's handler too, and a click on nothing the page shows fails as the test's own slip. Edge must be installed: without it these tests fail and say where they looked, they are never skipped. One run can cover a page's states and its click, and a fixture opens its runs at once through `PageRuns.AtOnce`: each start of Edge costs about half a second alone, and past about 20 starts in all the browser should be started once for the run. Home's farming and counts, Campaign, Roster, Ask and the Upgrades item box are there (docs/debt.md has what's left): for anything else a behaviour is still proven by a screenshot the designer checks, so put the decision in the engine where a test can reach it (docs/engineering.md). `tools/webcheck.mjs` proves the scripts load and take messages, and `AppPageTests.cs` reads the source for what only goes wrong in WebView2 | `dotnet test tests/EqlPathfinder.Engine.Tests --filter "FullyQualifiedName~HomePageTests"` (every page's: `~PageTests`; the harness's own: `~PageHarness`); `node tools/webcheck.mjs` |
| The window's own code: `src/EqlPathfinder.App/*.cs` | The test project can't load the app. Put the decision in the engine and test it there. What's left in the window is proven by a self-test's output (`EQLPATHFINDER_SELFTEST`, the `app-screenshots` skill) or, as a last resort, a source-text test in `AppPageTests.cs` | The self-test, in an isolated copy |
| The server | `server/test/*.test.ts`: vitest in workerd, on the real migrations | `npm run test` in `server/` (`npm install` once in a new worktree) |
| The pipeline's scripts and hooks | Samples inside `tools/check.ps1` | `.\tools\check.ps1` |
| Everything | `.\tools\check.ps1`, which is what `finish` runs | |

## Tests first

1. **One proof for each "Done when" line.** A test's name is a sentence about what the player gets:
   `ASessionHasNoRateUntilSixMinutesInCrawls`.
2. **It fails before the code exists, on its assertion.** When a test needs a member that isn't there yet, add the
   smallest stub that compiles, and nothing else. When nothing is broken yet (a refactor, a guard against a slip),
   a test that passes today counts once it's been seen to fail: break by hand what it guards, watch, put it back.
3. **A bug starts with the smallest real input that shows it:** a few log lines (docs/log-formats.md), one data
   message, one command of the command-line tool (the `game-data` skill). No reproduction, no fix: say what you tried.
4. **Test what happens, not how the code is written.** A test that reads source text breaks on a rename and passes
   on a wrong result. Use one only for an invariant nothing else can reach, and say in a comment what it protects.
5. **Nobody weakens a test to pass it.** A test that's wrong is corrected in a commit of its own that says why, by
   whoever holds the branch, and the architect's review reads that commit first.
6. **Fixtures never hold a real name, chat line or clock time.** Copy the few lines that matter and replace the
   names. A log checked in under `Fixtures/` is about the made-up `Standin` (tests reach it through `TestLog`),
   and `FixtureTests` fails on anyone else's name, a chat line, a guild, a kind of line nobody has looked at, or a
   clock that doesn't start at 12:00:00. The command-line tool's `scrub` does all of it: it drops chat, guilds and
   other players' kills and invites, writes `Standin` for the character's name, and moves every line's time by one
   amount so the first reads 12:00:00 (`LogScrubberTests`). Run it on a stretch and check the result in;
   `FixtureTests` says what it still misses. A message built from a fixture (the page data, an answer) carries
   its names and times, so a test builds it on each run and it's never checked in.
7. **A page test that says something is absent also says what is there, on the same element.** A negative alone
   passes on a page that hasn't drawn yet, and a hidden card reads as nothing. And a test that says which number
   is where gives the message two different numbers first: the fixture's session had one rate in crawls and by
   the clock, so Home's first page tests passed a page that drew either in the other's place (11-18).

## Recipes

Each of these was worked out more than once on 11-18, at a few dollars a time.

- **A page test.** Copy the shape of `HomePageTests.cs`. A fixture class opens its runs once, at once
  (`PageRuns.AtOnce`). A run is a `PageMessage` with the few values the test is about set by `With`, then the
  player's clicks. Each test reads one thing off a run: `run.At(name).Text(id)`, or `Posted`. The row for page
  scripts in the table above has the harness's rules, and rule 7 has the two mistakes Home's first tests made.
- **Proving nothing changed** (a refactor, code moved between files). Two proofs, each taken before the change and
  after it. What the pages are told: `PageDataTests.EveryPageGetsTheDataMessageItGotBefore` is the model, and it
  compares the whole data message, not field by field. What the pages draw: render main's build and the branch's
  with the same copy of the database (`--snapshot`, the `app-screenshots` skill) and compare the two folders of
  PNGs. Shots that are the same are also what lets the spec say `designer: not needed`.
- **A change to what's read from logs.** `RereadGuardTests` fails with the version to raise and the hash to set:
  do both in one commit, with the note's `reread: true`. Its hash covers facts only (a property with a setter), so
  a number worked out on load can change without it. It sees only what the fixture holds (levels 2 to 23, crawls at
  D1 and D4): a change to other lines (the cap, a raid, a reincarnation) adds a few scrubbed lines of them to what's
  hashed first, and watches the test fail on them.
- **A test that `Start()`s a TrackerService.** Keep the log's last line newer than `SilentLogMinutes`, or leave
  the service unstarted: the loop's first `Tick` ends the current visit and races the test (`OneUsualTests`, 11-18).
- **What's safe to run on this PC.** The user may be playing on it, with the installed app open.
  - Tests, `tools\check.ps1` and `node tools\webcheck.mjs` are safe: the page tests use headless Edge, and nothing
    opens a window. Run what you need once. Don't loop the suite, and don't copy the repository to run it again.
  - The app itself only as a render (`--snapshot <out> <a copy of the database>`) or in an isolated copy
    (`EQLPATHFINDER_DATA=<a scratchpad folder>`), never on the real `%LocalAppData%\EQL Pathfinder`. The
    `app-screenshots` skill has the commands, and what went wrong on 11-17 and 11-18 when this wasn't written down.
  - One render or app run at a time, and end only a process you started.

## Cases that have bitten

Go down this list for every spec. Write each case that applies under "Edge cases", with what should happen.

| Case | What went wrong once |
|---|---|
| **Nothing yet:** first run, or the log was turned on today | Pages planned from zeros as if they were facts |
| **Too little:** a sample below what the number needs | A two-minute crawl read as 124.6 T5-eq an hour on Home (0.10.3) |
| **Play the log never saw:** years before `/log on` | "Run 1 of 42" for a player six runs in. Ranks are a guess until counts are set |
| **A state the app never builds:** a test or snapshot that hand-makes a journey with 0 crawls | An unplayed reincarnation has no journey (`Journeys.Build` needs visits after it), so Home passed its tests while the reporter's unplayed relevel vanished (feedback #7). Build it as the log gives it: reincarnation lines through `PathfinderEngine` (or `Reinc` into a `Roster`), no visits, `Journeys.Build` empty; see `HomeStateTests` |
| **A second copy:** a developer copy, a self-test, two processes | 23 kills written twice. An isolated copy could share as the real install |
| **Starting and stopping:** a query during the first read, closing during a re-read | Queries ran together when the read ended and broke Home's refresh. A player was greeted as new |
| **The game isn't in the world yet:** patching, login, character select | "Your log is off" fired before a log line was possible, twice |
| **Same name, different thing:** two mobs with one name, same-second kills, a name on two servers, a mob named like a word | Real same-second kills looked like duplicates. A named mob was taken for a group member. A berserker's Frenzy hit a mob named "on ..." Two runs of one zone back to back were one Crawl on the fight meter (0.10.10) |
| **The rules change:** a patch, a bonus-XP event, a nerf | Fragments began to condense. Event play skewed comparisons. Crawls from before a nerf counted |
| **A game fact from a summary:** a spell's level or strength read from a guide's overview or a class page's summary | The class roles design said SHM slows from 38, ENC from 41 and BST only at 50 (11-19); the spell lists give 5, 9 and 20. Those were the strong slows' levels. Take a level from `spells_us.txt` (the game-data skill has the command), name the spell, and say which tier it is |
| **Old versions in the field:** older apps still upload and still read the community file | Fields are added, never given a new meaning; the old ones stay while old apps read them |
| **Re-sends and limits:** a reinstall, a lost ledger, a change to the math | A math change re-sent every player's history and hit the server's daily limit |
| **A message that isn't the page data,** and a name no script declares | Help's answers were taken for the page data, so opening Help could leave other pages without theirs (0.9.9 to 0.9.37). A leftover name threw before any page drew (0.9.37) |
| **WebView2 isn't Chrome** | Its datalist popup didn't filter. Ctrl+Shift+R reloaded the page |
| **A tab named like its group** | The tab couldn't be reached again once another was used (until 0.9.11) |
| **Locale and clock formats** | A time formatted another way would have stayed in a report's screenshot |
| **Window size and scaling:** narrow, 150% | Controls touched; a number's right edge was cut off |
| **Something leaving the PC:** a name, chat, a clock time | Never. Anything sent or captured is checked for all three, and `site/privacy.html` says what's sent |
| **A file format from one watch:** the game's own files (`dbg.txt`, the loadout files) tested only on lines the spec wrote out | A world entry's "Resetting UI." stayed pending, so the next zone-in read as a loadout swap (current-loadout, caught by the numbers check). A death respawning in the same zone read as a class change, and the overlay asked for a /who (11-20). Test the reader on a real session's file, with zoning after the entry and a death in it |
| **The pipeline itself:** a clean copy, PowerShell 5.1 | A parser kept the last value for every field (`-match` overwrites `$Matches`). A typecheck passed only where its generated types already existed. A folder named `releases` was ignored by git |
| **Something private in a tracked file:** an account ID, a login, the owner's name, a home folder | The code is public. `check.ps1`'s "nothing private" step refuses each (docs/pipeline.md, "Private settings"). A tool that needs one reads it from the environment and stops, naming the variable, when it's unset: it never falls back to a value in the source, and tests pass with none set |

## When a bug gets out

The fix's branch carries three things: the test that would have caught it (CLAUDE.md), the case in the list above
if it's a new kind, and the gate that would have stopped it when a script could check it, as `tools/webcheck.mjs`
was added after 0.9.37.
