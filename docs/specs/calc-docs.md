# How every number is worked out, on one page, and held by a golden History

**Built 11-19** ([calc-audit](calc-audit.md), [numbers-audit](numbers-audit.md)). **Tier:** trivial (docs and one pair of
tests), built by an engineer; no app change, so no designer and no release note (Help's "How Pathfinder counts" is
untouched). **Waited for:** every other draft in both audits; all are on main. **Unblocks:** nothing.

**Why:** the user, 11-18: "I would also like documentation of how all calculations are made. Even things like 'Active
time': what exactly that means." And 11-19: "I want 100% confidence in our numbers, and our math." A definition on a
page proves nothing unless a test holds the number to it.

## What was built

- `docs/calculations.md` (a line in docs/README.md): every number a player sees, with what it is, how it's worked out,
  stored or worked out when shown, its one function and the test that holds it to a hand-checked value. Where no test holds
  a number, its row says so; the list is in docs/debt.md.
- **The golden History:** golden-numbers (four Befallen crawls) and cap-fixture (the raid and the finished crawl) already
  held those visits' numbers by hand. Added only the whole History: `GoldenNumbersTests.TheWholeHistoryIsTheFourHeldCrawlsAndAFifthNotYetHeld`
  and `CapGoldenTests.TheWholeHistoryIsTheseInstancesAndNoOthers`, each listing every instance visit read off the log's
  "You have entered" lines. They found one visit with no hand value: The Estate of Unrest 4 (Befallen fixture, 13:50),
  and three at the cap.
- docs/code-map.md, docs/pace.md and docs/motes-and-fragments.md link to it.
- docs/pace.md: "The farm band" (0.25 by default, held to 0.15-0.6, from the log spread once there are 5 runs) and the place
  check's missing rule (a faster place needs 2 visits at the level, your own 1). `FarmPace.For` was read and says the same;
  `usual.mjs` now points at the doc.
- numbers-check skill: run from a worktree, never the main checkout; the charm figures (69 of 1,337 visits, 61 more and 8
  fewer, net 204 of 86,163 kills; also in docs/debt.md); place words listed as unproved; cut the live log at the dump's last time.

## Done when

- [x] Every number in the draft's list has a row, and each of the draft's "checked against real play" lines is true of the code on main (read again 11-19: idle, active, the clock, kills, the T5 and chest rules, the rate gate, the usual and its band, done) - `docs/calculations.md`
- [x] The whole History of each fixture is held - the two tests above
- [x] The farm band is in docs/pace.md and agrees with `FarmPace.For` - docs/pace.md, "The farm band"
- [x] The numbers-check skill's five corrections are made - the skill, docs/debt.md

## Not doing

- Hand values for the unheld numbers listed in docs/debt.md: each is its own fixture branch.
- Still not described anywhere: how a D0 re-entry with the open world's name is read (visit-boundaries).

## Reviews
