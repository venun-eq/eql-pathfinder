# Loot's chest share counts chest fragments at their worth

**Built 11-19, small.** The chest share is fixed in the page; moving the tiles' sums into the engine is not done (see Not doing). Same words and layout, so no designer: only a value changes. ([numbers-audit](numbers-audit.md))

**Why:** a bug, confirmed in the code on 11-19.
- Loot's "% from Reward Chests" counts a chest fragment as a whole mote. `loot.js` adds every chest drop to `t.chest`
  (fragments too, line 42; or `chestMotes + chestFragments`, line 45), then works out
  `chestT5 = t.chest * t.value` (line 56) with no fragment value.
- The total it's divided by does apply it (`tierT5 = (motes + frags * fv) * value`).
- With the fragment value at 3.56, chest fragments count at a quarter or so of their worth, so the chest's share reads
  low.

The engine already has this right (`LootTally.ChestT5(fv)`). The tiles on Loot and on Visits (History) are sums the page
scripts work out. Visits' tile was gated in 0.10.14; Loot's still adds up hours by hand.

## Done when
- [x] Loot's "% from Reward Chests" counts chest fragments at the fragment value, as the total does - `LootsChestShareCountsChestFragmentsAtTheirWorth`
- [x] The rest of Loot's tiles are as before - seen in `main-loot.png`, before and after

## Edge cases
- Before the full list arrives, the per-tier totals stand in: chest motes and chest fragments are kept apart there too.
- No loot in the selection: the tile reads "-", as before.
- A selection with a rated visit shows the rate in that tile, not the chest share, so it doesn't change.

## Not doing
- Moving Loot's and Visits' tile sums into the engine. They follow the player's filters (zone, source, time), which only the page knows, so it needs a question to the engine per filter (like Ask's): a standard change of its own. The debt row on the Crawls and Loot tiles stays.

## Where
- `src/EqlPathfinder.App/web/js/loot.js` (`renderLoot`: `chest` split into `chestMotes` and `chestFrags`, `chestT5`)

## Reviews

- architect: on Sonnet, no must fix or should fix; the test drives the real loot.js in the page harness with the fixture's own fragment value, and no other tile sums motes and fragments the same wrong way. The tiles' move into the engine goes to page-math-to-engine (the lead).
- designer: not needed, the tile's wording and layout are unchanged and the before and after renders of main-loot.png are identical; only the value of "% from Reward Chests" is corrected, when no visit in the selection has a rate.
