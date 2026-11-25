---
name: app-feedback
description: Read and act on what players send from the app (Report a problem on any page, with its screenshot and window details, or Help → Send feedback for bugs, ideas and "confusing"), reply to them, and read roadmap votes. Use for "fix reported bugs", "what are players saying", "look at that screenshot", "what should we build next".
---

# Feedback from the app

Players send feedback two ways, both to POST /v1/feedback (`server/src/feedback.ts`, sent by
`src/EqlPathfinder.App/Feedback.cs`), into D1's `feedback` table. See [production-data](../production-data/SKILL.md)
for how to query.

- **Report a problem** (the rail's Report button, or Ctrl+Shift+R, from any page; `web/js/report.js`): the page it was
  on, the window's details and, unless the player unticked it, a screenshot of the page.
- **Help → Send feedback**: text only, `page` is `help`.

| Column | Meaning |
|---|---|
| `id` | Stable. Use it to refer to a report |
| `received` | Unix seconds |
| `kind` | bug, idea, confusing, other |
| `text` | What they wrote |
| `app` | Their app version |
| `page` | The page they were on (home, ask, campaign, roster, crawls, loot, relevels, upgrade, help, settings). Always `help` from versions before Report a problem |
| `contact` | A name, only if they gave one |
| `voter` | An anonymous id per copy |
| `errors` | Their error.log, only if they ticked the box |
| `status` | new, read, planned, done or wontdo. **This is how sessions know what's handled**, and the player sees it |

`feedback_extra` (same `id`, only for reports that have any):

| Column | Meaning |
|---|---|
| `details` | Window size, display scaling, Windows and WebView2 versions: "1701×1213 window · 150% scaling · Windows 10.0.26300 · WebView2 157.0…". What a layout bug depends on |
| `shot` | The screenshot's key in R2 (`<day>/<id>.png`, or `.webp` when PNG was too big). NULL without one, and after 90 days |
| `shot_token` | Server bookkeeping for the upload. Ignore |
| `reply` | A note to the player, written by hand (step 4) |

## Steps

1. **Find what's new:**

   ```sql
   SELECT f.id, datetime(f.received,'unixepoch') AS at, f.kind, f.app, f.page, f.text, length(f.errors) AS errlen, x.details, x.shot
   FROM feedback f LEFT JOIN feedback_extra x ON x.id = f.id WHERE f.status = 'new' ORDER BY f.received
   ```

   Read `errors` only for the report you're working on: `SELECT errors FROM feedback WHERE id = ?`. It's the whole
   error.log, so most of it is older than the report: start with the lines just before `at`. Repeated
   `sharing: upload … Today's upload limit is reached` lines are the server's daily cap working, not a bug. Leave
   `contact` out of what you select unless you need it.
2. **Look at the screenshot** when `shot` is set. From `server/`, in the Bash tool (it writes the raw image to stdout;
   PowerShell 5.1's `>` would corrupt it), into the scratchpad, outside the repo:

   ```sh
   set -a && . "$APPDATA/EQLPathfinder-dev/private.env" && set +a
   npx cf r2 objects get <shot> --bucket-name eqlpathfinder-feedback > <scratchpad>/report-<id>.png
   ```

   The first line loads the private settings, which `cf` needs even for R2. Without it, the PNG comes out empty.

   Then read the PNG. It's the page at the player's own pixels (at 150% scaling, 1.5× the window's size), so a
   one-pixel layout problem shows as it did for them. Blocks (█) and grey bars are what the app hid (the character's
   name, clock times, folders); black boxes are what the player blacked out, gold boxes what they pointed at. To see
   the page at their size and scaling, render it with [app-screenshots](../app-screenshots/SKILL.md).
3. **Check whether it's already fixed.** Compare their `app` version with `docs/releases/*.md`. A report from an old
   version may be fixed already. A fix can also be finished but not released yet, or its status write may have been
   blocked (step 5), so look in `docs/releases/next/` and `git log main --oneline` too.
4. **Fix it the usual way,** with the [feature](../feature/SKILL.md) skill: a worktree, a spec and a test that
   reproduces it, the fix, the review, finish. Whoever writes the test gets the report's id, its text, and the
   screenshot's path. Two things for a report:
   - a bug in the page (`src/EqlPathfinder.App/web`) has no test that runs it (docs/quality.md says what proves
     it). Try the behaviour itself in Chrome (the [app-screenshots](../app-screenshots/SKILL.md) skill, "Trying a
     page in Chrome");
   - the release note in `docs/releases/next/` is written for players, in your own words.
5. **Record the outcome**, so the next session skips it. The player sees it in Help → Your reports (new reads
   "Received", read "Seen", planned "Planned", done "Done", wontdo "Not planned"), with the reply under it:

   ```sql
   UPDATE feedback SET status = 'done' WHERE id = 7   -- or 'planned', 'wontdo', or 'read' (seen, no action)
   INSERT INTO feedback_extra (id, reply) VALUES (7, 'Fixed in 0.9.38. Thanks for the report!') ON CONFLICT (id) DO UPDATE SET reply = excluded.reply
   ```

   - **A fix, right after `finish`:** mark it `done` with the version it ships in, both in one query (above). The
     release follows within the hour. For the version, run `.\tools\ship.ps1 -DryRun` (it changes nothing, and
     waits while a finish or ship runs). Its line `Version 0.10.9 -> 0.10.10 (…)` gives the version: the number
     after the arrow. If it says `Nothing waiting`, the hour's release already took the fix. In that case,
     `git log main --oneline -1 --grep "^v[0-9]"` names the version. The reply is always "Fixed in <version>.
     Thanks for the report!", which needs no go-ahead and replaces any earlier reply.
   - **The version is a prediction.** If the release that ships it gets a different number, correct the reply.
     That happens when a `bump: minor` note finishes first, or the ship fails (homelab-alerts).
   - **Any other reply** is optional: short, for the player, and only with the user's go-ahead, since the player
     reads it.
   - Each triaged report gets a place in docs/queue.md: its own row, an existing row with its id in From, or Parked.
     Mark it `planned`. Reports not triaged yet wait in the Inbox there.
   - Ideas worth building go on the roadmap as `planned` (below).

   Auto mode can refuse this write, because it changes production. If it does, the write is the user's. Give
   them one line in bash, since the `!` prefix runs bash from the main checkout. The line goes into `server/`
   (where `npx cf` is this project's) and loads the private settings itself:

   ```sh
   ! cd server && set -a && . "$APPDATA/EQLPathfinder-dev/private.env" && set +a && npx cf d1 query "$EQLP_D1_PRODUCTION" --sql "UPDATE …"
   ```

   It should answer `"changes": <n>`. Say in your summary that the report still reads `new` until they run it.

## Roadmap votes

```sql
SELECT r.id, r.title, r.status, r.shipped, COUNT(v.voter) AS votes
FROM roadmap r LEFT JOIN votes v ON v.item = r.id GROUP BY r.id ORDER BY votes DESC
```

- The app's Help → What's coming shows these rows.
- docs/roadmap.md is the long version: keep the ids and statuses in step with the table.
- When an item ships: `UPDATE roadmap SET status='shipped', shipped='0.9.x' WHERE id='...'`.
