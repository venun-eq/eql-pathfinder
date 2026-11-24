# The numbers check follows XP at the cap

**Tier:** standard (engine change that re-reads logs).

**Why:** xp-at-cap (11-19) said an XP line read at 50 is the bar filling, not levels. The numbers check (recompute.mjs, brought
up to the docs in 72d052e) still finds History disagreeing on a fresh `import`:
1. **xpKills at 50, 486 visits:** a kill at 50 with no XP line (or none sized) still counted as an XP kill, so a 50→50 visit of
   121 kills showed xpKills 121, killLevels 0, XP per kill 0, not "no XP per kill".
2. **The ding kill, 13 visits reaching 50:** on 3 the first kill whose slain line is read at 50 (its XP line read at 49,
   the ding between them) counted its percent; on 10 not. The XP line comes before the ding line, so the XP that filled the bar
   belongs to the kill whose XP line was read below the cap. Rule (docs/pace.md): a kill's level is the level its XP line was
   read at (its slain line's, with no XP line); a kill at the cap is never an XP kill and its percent is not levels.

## Done when
- [x] A kill at the cap is never an XP kill, whatever its line says: XpKills, XpPerKill and KillLevelsPerHour are 0/null at 50 - `KillsAtTheCapWithNoPercentAreNotXpKills`
- [x] The ding kill counts at the level its XP line was read, the stored kill row says 49 - `TheDingKillIsTheOneWhoseXpLineWasReadBelowTheCap`
- [x] EngineState.CurrentVersion 14 (reread) and RereadGuardTests follow; release note `reread: true`
- [x] docs/pace.md states the rule; recompute.mjs follows the doc (kills table now stores the XP line's level)
- [x] numbers check on a fresh copy and `import`: recompute 1335/1335 on every field (xpKills 1335, killLevels 1335), usual 476/476, killspeed levelingOnly 53/53

## Edge cases
- XP line with no percent at 50: still `UnknownXpKills`, not also a cap kill (no double subtraction).
- Slain line before the XP line in a second, or after a ding in the same second: the XP line's level decides.
- A kill with no XP line below 50 stays an XP kill with 0 (known no XP).

## Where
`Tracking/Tracker.cs` (EmitKill), `Tracking/EngineState.cs` (version), `RereadGuardTests`, `docs/pace.md`,
`tools/analysis/numbers-check/recompute.mjs`.

## Reviews

- architect: no must fix or should fix; both tests fail on the old code, and the stored kill level now being the level its XP line was read at changes only the kill that dings 50: every reader compares a kill's level with the cap, so the app and the community job now agree on it. Nothing new is sent, the re-read rides on xp-at-cap's in the same release, `job: false` holds, and no Decisions were needed.
