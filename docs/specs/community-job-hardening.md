# Community job hardening against untrusted rows

**Why:** Players see no change when everything is well-behaved. Once the repository is public, anyone can read the
sharing API's schema and the NAS job's parser, and upload rows to `POST /v1/summaries` whose `facts` blob is never
decompressed on the Worker. Today a single gzip bomb, corrupt blob, or partial JSON object in `units` crashes the
entire community job and stops `community/v1.json` from updating for all players; quarantined rows are not excluded
from the query; and a Cloudflare API error writes the account and database IDs into the failure message and alert.
**Players:** everyone who uses Ask's community numbers. Worst for all players: one hostile or broken row halts updates
until the operator intervenes by hand.

## Done when
- [x] `CommunityJob.Unhex` (and `ShareUnits.Unzip`) caps uncompressed bytes at `MaxFactsBytes` (4 MiB by default) and compressed input at `MaxCompressedFactsBytes` (1 MiB), throwing `InvalidDataException` before allocating unbounded memory - `UnhexRejectsGzipBombsAndOversizedPayloads`
- [x] `CommunityJob.BuildAsync` isolates bad rows per unit: invalid hex, non-gzip bytes, truncated gzip, oversized gzip bombs, malformed JSON, and JSON `null` are skipped and counted in `Stats`, while valid rows in the same and later pages still publish - `BadRowsAreSkippedAndValidRowsStillPublish`
- [x] `CommunityJob.BuildAsync` excludes `u.quarantined = 0` in its D1 query - `QuarantinedUnitsAreExcludedFromTheQuery`
- [x] `CommunityJob.BuildAsync` refuses a stalled D1 page whose last row ID does not advance past the cursor instead of looping forever - `AStalledD1PageStopsInsteadOfLoopingForever`
- [x] `CommunityJob` API errors name the operation (`D1 query`, `R2 upload`) and never embed the Cloudflare account ID or D1 database ID in exception messages - `CloudflareErrorsNeverRevealAccountOrDatabaseIds`
- [x] `CommunityBuilder.Visit`, `CommunityBuilder.Hunt`, and `CommunityBuilder.Recompute` handle partial or hostile deserialized JSON (`{}`, null collections/nested objects, negative/huge rank or difficulty, `NaN`/`Infinity`, blank/overlong/control-char zone, overflowing fragment counts and DDD damage) without throwing and drop invalid rows cleanly - `MalformedOrHostileSharedUnitsAreLeftOutWithoutCrashing`

## Edge cases
- **A gzip bomb inside a small compressed blob:** e.g., a few kilobytes of gzipped zeroes expanding past 4 MiB stops reading after `maxBytes + 1` bytes and throws `InvalidDataException` rather than OOMing.
- **A valid crawl right at the cap vs. one byte over:** `Unhex(hex, maxBytes: N)` succeeds for exactly `N` UTF-8 bytes and throws for `N + 1`.
- **Null nested fields from `{}` or partial JSON:** `SharedCrawl` / `SharedStretch` records have non-nullable C# types that `System.Text.Json` leaves `null` when omitted; every dereference (`Kills`, `Rewards`, `Deaths`, `Damage`, `Damage.ByAbility`, `Loot`, `Loot.Motes`, `Loot.Fragments`, `Derived`, `Pauses` elements) must tolerate `null`.
- **Negative rank or `NaN` / `Infinity`:** `Rank = -10` would make `XpMultiplier = 0` (dividing by zero), and `NaN` bypasses `< 0 || > 600` range checks; both must be rejected.
- **Valid older schema 2/3 rows:** crawls without `Pauses`, `End`, `TrioSource`, or `Casts`, and stretches without `Trio`/`Rank`, must continue to read and count unchanged.
- **Quarantined rows:** `units.quarantined` is already in `0001_initial.sql` (default `0`) and reset on upsert; adding `AND u.quarantined = 0` requires no migration and works on every existing database.
- **Bad row mixed with good rows on the same page:** cursor (`after`) still advances from the page's last row ID even if that row's facts were unreadable.

## Decisions
- **Bounds (`MaxCompressedFactsBytes = 1 MiB`, `MaxFactsBytes = 4 MiB`):** The Worker accepts up to 1 MiB of base64 `facts` (~768 KiB compressed), and real crawls are typically 2–50 KiB uncompressed. 1 MiB compressed / 4 MiB uncompressed gives wide headroom for legitimate marathon crawls while bounding per-row memory strictly. Both `CommunityJob.Unhex` and `ShareUnits.Unzip` share the same bounded decompression helper so neither path is unbounded.
- **Quarantine filter (`u.quarantined = 0`):** No schema migration is needed (`0001_initial.sql` already defines `quarantined INTEGER NOT NULL DEFAULT 0`). Existing rows have `0` unless quarantined by hand.
- **Per-row isolation and stats:** Unreadable rows are skipped and counted in `CommunityStats` under `"facts too large"` or `"unreadable facts"`, so the NAS log line makes hostile or broken uploads visible without stopping `community/v1.json` for healthy players.
- **Redacted Cloudflare errors:** `SendAsync` labels failures by operation (`"D1 query"` or `"R2 upload"`) rather than `RequestUri.AbsolutePath`, keeping `AccountId` and `Database` out of container logs and `homelab-alerts` notifications.
- **What's stored, what's sent, older apps, and taking it back:** Nothing stored on PCs or sent over the wire changes, and `community/v1.json`'s schema is unchanged. Older apps are unaffected. The release note sets `job: true` so `ship.ps1` deploys the hardened CLI to the NAS. A revert restores the previous behavior cleanly.

## Not doing
- Worker-side rate limits, base64/gzip validation in `server/src/sharing.ts`, and staging Worker access (separate server branch so Worker deploys stay independent).
- Changing `RouteAnalysis.FarmPatch` timezone handling in `CommunityBuilder` (tracked in `docs/debt.md` under `invariant-dates`).

## Where
- `src/EqlPathfinder.Engine/Community/CommunityJob.cs`: `MaxCompressedFactsBytes`, `MaxFactsBytes`, `Unhex`, `Gunzip`, `BuildAsync`, `RulesAsync`, `SendAsync`
- `src/EqlPathfinder.Engine/Community/CommunityBuilder.cs`: `Visit`, `Hunt`, `Recompute`, `Parts`, `DddShare`, `PartyShare`, `KnownXpKills`, `FirstLevel`, `ValidZone`
- `src/EqlPathfinder.Engine/Sharing/ShareUnits.cs`: `Unzip` (shares bounded decompression)
- `tests/EqlPathfinder.Engine.Tests/CommunityJobTests.cs`, `tests/EqlPathfinder.Engine.Tests/CommunityTests.cs`
- `docs/community-job.md`, `docs/releases/next/community-job-hardening.md`

## Reviews
- architect: 11-23: checked all six requirements and edge cases. Fixed during review: `EndOfStreamException` on truncated gzip streams caught per row in `BuildAsync`; `OverflowException` prevented in `CommunityBuilder.Visit` (fragment count sum widened to `long` and checked after `t5` bound) and `DddShare` (summed as `double`); `ArgumentNullException` guards on `Unhex`/`Unzip`; `id` JSON ValueKind checked before cursor comparison; `Redact` strips account/database/token from Cloudflare error bodies too. Full `tools\check.ps1` green (914/914 engine+page tests, nothing private, zero new warnings).
