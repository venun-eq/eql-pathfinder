# Caches that can't serve an old number

**Tier:** standard. Part of [numbers-audit](numbers-audit.md). Pays docs/debt.md's `BuildSnapshot` caches row.

## Why
`TrackerService` keeps caches keyed by hand: a key that leaves out something its builder reads goes stale with no
error. On 11-17 the done card showed 4 T5-eq/h for a 57 crawl. Still missing before this: `_paceCache` left out loot
(a farming verdict lagged a drop by up to a minute at the cap) and every cache left out the settings its builder reads
(what a fragment is worth, the cap, the farm rate).

## Done when
- [x] `_paceCache` follows loot landing between kills (`PaceFollowsLootThatLandsBetweenKills`).
- [x] `_farmCache` follows what a fragment is worth (`FarmRateFollowsWhatAFragmentIsWorth`).
- [x] `_askCache` (and `_paceData`/`_judged` built on it) follows it too (`AskFollowsWhatAFragmentIsWorth`).
- [x] `_nextCache`, `_journeys`, `_paceCache`, `_askCache`, `_farmCache` and `_judged` all key on `SettingsKey()`. Proven by `PaceFollowsWhatAFragmentIsWorth`, `NextOptionsFollowWhatAFragmentIsWorth` (manual farm rate), `JourneysFollowTheLevelCap`, plus the two farm and Ask tests.
- [x] `_paceCache` and `_nextCache` key on `_revision`, which the service raises each time the log moves the tracker.

## Edge cases
- Clock: keys that read the clock keep their hour (or minute) term; tests use a fixed `TimeProvider`.
- Import and entered counts bump the revision, so nothing built before them survives.
- `_facts`, `_presetCache`, `_fixCache` already key on every input they read; left alone.

## Different from the first draft
One revision on the service, not one on each `Visit`: every mutation comes through the log read, so one counter covers
all of them with no edits in `Tracker`. `_askCache`/`_journeys`/`_facts` don't take the revision, because they cover
every visit and would rebuild on every line; Ask's out-of-instance loot and deaths arriving without a kill or a
closed visit are left to its hourly rebuild (debt).

## Where
`src/EqlPathfinder.Engine/Live/TrackerService.cs`, `tests/EqlPathfinder.Engine.Tests/CacheKeysTests.cs`

## Reviews

- architect: one must fix, fixed (4039d19): no test covered settings in the pace, next and journeys caches; three tests now do, and taking `SettingsKey()` out of each key fails exactly its own test. The revision can't miss a change (log reads, import, entered counts; a restart starts empty; settings are read each time). Ask's cache lag for out-of-instance loot and deaths was already on main: a debt row, accepted.
