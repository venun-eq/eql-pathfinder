# Campaign shows the trios you played

**Why:** Plan → Campaign, "runs played before this plan", showed the classes the log reset together, not the ones you
leveled (report 13: "These were not my actual combinations"). On 11-11 the player reset everything in one burst, then
played other trios; Campaign said run 2 was ROG · RNG · WAR, but they leveled WAR/CLR/ROG, which History → Relevels
has right.
**Players:** anyone who reincarnated several loadouts at once, or chose a trio other than the one they reset.

## Picture
Before: `Run 2 · Ogre · ROG, RNG, WAR` (what the log reset) with gains `ROG 1/3 · RNG 1/3 · WAR 1/3 · Ogre 1/3`.
After: `Run 2 · <race Relevels shows> · WAR, CLR, ROG`, gains `WAR 1/3 · CLR 1/3 · ROG 1/3 · <race> 1/3`. The
numbers over all played runs are the same as before: every class and race gets the same ranks in the end.

## Cause
`Campaign.History` built each played row from the reincarnation event (the AA lines of one second). The relevel
(Relevels.Of, from /who) already knows the trio and race that were played and which event it belongs to
(`Relevel.EventAt`), but History never saw relevels.

## Done when
- [x] A played run shows the relevel's trio and race, and its gains read from them in order - `PlayedRunsShowTheTrioYouLeveled`
- [x] Ranks over all played runs are unchanged: each class and race ends on the rank the log counts - `PlayedRunsShowTheTrioYouLeveled`
- [x] A run whose trio can't be told (no relevel, no journey) keeps the log's grouping - `AReincarnationYouNeverPlayedKeepsTheLogsGrouping`
- [x] Where the played trios and the log's groups don't hold the same classes (or races) over the runs, those runs keep the log's - `WhenThePlayedTriosDontAddUpTheLogsGroupingStays`
- [x] A burst out of reset order is numbered in play order; an unplayed reset comes first in its burst - `ABurstIsNumberedByWhenEachTrioWasPlayed`

## What the user decided (11-19)
Played-before rows of a burst are ordered by when each trio was played, so the user's Nov 11 burst reads PAL/MNK/ENC, DRU/BRD/RNG, BST/MAG/NEC, WAR/CLR/ROG (SHD/SHM/WIZ first, on Nov 10). History → Relevels and its event links stay as they are.

On the user's real log the Nov 11 burst reads PAL/MNK/ENC, RNG/DRU/BRD, WAR/CLR/ROG, NEC/MAG/BST. That is the log's order, from /who's play times (WAR/CLR/ROG leveled Nov 12-13, NEC/MAG/BST from Nov 14), and differs from the user's recollection; the app follows the log.

## Edge cases
- The added-up check is per burst (a lone reset is a burst of one): a burst whose played trios or races do not match its resets keeps the log's grouping, the others show what was played.
- The burst window and `Relevels.Of`'s pairing window are one constant, `Relevels.PairWindow` (300 s), used by `Campaign.Bursts`. Kept the window rather than sorting on `Journey.Start ?? At`, which would also reorder separate bursts.
- Nothing yet / no plan yet: no events, no rows; unchanged.
- Play the log never saw: the "unseen" runs have no event and stay as they are.
- A burst with no play between (Nov 11, four events in under three minutes): the rows are numbered by when each shown trio was played (its relevel's start), not by the reset lines. A burst is resets each within 5 minutes of the one before. A row with no relevel (no /who yet) sorts by its reset time, so it comes first in its burst, before the played ones. Ranks walk in the shown order, so CLR 1/3 or Completionist land on the run that earned them; totals equal the roster.
- The race: Relevels' race (from /who) wins; the log's race line is the fallback. They can differ in a burst (the race
  line is reset with whichever classes share its second, not the ones it was played with); then the race ranks only
  agree if the same races appear in both, which the check above covers.
- A relevel still being leveled is not a played row (as before); its trio already came from the relevel.
- Reincarnated but never played: the log's grouping (no /who yet).

## Not doing
- Plan matching (`Matched`, `OffPlan`) still reads the event's classes: runs since the plan began are matched by what was
  reincarnated. Debt if a player resets one trio and plays another.
- Changing History → Relevels' links (the user chose to keep them as they are).

## Where
- `src/EqlPathfinder.Engine/Analysis/Campaign.cs`: `Evaluate` (passes the relevels), `History`, new `PlayedAs`
- `tests/EqlPathfinder.Engine.Tests/CampaignPlayedTriosTests.cs`

## Reviews
- architect: approved, no must fix. Tests fail without the change and kept their meaning (6ea4f0c only added a start to the helpers). On the real log, read-only: the seven rows read SHD/SHM/WIZ; PAL/MNK/ENC, RNG/DRU/BRD, WAR/CLR/ROG, NEC/MAG/BST; SHD/ROG/BER (Completionist 1); WAR/CLR/RNG, and their ranks plus the two open relevels equal the roster (16 classes, 9 races). The burst's last two are the other way round from the player's list, and the log agrees with the app: /who `[2 WAR/CLR/ROG]` 11-12 23:23 and crawls L4-49 to 11-13 21:16, then `[4 NEC/MAG/BST]` from 11-14 00:56. Should fix: (1) docs/debt.md's new line, second sentence, is no longer true (rows now follow `Journey.Start`); (2) `PlayedAs` checks the class and race bags over the whole log, so one bad pairing or a playable-race illusion on /who turns every played row back to the log's grouping: check per burst, as the spec's "those runs" says; (3) `PlayOrder`'s 5 minutes is the same window as `Relevels.Of`'s 300 s pairing (a longer burst can't be paired anyway): share one constant, or sort by `Journey.Start ?? At` with no window.
- engineer: (fixes) (1) debt.md sentence dropped. (2) `PlayedAs` checks classes and races per burst, `ABurstThatDoesntAddUpKeepsTheLogsGroupingAndTheOtherStaysPlayed`. (3) shared `Relevels.PairWindow` for `Relevels.Of` and `Campaign.Bursts`/`PlayOrder`. (4) `ARepeatedClassGetsItsRankOnTheRunPlayedFirst`. Nov 11 burst order recorded above.
- designer: not needed, the played-before rows keep their layout and wording; only which trio, race and order they show changes.
