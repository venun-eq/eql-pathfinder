---
name: production-data
description: Query or change EQL Pathfinder's live database (Cloudflare D1), which holds feedback, roadmap votes, usage counts, shared crawls, players and server config. Use whenever a task needs real data from players or the server.
---

# The production database (D1)

- **Databases:** production `eqlpathfinder` and staging `eqlpathfinder-staging`. Their IDs are private settings,
  `EQLP_D1_PRODUCTION` and `EQLP_D1_STAGING` (docs/pipeline.md, "Private settings"), which `tools\private.ps1` loads.
- **How to query:** in PowerShell, from `server/` (run `npm install` once in a fresh worktree):

  ```powershell
  ..\tools\private.ps1; npx cf d1 query $env:EQLP_D1_PRODUCTION --sql "SELECT ..."
  ```

  The output is JSON, and the rows are under `results`. If it says you're not signed in, ask the user to run
  `npx cf auth login` in `server/`.
- **Tables:** the schema is `server/migrations/*.sql`.

  | Table | What it holds |
  |---|---|
  | `feedback` | What players send from the app: the [app-feedback](../app-feedback/SKILL.md) skill |
  | `roadmap`, `votes` | What's coming, and votes on it |
  | `app_usage` | Usage counts. Ready-made queries are in docs/usage.md |
  | `players`, `installs`, `units` | Sharing. `units` are the shared crawls and hunts |
  | `config` | The operator's switches (docs/community-job.md) |
  | `codes`, `usage`, `register_ips`, `upload_ips` | Short-lived, pruned by the daily cron |

- **Rules:**
  - SELECTs are always fine.
  - Writes only where a skill says so (feedback `status`, `roadmap` rows), or with the user's OK (`config`).
  - Shared rows (`units`, `players`, `installs`) are read-only.
  - Players' text and error logs stay out of the repo and its commits.
