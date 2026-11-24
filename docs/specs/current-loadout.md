# The whole app knows the loadout you're on

**Built 11-19.** The app follows loadout swaps from the game's per-slot files and `dbg.txt`, stores each swap and
class change in `loadout_events`, and every page reads one "loadout on". It ships with a one-time re-read (Decision 9).
**Tier:** risky. Budgeted 700k, raised by the user to 800k, then 1M, then "keep going until finished"; it cost about
1.64M. The extra went on three engineers each stopping at the turn limit, two rounds of fixes, and a real-data bug
the numbers check found (Reviews).
**Waits for:** nothing. **Changes:** [swap-zones](swap-zones.md), which becomes the rule for play the app didn't watch
(Not doing, below).

**Why:** the game names your loadout in the log only when you type /who. Swap to your main for a crawl, or change a
class, and every page goes on showing the loadout you left: Home, the fight meter's labels and "your usual", the
overlays, and the crawl itself, credited and shared at the wrong level. After, the app sees the swap in the game's own
files within seconds and every page follows. You type /who once per loadout, ever, and again only after changing its
classes. This is one fact about you that the whole app reads, not a Home feature (the user, 11-19: "I would rather the
entire application's state knew the current loadout, as other features could use it in the future").
**Players:** anyone with more than one loadout. Worst for one releveling several trios while farming on a main, and
for anyone who never types /who.

```text
You're on relevel 8 (BRD/DRU/NEC Barbarian, 10). You swap to your main (PAL/MNK/SHM Froglok, 50) and
enter the Plane of Fear. No /who. (A sketch: the rows and words are made up.)

                         Today                                   After (seconds after the swap)
Home                     ⟦Leveling now · relevel 8 ·             ⟦At the cap, farming⟧
                          BRD / DRU / NEC Barbarian · 10 / 50⟧
Fight meter              ⟦fights labelled BRD/DRU/NEC,           ⟦PAL/MNK/SHM, against your main's usual⟧
                          against relevel 8's usual⟧
Overlays                 ⟦relevel 8's pace⟧                      ⟦your main's⟧
History → Crawls         ⟦Plane of Fear · BRD/DRU/NEC · 10⟧      ⟦Plane of Fear · PAL/MNK/SHM · 50⟧
Relevels → relevel 8     ⟦its pace counts the crawl⟧             ⟦leaves it out⟧
Shared with players      ⟦a level-10 BRD/DRU/NEC crawl⟧          ⟦a level-50 PAL/MNK/SHM crawl⟧
```

```text
The first time on a loadout the app hasn't seen, or right after changing a class in one (a sketch: the
designer writes the words). Today this card shows only after a level jump; after, it shows on the swap.

Home   ┌───────────────────────────────────────────────────────┐
       │ New loadout                                           │
       │ Type /who in game once                                │
       │ Pathfinder saw you swap but hasn't met this loadout   │
       │ yet. One /who and it knows it from then on.           │
       └───────────────────────────────────────────────────────┘
Fight meter, overlays: ⟦loadout not known yet⟧ until the /who, then they follow it.
```

## What the player sees

The designer's, 11-19. No new layout: the card is Home's Swapped hero (`swappedHero`) with new words, and every other
place reuses a line it already has, so there is no mock-up. Three states of the loadout on, each the same on every
page:

| The loadout on | What every page does |
|---|---|
| **Known**: a slot met before (its /who), no class change since | Follows it within seconds of the swap, with no new words |
| **New loadout**: a slot Pathfinder hasn't met | Says so and asks for one /who. Shows no trio, race or level from the loadout left |
| **Classes changed**: a class changed in the slot since its /who | Says so and asks for one /who. Keeps the slot's level and race (a class change leaves both, log-formats 11-19) |

"Hasn't met" is said the same way whether Pathfinder saw the swap or started on that loadout: first run, an app
start with the game open, or the first swap to each slot after this update (the slots are learned live, so every
slot is new once). A /who in the log after the slot file was last written, typed before Pathfinder started, counts
as meeting it, so the update doesn't ask again for a loadout the player has just /who'd (Decided, below).

**The slot's number** (the user, 11-19: the game's loadout window numbers them 1 to 9, as the `LO<n>` files do): the
words below name it where they'd otherwise say "this loadout", e.g. "loadout 6" (made up). It's the number the
player sees in game, so "loadout 6" finds the slot to /who on. Shown only on this PC (Proposal 6).

### Home, a loadout swapped to that's known

Nothing new on screen: Home is in the state for that loadout (Farming at the cap, Releveling for its open relevel,
Leveling) within seconds of the swap, as if a /who had been typed. The old "Swapped loadouts · Your level changed to
32 without a ding" card no longer shows for a known slot.

### Home, new loadout (below the cap, or level not known)

```text
NEW LOADOUT                                                              (.lbl)
Type /who in game once                                                   (.trio)
Pathfinder hasn't met loadout 6, the one you're on, yet. One /who names its  (.muted, max-width 620px)
classes, race and level, and Pathfinder remembers them from then on,
even after you swap loadouts.
OPEN RELEVELS                                                            (parkedBlock, as today's Swapped)
WAR/CLR/RNG Kerran      32 / 50   ≈ 6.2 h to go                          (made-up rows)
```

- Shown whenever the slot on hasn't been met, whatever relevels are open, also for the new player on their first
  run (today that player sees Leveling or "Your character").
- Right column: nothing, since the level is the other loadout's. After a ding on this slot (no /who yet): the level as
  on the other heroes, **"11 / 50"**, and under it (`.num.muted`) **"loadout not known yet"**.
- Below the hero: as today's Swapped state (Up next and the roster, no "now" cells, no "· now" race chip; Up next's
  steps don't say "Parked.").
- After the /who: the card goes at once and Home shows that loadout's state. No other message.

### Home, classes changed

```text
CLASSES CHANGED                                                          10 / 50     (.big.num, as today)
Type /who in game                                               Barbarian · classes not known yet   (.num.muted)
You changed a class on loadout 6. One /who names the new trio,
and Pathfinder remembers it from then on.
OPEN RELEVELS ...
```

- Below the cap: this card, the same layout as New loadout. The race comes from the slot's last /who; left out when
  not known.
- **At the cap**: Home stays on Farming (design.md, rule 16), with the trio line (`.trio`) reading **"Classes not
  known yet"** and the race beside it as today, and under it (`.muted`, 12.5px) **"You changed a class on loadout 6.
  Type /who in game and Home follows the new trio."** Its farming numbers are the place's, as today with no trio.
  On a **new** loadout at the cap the trio line stays **"Loadout not known yet"**, and under it (`.muted`, 12.5px)
  **"Pathfinder hasn't met loadout 6. Type /who in game and Home follows it."**
- **A reincarnation** keeps the slot: only the level drops to 1, so no card. A class changed in the slot before the
  reincarnation, with no /who, gives this card at "1 / 50".

### When the game's files can't be read

No new words anywhere: the folder unreadable, a patch that moves or stops the files, or the files of another
character. Home's "Swapped loadouts" (a level jump with no /who) and "Your character" (no level yet) states, the
farming hero's "Loadout not known yet" and every other page work exactly as today.

### The title bar

Today's chip, `1 · SHD/SHM/WIZ · Iksar`, never shows the loadout left:
- New loadout: **"Loadout 6 not known yet"**; with a level from a ding on it, **"11 · loadout 6 not known yet"**.
- Classes changed: **"10 · classes not known yet · Barbarian"**.
- Tooltip on the chip in both: **"Type /who in game once and Pathfinder knows loadout 6 from then on."**

### The crawl overlay

It has a "no usual yet" state today (`overlay-leveling-standard.png`); this reuses it.
- Head, right of the place: **"5:42 · type /who"** in place of "no usual yet" (the place's difficulty keeps its room; the
  note under says the full sentence). Bars are the "no usual yet" state's dashed empty track, no ticks, no green
  or red (rule 21: no verdict judges them). The **"+20% XP to level"** after the place is left out: it's a trio's.
- The note under the level line, where "No usual here yet: …" goes today: New loadout **"Pathfinder hasn't met loadout
  6 yet. Type /who once and your usual comes back."** Classes changed: **"You changed a class on loadout 6.
  Type /who once and your usual comes back."** Compact: the head's words only.
- Level line, new loadout: **"—"** on the left, an empty bar, and on the right **"level after a /who"** (today's
  "% after the next ding" would be the other loadout's). After a ding on the slot: the level, as today.
- Between crawls, NEXT: as today when the level is known, without "at +20% XP to level". With no level: in place of
  the rows, one line, **"Type /who in game for where to go next."**

### The fight meter

- Footer: **"· loadout not known"** where today it says "· no usual here yet", e.g. "4 fights · Kill time 3:20 · 301
  DPS · loadout not known". No usual tick or percent on any row.
- After the /who: the fights since the swap take that loadout, the percents and ticks come back, and the word goes.

### History → Crawls

The trio column's tooltip (`trioHow`), two new sources:
- Known from the slot: **"Your last /who on loadout 6; Pathfinder saw you swap to it."**
- Not known: the trio reads "?" as today, tooltip **"Played on a loadout Pathfinder hadn't met; no /who named it."**

The visit's level and its relevel follow the loadout it was played on; no other new words.

### Help

A new `section.card` after "How you play and the farm rate", heading **"Your loadouts and /who"**:

> Pathfinder follows your loadout swaps within seconds, from the files the game saves for each loadout.
> - The first time you're on a loadout, type /who once. It names the classes, race and level, and Pathfinder
>   remembers them.
> - Changed a class on a loadout? /who once more: the game's files show that a class changed, not which.
> - Reincarnating keeps the loadout you're on and only sets the level back to 1, so it needs no /who.
> - Until the /who, Home asks for it, and the overlay and the fight meter say the loadout isn't known rather than
>   compare you with another loadout's usual.
>
> Which loadout you're on never leaves this PC.

### Every state

- **Nothing yet** (first run): New loadout, as above, until the first /who.
- **Waiting** (the first read of the logs, a re-read): "Reading your history…" as today; the swaps a re-read replays
  change only which loadout old visits belong to.
- **The window**: the card is today's Swapped hero; its body wraps at 620px and the level column stays `flex: none`.
  The chip's longest text, "10 · classes not known yet · Barbarian", is about as wide as today's longest
  ("50 · PAL/MNK/SHM · Half Dark Elf"), so the title bar fits at 1150 px and 150%.

Changed from the draft's sketch: "Pathfinder saw you swap" went, because the card also shows on the first run and at
an app start, where no swap was seen. The class change got its own label, since "New loadout" is untrue for a slot
the player has used all along.

Shots the review needs (for qa's Done when): `main-home-newloadout.png` (with an open relevel),
`main-home-newloadout-ding.png` (level on the right), `main-home-classes-changed.png`,
`main-home-farming-classes-changed.png`, `titlebar-not-known.png`, `overlay-not-known.png`,
`overlay-not-known-nolevel.png` (between crawls, no level), `meter-not-known.png`, `main-help-loadouts.png` (Help scrolled to the section).

## The cause

The log names a loadout only in a /who line (docs/log-formats.md). The game also saves a settings file per loadout
slot on every swap, and its own debug log, `dbg.txt`, shows a class changed inside a slot: docs/log-formats.md, "Which
loadout is on" (watched 11-19, with a /who after each step). The files say which slot is on, never its classes, so a
/who on each slot is still needed once.

## Proposal

1. **One state, "the loadout on":** the character, its slot, trio, race and level, and how each is known (a /who now,
   the slot's last /who, or not known). The engine keeps it; Home, the fight meter, the overlays, Ask and the tracker
   read it, and none works it out for itself (docs/engineering.md, rule 12). The fight meter's trio timeline
   (docs/overlays.md, "One trio everywhere") is built from it.
2. **Which slot:** watch the game folder for the tracked character's `<Name>_<server>_LO<n>.ini` and
   `UI_<Name>_<server>_LO<n>.ini`. The slot on is the one written last, once a swap's writes settle (the slot left is
   saved about a second before the one swapped to). Started with the game already running: the slot file written last
   since the client started (`dbg.txt`'s first line).
3. **What's on the slot:** a /who while it's on names its trio, race and level, and the app remembers them; dings on
   the slot move its level.
4. **Classes changed:** `dbg.txt`'s `Received MSG_EQ_ADDPLAYER` in the same zone with no `Resetting UI.` and no zoning
   lines. The slot's trio is not known until the next /who.
5. **Stored as facts:** each swap and class change seen, with its time. They can't be read again later (a file keeps
   only its last write, `dbg.txt` is rewritten at each start), so a re-read of the logs replays them and a visit keeps
   the loadout it was played on.
6. **Nothing new is sent.** The slot number never leaves the PC. A shared crawl carries the trio and level it does
   today, now the right ones.

## Decided

- **The first time on a slot, ask for a /who** (the user, 11-19). Its trio isn't guessed from the spells cast: a guess
  credited to the wrong relevel is worse than one /who. The spell check stays what it is today.
- **No setting to turn it off** (the user, 11-19: "go, as drafted"). It reads files the game writes, as it reads the
  log.
- **A /who already in the log counts** (the user, 11-19). A /who typed while the slot was on, even one typed before
  Pathfinder started, names that slot, so the update doesn't ask again for a loadout the player has just /who'd.
  "While the slot was on" is after the last swap this run's `dbg.txt` shows ("Resetting UI."), and after the slot's
  file was last written only when `dbg.txt` doesn't cover it: zoning rewrites the file, so its time alone would ask
  again after a /who then a zone (qa's finding; the lead's reading of what the user asked, which was the lead's
  wording of "since the file was saved").
- **The words name the slot** (the user, 11-19): the game's loadout window numbers its slots 1 to 9 in the same order
  as the `LO1` to `LO9` files, so `LO6` is "loadout 6" on screen.
- **A reincarnation keeps the slot** (the user, 11-19): "You must be IN the loadout you want to reincarnate before
  reincarnating ... That reincarnation uses the same loadout spot that we just went into. There is no 'loadout' swap,
  just a level drop during a reincarnation. But technically the class loadout was set (maybe not known by logs) prior
  to reincarnating." So a reincarnation drops the slot's level and leaves its trio; a class change seen before it
  applies as usual. docs/log-formats.md's "Not checked yet" line says so in this branch.

## Done when

Engine tests are in `CurrentLoadoutTests`; the swaps and class changes go in as events between the log's lines.
- [x] The slot on is the tracked character's loadout file written last, since the client started - `TheSlotOnIsTheLoadoutFileWrittenLast`, `OnlyTheTrackedCharactersFilesSinceTheClientStartedCount`
- [x] A class change is read from dbg.txt: an arrival in the same zone with no reset and no zoning lines; a swap, a zone or another character's isn't one - `AClassChangeIsAnArrivalInTheSameZoneWithNoResetOrZoning`
- [x] Swapped to a slot met before, no /who: the engine is on its trio, race and level - `ASwapToASlotMetBeforeTakesItsTrioRaceAndLevelWithoutAWho`
- [x] Home, the title bar, the overlay and the fight meter follow it within 15 s of the swap - the engineer's self-test in an isolated copy with a stand-in game folder (touch a slot file, read the snapshot), its output in the review
- [x] A crawl entered after that swap has the slot's trio, race and level, so History, Relevels and the share unit do - `ACrawlEnteredAfterASwapHasTheSwappedToLoadoutAndLevel`
- [x] A slot not met: no trio, race or level from the loadout left; a ding gives the level only; its /who is remembered after swapping away - `ASwapToASlotNotMetShowsNoTrioRaceOrLevelFromTheLoadoutLeft`, `ADingOnASlotNotMetGivesItsLevelButNoTrio`, `AWhoOnASlotIsRememberedAfterSwappingAway`
- [x] A class change forgets the trio and keeps the slot's level and race until a /who - `AClassChangeForgetsTheTrioAndKeepsTheSlotsLevelAndRace`
- [x] A /who in the log after the slot's swap meets it, also when the files are read after the log (an app start); one before doesn't - `AWhoInTheLogAfterTheSlotsSwapMeetsItEvenWhenTheFileIsReadLater`, `AWhoBeforeTheSwapToTheSlotDoesNotMeetIt`
- [x] A reincarnation keeps the slot and its trio (a race one takes the named race) and sets the level to 1; a class change before it still asks - `AReincarnationKeepsTheSlotAndItsTrioAndDropsTheLevelTo1`, `AClassChangeBeforeAReincarnationStillAsksForAWho`
- [x] Home's state: "newloadout" for a slot not met (relevels open or not, a first run too), "classes" below the cap, "farming" at it with the loadout said - `HomeAsksForAWhoOnALoadoutNotMetWhateverRelevelsAreOpen`, `HomeSaysClassesChangedBelowTheCapAndKeepsFarmingAtIt`
- [x] Home draws each with the slot's number - `CurrentLoadoutPageTests` (4 tests); seen in `main-home-newloadout.png` (an open relevel), `main-home-newloadout-ding.png`, `main-home-classes-changed.png`, `main-home-farming-classes-changed.png`
- [x] The title bar, the overlay and the fight meter say "not known" and judge nothing - `titlebar-not-known.png`, `overlay-not-known.png`, `overlay-not-known-nolevel.png`, `meter-not-known.png`; the farming hero's ask - `HomeKeepsFarmingAtTheCapOnALoadoutNotMetAndAsksForTheWho`, `main-home-farming-new-loadout.png`
- [x] After the /who, the fights since the swap take its trio - a TrioTimeline test, written once the Decisions say what the timeline is built from
- [x] History's trio tooltip gives the two new sources - a test on `History`'s trio source, written after the Decisions
- [x] Help has "Your loadouts and /who" - `main-help.png`
- [x] Slots met survive a restart, and a re-read (`reread: true`) keeps each visit's loadout - store round-trip tests, written after the Decisions say where the swaps are kept; `RereadGuardTests` raised as its message says
- [x] The files unreadable or stopped by a patch: everything as today - `LoadoutSwapTests`, `HomeStateTests`, `HomePageTests` unchanged and green
- [x] Nothing new sent - `git diff main -- site/privacy.html` empty; `ShareTests` unchanged and green

## Edge cases

- **Nothing yet** (first run, or the first swap to each slot after this update): New loadout until its /who. Before
  the character enters the world (patching, character select) no slot file is written this run: no slot, no card.
- **Play the app didn't watch** (closed while swapping, or before this ships): missed; the log's rules apply as today
  (swap-zones). At an app start only the slot on now is learned, at its file's time.
- **Starting and stopping:** swaps seen during the first read or a re-read wait for it and go in by their times; one
  seen is kept even if the app closes mid-read.
- **A second copy:** an isolated copy or a self-test keeps what it learns in its own database. A `--snapshot` render
  reads no game folder: its loadout is a stand-in.
- **Same name, different thing:** another character, or the same name on another server, has its own files and its
  own dbg.txt lines; only the tracked one counts. `UI_<Name>_<server>.ini` and other ini files are no slot. Two
  clients at once: each character's own files.
- **The rules change:** a patch that renames or stops the files, or rewords dbg.txt: no swaps, everything as today.
- **Clocks:** file times are read in the local clock, as the log's lines are. A /who in the same second as the slot's
  write counts as after it.
- **The game crashes:** no camp write; the next login writes the slot on.
- **Swapped and back within seconds:** the slot written last. **Zoning, camping, moving a window** rewrite the slot
  on: no change, and no class change (the zoning lines).
- **A /who naming another trio on a slot with no class change seen:** the /who wins and the slot takes it.
- **A class reincarnation naming a class not in the slot's known trio:** its classes changed unseen: Classes changed
  until a /who. Without slot files a reincarnation forgets the trio, as today.
- **Re-sends:** a re-read that moves old visits to another loadout changes their share units, which are sent again;
  only visits played after this ships can move, so it's bounded.
- **Something leaving the PC:** the slot number, the file names (they hold the character's name) and their times
  never do.
- **Entering the world:** `dbg.txt` writes "Resetting UI." about 3 s after the world entry's arrival (the user's log,
  11-19). A reset within 15 s of the entry belongs to it, so neither the next zone-in nor a class change right after
  logging in reads as a swap. The 15 s is from that one log; a reset later than that would still read as a swap's.

## Not doing

- Reading the game's memory: docs/game-memory.md.
- Play the app didn't watch, and logs from before this ships: [swap-zones](swap-zones.md) (a /who anywhere between
  leaving town and entering it again names the loadout).
- Naming a slot's classes from its files: they don't hold them.

## For the architect

Open, and not settled by the tests (their events and the stubs' names are qa's; rename freely, the facts stay):
- Where the swaps, class changes and each slot's last /who are kept, and how a re-read replays them among the lines.
- Whether a swap is an event through `Tracker.Apply` (as the tests do) or enters another way, and whether a visit
  whose trio came from a slot counts as confirmed (spell inference must not overwrite it).
- What the fight meter's trio timeline is built from once the loadout can change with no visit.
- Where the files are watched (the app's loop, as `App` reads dbg.txt today, or `TrackerService`).

## Decisions

The architect's, 11-19. qa's stub names may change; these are the rules they follow.

- **What's stored: the swaps and class changes, as facts, in a table a re-read doesn't empty.** New table in
  `PathfinderStore`'s constructor: `CREATE TABLE IF NOT EXISTS loadout_events (id INTEGER PRIMARY KEY, character TEXT,
  time TEXT, kind TEXT, slot INTEGER, UNIQUE(character, time, kind, slot))`, written `INSERT OR IGNORE` the moment the
  watcher sees one (not in the checkpoint's batch, so one seen survives a close mid-read). `Reset` leaves it: it's the
  only copy, since the files and dbg.txt keep no history (log-formats). A new table changes no existing database.
- **What's worked out from them lives in `EngineState`, so the checkpoint carries it and a re-read rebuilds it:** the
  slot on and its state (`LoadoutOn`), each slot's last /who (classes, race, level, when), the stints (below), the last
  /who seen (time, classes, race, level), and `LoadoutRow`, the highest `loadout_events.id` the engine has taken. New
  properties only: an older checkpoint loads with none, and an older app ignores them (System.Text.Json skips unknowns).
- **How they reach the engine: one path, through `Tracker.Apply`, by time among the log's lines.** `PathfinderEngine`
  keeps a queue ordered by time; `ProcessLine` applies every queued event with `Time <= line.Time` before the line (a
  /who in the same second as the swap counts as after it), and the end of a read (`Flush`, and each live `Step` after
  its log read) applies the rest. Rows are queued only from the store: at resume, at the end of an import, and in a
  `Step` that wrote one, the rows with `id > LoadoutRow`. So the first read, a re-read and a crash between a row and
  the next checkpoint all replay the same facts, and a row is never taken twice.
- **An event later than the lines it describes** (an app start: the log is read before the files): a swap whose time is
  at or before the last /who seen takes that /who for the slot (`AWhoInTheLogAfterTheSlotsSwapMeetsIt...`). A swap to
  the slot already on changes nothing, so an app start that sees the same slot again is a no-op.
- **The swap's time** (the user's rule, Decided), worked out in `IO`: the last swap this run's dbg.txt shows for the
  tracked character (`Resetting UI.` then an arrival in the same zone, no zoning lines) at or before the slot file's
  write; else that character's first arrival this run (they entered the world on that slot); else, when dbg.txt is
  missing or from another run, the file's write time. A `ClientSession.SwapsOf(player, server)` beside `ClassChangesOf`.
- **Where the files are watched: `TrackerService.Step`**, on the tracker thread, so nothing touches the engine from
  another thread. It stats the 18 known names (`[UI_]<Name>_<server>_LO1..9.ini`, from `Logs.Name`/`Logs.Server`) in
  the log folder's parent and reads dbg.txt with its own `ClientDebugLog` (App's stays for the log-off check). A swap
  is emitted when the slot written last changes and no slot file has been written for 2 s (the slot left is saved
  about 1 s before the one swapped to). It runs while importing too, writing rows only. Any IO error: no event,
  everything as today. `--snapshot` and the page tests get a stand-in, never a game folder.
- **A visit's trio from a slot is not `TrioConfirmed`.** That field is shared and means "a /who during the crawl"
  (SharePayload); its meaning stays. The visit gets `Slot` (int?) and `TrioFrom = "slot"` (the `visits.json` column
  holds them; no schema change). `TrioInference.Attribute` and `Journeys.Attribute` treat a slot trio like a /who: an
  anchor spells never overwrite, `TrioCall.Source = "slot"`. A visit entered on a slot not met: Source `"unmet"`, trio
  null ("?"). `ForgetLoadout` never clears a slot trio. A swap inside an open visit freezes its levels when its trio
  was known (/who or slot), as `FreezeLevels` does today.
- **What's sent: nothing new.** `SharePayload` sends `"slot"` as `"carried"` (from an earlier /who, not this crawl's:
  true, and a value the job already gets) and `"unmet"` as `"none"`, `TrioConfirmed` false; `Slot` and `TrioFrom` are
  never in a `SharedCrawl`. Nothing on the server or the NAS job reads `TrioSource` today. `site/privacy.html` already
  says "the trio"; unchanged. No usage count or error line carries the slot or a file name (they hold the name).
- **A re-read.** A /who now fills the engine's loadout state (`LastWho`, `LastWhoTime`, the slot's `WhoAt`), which a version-15
  checkpoint lacks: without a bump, a player with the game open gets "New loadout" for the slot they just /who'd and
  loses their level. So `CurrentVersion` is 16, `RereadGuardTests` holds the new hash, and the release note says
  `reread: true`. A future re-read keeps each visit's loadout through the replay.
- **The fight meter's timeline: visits plus stints.** A stint is `(From, Slot, Trio?)` in `EngineState`, opened by
  each swap and each class change (trio null unless the slot is known), and a /who that meets the slot fills the
  open stint's trio, so the fights since the swap take it. `TrioTimeline` takes `string?` trios; `TrackerService.
  TrioTimeline()` merges the visits' points (nulls dropped, as today) with every stint's (nulls kept: "not known" from
  there). The `Facts()` cache key gains the slot, its state and the stint count, or a swap with no visit wouldn't
  reach the meter. All stints are kept (a few a day); `FightStore.Relabel` clears the trio on fights in a null stretch.
- **Taking it back:** an older app ignores the table and the new checkpoint fields; deleting the table forgets only
  which slot was on. Nothing on the server. No question for the user.
- **Limits:** 18 stats a step (every 0.5 s in game, 5 s out); a row per settled swap, deduped by its key. A tool
  rewriting a slot file in a loop gives one swap per 2 s at most, and only when the slot written last changes.

What the four waiting proofs prove (the engineer writes them):
1. **Store round trip** (`LoadoutStoreTests`, temp folder): rows written, `Reset`, rows still there; a fresh
   `TrackerService` on the same store re-reads a two-crawl log and the crawl after the stored swap has the slot's
   trio, race and level, the one before keeps its own. A restart (new service, resume from the checkpoint): the slot on
   and each slot's /who are back, and a row stored after the last checkpoint is applied once, not twice.
2. **Fight meter** (`TrioTimelineTests`): a stint to a known slot with no visit gives its trio to a fight after it; a
   swap to a slot not met gives a fight after it no trio; after the /who, a fight between the swap and the /who has
   that /who's trio; a later visit's point still wins after it.
3. **History's source** (`TrioInferenceTests`, with the spell file): a slot visit is `"slot"` and keeps its trio when
   its casts fit another; an unmet one is `"unmet"`, trio null; `VisitStats` carries `Slot` for the tooltip's words.
   And in `ShareTests`, one new test: a slot visit shares `"carried"`, `TrioConfirmed` false, and no `slot` in its JSON.
4. **15 s self-test** (a `SelfTest*Async` in an isolated copy, a stand-in game folder in its temp dir, never the real
   one): write slot 6's files then slot 1's; poll the snapshot until `HomeNow.Loadout`, the title chip's text and
   the overlay's and meter's "not known" follow; print the seconds; fail past 15.

## Where

- docs/log-formats.md: "The client's own log" and "Which loadout is on"
- `src/EqlPathfinder.Engine/IO/LoadoutFiles.cs` (new, stub `SlotOn`); `IO/ClientDebugLog.cs` (stub `ClassChangesOf`)
- `src/EqlPathfinder.Engine/Parsing/LogEvents.cs`: stubs `LoadoutSwapped`, `LoadoutClassesChanged`
- `src/EqlPathfinder.Engine/Tracking/EngineState.cs`: stub `Loadout`, `LoadoutOn`; `Tracker.cs`: `Apply`, `OnWho`,
  `OnLevel`'s `ForgetLoadout`, `OnReincarnated`
- `src/EqlPathfinder.Engine/Analysis/HomeState.cs`: `HomeNow.Loadout`, the `Of` overload (stubs)
- `TrackerService.BuildSnapshot`, `ReincarnationData` (`homeNow`, `currentTrio`), `TrioTimeline()`, `History` (trio
  source); `FightMeterService.SetTrioTimeline`; `PageDataTests` for each new field
- `src/EqlPathfinder.App/App.xaml.cs` (where dbg.txt is read), `OverlayWindow`, the title bar chip, `DevSnapshots.Run`
  for the nine shots
- `web/js/home.js` (`homeMode`, `swappedHero`), crawls.js (`trioHow`), help in index.html

## Reviews

- designer: the overlay's "not known" rows drew solid grey at 80%, which reads as "on your usual". They're now the
  dashed empty track. The head became "5:42 · type /who" so the difficulty shows. A Help shot proves the new section,
  and Home at the cap on a slot not met asks for the /who. All fixed, and the lead checked the re-rendered shots.
- architect: three must fixes. (1) Its own Decision 9 was overruled: a version-15 checkpoint has no `LastWho`, so the
  release re-reads (CurrentVersion 16, `reread: true`). (2) A swap seen during an import was never applied. (3) A swap
  read after the /who that followed it took a stale level and kept a settle-window /who on the slot left. Two should
  fixes: `WatchLoadout` catches everything, and the overlay reads `NotKnown` from the engine. All fixed with tests. The
  second look found no must fix. Its three should fixes (a test that couldn't fail, `>=` at the swap's time, one
  `IsNotKnown` rule) were done.
- numbers check (the release re-reads): all 1340 visits match the docs' definitions, and History is identical to
  main's. It found a real-data bug: the reset at world entry stayed pending, so the next zone-in read as a swap and
  the user's /who didn't count. A zoning line now clears it, and a reset within 15 s of the entry belongs to the
  entry, so a class change right after login isn't a swap either. Both have tests from the user's dbg.txt lines. A
  re-run of the import on a copy of the live database gives "level 13 DRU/BRD/NEC (Barbarian)" and one row, the swap
  to slot 6 at the world entry.
