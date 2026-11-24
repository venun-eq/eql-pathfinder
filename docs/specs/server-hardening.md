# Worker hardening before going public

**Why:** Once `server/` is public on GitHub, anyone can read the exact endpoints and schemas and probe the live
Cloudflare Worker (`api.eqlpathfinder.com`). Today `POST /v1/summaries` throws 500 on invalid base64 `facts`, stores
non-gzip blobs without checking, and lets a registered install re-send identical units indefinitely without hitting a
daily request cap; `POST /v1/feedback` lets one IP exhaust the global 50/day screenshot cap in minutes; `PUT /v1/feedback/:id/shot`
has a check-then-update token race; `POST /v1/register` skips `min_app` if `app` is omitted; and several routes accept
6 MiB bodies or lack per-IP rate limiting.
**Players:** everyone. Legitimate apps (including a 30-batch initial history sync) see no change; abusive callers can no
longer exhaust screenshot slots for other players or burn Worker/D1 quotas.

## What the player sees
`site/privacy.html`'s "Your internet address" section notes that the same 2-day hashed connection code used for sharing
also caps how many report screenshots one connection can attach in a day, and updates the date at the top.

## Done when
- [x] `POST /v1/summaries` validates `facts` as base64 gzip bytes (400 `bad_field`, never 500), rejects control characters in `app`/`server`/`zone`/`trio`/`race`, rejects invalid/future calendar `day` values (`> today + 2 days`), and enforces `min_app`, while keeping `content_hash` compatible with existing rows - `server/test/hardening.test.ts`
- [x] `POST /v1/summaries` enforces `MAX_REQUESTS_PER_DAY` (200) per install even on unchanged re-sends - `server/test/hardening.test.ts`
- [x] `POST /v1/register` enforces `min_app` even when `app` is omitted, and missing/empty `X-EQLP-Time` returns `401 unsigned` - `server/test/hardening.test.ts`
- [x] `POST /v1/feedback` enforces a per-connection daily screenshot cap (`shot_ips`, migration `0007_shot_ips.sql`, default 5, config `max_shots_per_ip`), pruned after 2 days by `scheduled()`, without blocking report text or other connections, and `connectionCode()` uses Web Crypto HMAC-SHA256 - `server/test/hardening.test.ts`, `server/test/connection.test.ts`
- [x] `PUT /v1/feedback/:id/shot` claims `shot_token` atomically in D1 so concurrent uploads with the same token produce only one winner - `server/test/hardening.test.ts`
- [x] Stream-capped per-route body limits (4–64 KiB on small routes, 1.5 MB on screenshots, 6 MB on summaries), `API_LIMIT` (60/min/IP on sharing, registration and read routes), HTTP `Retry-After` on rate/daily-limit errors, `x-content-type-options: nosniff`, `referrer-policy: no-referrer`, and D1 indexes (`0008_indexes.sql`) are enforced - `server/test/hardening.test.ts`
- [x] `site/privacy.html` and `server/README.md` match the implementation, and both `server/` tests and `site/` build pass - `tools/check.ps1`

## Edge cases
- **First-time sharing sync (3,000 units = 30 consecutive 100-unit batches):** `API_LIMIT` is 60/min (separate from `CODE_GUESSES`'s 10/min) and `MAX_REQUESTS_PER_DAY` is 200, so real full-history syncs never trip either limit.
- **Unchanged re-sends at the unit limit:** a player at 3,000 units/day with `< 200` requests can still re-send identical units cleanly (`sharing.test.ts`).
- **Existing `content_hash` in production D1:** `contentHash` still hashes the base64 `facts` string (excluding `factsBytes`), so deployed rows keep matching without triggering re-writes.
- **Invalid screenshot image or 413 on `PUT /v1/feedback/:id/shot`:** validated before the atomic token claim, so a rejected image does not burn a valid token (`reports.test.ts`).
- **Bare local dev run (`IP_SECRET` unset, rate-limit bindings absent):** `shotLimit` and `limitApi` fail open gracefully, matching `newPlayerLimit` and `limitPerAddress`.

## Decisions
- **Migrations `0007_shot_ips.sql` and `0008_indexes.sql`:** both idempotent (`IF NOT EXISTS`). `shot_ips` mirrors `0003_register_ips.sql` (`code`, `day`, `n`), pruned after 2 days by `scheduled()`; `0008_indexes.sql` adds `units_day`, `units_install`, `feedback_received`, `codes_player`, and `codes_expires` to eliminate full table scans on pruning and quota queries. Deploy order (by hand): `npm run migrate:staging`, `npm run deploy:staging`, verify, then `npm run migrate`, `npm run deploy`, and `site/` `npm run deploy`.
- **HMAC-SHA256 for `connectionCode()`:** replaces `SHA-256(prefix || secret || day || ip)` with standard Web Crypto `HMAC-SHA256(IP_SECRET, "eqlp-ip:" + day + ":" + ip)` (truncated to 32 hex). Any same-day rows in `register_ips`/`upload_ips` reset once at deploy cutover and prune after 2 days.
- **Separate `API_LIMIT` binding (60/min, namespaces 1003 prod / 1004 staging):** keeps code-guess/feedback/usage throttling tight at 10/min while allowing legitimate 30-batch initial history uploads and normal Help/roadmap reads.
- **No app change:** nothing under `src/` changes, no new client data leaves the PC, and existing installed apps work unchanged (they already send `app`, valid gzip `facts`, and handle `shot: "limit"` and `429`).
- **Taking it back:** revert the Worker deploy and `site/privacy.html`; `shot_ips` is harmless if left behind, and `max_shots_per_ip = "0"` disables the per-connection screenshot cap live without a deploy.

## Not doing
- Changes to `src/` (CommunityJob hardening is on `feature/community-job-hardening`).
- Adding an auth header to the staging Worker (keep staging gated via Cloudflare Access or disabled when idle).

## Where
- `server/migrations/0007_shot_ips.sql`, `server/migrations/0008_indexes.sql`, `server/test/apply-migrations.ts`
- `server/cloudflare.config.ts`, `server/src/env.ts`, `server/src/auth.ts`, `server/src/http.ts`, `server/src/index.ts`, `server/src/connection.ts`, `server/src/sharing.ts`, `server/src/feedback.ts`, `server/src/usage.ts`, `server/src/util.ts`
- `server/test/hardening.test.ts`
- `site/privacy.html`, `server/README.md`

## Reviews
- architect: checked migrations `0007_shot_ips.sql` and `0008_indexes.sql` idempotency, streaming body cap cancellation (no OOM on chunked uploads), HMAC-SHA256 `connectionCode`, D1 `UPDATE ... RETURNING` atomicity for `shot_token`, calendar + future-date bounds on `rows[i].day`, `min_app` enforcement on both register and summaries, strict `X-EQLP-Time` parsing, HTTP `Retry-After` and `referrer-policy` headers, backward compatibility of `contentHash`, and rate-limit coverage across every D1-touching route; no new data stored beyond the 2-day hashed connection code.
- designer: checked `site/privacy.html` wording and date — matches the existing plain-spoken player tone, clearly states why the 2-day hashed connection code covers both sharing and report screenshots without technical jargon, and builds cleanly in `site/`.
