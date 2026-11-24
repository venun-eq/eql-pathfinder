# Log line formats

Every line the engine reads, with the date it was last seen in a real log. When a patch changes a format,
add the new form here, keep the old one (archived logs still use it), and add a parser test with the real line.
`pathfinder unknown` lists lines that looked like loot but matched nothing.

| Event | Format (body after the `[Ddd Mmm dd HH:mm:ss yyyy] ` timestamp) | Last seen |
|---|---|---|
| Zone | `You have entered The Estate of Unrest 4 (Refined).` — difficulty instance; `… - Solo 4 (Refined).` raid; no suffix = open world | 2026-11-14 |
| Crawl done | `You have completed the Dungeon Crawl and earned reward loot!` | 2026-11-14 |
| Reward XP | `You gained reward experience from the Dungeon Crawl!` (no amount: sized at the next ding) | 2026-11-14 |
| Refund | `You earned a refund of your instance charge.` | 2026-11-14 |
| Kill XP | `You gain experience! (4.735%)` / `You gain party experience! (x%)` — written **before** the slain line, same second. Without `(x%)` (`You gain experience!`) when the regular bar doesn't move: on a loadout at 50 once it's full. The same line with no slain line is a quest or task reward, in either form. See "XP lines around a kill" below | 2026-11-17 |
| AA pool full | `You have reached the AA point cap, and cannot gain any further experience until some of your stored AA point pool is used.` Same second as a kill's XP line, once per kill while the pool is full, at any level. Not a kill on its own | 2026-11-18 |
| Raid, no XP | `You receive no experience for defeating this creature as you are in a raid.` In place of the XP line | 2026-09-28 |
| Kill XP in a bonus event | `You gain party experience (with a bonus)! (0.337%)` / `You gain experience (with a bonus)! (x%)`. Missed until 11-17 (Sep 5–8: 6,504 kills). The tracker counts them and keeps the event's span (`EngineState.BonusXp`); Ask and the pace checks leave that play out, and sharing marks it (`"bonus": true`) so the community job leaves it out too. The same with no `(x%)` is the same line when the bar doesn't move (above), not an older form | 2026-11-17 |
| Kill | `You have slain a tentacle terror!` / `A barbed bone skeleton has been slain by Jarann!`. A kill is its slain line: yours (`You have slain`, or slain by you or your pet), or a groupmate's when a party XP line shares its second. Another player's otherwise | 2026-11-14 |
| Level | `You have gained a level! Welcome to level 24!` | 2026-11-14 |
| Reincarnation | First time: `You have gained the ability "Race Reincarnation: Half Dark Elf" at a cost of 0 ability points.` (and `Class Reincarnation: Cleric`). Again: `You have improved Class Reincarnation: Shadow Knight 2 at a cost of 0 ability points.` (the new rank, no quotes). Every class done: `You have gained the ability "Reincarnation: Completionist" …`. One reincarnation's lines share a second. They are **AA rank-ups**, and each AA line has 3 ranks (patch notes 11-10), so a 4th or later reincarnation of a class or race logs nothing. The app counts ranks only up to 3 (roadmap `rank-check` measures the XP penalty past that). There is no "you reincarnated" or level-1 line, and `Token of Reincarnation has been placed in your inventory!` is a token bought in the Market arriving (11-11: seconds after `You have successfully purchased Iridium Coins for your Wallet.`), not one used. No line says which token a reincarnation spent: 7 of Venun's 9 had no token line just before them, and 11-18 11:08 came with no token earned or bought since the one before | 2026-11-19 |
| Token quest | The crawls' reincarnation token (roadmap reinc-token). All in the same second as `You have completed the Dungeon Crawl…`, and only for a listed place not yet counted in this round. **The first counted crawl of a round:** `You have been assigned the task 'Dungeon Crawler's Reincarnation Token'.` **Each counted crawl, the first included:** `Your task 'Dungeon Crawler's Reincarnation Token' has been updated.`, written twice. **The 5th:** `You have been given: Dungeon Crawlers Reincarnation Token` (no apostrophe), after its two updates. A repeat of a place already counted writes nothing: Kedge Keep 11-17 22:00, Plane of Fear 11-19 00:00. Any difficulty counts (Kedge Keep, `Kedge Keep 4 (Refined)`), and Hole is `The Ruins of Old Paineel`. Venun's two rounds: Nagafen 4, Permafrost, Kedge, Paineel, Guk (11-17 19:46 to 22:24); Nagafen 4, Guk 4, Kedge 4, Fear, Hate (11-18 14:13 to 11-19 00:58). The first line is 11-17 19:46: listed crawls before then (Kedge Keep 11-15 to 11-17, Hate 11-16, Guk 11-16) wrote none, so the quest probably came with that day's patch | 2026-11-20 |
| /who (you) | `[24 NEC/MAG/BST] Venun (Erudite) <Guild> ZONE: …` — race is the **illusion** if one is up. The only line that shows a loadout swap. A swap can happen only in a starting city or its newbie zone just outside (the user, 11-18), so a /who names the loadout from leaving those zones to entering one again: docs/specs/swap-zones.md | 2026-11-14 |
| Loot (current) | `You looted 3 Mote of Major Potential from Reward Chest and stored it in your currency` | 2026-11-14 |
| Loot (older) | `--You have looted a Fragment of Infinite Potential from Magi P`tasa's corpse.--` | 2026-11-12 |
| Untiered mote | `You looted a Mote of Potential from …` (tier 4) | 2026-11-14 |
| Condense | `You offered 12 Mote of Major Potential to Constructed Potential.` Fragments since the 11-17 patch: `You offered 2 Fragment of Major Potential to Constructed Potential.` (2026-11-17) | 2026-11 |
| VTP | `You have been given: Void-Touched Potential` | 2026-11 |
| Pet | `Jarann told you, 'Attacking a ghoul Master.'` (learns the pet name) | 2026-11-14 |
| Melee | `You crush Garanel Rucksif for 113 points of damage.` (+ ` (Critical)`) | 2026-11-14 |
| One point | `You slash Garanel Rucksif for 1 point of damage.` (the singular, in every shape above: "point", not "points") | 2026-11-19 |
| Spell | `You hit Garanel Rucksif for 1297 points of fire damage by Bolt of Flame VI. (Critical)` | 2026-11-14 |
| DoT | `A greater dark bone has taken 22 damage from your Negation of Life.` | 2026-11-14 |
| Damage shield | `Garanel Rucksif is burned by YOUR flames for 9 points of non-melee damage.` | 2026-11-14 |
| Pet melee | `Jarann punches Garanel Rucksif for 75 points of damage.` | 2026-11-14 |
| Hit you | `Garanel Rucksif bashes YOU for 27 points of damage.` / `You have taken 40 damage from Suffocate by a werebat.` | 2026-11-14 |
| Death | `You have been slain by a seahorse patriarch!` / `You died.` | 2026-11-11 |
| Instance | `Player Venun creating instance Befallen 780.` / `X has asked you to join the instance: …` | 2026-11-14 |

Chat (`Name tells General:1, '…'`, `Name says, '…'`, `You tell …`) is never parsed, except your pet's `told you, 'Attacking … Master.'`.

## XP lines around a kill (checked 11-18)

Venun's three logs, Jul 31 to Nov 18: every slain line of yours (or your pet's), and the XP lines within a second of
it. "Level" is the last `Welcome to level N`, set back on a reincarnation line (the next ding logged is 11). **That
misses loadout swaps** (the user, 11-18): the main (PAL/MNK/SHM, 50) is swapped in between relevels with only a /who
to show it, so "Below 50" holds the main's kills too, such as all its Planes of Fear, Sky and Hate kills (the planes
need level 46). The totals and the rule don't depend on the split.

| What the log shows | Below 50 | At 50 | What it means |
|---|---|---|---|
| `(x%)` line, no AA-cap line | 13,741 | 390 | Both bars earning. At 50, right after reaching it and in later stretches (Nov 5, Ruins of Old Guk 4: 124 kills) with no death between; the log doesn't say why the bar moved again |
| `(x%)` line and the AA-cap line | 13,345 | 687 | AA pool full, regular XP still earning |
| `You gain experience!`, no `(x%)`, no AA-cap line | 3,791 | 2,041 | The bar didn't move, AA still earning: a loadout at 50 with the bar full. The "below 50" ones are the main at 50 (below) |
| No `(x%)`, and the AA-cap line | 4,568 | 7,969 | Both full ("receive none"): the game still writes `You gain experience!` |
| No XP line at all | 8,766 | 32,141 | A mob that gives no XP: at 50, mostly low zones (Qeynos Hills, the Karanas, Nov 4 to 5) |
| `You receive no experience … in a raid` | 42 | 81 | A raid kill |

- **The tracker counted only `(x%)` lines** (`ExperienceGained` needs the percent), so at 50 it saw 1,077 of 43,309
  kills, and below 50 it missed 17,167 of 44,253. The 45 visits at 50 with kills (of 848) were counted from those
  `(x%)` stretches and from lone `(x%)` lines.
- **An XP line with no slain line within a second isn't a kill:** 8,858 of them (2,830 with `(x%)`, 6,028 without),
  7,740 more than a minute from any slain line, about 2,360 right after an NPC's `says` or a task update (hand-ins,
  several in one second). Only 128 sat 2 to 3 seconds from one. Today's tracker counted the 2,830 with `(x%)` as kills.
- **Another player's slain line** (`X has been slain by Y!`): 95,766, of which 26,371 share their second with a
  `You gain party experience…` line (a groupmate's kill). The rest are other players', or a groupmate's on a mob
  that gave no XP: the log can't tell those two apart.
- **The 3,791 below 50 are kills, not quest rewards** (checked 11-18, asked by the user). 2,975 of their 3,062 seconds
  hold your slain line, and the other 87 are a second off. They come in runs: 274 runs, three quarters of the kills in
  runs of 10 or more, the longest 232, nearly all with a kill that gave no XP line on each side. An NPC's `says` sits
  within 5 seconds of 21% of them, against 17% of `(x%)` kills and 71% of the rewards; a task update, 0.4%. They are
  Aug 2 to Sep 9 only, mostly in Planes of Fear, Sky and Hate.
- **Why the bar didn't move: they're the main's, at 50** (the user, 11-18: the planes need level 46). The ding-only
  level put them at 22 to 35 because the main was swapped in between a relevel's dings with no ding of its own: on
  Aug 2 a relevel dinged 22, then Plane of Fear - Solo was entered, and the next /who, inside a plane on Aug 3, was
  `[50 PAL/MNK/SHM]`. A `You have gained an ability point!` line comes within 3 seconds of 1 in 10 of them, five
  times the rate of `(x%)` kills, and no ding follows them: the bar is full and AA still earns. A first guess, an AA
  slider, was wrong. The amount each kill gave isn't in the log.
- **So a kill's XP line with no `(x%)` means the loadout played is at the cap.** Below it every kill moves the bar
  (the user, 11-18).
- **No plane visit was ever below 46** (checked 11-20, report #19). The game has always gated the planes at 46; there
  was no "before the rule". Venun's database has 60 plane visits read below 46, from Aug 2 to Oct 26, at 9 to 42.
  None has a /who or a ding inside it: the level is the last ding before, and the trio the last /who before. Of the
  54 that start below 46, 52 have only no-`(x%)` kills, so `CapLoadout.Played` already keeps them out of relevels, and
  one has no kills. The last, Plane of Fear D4 on Sep 9 "at 35" (`ZoneGates.cs` cites it as the gate's date), has one
  kill at 1.37% and four with no XP line, and the same zone half an hour later is all no-`(x%)`: the main at 50, its bar
  filling on that kill. The swap-zones draft's stretch rule would give both visits to the cap loadout.

## What kind of instance: what the log says and what it doesn't

Worked out on 11-18 and checked against Venun's log (Oct 14 to Nov 18). Each instance is picked
at the NPC when it's made: **crawl or respawning**, and **solo or multiplayer**. The log says only part of that.

| Kind | How the log shows it |
|---|---|
| Open world | A plain zone name (`Kedge Keep`), unless a creation or join line came just before it (then it's D0) |
| Difficulty instance | `Kedge Keep 4 (Refined)`; D0 is the plain name right after `creating instance` (`InstanceWatch`). Outdoor zones can be instances too (`East Commonlands 1 (Awakened)`) |
| Raid | `Nagafen's Lair - Solo 4 (Refined)` or `- Group`. Its creation line is the plain one (`creating instance Nagafen's Lair 132`); the next line names it (`Nagafen's Lair - Solo is now available to you.`). Raids step through D0 to D4, and the boss's kill shows: Innoruuk, the Prince of Hate; Lord Nagafen; Lady Vox; Master Yael. None ever logged a crawl done |
| Crawl or respawning | **Not in the log** until the end. Almost every dungeon can be made as either, with the same name and creation line. Only a crawl ends `You have completed the Dungeon Crawl`. Kedge Keep 4 was done in 34 of 37 entries; Nektropos Castle 4 in 3 of 128 |
| Solo or multiplayer | **Not in the log** for crawls and respawning instances: chosen at creation, never named. Multiplayer mobs have more health, so one player in a multiplayer instance kills slower. A join offer (`X has asked you to join the instance`) is someone else's, so multiplayer. Whether you *played* grouped is known: `You gain party experience` on a kill |

**Zoning out and back in** (to get back to the entrance) is common, and is the same instance. In that log, 116
entries went back into the same instance with no new one made and no crawl done: 40 within 2 minutes, 25 within 2 to
10, 26 within 10 to 30 and 25 after longer. The crawl tracker and the fight meter continue the same visit within 10
minutes, so the last 51 are split today. A new instance or a crawl done always starts the next one.

**A D0 instance enters under the open world's name, so the way back goes through the open world.** A zone line from a
neighbour lands in the open world; the instance is entered from there. In the log (Jul 31 to Nov 19), a named
instance came straight after the open world of its name 967 times and straight after another zone 10 times (planes,
two Befallens). After a death in an instance, the way back went through the open world 55 times and was a respawn in
the same instance 5 times, never straight from the bind zone. So the crawl tracker and the fight meter take a plain
name as the D0 instance only when the zone just left is the open world of that name (Nagafen's Lair, Solusek's Eye,
Nagafen's Lair, Nagafen's Lair: the first is the open world, the second the instance); straight from another zone
it is the open world. Nothing looted, killed or died in the open world counts to the visit left behind.

## The client's own log: `Logs\dbg.txt`

EverQuest rewrites `dbg.txt` each time eqgame.exe starts and writes to it from the first second. The eqlog gets nothing
until a character enters the world, so patching, login, server select and character select leave it quiet. The app
reads `dbg.txt` for one thing: whether the tracked character (name and server) is in the world. "Your log is off" shows
only when it is, the zone finished loading over a minute ago, and that character's log has had no line since it arrived
(`Engine/IO/ClientDebugLog.cs`, `Engine/Live/LogOffCheck.cs`). Without a `dbg.txt` from this run, it never shows. Same
timestamp, then a line counter.

| Event | Format | Last seen |
|---|---|---|
| Client start (first line) | `[Tue Nov 17 17:16:40 2026]00000:Starting EverQuest (Built Nov 17 2026 09:39:03)` | 2026-11-17 |
| Server logged in to | `[Tue Nov 17 17:20:16 2026]00039:WorldRPServer message: server name oggok` (the eqlog name's server part), after server select | 2026-11-17 |
| Entered the world | `[Tue Nov 17 17:20:45 2026]00321:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = New Sebilis Expedition`, about 10 s before the eqlog's first line. Written again on every zone, loadout swap and class change (below) | 2026-11-19 |
| Zone loaded | `[Tue Nov 17 17:21:02 2026]00420:Initialization complete.`, after the eqlog's `You have entered …` | 2026-11-17 |
| Zoning (not read) | `Waiting to connect to zone... Character Venun`, `Zone Connect -- 2 -- Sending MSG_EQ_ADDPLAYER`, then `Received MSG_EQ_ADDPLAYER` with the new zone and `Initializing zone.` | 2026-11-19 |
| Loadout swap (not read) | `Resetting UI.`, then about 6 s later `Received our Player from zone.` and `Received MSG_EQ_ADDPLAYER …` with the **same** zone and none of the zoning lines. The eqlog writes nothing | 2026-11-19 |
| Classes changed inside a loadout (not read) | `Received our Player from zone.` and `Received MSG_EQ_ADDPLAYER …` alone: no `Resetting UI.`, no zoning lines, no `I died.`. The eqlog writes nothing, so only a later /who says which classes | 2026-11-19 |
| Died, respawned in the same zone | `I died.` and `knockout 3`, then within a second `Received our Player from zone.` and `Received MSG_EQ_ADDPLAYER …` with the **same** zone and none of the zoning lines: a class change's lines, with `I died.` before them (Mistmoore 227, 11-20). The eqlog has its own `You have been slain by …` | 2026-11-20 |

Character select is `Starting char select.`; server select is `Server selected <host> (<id>).`. Neither is read.

## Which loadout is on: the per-loadout files (checked 11-19)

The game keeps settings for each loadout slot in its folder, named for the character, server and slot:
`UI_<Name>_<server>_LO<n>.ini` (windows) and `<Name>_<server>_LO<n>.ini` (hot buttons, spell sets, socials). Venun on
oggok has LO1 to LO9. A watch of every write in the game folder while the user swapped (11-19, 15:35 to 15:51, a /who
after each step) showed:

| What the user did | What the game wrote |
|---|---|
| Entered the world on LO6 | `UI_Venun_oggok_LO6.ini`. /who: `[10 DRU/BRD/NEC] Venun (Barbarian)` |
| Stood still 4 minutes | Nothing: no loadout file is saved on a timer |
| Swapped LO6 → LO1 | `UI_…_LO6.ini` (the slot left), about 1 s later `UI_…_LO1.ini`, about 6 s later `Venun_oggok_LO1.ini`. /who: `[1 PAL/MNK/SHM] Venun (Froglok)` |
| Swapped LO1 → LO6 | The same the other way. /who: `[10 DRU/BRD/NEC]` |
| Changed a class in LO6 | No loadout file. /who: `[10 BRD/NEC/ENC]` |
| Changed another class in LO6 | No loadout file. /who: `[10 NEC/ENC/BST]` |
| Swapped LO6 → LO1 → LO2, no /who, then zoned | Each swap the same pattern; zoning wrote `UI_…_LO2.ini` twice. /who in the new zone: `[1 SHD/SHM/WIZ] Venun (Iksar)` |
| Camped | `UI_…_LO2.ini` when the camp began |

What that means:
- **The slot on is the loadout file written last**, once a swap has settled (the slot left is saved about a second
  before the one swapped to). `<Name>_<server>_LO<n>.ini` is written only for the slot swapped to.
- **A slot's classes and race aren't in its files.** The spell sets in `Venun_oggok_LO6.ini` (the DRU/BRD/NEC slot)
  hold cleric and shaman spells. A /who on a slot (or the spells cast) names its trio once; it holds until the classes
  change.
- **Classes changed inside a slot write no file.** Only `dbg.txt` shows that they changed (above), not to what.
- **Live only.** A file keeps only its last write time and `dbg.txt` is rewritten at each start, so nothing here helps
  a re-read of old logs.
- **A reincarnation keeps the slot** (the user, 11-19): you swap to the loadout first and reincarnate inside it, so
  there is no swap at the reincarnation, only the level dropping to 1. The slot's classes are the ones set before it
  (a class change then shows in `dbg.txt` as usual). On 11-19 at 01:15 `LO7` then `LO3` were written just before the
  reincarnation lines: that was the swap to the slot, not the reincarnation.
- **Not checked yet:** another character on the same PC, the game crashing.

Reading the game's memory instead was looked at and not done: [game-memory.md](game-memory.md).
