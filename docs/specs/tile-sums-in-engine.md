# Loot's and Crawls' summary tiles are worked out where every other number is

**Draft, 11-19, not started** ([numbers-audit](numbers-audit.md), from [page-math-to-engine](page-math-to-engine.md)'s
"Not doing"). **Tier:** standard, 300k. Players see no change if it's moved faithfully. **Waits for:** nothing.
Pays the first part of docs/debt.md's `crawls.js`, `loot.js`, `race.js` row.

**Why:** players see no change. The totals in the tiles at the top of Loot and History → Crawls are added up by the
page itself, for whatever filters are picked, and only two of the ten are tested. One could drift from the same
number elsewhere (History's rows, Home, the overlay) and nothing would notice. After, the engine adds them up for the
filters shown, tested like every other number, so a tile and the rows under it can't disagree.
**Players:** everyone who filters Loot or Crawls.

```text
Loot  [ Last 24h ▾ ] [ All places ▾ ] [ All sources ▾ ]
┌──────────────────────┬───────────────────┬──────────────┬──────────────────────────┐
│ ⟦T5-eq⟧ · ⟦motes and │ ⟦fragments⟧ ·     │ ⟦T5-eq of it │ ⟦T5-eq per active hour   │
│ fragments⟧           │ ⟦T5-eq⟧           │ above T5⟧    │ in instances⟧, or        │
│                      │                   │              │ ⟦% from Reward Chests⟧   │
└──────────────────────┴───────────────────┴──────────────┴──────────────────────────┘
History → Crawls  [ search… ] [ ] hide short  [ kind ▾ ]
┌──────────┬──────────┬──────────┬──────────────┬──────────┬──────────┐
│ ⟦visits  │ ⟦active  │ ⟦levels  │ ⟦T5-eq per   │ ⟦T5-eq   │ ⟦of it   │
│ shown⟧   │ time⟧    │ gained⟧  │ active hour⟧ │ looted⟧  │ above T5⟧│
└──────────┴──────────┴──────────┴──────────────┴──────────┴──────────┘
⟦ ⟧ = moves from the page to the engine. Same words, same values.
```

## The cause

docs/engineering.md (engine-owns-numbers). The tiles over the visits shown are added up in the page, because
the filters are the page's: the engine can't send the totals ahead for free-text search. Only the chest share
(`LootTilesPageTests`) and the T5 tile's dash (`RatePageTests`) are tested.

## What the page works out today (as of 05405d9)

- **loot.js:** filters (:25) are time (from `Date.now()`), zone and source. Sums (:35-56): motes, fragments and chest
  per tier, total T5-eq, above T5, the chest share, and the instance T5-eq an hour over visits with a rate. The
  table's rates (:81) and zone totals (:68). Inputs: the `kind:"loot"` message (`TrackerService.AllLootCompact` :608,
  `MainWindow.SendLoot` :237), `ledger`, History's visit rows, `settings.fragmentValue`.
- **crawls.js:** filters are search, hide-short and kind. Sums (:151-173): count, active hours, levels gained, T5-eq
  an hour over visits with a rate, T5, above T5; kind counts and per-relevel counts (:158).

## Proposal

- A question to the window on each filter change, as Ask's is: the page sends the filters, the engine answers with
  the tiles. The page keeps only the filtering of rows it lists, and draws the engine's totals.
- Edges to pin, each with a test: Loot's T5-eq an hour ignores the source filter; the zone filter matches the loot
  row's place (from `visits.zone`) to `zoneShort(baseZone, difficulty)`; "24h" counts from the engine's clock
  (one-clock), not the page's; no visit shown with a rate is a dash, not 0.

## Not doing

- Folding `relevelStops` into `Journey`: that is [journey-race-in-engine](journey-race-in-engine.md).

## Where

- `src/EqlPathfinder.Engine/Analysis/LootTally.cs`, `Rates`; `Live/TrackerService.cs`; `src/EqlPathfinder.App/MainWindow.xaml.cs`
  (`OnMessage`, the new question)
- `src/EqlPathfinder.App/web/js/loot.js`, `crawls.js`
- `tests/EqlPathfinder.Engine.Tests`: `LootTilesPageTests`, `RatePageTests`, a Crawls page test (debt's page-tests row)
