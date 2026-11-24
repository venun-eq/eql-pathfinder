---
name: release-status
description: What's released and what's waiting. The version players have, release history, the hourly ship task's state and logs, why something didn't ship, and usage counts by version. Use for "is X out yet", "did the release work", "what version are people on".
---

# Releases, from the outside

Releases ship themselves. Sessions only finish features (CLAUDE.md). docs/pipeline.md explains the machinery.

| Question | Where to look |
|---|---|
| What's live? | `curl -s https://updates.eqlpathfinder.com/releases.win.json` (the highest `Version` with `"Type":"Full"`); https://eqlpathfinder.com/changelog |
| What did each version bring? | `docs/releases/<version>.md` |
| What's waiting for the next release? | `docs/releases/next/*.md`. From the main checkout, `.\tools\ship.ps1 -DryRun` shows the version and notes it would release |
| What's in flight? | `.\tools\feature.ps1 list` |
| Did the last run work? | `artifacts\ship-logs\` in the main checkout (only runs that did something leave a log), and the task: `Get-ScheduledTaskInfo -TaskName 'EQL Pathfinder ship'` |
| Why didn't it ship? | Usually one of: nothing waiting, paused (`.git\eqlp-ship-paused`), uncommitted changes in the main checkout, or a lapsed login. Each of these except "nothing waiting" sends a homelab-alerts notification |
| Which versions are players on? | Usage counts: the "Versions in use" query in docs/usage.md (copies at 0.9.28 or later). Sharers: `SELECT app, date(last_seen,'unixepoch') FROM installs` |

- **Copies update themselves** once the game has been closed for 10 minutes and the app is in the background.
  Copies older than 0.9.33 need one restart.
- **`ship.ps1` alone runs `release.ps1` and `publish.ps1`.** To hold releases, ask the user before `.\tools\ship.ps1 -Pause`.
- **`-Resume` ships at once** when notes are waiting: it is a release, not only an unpause. Run it, like every
  `ship.ps1` call, as the bare command, its output left as it is: in Windows PowerShell 5.1 a `2>&1` or other
  redirect turns npm's normal stderr into a
  stopping error, and the run ends after the website, before the NAS job, the checks and the notification (11-19,
  0.10.17: the NAS step and the checks were run by hand from ship.ps1's own lines).
