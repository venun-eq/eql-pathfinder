# Dying isn't changing a class

**Why:** The user, 11-20, xp'ing on LO6 all night with no change: the overlay said "You changed a class on loadout 6.
Type /who once and your usual comes back." Their dbg.txt shows two deaths in The Castle of Mistmoore 227 (00:22:56 and
00:27:07). Each respawned them in the same zone: `I died.`, then `Received our Player from zone.` and
`Received MSG_EQ_ADDPLAYER …` with no zoning lines. That's the pattern `ClientDebugLog` reads as a class change, so
the slot went to "classes", the trio was forgotten, and the overlay and fight meter stopped judging.
**Players:** anyone who dies and respawns in the zone they died in, on any loadout. The more you die, the more often
you're asked.

## Done when
- [x] The real lines of both deaths give no class change and no swap; a class change after them still counts -
  `ADeathThatRespawnsInTheSameZoneIsNotAClassChange`
- [x] Every earlier dbg.txt test still passes: swaps, class changes, zoning, a world entry's resets, another character

## Edge cases
- **A file format from one watch:** the test is the real session's lines, blank lines and `knockout` lines included.
- **A death that respawns in another zone:** zoning lines come first, so it was never a class change; `I died.` changes
  nothing there.
- **A class change after a death:** `I died.` excuses only the next arrival (the flags clear at every arrival), so a
  later class change is still one (the test's last arrival).
- **Not checked:** a resurrection accepted in the same zone, and feigning death. Neither was in the log; if one shows
  up as a class change, it's the same fix with its own line. A death with no arrival soon after it (waiting for a
  resurrection) excuses whatever arrival comes next, so a class change made then goes unseen until a /who: rarer and
  milder than asking for a /who after every death.

## Reviews
- architect: no must fix, no should fix. Matched the test's lines to the live dbg.txt and confirmed the test
  fails without the fix. Pointed out the death-then-no-arrival case; it's now under "Not checked" above, not debt.

## Not doing
- Reading deaths from the eqlog: dbg.txt alone says when the arrival happened, and `I died.` sits right before it.
- Changing the overlay's or Home's words: they're right when a class did change.

## Where
- `src/EqlPathfinder.Engine/IO/ClientDebugLog.cs`: `I died.` joins the lines that say an arrival isn't a swap or a
  class change.
- `docs/log-formats.md`: the death's lines in the dbg.txt table.

## Reviews
