# A test log that reaches the cap

**Built 11-19** ([numbers-audit](numbers-audit.md)). **Tier:** small, built by an engineer (`scrub` changes no upload).
**Pays:** docs/debt.md's `LogScrubber` row and the "a checked-in log that reaches the cap" parts of the page-tests rows.
**Unblocks:** calc-docs's golden History.

**Why:** the one fixture (`TestLog.BefallenUnrest`) stops at level 23, so nothing at the cap had a golden value (farming,
raids, kills counted from slain lines at 50, at-cap idle). Most hours are at the cap. `scrub` couldn't make a fixture: it
kept the player's name and every clock time, so the first fixture was rewritten by hand.

## Done when

- [x] `scrub` writes the stand-in's name for the character's (the pet's too: "Standin`s warder"), and moves every time by one amount so the first kept line reads 12:00:00, keeping the weekday with the date, across midnight - `LogScrubberTests`
- [x] A made-up raw log with chat, a guild, an inviter and another player's kill scrubs to a file that passes `FixtureTests`' name, kind and clock checks - `AScrubbedLogPassesTheChecksEveryFixtureIsHeldTo`
- [x] A scrubbed stretch at level 50 is checked in (Nov 17-18: a raid, a finished crawl at the cap, open world) and passes `FixtureTests` - `Fixtures/eqlog_Standin_oggok_cap-nagafen.txt`
- [x] Its raid and its finished crawl hold hand values: clock, kills (a groupmate's and the pet's counted), none an XP kill, CapPercent, T5-eq, and rates null under 8 kills - `CapGoldenTests`

## Edge cases

- Midnight inside the stretch: a time after it takes the previous date, and its weekday follows (tested).
- A word in a name's place: only whole-word matches of the character's name are replaced.
- Kinds of line the stretch brought that no fixture had (casts, damage over time "from your", a quest NPC's gift and hand-in,
  an invite) were each looked at and added to `FixtureTests.KindsOfLine`; raid bosses that read like players' names to its
  name check were added to its list of mobs.
- A raid never completes: its clock is the whole visit and nothing is "after". It has under 8 kills, so no rate (never 0).
- The 14-hour gap in the stretch (an AFK night) is between visits, not inside one.

## Not doing

- At-cap idle: no gap between kills passes 3 minutes inside any visit in the stretch, so idle at the cap is held only on
  GoldenNumbersTests' picked gaps. A stretch with a real gap is a later fixture.
- XP per kill at 50 as a number: with every kill a cap kill it is null (held); the rule itself is KillsAtCapTests'.
- Rendering the cap page (another branch).

## Where

- `src/EqlPathfinder.Engine/IO/LogScrubber.cs`, `src/EqlPathfinder.Cli/Program.cs` (`scrub`)
- `tests/EqlPathfinder.Engine.Tests/`: `LogScrubberTests`, `CapGoldenTests`, `FixtureTests` (checks now callable on any lines), `TestLog.CapNagafen`, `Fixtures/`

## Reviews

- architect: on Sonnet, no must fix; the fixture is clean (no other players, chat or guild; times start at 12:00:00), the scrubber shifts every stamp by one amount, and the hand values at 50 re-derive from the raw lines and docs/pace.md. Should fix, fixed by the lead: whole-word name matching had no test (`OnlyTheWholeNameIsReplaced`). Left as is, the reviewer's advice: the fixture keeps the real dates of play (Nov 17-18), not the time of day.
