---
name: community-pool
description: Check the sharing pool and the community numbers (players, crawls, coverage, freshness), the NAS job that builds them every 30 minutes, and its logs. Use for "is sharing working", "how many sharers", "why does Ask say not enough players".
---

# The sharing pool and the community job

How it fits together:
- Copies that share upload crawls and hunts to the Worker (`units` in D1).
- The NAS job, run every 30 minutes at :10 and :40 New York time, builds the community numbers with the app's own
  math.
- The job publishes `https://updates.eqlpathfinder.com/community/v1.json`, and every copy reads that file.

docs/community-job.md has everything about the job: paths, token, rules, updating it.

## Quick checks

- **The published file:**

  ```sh
  curl -s https://updates.eqlpathfinder.com/community/v1.json | head -c 400
  ```

  It shows `generated`, `players`, `crawls` and `rules`.
- **The pool in D1** (the [production-data](../production-data/SKILL.md) skill):

  ```sql
  SELECT (SELECT COUNT(*) FROM players) players,
         (SELECT COUNT(DISTINCT player) FROM units WHERE day >= date('now','-7 days')) sharing_7d,
         (SELECT COUNT(*) FROM units) units
  ```

  `installs.app` and `last_seen` show each sharing copy's version.
- **The job's log on the NAS** (key-based ssh works from this PC; the login is the private setting
  `EQLP_NAS_LOGIN`, which `tools\private.ps1` loads). In PowerShell, from the repo folder:

  ```powershell
  .\tools\private.ps1; ssh $env:EQLP_NAS_LOGIN '/share/CACHEDEV1_DATA/.qpkg/container-station/bin/docker logs --tail 40 scheduler 2>&1 | grep eqlp'
  ```

- **Coverage:** most cells need 2 players and 5 crawls (config `min_players`, `min_crawls`). With few sharers, "not
  enough players" is expected, not a bug.

## Changing the math

A change to what the numbers are made from (`Engine/Community`, `AskData`, `ShareUnits`, `CommunityJob`) sets `job: true`
in its release note. `ship.ps1` then publishes the new build to the NAS (keeping the previous one in `eqlp-prev`) and
runs it once. Changing the rules (config) or sharing itself needs the user's OK.

The job works levels and active time out from the shared facts, and the app sends nothing it worked out itself
(docs/community-job.md, "Facts in, nothing baked in"), so there is nothing to cross-check.
