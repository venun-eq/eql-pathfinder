# Calculation audit: one formula per number, facts kept airtight

**Audit, 11-18 (v0.10.12). Not a change of its own:** this page is the index of the drafts it led to. Each draft is
solved in its own branch and session, in the order below, by the tier it names. **Followed by**
[numbers-audit](numbers-audit.md) (11-19): the check of these fixes against real play, and the drafts it led to.

**Why:** the user, 11-18: "a review of how we make all of our calculations, and if they're consistent across the
application, and ideally calculated in just one place ... The heart of our application ... needs to be its ability to
parse and translate the EverQuest log into our own dataset. Whatever abstractions we come up with should be airtight."
It started from History → Crawls, where the WAR/CLR/RNG relevel read "+34%, +50%, +54% more T5-eq/h".

## What the audit found

- **Those verdicts were right, and soft.** A finished Estate D4 crawl gives about 17–25 T5-eq, about 15 of it the chest,
  so T5-eq/h there mostly measures how fast it was cleared (9.7 min against about 15). A "more" needs to clear the place's
  band (±25% Estate, ±35% Kedge), so 50 of the history's 62 "more" verdicts are +40% or above. The usual is every other
  visit at the place: any level, any trio, and later visits too.
- **Stored facts are parse-time decisions.** `visits.json` holds the whole `Visit` as the tracker built it. Everything
  on screen is worked out at each refresh. Parse changes reach old visits only when `EngineState.CurrentVersion` is
  raised, and nothing checks that it was.
- **Kills are counted from XP lines only,** so at the cap 803 of 848 crawl visits have no kills, no K/min, no Kill s and
  no idle. Four finished crawls, all at level 50, have no loot and no chest.
- **The same quantity is written several times.** Five copies of the 6-minute early gate, eight T5-eq/h paths with gates of
  1, 3, 6 or none, four T5 valuations, three active-minute formulas, five or more "usual" baselines, DPS over time inside
  while every other rate is over active time. Page scripts work out numbers the engine should own.
- **Sharing still sends worked-out numbers,** and the community job falls back to them for shares older than schema 4.

Locations in the drafts come from reading the code on 11-18. Whoever builds one checks them again first.

## The user's decisions (11-18)

- A visit's usual is made of **earlier visits only**, so its verdict is fixed once it's over.
- **DPS divides by active time,** like every other rate on a visit.
- Each problem is drafted and solved **independently**, one branch each, with the order below.
- **Documenting how every number is worked out** comes last, as its own task.

## The drafts, and what each waits for

| Order | Draft | Tier, budget | Waits for |
|---|---|---|---|
| 1 | [reread-guard](reread-guard.md): a test that fails when parsing changes without `CurrentVersion` | Trivial (tests) | nothing |
| 1 | [chestless-crawls](chestless-crawls.md): why four finished crawls at 50 have no loot | Small to start, it may turn risky | nothing |
| 1 | [usual-from-earlier](usual-from-earlier.md): verdicts against earlier visits only | Small, 60k | nothing |
| 1 | [one-rate](one-rate.md): one rate an hour, one gate, one active time, one T5-eq, DPS on active time | Standard, 300k + designer | nothing |
| 2 | [kills-at-cap](kills-at-cap.md): count kills at the cap from the slain line | Risky, 600k | reread-guard |
| 2 | [engine-owns-numbers](engine-owns-numbers.md): page scripts only format | Standard, 300k | one-rate |
| 3 | [one-usual](one-usual.md): one baseline per kind of play on every screen | Standard, 300k + designer | usual-from-earlier, engine-owns-numbers |
| 3 | [visits-tab](visits-tab.md): History → Crawls named and filtered for what it holds, with the whole time | Small, 60k + designer | engine-owns-numbers |
| 3 | [share-without-derived](share-without-derived.md): sharing sends facts only | Risky, 600k | one-rate |
| 4 | [calc-docs](calc-docs.md): how every number is worked out, on one page | Trivial (docs) | all of the above |

```
reread-guard ──► kills-at-cap ─────────────────────────────┐
chestless-crawls ──────────────────────────────────────────┤
usual-from-earlier ───────────────► one-usual ─────────────┤
one-rate ──► engine-owns-numbers ─┬► one-usual             ├──► calc-docs
                                  └► visits-tab ───────────┤
one-rate ──► share-without-derived ────────────────────────┘
```

A draft in one row may start while another in the same row is open. One that waits starts after what it waits for
is on main.
