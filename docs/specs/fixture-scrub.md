# The checked-in log carries no player's name and no play time

**Why:** The one log in `tests/EqlPathfinder.Engine.Tests/Fixtures/` holds a real character's name 85 times and in
its file name, and the hours it was played, against docs/quality.md rule 6. The next branch builds a page-data
message from it for the page tests (docs/debt.md, row 1), so the name would be in everything those pages draw. And
nothing stops it coming back: `scrub` leaves your own name and the times, and no test looked.
**Players:** nobody sees it. It protects whoever's log a fixture is cut from.

## Done when
- [x] The log is `Fixtures/eqlog_Standin_oggok_befallen-unrest.txt`, and no file there is named for anyone else - `AFixturesFileIsNamedForTheStandIn`
- [x] Wherever a line says who did something (the line's subject, who was hit, who made the kill, whose pet, whose instance, whose /who line), it's `Standin`, one of Standin's own pets or a mob - `AFixtureNamesNoPlayerButTheStandIn`
- [x] Its clock starts at 12:00:00, never goes back, and each stamp's weekday goes with its date - `AFixturesClockStartsAtNoon`
- [x] It holds no chat, no guild tag and no kind of line nobody has looked at - `AFixtureHoldsOnlyWhatTheGameWroteAboutPlay` (passes today; seen failing on a planted file with a group line, a guild tag, a cast line and a line with no stamp)
- [x] The tests that read the log prove what they proved, on the same hand-checked numbers - `GoldenTests` (all nine cases), `LedgerCountsEveryTierAndEveryFragment`, `EveryPageGetsTheDataMessageItGotBefore`, `QueriesBeforeTheLoopRunOneAtATime`
- [x] Nothing but the name and the stamps changed in its 9,651 lines - this prints nothing, in Git Bash: `diff <(git show main:tests/EqlPathfinder.Engine.Tests/Fixtures/eqlog_Venun_oggok_befallen-unrest.txt | cut -c28- | sed 's/Venun/Standin/g') <(git show HEAD:tests/EqlPathfinder.Engine.Tests/Fixtures/eqlog_Standin_oggok_befallen-unrest.txt | cut -c28-)`
- [x] Nothing under `src/` changed, so there's no release note - `git diff --name-only main...HEAD -- src` prints nothing

## Edge cases
- **Same name, different thing:** seven mobs read like a player by their capitals ("Abomination of Ro", "Ice boned
  skeleton hits ..."). They're listed in `FixtureTests.MobsThatReadLikeAPlayer`, each checked to be a mob. A new one
  fails until it's listed. A pet is known by its own "Attacking ... Master." line in the same log; "X`s warder" must
  be Standin's. "Standin" is no word in any line today, so the replacement can't collide.
- **The name's length and order:** no assertion on the log depends on either. The golden numbers hold no name, the
  page data is compared with itself, the ledger's key comes from `TestLog.Key`. The engine knows the warder as yours
  only by `<name>`s `, so the name is replaced in all three shapes: the warder's 78 lines, "Player ... creating
  instance" (5) and the /who line (2).
- **The rules change:** only the time of day moves. The date stays Sat Nov 14, after `RouteAnalysis.FarmPatch`, and
  the two hours cross midnight neither before nor after, so no crawl changes sides of a dated rule.
- **Locale and clock formats:** stamps keep the game's form, `[Sat Nov 14 12:00:00 2026]`. The log reader ignores
  the weekday, so the test checks it.
- **A second copy:** after the rename the old file lingers in `bin` until a clean build. `FixtureTests` reads the
  checked-in folder and `TestLog` asks for the new name, so neither sees it.
- **The pipeline itself (a clean copy):** `FixtureTests` finds the folder from its own source path, as
  `AppPageTests` does. An empty `Fixtures/` fails, so the checks can't pass on nothing.
- **Something leaving the PC:** nothing is sent. A failing check prints the offending line, on this PC only.

## Not doing
- Rewriting git history: the old file stays in it under its old name. Commits never leave this PC.
- `scrub` itself (`src/EqlPathfinder.Engine/IO/LogScrubber.cs`): it leaves your own name, your warder's and the
  times, and its comment says it's used for bug reports, which nothing does. It's under `src/`, so it's a debt row
  for the architect, and the gate catches what it leaves. docs/quality.md rule 6 now says so.
- The same name in the inline lines of 17 other test files (116 times), and another player's name there (29 times,
  also in docs/fight-meter.md and a comment in `Combat/CombatParser.cs`). Rule 6 covers them. Some of those tests
  assert on the name and its order (`FightMeterPetTests`, `CharmTests`), so it's a branch of its own.
- The character's name in comments and docs (16 times in 9 files under `src/`, five pages under `docs/`), in the
  self-test's probe (`App.xaml.cs:151`), and as the package's author (`tools/release.ps1`).
- The server's name, `oggok`: a public server, not a person, and the reader needs one in the file name.
- The pets' names (Jarann, Zebektik and nine more): the game makes them up.
- The date. Only the time of day says when someone was at the keyboard, and dated rules read the date.

## Where
- `tests/EqlPathfinder.Engine.Tests/Fixtures/`: `git mv` the log to `eqlog_Standin_oggok_befallen-unrest.txt`,
  replace `Venun` with `Standin`, and add the same amount to every stamp so line 1 reads 12:00:00 (the last line
  then reads 14:06:25). Keep the line endings and the order. Don't write the amount down anywhere.
- Already written, and not for changing: `FixtureTests.cs` (the gate), `TestLog.cs` (the stand-in's name, once, and
  the way to the log: the page-test harness uses `TestLog.BefallenUnrest` and `TestLog.Logs`), and the four tests
  that read the log. `GoldenTests`' four start times are already on the moved clock.
- `docs/quality.md` rule 6 and `docs/code-map.md` section 6: already reworded.

## Reviews
