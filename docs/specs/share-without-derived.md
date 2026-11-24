# Sharing sends facts only

**Draft, 11-18, not started** ([calc-audit](calc-audit.md)). `qa` writes the rest of the spec and the failing tests, then
the architect's Decisions. **Tier:** risky, 600k: it changes what's sent, and the job. **Waits for:** one-rate (the
job's active minutes come from the same code). **Unblocks:** calc-docs. Of the drafts, this one matters least now.
**Also pays** the stale shared units debt (docs/debt.md: a unit the app stops building stays counted on the server),
folded in by the user on 11-20: one branch, risky, about 900k (docs/queue.md).

**Why:** players see no change in the app. What a sharer sends gets smaller: a shared crawl stops carrying numbers
their copy of the app worked out (levels gained, active and idle minutes) beside the facts they come from. The
community numbers then never lean on an old app version's math. The privacy page is checked against what's sent.
**Players:** everyone who reads community numbers. The effect shrinks as old shares age out.

## The cause

docs/engineering.md, Modeling 1: "Store and share facts." A shared crawl still carries worked-out numbers
beside its facts: levels, active and idle minutes (`SharePayload`'s `CrawlDerived`). The hash leaves them out
(`ShareUnits.Make` hashes with `Derived = null`), which is right. But `CommunityBuilder.Recompute` falls back to them
for crawls with no `End`, from apps before schema 4. Those numbers are from that app's math, and they'll never be
redone. `StretchDerived` is used only when a stretch has no kills.

## Proposal

- Count what the pool still holds from before schema 4, through the production-data skill (D1). If it's a few, the job
  leaves them out.
- Stop sending `CrawlDerived` and `StretchDerived`, raise `ShareBuilder.Schema`, and drop the fallback in
  `CommunityBuilder.Recompute`.
- docs/usage.md and `site/privacy.html` are checked for what's sent (CLAUDE.md, privacy).

## Where

- `src/EqlPathfinder.Engine/Sharing/SharePayload.cs`, `ShareBuilder.cs`, `ShareUnits.cs`
- `src/EqlPathfinder.Engine/Community/CommunityBuilder.cs` (`Recompute`), `server/src/sharing.ts`, `tests/ShareTests`

## Moved here from one-rate (11-18, the user)

one-rate (standard) left `src/EqlPathfinder.Engine/Community/` alone, so two copies of a calculation stay until this branch:
`CommunityBuilder.Recompute`'s active minutes (for `SharedCrawl`, and the open-world stretch's minutes) should call
`ActiveTime.Minutes`/`ActiveTime.OpenWorldMinutes`, and `CommunityBuilder.Parts`' T5 valuation should call `Valuation.T5`.
Its DPS already divides by active seconds.
