# Community API

The Cloudflare Worker behind sharing, usage counts, feedback and the roadmap: TypeScript in `src/`, one D1 database
and one R2 bucket (the screenshots players attach to reports), configured in `cloudflare.config.ts` and deployed with
Cloudflare's `cf` CLI. The databases' IDs aren't in this repository: a deploy or a migration reads its own from the
environment (`database-ids.ts`), and the tests need none. The heavy math (the community numbers) is
the app's own C# on the NAS, never here: the free plan allows about 10 ms of CPU per request.

| Path | What |
|---|---|
| `src/index.ts` | Routes and the daily cleanup |
| `src/sharing.ts` | Register, upload units, what the server holds, delete, link PCs, invite codes |
| `src/auth.ts` | Request signatures (each install's ECDSA P-256 key), per-address limits, one-time codes |
| `src/connection.ts` | Brakes on made-up players: one character per game server per connection, new players per connection |
| `src/usage.ts`, `src/feedback.ts` | Usage counts (docs/usage.md); feedback, report screenshots, Your reports and roadmap votes |
| `migrations/` | The database schema, one numbered file per change. Every statement is idempotent |
| `test/` | Vitest tests that run inside workerd against a local database built from `migrations/` |

Every value from a request reaches SQL only through `.bind(...)`; the SQL text is always fixed.

## Working on it

Needs Node.js 22.18 or later. In this folder:

```sh
npm install          # once (esbuild and workerd install scripts are approved in package.json)
npm test             # the Worker in workerd, against a fresh local database per test
npm run typecheck    # writes the Worker's types (.cloudflare/types) first, so it works in a fresh checkout
```

Neither the tests nor the typecheck needs a database's ID or a login: both read the configuration in mode `test`, which
has a local database only.

To try the app against a Worker on this PC (its own local database and bucket under `.cloudflare/state`; nothing
reaches staging or production): apply the migrations locally, start it, and point an isolated copy of the app at it.

```sh
export EQLP_D1_STAGING=00000000-0000-4000-8000-000000000000   # any made-up ID will do: --local never leaves this PC
npm run migrate:staging -- --local
npx cf dev --mode staging -- --port 8799 --strictPort
# then run the app with EQLPATHFINDER_API=http://localhost:8799 and EQLPATHFINDER_DATA=<a copy> (app-screenshots skill)
```

The dev server prints its Local Explorer address, which reads the local database and bucket.

## Changing the database

Add the next numbered file to `migrations/` (`0005_something.sql`), written so it can run on a database that already has
it (`IF NOT EXISTS`, `OR IGNORE`). The tests pick it up at once. Then staging, then production (PowerShell):

```powershell
..\tools\private.ps1; npm run migrate:staging
..\tools\private.ps1; npm run migrate
```

`migrate.mjs` asks `database-ids.ts` which database, as a deploy does: `EQLP_D1_STAGING` or `EQLP_D1_PRODUCTION`. It
stops, naming the variable, when that one is unset or blank. `tools\private.ps1` loads this PC's private settings into
the PowerShell that ran it (docs/pipeline.md, "Private settings"), and nothing after the `;` runs when one is missing.
A CI service sets the two names as secret variables instead.

## Deploying

Sign in once with `npx cf auth login`. Try every change on staging first (its own Worker and database, enrollment off, so
`pathfinder share-smoke --api https://api-staging.eqlpathfinder.workers.dev --rules` can run end to end):

```powershell
..\tools\private.ps1; npm run deploy:staging
# run share-smoke / probes against staging, then put its workers.dev route back to sleep:
..\tools\private.ps1; npm run staging:sleep
..\tools\private.ps1; npm run deploy        # production: api.eqlpathfinder.com only (workers.dev is off)
```

`IP_SECRET` is a Worker secret (it makes the daily connection codes), set once per environment and kept across deploys;
it is never in the source or the database.

`SHOTS` is the R2 bucket for report screenshots, one per environment (`eqlpathfinder-feedback-staging`,
`eqlpathfinder-feedback`), created once with `npx cf r2 buckets create --name <bucket>` before the first deploy that
binds it. It has no public address. The daily cleanup deletes screenshots after 90 days; at most 50 are taken a day across
all players (config `max_shots_per_day`) and 5 a day per connection (config `max_shots_per_ip`, using the same 2-day
hashed connection code as sharing), and a transient R2 write error restores the report's one-time upload token so a retry
can still attach the screenshot. Feedback reports are capped at 10/day per connection (`max_feedback_per_ip`, `"0"` = off)
and 1,000/day across all players (`max_feedback_per_day`, `"0"` = off); roadmap votes are capped at 50/day per connection
(`max_votes_per_ip`, `"0"` = off, while removing a vote is always allowed). Shared unit `facts` must be valid base64 gzip
bytes (`<= 128 KiB` base64 per row), uploads are capped per install at 200 requests/day, 3,000 changed units/day, and
16 MiB/day of stored facts + derived JSON (plus global fuses `max_upload_bytes_per_day` = 200 MiB and `max_units_per_day` =
200,000; `"0"` = off), and all routes enforce per-route body caps and in-memory per-address rate limits (`CODE_GUESSES`
10/min, `API_LIMIT` 60/min). The app-feedback skill says how to look at a screenshot. The operator's switches
(enrollment, the fake-player brakes, minimum app) are rows in the `config` table: see docs/community-job.md.
