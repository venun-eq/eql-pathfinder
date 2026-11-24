# DPS counts a new pet's first hits

**Draft, 11-19, not started** ([numbers-audit](numbers-audit.md)). **Tier:** risky, 600k (what's stored, plus a
re-read), and small in effect: of the drafts, it matters least for the numbers. **Waits for:** nothing. Its other
half, hits "for 1 point of damage", went into [visit-boundaries](visit-boundaries.md) (the user, 11-19).

**Why:** when you summon or charm a new pet, its hits before the log first shows it attacking for you aren't
counted, so that visit's damage and DPS read low: 1.4% on the one visit checked. After, the pet's hits count from
its first one, whenever the log shows whose pet it is.
**Players:** pet classes, on visits where a new pet comes out. Nobody else sees a change.

```text
History → Crawls, the DPS column on one visit                        made-up DPS
┌──────────────────────────────┬──────────┐
│ Place                        │ DPS      │
│ ...                          │ ⟦412⟧    │   today
│ ...                          │ ⟦418⟧    │   after: +1.4%, as on the visit checked
└──────────────────────────────┴──────────┘
```

## The cause

The 11-19 check, against the raw log.
- **"for 1 point of damage" was never read.** Every damage pattern in `LineParser` needed "points": 7,529 of the
  player's own lines, about 0.002% of damage. Fixed in visit-boundaries.
- **A new pet's hits before its first "Attacking … Master." are missed.** v-20261115125432: 474,906 in History,
  481,648 in the log, so DPS reads 1.4% low on that visit. docs/fight-meter.md already says the tracker misses this.

## Proposal

- A pet is known from its first hit when the log shows whose pet it is before "Attacking … Master" (the fight meter's
  rule, docs/fight-meter.md), if the meter's parser already knows. Otherwise, say in Not doing what it would take.
- Raise `EngineState.CurrentVersion`, `reread: true`.

## Where

- `src/EqlPathfinder.Engine/Tracking/Tracker.cs` (damage, pets)
- `src/EqlPathfinder.Engine/Combat/CombatParser.cs` (only to read its pet rule), `tests/ParserTests`
