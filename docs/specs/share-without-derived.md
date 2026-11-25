# Sharing sends facts only

**Spec and failing tests 11-25 (`qa`), from the 11-18 draft ([calc-audit](calc-audit.md)); not built yet.** **Tier:** risky,
about 900k in all: it changes what's sent, the job and the server. **Unblocks:** calc-docs, community-span-bound. Pays
three docs/debt.md rows: stale shared units (folded in by the user on 11-20), a shared unit whose id moves, and
sharing.ts's "invite-only" comment. **Order (the user, 11-25):** the Worker's withdraw route is deployed to staging,
then production, before `finish`; the app ships only after. Old apps never call it, so it's purely additive.

**Why:** players see no change in the app. What a sharer sends gets smaller: a shared crawl stops carrying numbers their
copy of the app worked out (levels gained, active and idle minutes) beside the facts they come from, so the community
numbers never lean on an old app's math. And a crawl or hunt the app stops sending (a re-read moved it, or it stopped
counting) leaves the community numbers instead of being counted twice for a year. The privacy page says what's sent.
**Players:** everyone who reads community numbers, the not-sharing player most (they have nothing of their own to weigh
them against). Worst for a sharer among few sharers, whose moved hunts are a large part of a place's numbers.

## The cause

docs/engineering.md, Modeling 1: "Store and share facts." `SharePayload`'s `CrawlDerived` and `StretchDerived` ride beside
the facts. The hash leaves them out (`ShareUnits.Make`), which is right, but `CommunityBuilder.Recompute` falls back to
them for crawls with no `End` (apps before schema 4) and stretches with no kills. Separately, the server has no way to
hear that an app no longer builds a unit, so it keeps it 365 days and the job counts it beside its replacement. And
one-rate (11-18, the user) left two copies of a calculation here: the job's active minutes and its T5 valuation.

**The pool, 11-25 (D1, read-only):** 10 crawls of schema 3 against 4,610 crawls and instances of schema 4, all from one
install now on 0.10.34 that holds 367 schema-4 units. So they're units its app no longer builds (a schema raise re-sends
every unit still built), and the job drops them. The same holds for 66 schema-3 stretches (two installs on 0.10.34
and 0.10.35) and 1 relevel: the first sync of this version withdraws all 77.

## What the player sees

The designer, 11-25. A light pass: no new layout, so no mock-up. Three places change words, and one number may move
once. Nothing in the app announces a withdrawal: it's housekeeping the player didn't ask for and can't act on (design.md,
"a line that only says all is well is noise"); the release note says it once.

**Settings → Sharing → See exactly what's sent, built** (`main-settings-share.png`). Same tiles, same layout. The
paragraph under the tiles (share.js:91, `.muted`, as now) today ends on a sentence that becomes false. After:

> Each crawl carries the facts it's made of: every kill (seconds into the crawl, XP %, solo or party, how long each kill took, level),
> crawl rewards, dings and deaths, damage per spell or ability (the game's names), the spells you began casting, and
> motes and fragments by tier. Hunts in the open world carry their kills the same way. The community numbers are worked
> out from these facts each time they're rebuilt, the same way for everyone's play, whichever version of Pathfinder sent it.

The JSON below it has no `"derived"` in a crawl or a stretch (a relevel keeps its hours: that's a fact the job doesn't
read, and the privacy page lists it). The sentence says nothing about "no worked-out numbers", on purpose: a relevel's
hours are still sent, and a sentence a player can disprove from the JSON beside it breaks rule 2.

**Settings → Sharing, on** (`main-settings-share-on.png`), the first sync after updating. Schema 5 re-sends every unit,
so the existing line reads "Sending 1,760 more…" (the largest install's count) for that sync (wording unchanged), then the tiles come back from
`/v1/me`. "crawls and instances shared" and "open-world stretches" may be a little lower than before the update (the
withdrawn units). No new line, chip or notice. Paused: nothing is withdrawn, nothing changes. Offline, an older Worker
(404), the half guard holding back, an isolated copy: nothing visible, the counts stay. A withdrawal refused for any
other reason shows in the card's existing error line (`.warn`), as an upload's refusal does, in the same words.
New to sharing, or not sharing: no change (the off card and its preview read the paragraph above).

**site/privacy.html, Sharing.** The intro paragraph stays. The table's "Sent" cell becomes five short paragraphs (`<p>`
in the cell, each opening with its bold label); "Never sent" stays word for word:

> **Each crawl or instance:** the zone and difficulty, and whether it was a crawl or an instance. Your levels at the start
> and end, the trio, race and reincarnation rank, and how Pathfinder knows the trio (a /who, the spells you cast, or
> carried over from before). How long it lasted, whether you completed it, whether the instance charge was refunded, and
> the minutes since the one before. Each kill's seconds into the crawl, XP, solo or grouped, how long each kill took and your level.
> Crawl rewards and dings, also as seconds into it. When you stepped out and came back, and when it ended. Deaths.
> Damage dealt (melee, spells, damage over time, pets and damage shields, and per spell or ability), time in fights, and
> damage taken. The spells you began casting, by name, and how many times. Motes and fragments by tier, and how many of
> them came from the Reward Chest. Whether it was during a bonus-XP event (those are left out of the community numbers).
>
> **Each hunt in the open world:** the zone, each kill as above, the trio and rank you had then, and whether it was
> during a bonus-XP event.
>
> **Each reincarnation:** whether it was a class or a race, and which one.
>
> **Each relevel:** the trio, race and rank, the levels it went from and to, whether it's finished, its hours played and
> its hours in the open world, and deaths.
>
> **With all of it:** the day and an order instead of times (and, if you share only play from now on, the day you
> started), the game server's name, and the version of Pathfinder that sent it.

The paragraph on Pause and delete gains one sentence, between its two:

> **Pause** stops sending and keeps what you shared. **Delete everything I shared** removes it all from the server at
> once. When Pathfinder stops counting something it shared (a crawl a re-read put together differently, say), it deletes
> that from the server at its next send. Shared rows are deleted after a year in any case.

The engineer checks each clause against `ThePreviewHoldsTheFactsButNoNameAndNoClockTime`'s payload, field by field, and
fixes the words where the JSON says otherwise (`Damage.BySource` is `DamageSource`'s five; `Hours`/`OpenWorldHours` are
`Journey.ActiveHours`/`FieldHours`). The page goes live by hand (`npm run deploy` in `site/`), the day the app ships.

**The release note** (`docs/releases/next/share-without-derived.md`, `reread: false`, `job: true`):

> Sharing sends only what happened in your play, and takes back what Pathfinder no longer counts.
>
> - A shared crawl or hunt no longer carries numbers your copy of Pathfinder worked out from it (levels gained, active
>   and idle minutes). The community numbers work those out from the facts, the same way for everyone's play.
> - When Pathfinder stops counting something you shared (a crawl a re-read put together differently, say), it deletes
>   it from the server at the next send, so it isn't counted twice. Settings → Sharing may show a few fewer crawls and
>   stretches after that.
> - The first send after updating sends everything you've shared once more, in the new form. Nothing is counted twice.
> - The privacy page now lists everything that's sent, including the spells you began casting, reincarnations and
>   relevels.

**Reused:** everything. **New:** nothing on screen but words.

## Done when

- [x] A shared crawl or hunt carries no `derived`, in its facts or its columns, and the schema is 5 - `WhatsSentCarriesTheFactsAndNoNumbersTheAppWorkedOut`, `ShareTests.UnitsCarryTheWholeUnitAndAHashThatOnlyChangesWithIt`
- [x] The job leaves out a crawl with no end time whatever it was sent with, and its log line says it's from before schema 4 - `ACrawlFromBeforeSchemaFourIsLeftOutWhateverItWasSentWith`
- [x] The job leaves out a hunt with no kills whatever it was sent with - `AHuntWithNoKillsIsLeftOutWhateverItWasSentWith`
- [x] The app withdraws the ids its ledger holds for a character and no longer builds, and only those - `AUnitTheAppNoLongerBuildsIsWithdrawnAndNothingElse`
- [x] A build with nothing in it, or that would withdraw more than half of what was sent, withdraws nothing - `NothingIsWithdrawnWhenTheBuildHoldsTooLittleToTrust`
- [x] The Worker drops the units a PC names, never another player's or a linked PC's, and refuses a bad call - `server/test/withdraw.test.ts`
- [x] A sync withdraws after it uploads, and a withdrawn id leaves the ledger only once the server answers - the share self-test (`EQLPATHFINDER_SELFTEST=share`) against staging prints the withdrawal
  - Ran 11-25 against staging (EQLPATHFINDER_API=https://api-staging.eqlpathfinder.workers.dev, isolated copy, fresh registration):
    `stale id deadbeefdeadbeefdeadbeefdeadbeef put in the ledger for 12955ba8b20c`;
    `after second sync: ... withdrawal=withdrew 0 of 1, held back 0` (the server held no such row, so 0 dropped; the call answered);
    `ledger still holds the stale id after the second sync: False`. First sync: `withdrew 0 of 0, held back 0`.
- [x] The job's active minutes and T5-eq call `ActiveTime.Minutes`/`OpenWorldMinutes` (or a seconds form of them) and `Valuation.T5`, the app's one copy - `CommunitySameRulesTests` and `CommunityTests` stay green (a refactor)
- [x] The withdraw route is live on staging, then production, before `finish` - the output of `npm run deploy` for each
  Staging 11-25 01:39: `npm run deploy:staging`, 65 of 65 tests, "Deploy complete", version dc519c07. Production 11-25: `npm run deploy`, 65 of 65 tests, "Deploy complete", version 0defb55b.
- [x] `site/privacy.html` lists what's sent: today it leaves out spells begun (by name, with counts), reincarnations and relevels - read against `ThePreviewHoldsTheFactsButNoNameAndNoClockTime`'s payload. docs/usage.md is about usage counts and names no shared field: no change
- [x] sharing.ts's new-players-per-connection comment says it's on with registration open (connection.ts's 11-23 wording) - the diff
- [x] The preview's paragraph reads as in "What the player sees", with no "derived" in it or in a crawl or stretch of the JSON below - seen in main-settings-share.png
- [x] The sharing card on, mid-send, reads as today (tiles, "Sending … more…", no new line) - seen in main-settings-share-on.png
- [x] site/privacy.html's "Sent" cell and its Pause and delete paragraph read as in "What the player sees", corrected only where the payload says otherwise - the diff
- [x] The release note reads as in "What the player sees" - the diff

## Edge cases

- **Old versions in the field:** old apps keep sending `derived` and never withdraw; the job reads neither. Their stale
  units stay until they update (the "keep the newest per `seq`" fallback is not done). The community file is unchanged.
- **Re-sends and limits:** schema 5 re-sends every unit once. The largest install holds about 1,760 units, under the
  3,000 a day; all of them together are about 7,600 of the 200,000 a day. A withdrawal of up to 200 ids counts as one
  request of the day, no units and no bytes.
- **A second copy:** an isolated copy never withdraws, as it never uploads. Linked PCs: the route drops only units this
  install sent last (`install`, not `player`), so a PC never withdraws what its linked PC still builds.
- **Starting and stopping:** the build runs behind any history import (`TrackerService.Query`), so a part-read store
  can't look like withdrawn play; the half guard covers a store that opened empty or a build gone wrong.
- **The rules change:** a rule that stops sharing a kind of play (a cap loadout) withdraws it, within the half guard.
- **Same name, different thing:** the ledger is per character code; a sync compares only the character it built.
- **Something leaving the PC:** a withdrawal sends the character code and unit ids only, both already sent.
- **The route isn't there yet** (a 404 from an older Worker): the ids stay in the ledger and go at the next sync; no error.

## Not doing

- Relevels' `Hours` and `OpenWorldHours`: worked out by the app too, but the job doesn't read relevels. calc-docs notes it.
- Deleting old `derived` columns on the server: the re-send overwrites them; old apps' stay until they update.

## Where

- `src/EqlPathfinder.Engine/Sharing/SharePayload.cs` (`CrawlDerived`, `StretchDerived`, `ShareBuilder.Schema`), `ShareUnits.cs`
  (`From`, `Withdrawn`: a stub now), `ShareClient.cs` (a withdraw call)
- `src/EqlPathfinder.Engine/Community/CommunityBuilder.cs`: `Visit`, both `Recompute`s, `Parts`; `Tracking/ActiveTime.cs`
  (a seconds form, if needed), `Tracking/Valuation.cs`
- `src/EqlPathfinder.App/Sharing.cs`: `SyncAsync`, `ShareLedger`; `App.SelfTests.cs`: `SelfTestShareAsync`
- `server/src/sharing.ts` (a withdraw handler, the comment at line 76), `server/src/index.ts` (the route; the tests call
  `POST /v1/units/withdraw` with `{character, ids}` and read `{withdrawn}`, which the architect may change). No migration
- `site/privacy.html` (Sharing), `docs/community-job.md` if it names the fallback
- Tests: `tests/EqlPathfinder.Engine.Tests/ShareWithoutDerivedTests.cs`, `ShareTests.cs`, `server/test/withdraw.test.ts`

## Decisions

The `architect`, 11-25, before code. The user answered yes to all three (11-25): the half guard, withdrawal as a real delete on the server, and shipping before the Reddit launch.

- **What's stored:** nothing new. The ledger keeps its shape (`Characters[code][id] = hash`), no table, no migration, no
  re-read (`reread: false`): what's read from the log doesn't change, only what's sent of it.
- **What's sent:** less. `CrawlDerived`, `StretchDerived` and the `Derived` members of `SharedCrawl` and `SharedStretch`
  are deleted, not nulled, so nothing can fall back to them again; `ShareUnit.Derived` (the column) stays for a relevel.
  `ShareBuilder.Schema` is 5. An old unit's facts still say `"derived"`: the reader ignores a member it doesn't know.
  `ShareTests`' assert that the hash ignores `Derived` goes with the member (it has no subject left); that edit is meant.
  One new call, the character code and unit ids, both already sent. In the same branch: `site/privacy.html`, and
  `web/js/share.js:91`, which tells players today that worked-out numbers "come along as derived" (the designer's pass).
- **The route:** qa's shape stays: `POST /v1/units/withdraw`, `{character, ids}` → `{withdrawn}`, signed like the rest.
  POST because the signature covers a body. 1 to 200 ids, each `^[0-9a-f]{16,40}$`, else 400 or 413; its body limit is
  `MAX_SMALL_BODY` (200 ids are about 9 KB, over `MAX_TINY_BODY`). One statement: `DELETE FROM units WHERE install = ?
  AND player = ? AND character = ? AND id IN (json_each)`. `install`, so a PC never drops what its linked PC sent last.
  A real delete, not a flag: `/v1/me` and the privacy page say what the server holds, and a withdrawn unit the app builds
  again has left the ledger, so the next sync sends it again. That's what makes a wrong withdrawal heal itself.
- **The app's sync:** upload, then withdraw, then `/v1/me`. Withdrawal runs only when every batch of this sync went up,
  and compares the ledger as it is after them. So the guard's "more than half" means the build holds fewer units than it
  lost: a re-read that moved every id is exactly half and goes through, a store that lost most of its play doesn't.
  The decision (`ShareUnits.Withdrawn`) is the engine's; `SyncAsync` only calls it. On any 200 every named id leaves
  the ledger, whatever `withdrawn` says (a unit another PC sent last will never be this PC's to drop; asking again
  changes nothing). 404 `not_found` or a transient failure: quiet, the ids stay, next sync. Anything else goes to `Run`
  as an upload's would. Ids go 200 a call, the ledger saved after each answer.
- **The spec's "the build runs behind any history import" is half true, and the build must know it.** `Query` waits for
  an import only before the loop starts (`BeforeLoop`), and then swallows a failed import: "the query reads what there
  is", after `_store.Reset`. Once the loop runs, `Step` runs queries before it looks at `_importing`. The app awaits the
  import before `Start`, so today only a failed or cancelled read gets through, and the half guard catches it only when
  over half is missing. So: the sync reads `Importing` and `NeedsRebuild` in the same `Query` as the payload and
  withdraws nothing when either is set. The engineer confirms what a failed import leaves, and qa adds the test.
- **A unit that leaves the build and comes back** (only closed visits are built, so a crawl rejoined after a sync) is
  withdrawn, then sent again when it closes: one delete and one upsert it would have made anyway. Accepted.
- **Who else reads it:** old apps keep sending `derived` and the Worker keeps taking it (validation and column stay).
  The job's current build reads a schema-5 unit correctly (`Derived?.`, and schema 5 always has `End` and kills), so the
  app and the job can arrive in either order; the note says `job: true` so the job drops its fallback. The job's
  reason for a crawl with no end is its own line ("from before schema 4"), counted in `LeftOut`. `pathfinder status
  --facts-check` compares `derived` with the job's math and has nothing left to compare: it's deleted, with its lines
  in docs/community-job.md and `.claude/skills/community-pool/SKILL.md`.
- **One formula:** `ActiveTime` gets a form that takes seconds from the start (kills, pauses, end), and the `DateTime`
  form converts and calls it: one body, not two with the same rule. The job calls `Valuation.T5` as it is.
- **The server:** no migration. Rows from before need nothing: `install` is there since 0001, and the schema-5 re-send
  overwrites `derived` with null. Additive: one route, one body-size line, one comment. Deploy by hand after the user
  says so, staging first, **from this worktree with main merged in first**, and `git diff main -- server/src` showing
  only those three, so the deploy can't take back a server change another branch finished meanwhile. Taking it back is
  deploying main's Worker: the app reads the 404 as "later".
- **How updates arrive:** untouched. An old, offline or half-updated copy never withdraws and its units stay as today.
- **Limits and cost:** schema 5 re-sends about 7,600 units once (200,000 a day; 1,760 for the largest install against
  3,000). A withdrawal is one row in `usage.requests`, no units and no bytes, and is refused with `daily_limit` once the
  install passed `MAX_REQUESTS_PER_DAY`, so a runaway copy costs 200 small deletes a day. The delete is by primary key.
- **What the guard leaves behind:** it fails closed. A character whose ledger holds more ids it no longer builds than
  ids it does (the ledger was never pruned, so moved ids have piled up since sharing began) withdraws nothing, every
  sync, silently; so does a PC that lost its ledger. Both are today's behaviour, not worse. The self-test prints
  "withdrew N of M, held back K", and a day after shipping the lead counts units under schema 5 per install
  (`community-pool`): what's left is an old app or one of these. The 77 is a floor: a schema-4 unit whose id moved
  since 11-17 looks live in the pool, so the first sync withdraws more.
- **If the user says no to the half guard:** `Withdrawn` keeps "nothing built, nothing withdrawn" only, the last assert
  of `NothingIsWithdrawnWhenTheBuildHoldsTooLittleToTrust` and its Done-when line go, and nothing is left behind above.
  The `Importing`/`NeedsRebuild` rule is then the only thing between a bad read and a withdrawal of real play (which
  the next good build sends again, against the 3,000 a day).
- **Taking it back:** a later version stops calling withdraw, and the route can go. Schema 5 can't be un-sent, and
  doesn't need to be: 6 re-sends again. What's gone for good is the old `derived` numbers and units no app builds.
- **Players see two things:** the count in Settings → Sharing may drop a little at the first sync, and the preview no
  longer shows `derived`. The release note says the first in a line.

## What a failed import leaves (engineer, 11-25)

Read from `ImportCoreAsync`: it resets the store and the checkpoint first, then sets an offset per file as each one ends; a throw or cancel
leaves `_importing` false and `NeedsRebuild` false (the new checkpoint has the current version). So `Importing || NeedsRebuild` alone
would pass a read that failed before its first file ended (`HasHistory` false, nothing built). The rule is therefore also `!HasHistory`:
`TrackerService.ShareRead` returns the payload with `Settled`. A read that threw or was cancelled after one file ended used to look settled
and hold part of the play (the review, 11-25): `_importFailed` is set when a read starts and cleared only when it completes, and
`ShareRead` is not settled while it is set (`ShareReadTests.AReadCancelledAfterItsFirstFileIsNotSettled`). The half guard stays behind it.

## Reviews

- qa: spec and failing tests (cbd7b36); counted 10 schema-3 crawls, 66 stretches and 1 relevel in the pool, all stale.
- architect (decide, Fable): the import gate, schema 5 by deletion, the withdraw route, upload-then-withdraw, deploy order (add39b3). The user said yes to the half guard, a real delete and shipping before the Reddit launch.
- designer (design): the wording for share.js, privacy.html and the release note (77e1a7b).
- architect: (review) must fix, a read failing after its first file looked settled; fixed with `_importFailed` and `AReadCancelledAfterItsFirstFileIsNotSettled`. Must fix, `negRankStretch` no longer reached the rank check; it keeps a kill. Should fix, the preview test compares active minutes with `ActiveTime.Minutes` again. All fixed in cabe4d5 and 3c5e8ed; the lead read the import fix.
- designer: (review) must fix, privacy's date; should fix, "how long each kill took" and the release note's parenthetical; the CSS idea taken. All fixed in 3c5e8ed. The two "See exactly what's sent" went to docs/debt.md.
