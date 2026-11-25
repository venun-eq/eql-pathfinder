# A swap seen at login counts only once the game confirms it

**Why:** On 11-24 the player logged in on loadout L03 (PAL/WIZ/BER, level 47) and the app read a swap to slot 4 (DRU/BRD/NEC, 50)
at 17:45:32, 16 s before "Welcome to EverQuest Legends!". The game writes a slot file only on swap, zone and camp, so at launch
the newest file can be the slot last played in the run before. Levels 47 to 50 were then played as DRU/BRD/NEC at 50,
Home showed PAL/WIZ/BER parked at 47/50 and History a phantom BRD/WIZ/ENC relevel. Stored events stay raw; the reading mends it.
**Players:** anyone whose slot file at login isn't the slot they play.

## What the player sees
After the re-read, Home shows PAL/WIZ/BER finished at 50, History has its visits 47 to 50 and no BRD/WIZ/ENC 11-24 visit.
No new wording.

## Done when
- [x] A swap from 2 min before to 30 s after a login line ("Welcome to EverQuest Legends!"), the first since it, is held: the slot last played stays on - `ASwapSeenAtLoginIsHeldUntilTheGameConfirmsIt`
- [x] A ding the held slot's known level rules out (48 on a slot at 50) drops it; one only the held slot can make (51 on slot 3 at 47, held slot 4 at 50) confirms it and leaves slot 3's memory - `ADingTheHeldSlotsLevelRulesOutDropsTheSwap`, `ADingThatFitsOnlyTheHeldSlotConfirmsItAndSlotThreeKeepsItsMemory`
- [x] The 11-24 sequence reads PAL/WIZ/BER 47 to 50, then slot 4 from the real swap at 19:26 - `The1124SequenceReadsTheLevelsAsTheLoadoutPlayed`
- [x] A /who or a later swap confirms it, from the swap's own time; a zone does not (the 11-24 player zoned into an instance 60 s after the login on the slot they were on) - `AWhoOrALaterSwapConfirmsASwapSeenAtLogin`, `AZoneAfterTheLoginDoesNotConfirmIt`
- [x] A /who confirms it only when its trio is the held slot's memory (or that slot has none); a /who of the slot on drops the hold and the slot on stays - `AWhoOfTheSlotOnDoesNotConfirmTheHeldSwap`
- [x] The live step takes queued rows up to the clock, so a login's swap waits for the Welcome line - `TheLiveStepHoldsALoginSwapUntilTheWelcomeLine`
- [x] A /who names the levels before it when nothing since the last swap, class change or reincarnation says otherwise: a ding the slot's memory rules out (slot 4 "DRU/BRD/NEC 50", dinged to 2) makes the memory stale, so the visit has no trio until the /who - `ALevelTheSlotsMemoryRulesOutLeavesTheLevelsToTheWhoThatNamesThem`
- [x] A swap away from a login, or to the slot already on, works as before - `ASwapAwayFromALoginIsTakenAtOnce`
- [x] The engine state version rises, so every log is re-read once: `EngineState.CurrentVersion` 19, release note `reread: true`

## Edge cases
- Nothing before the swap (a fresh database): held the same, no slot is on until it's confirmed.
- A held swap to the slot already on needs nothing. A class change while held belongs to the slot on.
- A ding to level 51 on a slot at 50 is not ruled out: when the slot on can't make it, the held slot is the one played. A later row to the held slot itself counts from its own time. A reincarnated slot's level is 1, so low dings fit. A ding more than one above the held slot's level (held at 30, slot on at 47, ding 48) is the slot on's: the hold drops - `ADingFarAboveTheHeldSlotsLevelDropsTheSwap`.
- The startup lines come before the Welcome line: a swap after a silence in the log waits up to 30 s for one - `StartupLinesBeforeTheWelcomeLineDoNotLetTheSwapThrough`, `ASwapAfterChatterIsNotWaitedFor`
- Live: the app starts after the login, so the swap row comes after the Welcome line is read.
- Old logs without a Welcome line (or a log opened mid-session): no login, nothing held.

## Not doing
- Deleting or editing the stored swap rows. Changing the watch (`LoadoutWatch`): the reading decides.

## Reviews
- architect: first review (bc7576a) asked two must fix (live Step applied queued swaps with MaxValue, so the hold never worked live; a ding fitting the held slot wiped the slot on's memory) and a should fix (a later row to the held slot counted from the held time), fixed in 6eb6b67; second review asked for a test that runs Step, a /who that confirms only a matching trio, and a ding far above the held level to drop the hold, fixed in c47336b with each test shown failing without its fix; the lead read the fix. Proof on a copy of the user's database: 11-24 PAL/WIZ/BER 47 to 50, RNG/ENC/BST 1 to 4, no BRD/WIZ/ENC.
- designer: release note read against design.md (rules 2, 14, 16) and the notes' format. Fixed in the note: "slot", which the app never shows players, became "loadout"; "a real swap" became "your next swap"; "a level-up the slot can't have made drops it" now says what it means, with an example (a ding to 48 on a loadout at 50); the second bullet no longer repeats the re-read line ship.ps1 adds for `reread: true`, and keeps what the player gets from it. Title, first line and flags kept. No screenshots: nothing drawn changes, and the corrected 11-24 visits were proved on a copy of the user's database.
