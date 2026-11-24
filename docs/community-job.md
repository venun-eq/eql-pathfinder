# The community job (on the QNAP)

Every 30 minutes, at **:10 and :40 past each hour (New York time)**, the NAS turns every friend's shared crawls into the
community numbers the app reads (copies upload every 10 minutes and look for a new file every 30 and at startup, so
numbers are under about an hour old), and publishes them as `community/v1.json` in the update bucket
(<https://updates.eqlpathfinder.com/community/v1.json>; the r2.dev address of the same bucket keeps working for older copies). The server's config points the app there.

It runs in the `scheduler` container (Alpine, cron) next to the other homelab jobs, using the app's own command-line
tool: `EqlPathfinder.Cli community --upload`. The math is the app's Ask math (`AskData.From`), run per player and then
combined (`Engine/Community/CommunityBuilder.cs`).

## Where things are

| What | Where |
|---|---|
| The program (Linux build of `EqlPathfinder.Cli`) | `/share/CACHEDEV1_DATA/AppData/scheduler/eqlp/` (the container sees `/scripts/eqlp/`) |
| The script | `AppData/scheduler/eqlp-community.sh` (copy in `server/nas/`) |
| The schedule | a line in `AppData/scheduler/crontab` (`10,40 * * * *` since 11-16; `40 4,10,16,22 * * *` before that). The container copies it at start; after editing, reload it with `docker exec scheduler crontab /scripts/crontab` (or restart `scheduler`). A backup from before the change: `crontab.bak-20261116` |
| The token, the account and the database | `AppData/scheduler/eqlp.env` (owner-only). `eqlp-set-token.sh` writes `CF_ACCOUNT_ID` and `CF_API_TOKEN` and keeps every other line. `EQLP_D1`, the D1 database's ID, is a line added by hand: the program has no database of its own and stops without one |
| The last file it made | `AppData/scheduler-state/eqlp-community.json` |
| Its log | `docker logs scheduler` (Container Station → scheduler → Logs), lines tagged `[eqlp-community.sh]` |
| Failures | a notification on ntfy, topic `homelab-alerts` |

The scheduler needs `libstdc++` for .NET; it's in the container's `apk add` line in the compose file (added 11-16).

## The token

A Cloudflare **custom API token** named "EQL Pathfinder community job (NAS)":
- Permissions: **Account · D1 · Read** and **Account · Workers R2 Storage · Edit**
- Client IP address filtering: **Is in** the home's public address, which the NAS uses (the token's page in the
  Cloudflare dashboard shows the one it was made with)

To replace it (expired, IP changed, leaked): make a new token the same way, then from PowerShell in the repo folder
(the NAS's login is the private setting `EQLP_NAS_LOGIN`, which `tools\private.ps1` loads: docs/pipeline.md, "Private
settings"):

```powershell
.\tools\private.ps1; ssh -t $env:EQLP_NAS_LOGIN sh /share/CACHEDEV1_DATA/AppData/scheduler/eqlp-set-token.sh
```

It asks for the token without showing it, saves it, and runs the job once. Delete the old token in Cloudflare after.
The account stays the one `eqlp.env` already has (a first run asks for it), and every other line of the file is kept.
The NAS's copy of the script is `server/nas/eqlp-set-token.sh` as it is, with nothing filled in.

## Rules for what gets published

Each level, band and difficulty number needs enough players and crawls behind it, and no player outweighs the others.
They live in the server's `config` table (missing = default):

| Key | Default | Meaning |
|---|---|---|
| `min_players` | 2 | players behind a number (the spec's 3; 2 while the pool is small) |
| `min_crawls` | 5 | crawls behind a number |
| `max_share` | 0.2 | most weight one player can have (never below an equal share) |

Change one with `INSERT OR REPLACE INTO config (key, value) VALUES ('min_players', '3')`; the next run uses it.

Left out every run: flagged players and installs, quarantined units (`units.quarantined != 0`), unreadable or oversized
fact blobs (`length(u.facts) <= 96 KiB` filtered in SQL over 500-row pages, and each unit is bounded to 96 KiB compressed /
4 MiB uncompressed and isolated so one bad row never halts the run), runs before the 11-10 patch that were mostly Denon's Desperate Dirge (shared crawls carry only the day, so all of
11-10 counts as before), crawls and hunts marked `"bonus": true` (played during a bonus-XP event, which makes a place
look faster than it gives; apps from 11-17 on mark them), and crawls with impossible numbers.

## Updating the program

After a change to the math, publish the Linux build and copy it over (PowerShell, from the repo folder):

```powershell
dotnet publish src/EqlPathfinder.Cli -c Release -r linux-musl-x64 --self-contained -p:PublishSingleFile=true -p:InvariantGlobalization=true -p:DebugType=none -o out/nas-cli
.\tools\private.ps1; scp out/nas-cli/* "$($env:EQLP_NAS_LOGIN):/share/CACHEDEV1_DATA/AppData/scheduler/eqlp/"
```

Run it right away instead of waiting for the next half hour:
`.\tools\private.ps1; ssh $env:EQLP_NAS_LOGIN /share/CACHEDEV1_DATA/.qpkg/container-station/bin/docker exec scheduler sh /scripts/eqlp-community.sh`

## Schema 2 (open world, solo and grouped)

From the build after 0.9.3, the job also reads `stretch` units (open-world hunting). A stretch carries no trio, rank or
loot, so it takes the trio and rank of the same player's nearest crawl that day; before the 11-10 nerf, a stretch on a
bard or unknown trio is left out (the open-world DDD rule). Open-world places are named "… · open world" and never
rank as farms (their motes aren't shared). Beside the all-play tables, `solo` and `group` hold the same tables from
solo and grouped play (grouped = most kills gave party experience). `stretches` counts the hunts behind the numbers.

Apps up to 0.9.3 read schema 1 only: given a schema 2 file they keep their last saved copy. So publish the app that
reads schema 2 first, then copy the new Linux build to the NAS.

## Facts in, nothing baked in (11-17)

The pool keeps facts, and the job works everything out from them each run, so a fix to the math reaches every crawl
in the pool at the next run, not only crawls whose player re-sends them. A published number never carries a choice
that's each player's own: those are applied by the app, when it reads the file.

- **Same rules as the app (11-19).** Damage a second divides by active time and is 0 for a visit with no rate (`Rates.PerSecond`). Places and difficulties rank only visits with a rate (finished, or 8 kills or more; `onlyWithRate`), here and in the app's `Without`; bands still count them, and the log line counts them as "no rate". A kill at the level cap adds no XP and isn't counted for XP per kill (it still counts as a kill).
- **Levels** of a crawl: its kill rows' XP plus its rewards' share. A hunt's minutes and levels: its kill rows (first
  kill to last plus a minute, as `FieldHunts` does). `CommunityBuilder.Recompute`.
- **Active minutes** of a crawl: time inside less idle, by the app's own rule (`ActiveTime`, the 3-minute threshold),
  from the kill rows and two facts upload schema 4 added: `pauses` (out of the instance mid-visit) and `end` (where
  active time ends), both in seconds from the start. A crawl from an app before schema 4 has neither and keeps the
  `derived.activeMinutes` it was sent with, until that app re-sends it. A schema change re-sends every unit, so a
  sharer's whole history comes back with them within a day or so (3,000 units a day each). Since 11-17 a unit is
  re-sent only when its facts or the schema change, not its `derived` numbers or the engine version (`ShareUnits.Make`):
  updates that changed the app's math used to re-send whole histories and hit the daily limit. And the server skips a
  unit it already holds unchanged (`content_hash`, migration 0006): it's neither written nor counted against the
  limit, so a reinstall or a lost ledger costs nothing. The limit stays, as a fuse against a runaway copy.
- `derived` is still sent and kept, only as a cross-check: `pathfinder status --db <copy> --facts-check` compares
  it with what the job works out, for one history (11-17, on Venun's: 1,225 crawls and 86 hunts agree, but for 6
  crawls whose kill rows hold duplicates).
- **Duplicate kill rows (found and fixed 11-17):** two processes following the same log into one database wrote some
  kills twice, each pair in a row (23 rows on 11-17, all in crawls; a second live copy for a few minutes, likely a
  developer run against the real data folder). Same-second kills that look alike are real: AE pulls, with as many
  "You gain experience" lines in the log (1,780 such seconds in Venun's history, none extra before 11-17). Since the
  fix, a database takes one writer (`WriterLock`, a `.writer` file beside it) and a second open fails. The 23 rows
  stay until a re-read.
- **Loot apart:** every place, band and difficulty has `moteT5PerHour` and `fragmentT5PerHour` (fragments at 1× a
  same-tier mote), and the same for above T5 (`moteHighTierPerHour`, `fragmentHighTierPerHour`). The job runs each
  player's Ask math on motes alone and on fragments alone; it's a weighted sum of each crawl's loot, so an app adds
  motes + its fragment value × fragments and gets what the math would give at that value (`CommunityData.For`).
  `t5PerHour` and `highTierPerHour` stay, at 2.0 (`CommunityBuilder.FragmentValue`), for apps before the split; the
  file's schema is still 2, since the new fields are additions.
- Still modelling, and rebuilt from facts every run, so they're fine: times at rank 0 (the app applies its rank),
  recency weighting, the level window, and leaving out pre-nerf DDD runs and impossible numbers.
- Still taken from the app, as labels beside their facts: the trio (the casts it was judged by are shared too), the
  reincarnation rank (could be worked out from the shared reincarnations), and where one open-world hunt ends and
  the next begins (kills are only shared inside a hunt, so that can't be redone).

## Revisit later: open-world hunts dropped for want of a trio

Measured 11-16 on one player's 129 shared hunts: the job keeps 59, the app itself keeps 84 for its own Ask answers.
The job borrows a hunt's trio and rank from a crawl the same player shared the same day; the app uses any visit within
6 hours (including visits where nothing was slain, which are never shared) and its spell-checked trio. Of the 70 the job drops,
42 are pre-patch hunts on a bard trio (the DDD rule, correctly) and the rest have no shared crawl that day.

Two fixes, not built (little to gain while no open-world place has 2 players behind it, and most recovered hunts are
from before the patch, which the 30-day fade weighs down anyway):
- Job only: borrow from the nearest crawl by `seq` within the same `character`, up to a day away, not across a
  reincarnation unit. Keeps 65 of the 129.
- App and job: the app sends each hunt's trio and rank (it already works them out); the job uses them and falls back to
  the borrowing for older apps. Keeps 84. Needs a release; sharing friends re-send only their hunts.

Check again when more friends share: if open-world places stay empty or thin while friends hunt in the open world, build
the second fix with the next release that touches sharing.

## How often, and what it costs (11-16)

Every 30 minutes since 11-16 (it was every 6 hours, with the app looking every 3):
- **Freshness** is set by the app as much as the job, so both changed together: copies upload every 10 minutes and
  look for the community file every 30 (skipping the download when theirs is under 25 minutes old).
- **Privacy while the pool is small** was the reason for 6 hours. Shared facts carry the day, never a clock time, but
  with a handful of sharers a file rebuilt often could be compared version to version to see who just played where (a
  place-and-level row still only shows with 2 or more players). The user chose 30 minutes knowing that, with 3 sharers.
- **Cloudflare's free plan** is far off. The job reads its rows a page at a time, each page starting after the last
  row of the one before. (OFFSET paging made D1 count every skipped row again: 13,832 reads for 1,576 rows on 11-16,
  growing with the square of the pool.) Now it's about 3 reads per row (the joins): ~5,200 per run, ~250,000 a day
  at 48 runs, against 5 million a day allowed. R2 takes one write per run (48 a day; a million a month are free). Each run's log line ends with the rows it read.

Build the Linux program with `-p:InvariantGlobalization=true -p:DebugType=none` (and `--self-contained -r linux-musl-x64
-p:PublishSingleFile=true`): without invariant globalization it crashes at start in the container, which has no ICU.

## Fake players (built 11-17; registration opened 11-19)

While sharing was invite-only (config `require_enrollment` = 1), a new player needed a code from someone who shares, so
nobody could make players in bulk. Two brakes were built for the day registration opened, each a config row, no release:

- **The job: a player counts only after `min_player_days` days registered** (`CommunityRules.MinPlayerDays`, default 0 =
  off). It uses the server's own `players.created`, which shared facts can't fake, so a made-up player has to be kept
  that long. The job's log line counts the players still waiting.
- **The Worker: at most `max_new_players_per_ip` new players per address per day** (0 or missing = off). Linking another
  PC to an existing player never counts. The address isn't stored: only a code from it, the `IP_SECRET` Worker secret (never in the database or the source,
  different for production and staging) and the day, in `register_ips`, deleted after 2 days. Over the limit, registering answers 429
  `too_many_players`.

All current players registered 11-16/17, so switching the job's rule on early would hide everyone for days.

**One character per game server per connection (on now; user 11-17):** EverQuest Legends allows one character per server
on an account, and more than one account isn't supported, so a connection may share only one character per game server a
day (`upload_ips`: the connection's daily code, server, character code, player; 2 days). A second character, or the same
one under a second player (salted differently, so it looks like another character), gets 409 `one_character_per_server`
until UTC midnight; the app waits that long (0.9.30+). Deleting your shared data or linking to another player clears the
note. Config `one_character_per_server` = "0" turns it off. Known cost: two people sharing one connection (a household,
or a carrier that puts many customers behind one address) can't both share characters on the same server.

**Deployed 11-17** with the TypeScript Worker (server/README.md): the one-character rule is on in production; the new-player cap
and `min_player_days` are off. Both rules passed end to end on staging (`share-smoke --api https://api-staging.eqlpathfinder.workers.dev
--rules`, with staging's `max_new_players_per_ip` set to 1 for the run).

**Launch day (registration opens):** set `min_player_days` = 3 and `max_new_players_per_ip` = 1, decide `min_players`
(3 is the spec's number; with a small pool it hides most cells), then `require_enrollment` = 0. Run `pathfinder
share-smoke` once afterwards: it registers a throwaway player, which also checks the per-address count works.

**Opened 11-19 (the user's go):** in production, `max_new_players_per_ip` = 1 and `require_enrollment` = 0. `min_players`
stays 2 (no row; 5 sharers). The app needed no release: it reads `requireEnrollment` from `GET /v1/config` and stops
asking for a code. The website lost "invite-only for now" (index, privacy).

**Still to do: `min_player_days` = 3, on or after 11-20 8:30pm New York time (2026-11-21 01:30 UTC).** It waits so the
five sharers who registered 11-16/17 keep counting: set now, only 2 of 5 would count, and Ask would say "not enough
players" in more places for a day (the newest of them registered 2026-11-18 01:22 UTC). Insert the row; the job reads
it on its next run, and its log line counts the players still waiting.
