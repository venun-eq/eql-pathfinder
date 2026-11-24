# Numbers audit: is the math right, and can we prove it?

**Audit, 11-19 (main at 70601d5, v0.10.15). Not a change of its own:** this page is the index of the drafts it led
to. Each draft is solved in its own branch and session, in the order below. It follows [calc-audit](calc-audit.md),
whose eight fixes are on main.

**Why:** the user, 11-19: "Please perform another review as you did in the beginning, double check the work and see
where we stand ... it seemed like there were a lot of very big/long files, and perhaps our engine is in need of a
refactor. I want 100% confidence in our numbers, and our math."

## What the three checks found

- **The numbers match real play.** An independent recompute, from the docs' definitions and the database's rows, not
  the engine's code, matched everything checked:
  - kills, T5, chest and above-T5 on 1,333 of 1,333 visits, from the database's rows. From the raw log, kills on visits
    with a charmed pet aren't proven yet: 68 of 1,335 differ, 207 of 85,529 kills
    ([numbers-check-skill](numbers-check-skill.md));
  - every rate and the gate on 1,333 of 1,333;
  - the usual, band and verdict on 476 of 476 farming and grouped rows;
  - kill speed on 51 of 51.

  Ten visits worked out by hand from the raw log matched field by field: leveling, a D4 farm, one at 50, a raid,
  unfinished, a trip outside, a death, kills after "completed", and the latest. It ran on 0.10.15's re-read (`import`).
  The player's app was still on 0.10.14 at the time, with kills counted the old way until it restarts.
- **Small engine issues, from that check:**
  - one visit's edges were read wrongly, 0.31 T5-eq ([visit-boundaries](visit-boundaries.md));
  - DPS misses 1-point hits and a new pet's first hits ([damage-lines](damage-lines.md));
  - kill XP at 50 counts as levels ([xp-at-cap](xp-at-cap.md)).
- **One plain bug:** Loot's "% from Reward Chests" counts chest fragments as whole motes
  ([loot-tiles-in-engine](loot-tiles-in-engine.md)).
- **calc-audit's fixes:** six are done as their specs say. one-rate and engine-owns-numbers are partly done:
  - cost per level has two functions ([farm-rate-value](farm-rate-value.md));
  - the community job keeps its own rules ([community-same-rules](community-same-rules.md));
  - page scripts still work out some numbers ([page-math-to-engine](page-math-to-engine.md)).
- **What stands between the code and proof is not file size:**
  - No test holds the numbers on screen to hand-checked values ([golden-numbers](golden-numbers.md)).
  - Some math has inputs it can't see: the clock ([one-clock](one-clock.md)), a farm rate set as a side effect
    ([farm-rate-value](farm-rate-value.md)), and ten caches keyed by hand ([cache-keys](cache-keys.md)).
  - The verdicts live inside the tracker's service ([verdicts-out](verdicts-out.md)).
- **The math functions themselves are sound and pure:** `Rates`, `ActiveTime`, `Valuation`, `LootTally`, `RunPace`,
  `FarmPace`, `KillSpeeds`, `Fit.Slope`. They need tests and plain inputs, not a new layer.

**What not to refactor:**
- `Campaign.cs`: large, but pure and tested.
- `Tracker.cs`: one state machine. Splitting it risks a re-read for nothing.
- `LineParser.cs`: a table of patterns.
- `App.xaml.cs`: host wiring with no math.
- `DevSnapshots.cs` and the page scripts: their size is markup.

## The drafts, and what each waits for

| Draft | Tier, budget | Waits for |
|---|---|---|
| [numbers-check-skill](numbers-check-skill.md): the real-data check, kept and run again. **First:** its scripts are in a scratch folder | Small, 60k | nothing |
| [golden-numbers](golden-numbers.md): hand-checked values for every number on a visit | Trivial (tests), 40k | nothing |
| [loot-tiles-in-engine](loot-tiles-in-engine.md): the chest-share bug; the tiles from the engine | Small, 60k | nothing |
| [xp-at-cap](xp-at-cap.md): XP at 50 isn't levels; one XP per kill | Standard, 300k | nothing |
| [one-clock](one-clock.md): the time comes from one clock that's given | Small, 60k | nothing |
| [farm-rate-value](farm-rate-value.md): the farm rate and cost per level, one place each | Small, 60k | nothing |
| [community-same-rules](community-same-rules.md): the job's DPS, gates and idle follow the app | Standard, 300k, job | nothing |
| [visit-boundaries](visit-boundaries.md): two visits' edges read wrongly | Investigation, then risky | nothing |
| [damage-lines](damage-lines.md): 1-point hits, a new pet's first hits | Risky, 600k (small in effect). **Left out (the user, 11-19)**; its 1-point fix goes into visit-boundaries if that one re-reads | nothing |
| [share-without-derived](share-without-derived.md) (from calc-audit) | Risky, 600k. **Left out (the user, 11-19)** | nothing now |
| [cap-fixture](cap-fixture.md): a test log that reaches 50, and `scrub` fixed | Small, 100k. **Built 11-19** | golden-numbers |
| [verdicts-out](verdicts-out.md): verdicts in pure code | Standard, 300k | one-clock, farm-rate-value |
| [page-math-to-engine](page-math-to-engine.md): the rest of the page scripts' math | Standard, 300k | xp-at-cap |
| [verdicts-as-of-visit](verdicts-as-of-visit.md): the place check and weights as of the visit | Standard, 300k + designer | verdicts-out |
| [cache-keys](cache-keys.md): caches that can't go stale | Standard, 300k | verdicts-out |
| [sample-gates](sample-gates.md): one named smallest sample for each purpose | Standard, 300k + designer | verdicts-out, community-same-rules |
| [calc-docs](calc-docs.md): every number's definition, function and test, and a golden History | Trivial, 60k | all of the above |

**After page-math-to-engine (drafted 11-19, outside the first estimate):** the math its "Not doing" left in the pages.
calc-docs doesn't wait for them; each updates its rows when it lands. **The user, 11-19:** build
overlay-metric-tones (the bars follow the verdict) and journey-race-in-engine, as new budget; leave tile-sums-in-engine
and ask-xp-per-kill for now.

| Draft | Tier, budget | Waits for |
|---|---|---|
| [journey-race-in-engine](journey-race-in-engine.md): Relevels' and Home's race (re-figured relevels, band, splits, `ahead`) | Standard, 300k | nothing; unblocks debt row 13's fix |
| [tile-sums-in-engine](tile-sums-in-engine.md): Loot's and Crawls' tiles for the filters shown | Standard, 300k | nothing |
| [overlay-metric-tones](overlay-metric-tones.md): the overlay's bars say what the verdict says | Small, 60k; standard +100k if the bars follow the verdict | nothing |
| [ask-xp-per-kill](ask-xp-per-kill.md): Ask's "Both" XP per kill over the kills whose XP is known | Risky, 600k, job | nothing |

```
one-clock ───────┐
farm-rate-value ─┴──► verdicts-out ──┬──► verdicts-as-of-visit ──┐
                                     ├──► cache-keys ────────────┤
community-same-rules ────────────────┴──► sample-gates ──────────┤
golden-numbers ───────► cap-fixture ─────────────────────────────┤
xp-at-cap ────────────► page-math-to-engine ─────────────────────┼──► calc-docs
numbers-check-skill, loot-tiles-in-engine, visit-boundaries,     │
damage-lines, share-without-derived ─────────────────────────────┘
```

Drafts that wait for nothing can run at once. Two pairs touch the same code, so finish the smaller of each first:
community-same-rules and share-without-derived (`CommunityBuilder`), and xp-at-cap and loot-tiles-in-engine (none
today, but both edit page scripts). The real-data check (numbers-check-skill) is run again after each one that changes
a number.
