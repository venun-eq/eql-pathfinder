# Usage summaries keep the day only

**Why:** The app sends a usage summary with its day and no time, but it goes up hourly, and the server keeps the
second each one arrived (`app_usage.received`). That stored time shows when a copy was running, and "clock times stay
on this PC" is the project's promise. The source goes public on GitHub as soon as 11-25. The user, 11-24: "yes, store
only the day". Players see no change: the server keeps less about them, and the public schema shows it.
**Players:** everyone whose usage counts are on. Worst for a player who also shares: an arrival second could line a
usage id up with a sharing key's upload of the same session (`units.received`).

## Done when
- [x] The usage table has no time column: its columns are exactly id, day, app, channel and body - `keeps a summary's day and no time: its row is id, day, app, channel and body` (fails now: six columns)
- [x] A summary sent with `received`, a clock time and a timestamp stores none of them, and its row is those five fields - `keeps only the known fields, each checked` (fails now: the row has `received`)
- [x] Send, re-send and delete still work on the new table - `replaces a day sent again, and deletes a copy's days` (passes now; with the migration and the old INSERT it fails, seen 11-24)
- [x] The daily cleanup still prunes by day - `drops expired rows and keeps current ones` (fails now: its INSERT has no `received`, NOT NULL)
- [x] Staging and then production keep every row through the migration, with five columns: the counts and `PRAGMA table_info` before and after (Deploy, below)
- [x] A real copy's summary is stored and deleted on staging - the `usage` self-test against staging (Deploy, below)

## Edge cases
- **Rows already stored:** the migration drops the time from every row and keeps the rest. The tests run on an empty
  table, so staging's counts before and after prove it.
- **Old versions in the field:** every app version sends the same body as today and gets `{ ok: true }`. No version
  sends a time. Any field named `received` or `time` is dropped like any other unknown field.
- **The gap between migrate and deploy:** the old Worker's INSERT names a column that's gone, so a copy that sends
  in that gap gets a 500. `UsageClient` throws, the day stays pending (up to 14 days) and goes out at the next hourly
  round, within the per-address limit as today. Nothing is lost, and nothing can store a time in the gap.
  `npm run deploy` runs `npm test` first, so the gap lasts that long, about a minute.
- **The wrong order** (Worker first): the new INSERT fails on `received` NOT NULL with no default, and the old rows
  stay timed. Migration first, always.
- **The pipeline:** DROP COLUMN has no `IF EXISTS`, so 0010 can't be re-run, despite server/README.md's rule. That's
  safe: `d1 migrations apply` records 0010 and never runs it twice.
- **Something leaving the PC:** nothing new is sent. The test's times (14:23:05) are made up.

## Decisions
Copied from the architect's decisions for usage-choice, 11-24 (`docs/specs/usage-choice.md` on feature/usage-choice,
commit ce4212d, "The server's `received`: usage rows keep the day only"). Decided by the user, 11-24.
- **A branch of its own:** the app ships within the hour; the server goes by hand, staging first. Neither branch
  depends on the other.
- **The column goes:** `server/migrations/0010_usage_day_only.sql` is `ALTER TABLE app_usage DROP COLUMN received;`,
  and usage.ts's INSERT and ON CONFLICT stop naming it. The arrival day isn't kept either: nothing reads it (the cron
  prunes by `day`, no query in docs/usage.md uses it), and beside the player's local `day` it narrows the hour. It's
  dropped rather than zeroed, because a table with no time column can't store one, and the public schema shows it.
- **Why it matters beyond the wording:** `units.received` and `feedback.received` are to the second too. The arrival
  second is the one thing that could line a usage id up with a sharing key.
- **Rows already stored** lose the time and keep the rest. D1 Time Travel can restore old values for up to 30 days.
- **Order: migration, then the Worker, back to back.** The gap fails closed (Edge cases).
- **What the tests prove:** the five columns, the dropped fields, send/re-send/delete, and the cleanup. Staging proves
  that rows survive.
- **Fallback, if D1 refuses DROP COLUMN:** rebuild in the same migration: `CREATE TABLE app_usage_new (id TEXT NOT
  NULL, day TEXT NOT NULL, app TEXT, channel TEXT, body TEXT NOT NULL, PRIMARY KEY (id, day))`; `INSERT INTO
  app_usage_new SELECT id, day, app, channel, body FROM app_usage`; `DROP TABLE app_usage`; `ALTER TABLE app_usage_new
  RENAME TO app_usage`; `CREATE INDEX app_usage_day ON app_usage (day)`. qa, 11-24: workerd's D1 accepts DROP COLUMN
  (a scratch 0010 left the column test and the cleanup green), so the fallback is for staging only, if it refuses.
- **Words:** docs/usage.md's Server paragraph, and one sentence in site/privacy.html that the time a summary arrives
  isn't kept (the designer's words), deployed after production is migrated.

## Deploy (by hand, on the user's go)
1. Staging (`eqlpathfinder-staging`, the production-data skill): `SELECT COUNT(*) FROM app_usage`, and one row's `body`.
   Then `cf d1 migrations list` and `SELECT sql FROM sqlite_master WHERE tbl_name = 'app_usage'`: `migrate` applies every
   pending migration and `deploy` ships all of main's `server/`, so the user sees what else rides along before saying go,
   and an index made by hand on `received` (which would make the DROP fail) shows here.
2. `..\tools\private.ps1; npm run migrate:staging`, then, once migrate reports 0010 applied, `..\tools\private.ps1; npm run deploy:staging`.
3. Count again (the same), `PRAGMA table_info(app_usage)` (five columns), and the same row's `body` (unchanged).
4. The `usage` self-test (docs/usage.md) in an isolated copy with `EQLPATHFINDER_API=https://api-staging.eqlpathfinder.workers.dev`:
   first with `EQLPATHFINDER_USAGE_KEEP=1` (the row is there, five columns, no time), then without (deleted). `npm run staging:sleep`.
5. Production: steps 1 to 3 with `npm run migrate` and `npm run deploy`, before the launch post. Then `npm run deploy` in `site/`.
Staging: done 11-24, 0 rows before and 0 after (the self-test's own row came and went), five columns (id, day, app, channel, body), self-test stored and deleted.
Production: done 11-24 by the lead on the user's go. 51 rows from 9 copies before and after, the bodies' total length unchanged (23,058 characters), five columns (id, day, app, channel, body), no migration pending, `api.eqlpathfinder.com` answering. The site's privacy sentence deploys with usage-choice.

## Not doing
- Dropping or rounding `units.received` and `feedback.received`: other tables and other uses (the screenshot key, the
  upload budget). The usage id stays apart from them because no time is left beside it.
- Checking the free text fields (`app`, `os`, `updatedFrom`, `state` values, `errors[].at`) for a time. The app fills them
  with versions, setting words and code locations. A time there would be the app's bug.
- An app change: the app already sends only the day. No `src/` change, so no release note.

## Where
- `server/migrations/0010_usage_day_only.sql`: new, the DROP COLUMN.
- `server/src/usage.ts` `usage`: the INSERT and ON CONFLICT lose `received`, and the `nowS` import goes with it.
- `docs/usage.md` line 67, the Server paragraph: columns app, channel, body. Add that 0010 dropped `received`, and when.
- `site/privacy.html`, under "Anonymous usage counts" near "Never sent": the designer's one sentence.
  (Moved: the usage-choice branch writes this sentence, with the designer's review.)
- Read and unchanged: `server/src/index.ts` (the cron prunes by `day`), docs/usage.md's queries (all by `day`), the
  production-data and release-status skills (they point to those queries), the community job and tools (they never
  read `app_usage`), and `0002_app_usage.sql` (history).

## Reviews
- architect: (11-24) no must fix. Checked: the pragma test and `SELECT *` keys fail if the column comes back or the old INSERT returns, and the cleanup test's INSERT fails on the old schema; 055d32d only reformats; nothing else on the server names `received` for usage (the cron prunes by `day`, DELETE by `id`, the gap's 500 logs a stack with no id); DROP COLUMN is legal here (no index, view or trigger names it). Should fix, in Deploy: (1) step 1 lists pending migrations (`cf d1 migrations list`) and `SELECT sql FROM sqlite_master WHERE tbl_name = 'app_usage'`, because `npm run migrate` applies 0009 too and `npm run deploy` ships upload-budget's Worker if they aren't out yet, and a hand-made index on `received` would refuse the DROP; (2) step 2 deploys only once migrate reports 0010 applied, else the wrong-order case; (3) docs/usage.md still owes "when" 0010 ran, after production. Fixed by the lead on 11-24: the deploy steps list the pending migrations and the table's SQL first, and deploy waits for migrate to report 0010 applied. The date in docs/usage.md's Server paragraph goes in after production is migrated.
