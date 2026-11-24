# A way back into a D0 instance goes through its open world

**Risky, 11-19 (qa).** From the draft of [numbers-audit](numbers-audit.md). The parse changes, so the tracker and the
fight meter re-read: `EngineState.CurrentVersion` goes to the next after main's when this finishes (main is 13
since xp-at-cap, 14 with numbers-check-cap, so 15), `FightMeterService.CurrentVersion` 5 to 6, the note `reread: true`. The re-read
also takes damage-lines' 1-point fix (the user, 11-19); the rest of damage-lines stays out.

**Why:** a D0 instance enters under the open world's name. Leaving Nagafen's Lair by a zone line and walking back in,
the tracker took the open world for the instance and the instance for the open world: a 2 h 49 min visit reads 37
minutes, 375 kills and 9 motes have no visit, and two motes from after it stay credited to it. 1-point hits are never
read (7,529 of the player's own lines).
**Players:** everyone who levels or farms at D0, worst for a leveler at D0 who zones out to a neighbour (Nagafen's
Lair to Solusek's Eye is a common one). Every player's History changes on the re-read.

## Done when
- [x] A plain zone name entered from another zone is the open world; the same name entered next from there, within 10
  minutes and with no new instance, is back in the unfinished D0 visit. Nagafen's case is one visit from creation to
  Toxxulia Forest, one pause, every kill and mote - `AD0EntryFromANeighbourIsTheOpenWorldAndTheNextIsTheInstance`
- [x] Nothing in the open world counts to the visit you left, though the names are the same: no loot, kill or death
  - `NothingInTheOpenWorldCountsToTheVisitItLeft`
- [x] The fight meter reads the same entries the same way - `AD0EntryFromANeighbourIsTheOpenWorldForTheMeterToo`
- [x] Two tests corrected to the path the log shows (bind zone, open world, instance; never bind zone straight in):
  `ADeathInAD0CrawlComesBackToTheSameVisit`, `AD0InstanceIsACrawlForTheMeterToo` (own commit, rule 5)
- [x] "for 1 point of damage" is read in every shape, yours, your pet's, a damage shield, a spell and on you -
  `AOnePointHitIsRead`, `AOnePointHitOnYouIsRead`; and another fighter's on the meter - `AnotherFightersOnePointHitIsOnTheMeter`
- [x] Both readers re-read: each version raised with its hash, the release note `reread: true` - `RereadGuardTests`
  (its hashed lines now hold a Nagafen-shaped return and 1-point hits, so it fails until then)
- [x] docs/log-formats.md says how a D0 return is told from the open world, with the counts below - read in review
- [x] After the re-read, v-20260816210336 runs 21:03:36 to 23:52:53 - the numbers-check skill on a copy of the
  database (`analyze.mjs` for that visit), its kills and motes matching the log's. Checked 11-19: 21:03:36 to 23:52:53, 482 kills and 1.6875 T5-eq both match the log (fullcheck); the kill mismatches elsewhere are the charmed-pet ones (68 before, 69 after: see Edge cases).

## Edge cases
- **Same name, different thing** (this spec): open world then instance, both plain. Out to the entrance and back in
  (instance, open `X`, `X`: Cazic-Thule's shape at D0) stays one visit, as today.
- A new instance made in the open world before going back in: a new visit, as today (`NewInstanceSinceEntry`).
- Back more than 10 minutes later: the open world, as today (docs/log-formats.md: 51 such entries split).
- A chest looted after the crawl is done, before leaving: still the crawl's.
- **Starting and stopping:** a restart between the open-world entry and the instance's: the zone and the pause are in
  the saved state, so it resumes the same.
- **Re-sends and limits:** the re-read changes D0 visits' ends, kills and loot. The community numbers are from D1, but
  whether a changed visit is shared again is the architect's call (the daily limit, 11-18).
- **Old versions:** nothing sent changes shape.

- **The 69th raw-log kills mismatch (11-19):** v-20260828231114, Plane of Sky D0, app 61 and log 64. It is a charmed pet's visit (the pet Hellsfury fights a heart harpie in the log; `fullcheck` counts 4 mob-on-mob). Matching "hits YOU for 1 point" now moves a remembered pet name into Hostile in `LineParser.ParseHitsYou`, which `fullcheck` does not follow, so it belongs to the existing charmed-pet debt in docs/debt.md. The other 68 are unchanged (diffed by visit id before and after).
## Not doing
- Temple of Cazic-Thule, 5 Nov: already one visit (below).
- A respawn inside the same D0 instance after a death (plain `X` straight after plain `X`): read as out, as today. The
  log has it 5 times in 60 deaths in instances, none at D0.
- damage-lines' new pet's first hits (left out by the user, 11-19).

## Where
- `src/EqlPathfinder.Engine/Tracking/Tracker.cs`: `AsInstance` (the D0 return needs `State.Zone == c.Zone`), `OnLoot`
  and `OnDied` (credit only `Inside`, or a done crawl not yet left), `EngineState.CurrentVersion`
- `src/EqlPathfinder.Engine/Combat/FightTracker.cs`: `OnZone`, the same D0 rule; `FightMeterService.CurrentVersion`
- `src/EqlPathfinder.Engine/Parsing/LineParser.cs`: the " points of damage." gate (line 254) and the damage regexes
  (529-564); `Combat/CombatParser.cs`: lines 35, 46, 97, 100
- `docs/log-formats.md`: "What kind of instance", the Open world row and "Zoning out and back in"
- Tests: `TrackerTests`, `FightMeterTests`, `ParserTests`, `RereadGuardTests`

## What the log says (qa, 11-19)

- **Nagafen's Lair, 16 Aug.** 21:02:23 entered Nagafen's Lair (from Lavastorm), 21:03:04 `creating instance Nagafen's
  Lair 10081`, 21:03:36 entered it again: the D0 visit. 21:40:58 entered Solusek's Eye, 21:41:13 Nagafen's Lair,
  21:41:30 Nagafen's Lair, then kobolds and motes (21:43, 21:48, 21:52, 22:03, 22:13, 22:41) until Toxxulia Forest at
  23:52:53. The tracker read 21:41:13 as back in (a D0 return) and 21:41:30 as out, so the visit ends 21:41:30 with
  104 kills, the two motes after it are still credited (`OnLoot` keys on the zone's name, the same for both), and
  2 h 11 min of instance play has no visit. The fight meter has the same rule (`FightTracker.OnZone`).
- **Which reading is right: out, then in.** A zone line from a neighbour lands in the open world; the instance is
  entered from there. In the player's log (Jul 31 to Nov 19), a named instance (`X 4 (Refined)`) came straight after
  the open world `X` 967 times and straight after another zone 10 times (planes and two Befallens). Neighbour, open
  `X`, instance `X`: 414; neighbour, instance `X`, open `X`: 11. After a death in an instance: back through the open
  world 55 times, respawned in the same instance 5 times, straight from the bind zone never.
- **Temple of Cazic-Thule, 5 Nov: already right.** 14:38:15 TCT 4, 14:56:41 TCT (open world), 14:56:56 TCT 4, done
  14:59:06. The live database (copied 11-19 00:21) has one visit, v-20261105143815, 96 kills, one 15-second pause.
  The split chestless-crawls saw came from 0 kills at the AA cap (no activity, so the zone-out closed the visit);
  kills-at-cap counts kills from the slain line, and its re-read joined the two.

## Decisions (architect, 11-19)

- **What's stored:** no column, table or saved-state field changes. Both readers rebuild from the logs:
  `EngineState.CurrentVersion` 15, `FightMeterService.CurrentVersion` 6 (no open branch has raised either; if one
  lands first, take the next and refresh both hashes). The saved zone and pause keep their shape, so a restart
  between the open-world entry and the instance's resumes as the edge case says, with no migration.
- **What's sent:** nothing new and no new shape: `ShareBuilder.Schema` stays, `site/privacy.html` stays. Changed D0
  visits are shared again by the rule already there, with no special case in `ShareUnits`: their ends, kills, loot
  and damage are facts (rule 1), so their hash moves and they go up. The 1-point hits change `SharedDamage` (total,
  by source, taken) on nearly every crawl, and a merged stretch shifts `Seq` after it, so expect one re-send of most
  of each sharer's crawls. That fits the 3,000-a-day fuse (this player's history is about 1,300 crawls and hunts).
- **Who else reads it:** the community job reads damage (DPS) and kill rows; neither changes meaning, so `job: false`.
  Crawls from apps not yet updated lack 1-point hits (a point a hit); no filter by engine for that.
- **Limits and cost:** a one-time re-send per sharer, under the daily limit; D1 writes are those rows once. No change
  to the limit or to `ShareUnits`.
- **Taking it back:** a later re-read rewrites and re-sends, so it's reversible, except for units the re-read stops
  making. Nagafen's old open-world stretch (from 21:41:30, the instance's kills) is no longer built, and nothing
  withdraws a unit from the server, so it stays in the pool beside the crawl that now holds those kills. Not in this
  branch (a withdrawal needs a server endpoint, deployed by hand): docs/debt.md, 11-19.
- **The guard:** take `TrackerHash` and `MeterHash` from `RereadGuardTests`' message after the fix, never by hand.

## Reviews

- architect: one must fix, fixed (7802e0b): the cap fix took tracker version 14 on main first, so this branch merged main and took 15 with a fresh hash; the meter stays 6. Should fix, fixed: the last Done-when line's pasted text, and the one new raw-log kill mismatch was checked visit by visit (a charmed pet in a Plane of Sky D0, so it joins the fullcheck charm debt). The fixture fix to `AD0EntryFromANeighbourIsTheOpenWorldAndTheNextIsTheInstance` is sound (the old one was silent past `SilentLogMinutes`). Debt: the D0 return rule is written twice (tracker and meter); withdrawing a re-read's stale units from the server (the lead's call: debt). The stale Temple of Cazic-Thule debt row is removed.
