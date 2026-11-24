# Usage counts

Pathfinder sends anonymous usage counts so we can see installs, versions in use, crashes and which pages earn their
place. **Off until the player says yes**: the welcome tour's second step asks, with neither answer chosen (docs/specs/usage-choice.md), and nothing is counted for sending or sent before then. Copies installed before the question keep the setting they had (on by default then). The switch is in Settings → Character and data → "Send anonymous usage counts"; the same card has
"See exactly what's sent", which shows today's summary as it would go now. Those copies get a one-time banner across the top of Home (`web/js/notices.js`, id `usage-counts`:
See what's sent / Turn off / OK; finishing the tour marks it seen).

## What is sent

**Distinct ids count a player who says no and later yes twice** (a new id and a new `firstSeen`, with a `life.seen`).

**Installs and the setup funnel are a floor.** Only copies whose player said yes are counted, so `life.firstrun`, `life.setupquit` and the `setup-*` steps undercount the real installs and drop-offs.

One summary per copy per day (POST `/v1/usage`): one row per day, never finer, so nothing says when someone plays. Today's
summary is sent again whenever it has changed (2 minutes after start, every hour, and on closing with a 3-second cap), and the
server replaces that day's row.

| Field | What it is |
|---|---|
| `id` | A random 32-hex id made when this copy starts counting (`AppSettings.UsageId`); switching off retires it and a later Yes makes a new one. It isn't the sharing key's id or the roadmap voter id, so usage can't be joined to shared crawls or feedback. |
| `day` | The player's local day. |
| `app`, `channel` | App version, and stable or beta. |
| `os` | Windows version (`10.0.26300.0`). |
| `firstSeen` | The day this copy first counted. |
| `updatedFrom` | The version it updated from that day, if it did. |
| `counts` | Counters by key (below). |
| `stats` | History reads: `import.seconds` and `import.mb` as count, sum and max. |
| `state` | What was on at the day's last check, as words: sharing (on/paused/off), beta, overlays shown, start window, goal, whether it can update itself. |
| `errors` | Up to 20 per day: what Pathfinder was doing (its own label, cut before anything that could be a path or address), the exception type, the first frame inside Pathfinder as `Type.Method`, and a count. |

**Never sent:** log lines, character or server names, play numbers, file paths, exception messages (they can hold paths
or names), clock times beyond the day. The Worker keeps no IP address; the rate limiter sees it only in memory.

### Counter keys

| Key | Counts |
|---|---|
| `page.<page>` | Main-window pages opened (home, ask, campaign, classroles, roster, crawls, loot, relevels, upgrade, help, settings; "plan" until 0.9.35, when Plan → Next trio was removed). |
| `ask.<q>.<pool>` | Ask questions that changed (level, farm, diff, dmg, places) and whether the shared numbers answered: `pool`, `nopool` (not enough players there, or none loaded), `na` (Every place doesn't use them). |
| `feature.<name>` | `share-on`, `share-preview`, `usage-preview`, `vote`, `feedback` (sent from Help), `report` and `report-shot` (sent from Report a problem, without and with its screenshot), `arrange`, `tour`, `campaign-build`, `campaign-edit`, `checkupdates`, `autoupdate` (an update installed itself), `changecharacter`, `opendata`; setup and tour steps reached: `setup-<step>`, `change-<step>`, `tour-<step>` (welcome, usage, log, reinc, overlay, goal, share, ready; `…-usage` only when counts aren't off) and `…-done`; one-time notices answered: `notice-<id>-ok`, `-see` or `-off`. |
| `life.<event>` | `start`; `firstrun` (setup finished on this launch); `setupquit` (setup closed before the end, sent with the next start, only by a copy whose player said yes); `updated`; `seen` (a copy from before usage counts reporting the first time). |
| `minutes.game`, `minutes.nogame` | Minutes Pathfinder ran, with and without EverQuest Legends running. |
| `error.more`, `more` | Errors past the 20 kept, counter keys past 150. |

## How it works

- **Instruments:** `Engine/Usage/UsageMetrics.cs`, on .NET's `System.Diagnostics.Metrics` (meter `EqlPathfinder`):
  `pathfinder.pages`, `pathfinder.ask`, `pathfinder.features`, `pathfinder.lifecycle`, `pathfinder.minutes`,
  `pathfinder.errors` (counters), `pathfinder.import.duration` (s) and `pathfinder.import.size` (MB) (histograms).
  OpenTelemetry reads these as they are: an exporter later is `AddMeter("EqlPathfinder")`. Everything is scrubbed when
  recorded, before any listener sees it.
- **Recorder:** `Engine/Usage/UsageRecorder.cs` listens to that meter, adds up per local day, and keeps days in
  `usage.json` in the data folder until the server has them. A finished day is dropped once sent; days older than 14
  unsent are dropped unsent.
- **App:** `App/Usage.cs` listens from the moment settings load (so a first run's setup steps count), then counts minutes
  and sends 2 minutes after start, every hour (only days that changed) and on closing (at most 3 seconds, so closing never
  hangs). Errors are counted from `AppSettings.LogError`.
- **Development copies never send:** a development build or an isolated copy (`EQLPATHFINDER_DATA`) counts and previews
  but sends nothing unless `EQLPATHFINDER_USAGE=1`. The self-test (`EQLPATHFINDER_SELFTEST=usage` in an isolated copy whose
  copied settings.json has the Share* fields removed) opens pages and an Ask question, previews, sends with
  `EQLPATHFINDER_USAGE=1`, then switches off so the server deletes the row; `EQLPATHFINDER_USAGE_KEEP=1` stops before
  that to look at the row in D1.
- **Switching it off** stops counting, deletes `usage.json` and asks the server to delete every row with this id
  (DELETE `/v1/usage`). In the same save the id moves from `UsageId` to `UsageForget` (settings.json), so an off copy has
  no id. Any 2xx clears it from the list, `deleted: 0` included; anything else keeps it. It is asked again 2.5 minutes
  after every start and every hour while the app runs, off copies too, and a copy switched off under an older build that
  still holds an id has it asked once more. A later Yes counts under a new id. An entry that isn't 32 lowercase hex is
  dropped without a request.

## Server

Worker routes POST and DELETE `/v1/usage` (no key; rate-limited per address with the `CODE_GUESSES` limiter, key
`use:<ip>`). The Worker keeps only the fields above, each checked and cut to size, and drops anything else. D1 table
`app_usage` (`server/migrations/0002_app_usage.sql`): primary key (id, day), columns app, channel, body (the
cleaned JSON; `0010_usage_day_only.sql` dropped the `received` arrival time, so no time is kept). The daily cron deletes rows older than 400 days. `0010` ran on production on 11-24. Deployed 11-17 (Worker etag 69546168…, source = commit
ac9f648 `server/worker.js`, since ported to TypeScript in `server/src/`); end-to-end checked the same day: send, stored as sent, delete from the app switch.

## Questions and their queries

Run in D1 (Cloudflare MCP `execute`, database `eqlpathfinder`).

```sql
-- Copies active per day
SELECT day, COUNT(*) AS copies FROM app_usage GROUP BY day ORDER BY day DESC LIMIT 30;

-- New installs (setup finished) per day, and setups closed before the end
SELECT day,
  SUM(json_extract(body, '$.counts."life.firstrun"') > 0) AS installs,
  SUM(json_extract(body, '$.counts."life.setupquit"') > 0) AS setup_quit
FROM app_usage GROUP BY day ORDER BY day DESC LIMIT 30;

-- Versions in use: each copy's latest day in the last 14
SELECT app, COUNT(*) AS copies FROM app_usage a
WHERE day = (SELECT MAX(day) FROM app_usage b WHERE b.id = a.id) AND day >= date('now', '-14 days')
GROUP BY app ORDER BY copies DESC;

-- Setup funnel: copies that reached each step
SELECT c.key, COUNT(DISTINCT a.id) AS copies FROM app_usage a, json_each(a.body, '$.counts') c
WHERE c.key LIKE 'feature.setup-%' OR c.key IN ('life.firstrun', 'life.setupquit', 'feature.share-on')
GROUP BY c.key ORDER BY copies DESC;

-- Ask: how often the shared numbers answered (last 7 days)
SELECT c.key, SUM(c.value) AS asked FROM app_usage a, json_each(a.body, '$.counts') c
WHERE c.key LIKE 'ask.%' AND a.day >= date('now', '-7 days') GROUP BY c.key ORDER BY c.key;

-- Pages opened (last 7 days)
SELECT c.key, SUM(c.value) AS opened, COUNT(DISTINCT a.id) AS copies FROM app_usage a, json_each(a.body, '$.counts') c
WHERE c.key LIKE 'page.%' AND a.day >= date('now', '-7 days') GROUP BY c.key ORDER BY opened DESC;

-- Errors by version
SELECT a.app, json_extract(e.value, '$.kind') AS kind, json_extract(e.value, '$.type') AS type, json_extract(e.value, '$.at') AS at,
  SUM(json_extract(e.value, '$.n')) AS n, COUNT(DISTINCT a.id) AS copies
FROM app_usage a, json_each(a.body, '$.errors') e GROUP BY 1, 2, 3, 4 ORDER BY n DESC LIMIT 50;

-- Still using it a week later: copies first seen 7–14 days ago that were active in the last 3 days
SELECT COUNT(DISTINCT id) FROM app_usage
WHERE json_extract(body, '$.firstSeen') BETWEEN date('now', '-14 days') AND date('now', '-7 days') AND day >= date('now', '-3 days');
```

Downloads aren't counted yet: r2.dev keeps no logs. Once the installer moves to our own domain (1.0 plan step 2), serve
it through the Worker or a custom domain to count downloads per version.
