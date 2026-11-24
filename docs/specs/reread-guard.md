# A parsing change can't ship without re-reading old logs

**Built 11-18 from the draft of the same day** ([calc-audit](calc-audit.md)). **Tier:** trivial (a test only). **Waits
for:** nothing. **Unblocks:** kills-at-cap.

**Why:** a visit's facts (where it starts and ends, seconds inside, kills, reward sizes, loot) are decided once, when its
line is read, and stored (the JSON of each row in the store's `visits` table). A change to those rules reaches old visits only when
`EngineState.CurrentVersion` is raised (now 11), so the app reads every log again. Today that rests on whoever changes
the parser remembering it (docs/engineering.md, Modeling 6). Forgotten, old and new visits are figured two ways, and
every comparison between them drifts with no error.
**Players:** everyone with history from before the change.

## Proposal

- A test parses the fixture (`TestLog.BefallenUnrest`) through `PathfinderEngine`, writes what the tracker emits
  (closed visits, kill, loot and level records) in a fixed order, and hashes it.
- The test holds one pair: the `CurrentVersion` and the hash at that version. A different hash at the same version
  fails with "what's read from logs changed: raise `EngineState.CurrentVersion` and mark the note `reread: true`". A
  raised version with the new hash passes, and the pair is updated in the same commit.
- The fight meter has its own `CurrentVersion` (`FightMeterService`): the same test for `CombatParser`, or a line under
  Not doing saying why not.

## Where

- `tests/EqlPathfinder.Engine.Tests/` (beside `GoldenTests`, which checks hand-checked numbers but not the version)
- `src/EqlPathfinder.Engine/Tracking/EngineState.cs`: `CurrentVersion`
- docs/quality.md: a line among the recipes. docs/engineering.md, Modeling 6: "a test holds it".

## Not doing

- A test that serializes the computed getters `Visit` writes into `visits.json` (`LevelsGained`, `RewardPercent`,
  `RewardSizing`, `IsCrawlish`, `MedianKillSeconds`, `EndTime`). Nothing reads them back, so they're recomputed on load.
  Leaving them out of the stored JSON is a stored change of its own (docs/debt.md).

## Done when

- [x] A change to what the tracker reads fails a test that names the version to raise and the hash to set -
  `WhatTheTrackerReadsChangesOnlyWithItsVersion`. Seen to fail: a kill's percent nudged by 0.01% in `Tracker`
- [x] The same for the fight meter, whose own `CurrentVersion` is 5 - `WhatTheFightMeterReadsChangesOnlyWithItsVersion`.
  Seen to fail: a slain mob counted twice in `FightTracker`
- [x] A number worked out on load can change with no re-read - `ANumberWorkedOutOnLoadIsNotPartOfTheHash`. Seen to
  pass: `RewardPercent` doubled
- [x] A fact the store keeps in a column beside its JSON (a fight's mob and zone) is hashed -
  `AFactTheStoreKeepsInItsOwnColumnIsPartOfTheHash`
- [x] The same read gives the same hash - `TheHashIsTheSameOnEveryRead`
- [x] docs/quality.md has the recipe, docs/engineering.md's Modeling 6 says a test holds it, docs/code-map.md names it

## Edge cases

- **What's hashed is facts:** every public property with a public setter, by name and in name order, whatever the
  store's own JSON attributes say. The end `EngineState` is hashed too (the roster, the level, what a re-read
  rebuilds), without its `Version`, which is held beside the hash.
- **The rules change** where the fixture has no lines (the cap, raids, reincarnations): the hash doesn't move. The
  recipe says to add a few scrubbed lines of that play to what's hashed first. kills-at-cap is the first, and its
  draft says so.
- **A raised version with nothing changed** (a re-read for lines the fixture lacks): the test fails until the pair
  says the new version, with the same hash.
- **Updating the hash without the version** passes the test. It's a visible edit to a line whose message says what to
  do, and the architect reads every change under `src/` that comes with it.

## Reviews

- lead: a test and docs only, so no role reviews it (Trivial). Each guard was seen to fail on a broken rule, and to
  pass on a changed worked-out number.
