# Crawls played on another loadout count to that loadout

**Draft, 11-18, not started** (found building [kills-at-cap](kills-at-cap.md)). `qa` writes the rest of the spec and
the failing tests, then the architect's Decisions (docs/specs/README.md). **Tier:** risky, 600k: it changes which
loadout and level a stored visit has, and needs a re-read. **Waits for:** kills-at-cap (on main). **Unblocks:** lifting
kills-at-cap's stopgap, if it has one (below-cap visits where no kill moved the bar, left out of relevels and sharing).
**Narrowed by:** [current-loadout](current-loadout.md) (11-19): while the app runs, the game's per-loadout files name
the slot on, so this draft's rule is for play the app didn't watch and logs from before that ships.

**Why:** you swap from a relevel to your main for a crawl and don't type /who. Today the app goes on crediting the
relevel: the crawl gets its level, joins its pace and kill speed on Relevels and Home, and is shared at that level.
After, the app knows a swap can only happen in a starting city or the newbie zone beside it, so each trip out of town
has one loadout, named by a /who anywhere in the trip or by a kill at the cap.
**Players:** anyone with more than one loadout, worst for one releveling several trios while farming on a main.

```text
Aug 2–3 (the user's log; the rows are a sketch). Relevel 3 dings 22, goes to town, swaps to the main,
enters the Plane of Fear. The next /who, inside the plane, says [50 PAL/MNK/SHM].

                                   Today                          After
History → Crawls
  Plane of Fear · Solo   loadout   ⟦PAL/MNK/ENC, relevel 3⟧       ⟦PAL/MNK/SHM, your main⟧
                         level     ⟦22⟧                           ⟦50⟧
Relevels → relevel 3     pace      ⟦counts this crawl⟧            ⟦leaves it out⟧
Shared with players as             ⟦a level-22 PAL/MNK/ENC crawl⟧ ⟦a level-50 PAL/MNK/SHM crawl⟧
```

## The cause

The app learns a loadout swap only from /who ([visit-loadout-swap](visit-loadout-swap.md)), and on purpose
never guesses a trio for a crawl no /who saw. The user swaps between the main (PAL/MNK/SHM, 50) and relevels all the
time, with no /who. On Aug 2 a relevel (PAL/MNK/ENC) dinged 22, then Plane of Fear - Solo was entered. The next /who,
inside a plane on Aug 3, was `[50 PAL/MNK/SHM]`. Early August has this pattern across three relevels (PAL/MNK/ENC,
PAL/DRU/MNK, PAL/MNK/BRD). Such visits count to the relevel played before. They get its level, join its pace and kill
speed, and once kills-at-cap counts their kills, they're shared at its level.

## The user's rules (11-18)

- **A loadout can be swapped only in a starting city or its newbie zone just outside.** So between leaving those
  zones and entering one again, the loadout can't change. A /who anywhere in that stretch names the loadout for all of
  it, before the /who as well as after. A /who inside a crawl names the whole crawl's loadout.
- **Below 50 every kill moves the bar.** A kill whose XP line has no `(x%)` means the loadout played is at the cap
  (docs/log-formats.md, "XP lines around a kill").
- **The planes need level 46.** A case for the tests (docs/log-formats.md, 11-20): Plane of Fear D4 on Sep 9 at
  01:29, read at 35 with one kill at 1.37% and four with no XP line, then the same zone at 01:59, all no-`(x%)`. The
  stretch rule gives both to the main at 50; today the first one counts to the relevel.
- "Maybe a state machine would work here" (the user): the loadout is a state that can change only inside a swap zone.

## Proposal

- **A stretch** runs from leaving a swap zone to entering one again. It has one loadout. The loadout is decided by
  whichever comes first in this order:
  1. a /who in the stretch;
  2. a kill with no `(x%)`, meaning a loadout at the cap (which one, when several are at 50, is a question below);
  3. a zone whose entry level rules the last-known loadout out;
  4. otherwise, today's carry-over.
- The swap zones are a list: the starting cities and their newbie zones, by the names the log uses for them
  (`You have entered …`).
- Raise `EngineState.CurrentVersion`. The note says `reread: true`.
- Lift kills-at-cap's stopgap, if it has one.

## Questions for qa and the architect

- Which zones are swap zones? Find them by name in the user's logs, and check that no swap ever happens outside them:
  a /who's trio changing between two /who lines with no swap zone between them.
- A visit's facts are decided once and stored. A /who later in the stretch rewrites the loadout of visits already
  closed. Do the stretch's visits wait until it's known, or are they revised? What does the overlay show live before
  any /who?
- With several loadouts at 50, a no-`(x%)` kill says "a loadout at the cap", not which one. Is that enough for
  leveling (it isn't a relevel) and for sharing (which trio)?
- A login (`dbg.txt`, docs/log-formats.md last section) or a character select: can a swap happen there, outside a
  swap zone?

## Where

- `src/EqlPathfinder.Engine/Tracking/Tracker.cs`: where /who sets the loadout (visit-loadout-swap's code)
- `src/EqlPathfinder.Engine/Analysis/Relevels.cs`, `Journeys`: which visits belong to a relevel
- `tests/`: a stretch from the user's Aug 2–3 log, scrubbed (docs/quality.md rule 6)
