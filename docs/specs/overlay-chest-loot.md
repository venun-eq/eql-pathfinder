# The overlay's finished-crawl card counts the Reward Chest right away

**Why:** After a Nagafen's Lair D4 crawl the overlay said 4 T5-eq/h with 4 Major fragments from the Reward Chest
already looted. "It didn't update until I Leave and Quit the instance", and then it read 57 (the user, 11-17).
**Players:** everyone who farms with the overlay up. Worst for someone judging a place by one crawl.

## Done when
- [x] Loot that lands after "You have completed the Dungeon Crawl", while you're still inside, is in the card's T5-eq and rate at once - `ChestLootAfterCompletionReachesTheDoneCard`
- [x] A kill finished off after the completed line is in the card's kills and levels at once - `KillsAfterCompletionReachTheDoneCard`

## Edge cases
- Reward XP: a reward that takes you up a level already rebuilt the card, and one that doesn't is sized at the next
  level, so nothing changes here.
- Nothing lands after the completed line: the card is built once, as before.

## Not doing
- Changing how a crawl's loot is counted. The log and the tracker had it right (15.2 T5-eq, 57 an hour); only the
  card was stale.

## Where
- `src/EqlPathfinder.Engine/Live/TrackerService.cs`: the `_nextCache` key in `BuildSnapshot`

## Reviews
- architect: the key covers everything the done card draws. It found the "reward XP" wording wrong (what the levels term catches is kills finished off after completion) and that term untested. Reworded in the code, the note and here; kills added to the key; `KillsAfterCompletionReachTheDoneCard` added. Three debt lines logged.
