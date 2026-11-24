# A kill is its slain line

**Spec, 11-18, building** ([calc-audit](calc-audit.md)). `qa` checked the logs and wrote the failing tests; the lead
wrote this page from them and the user's answers. **Tier:** risky, 700k with the designer: it changes what's stored
and what's sent, and needs a re-read. **Waited for:** reread-guard (on main). **Unblocks:** calc-docs.

**Why:** a kill is counted only from an XP line with a percent (`Tracker.OnExperience`: `Kills++`, `KillTimes`, and
the `KillRecord` sent through `EmitKill`). On the user's logs, Jul 31 to Nov 18 (docs/log-formats.md, "XP lines
around a kill"):
- **At 50** the tracker saw 1,077 of 43,309 kills. 803 of 848 crawl visits at 50 have 0 kills. That empties Kills,
  K/min and Kill s at the cap. It also takes idle away, because idle is measured between kills (`Visit.IdleSeconds`
  returns 0 with no `KillTimes`). So all the time inside counts as active, AFK included, which lowers every at-cap
  T5-eq/h and its usual.
- **Elsewhere** it missed 17,167 of 44,253 kills: those whose XP line had no `(x%)` and those of mobs that give no XP.
  Those with no `(x%)` were the main at 50: the split by level takes it from dings and misses swaps
  (docs/log-formats.md).
- **Quest and task rewards counted as kills:** 2,830 `(x%)` lines with no slain line near them.

**Players:** everyone farming at the cap, which is most of the hours played. Everyone who kills mobs that give no XP.
Everyone who hands in quests.

## The rule (the user, 11-18)

- **A kill is its slain line,** at every level: `You have slain …`, or `… has been slain by <you or your pet>!`.
- **Another player's slain line** (`A ghoul has been slain by Groupmate!`) is a kill **inside an instance** (crawls,
  respawning instances: everyone inside is in your group, the user), and **outside one** only when a
  `You gain party experience…` line shares its second. Otherwise it's someone else's.
- **A player's death** (`Groupmate has been slain by a ghoul!`, `You have been slain by …`) is never a kill.
- **An XP line with no slain line is never a kill,** in either form. It's a quest or task reward. It still fills the
  bar as today (a crawl's completion XP is sized from it).
- **XP per kill: nothing seen while playing is hidden, and unknown isn't zero** (docs/engineering.md, Modeling 11):
  - a kill with no XP line (a mob that gives none) is a 0;
  - a kill whose XP line has no `(x%)` gave XP the log doesn't size: it's left out of XP per kill, the app's and the
    community job's, and counts as a kill everywhere else (kills, K/min, Kill s, idle);
  - raid kills (`You receive no experience … in a raid`) keep today's handling.
- **K/min at the cap** is shown as it is below the cap, with its column and its usual (the user, 11-18).
- **One-rate's cap exceptions go:** a cap visit with no rate is "too short" like any other, and "Hide visits too short
  to judge" hides it. The overlay's at-cap note and Visits' "—" with "No rate: kills at level 50 aren't counted yet."
  are removed.

## What the player sees

(The designer, 11-18.) Nothing new is laid out and no class is added: numbers that were empty at the cap fill in,
with the words and parts the cap already uses below it (`rateCell`, `noRateChip`'s "too short", `.dim`, the overlay's
`Row` and `Note`). Each "8" below is the rate rule's (`rateFromKills`, `Rates.MinKills`), and each "50" the cap from
the data, as today. The players it touches: **the regular** farming at the cap (every visit at 50 changes),
**everyone below 50** (kill counts change), and **not sharing / among few sharers** (the community numbers move
as players update).

**History → Visits, a visit at the cap** (Levels `50→50`):
- **Plenty** (8 kills, or a finished crawl): Kills is the count, K/min and Kill s have numbers, Idle shows when it's
  0.5 min or more. Active is smaller by that idle, so T5/h reads higher than before. Verdict: the T5-eq chips as
  today ("12% more T5-eq/h", "Usual T5-eq/h", "no usual yet"), against usuals rebuilt the same way. Lv/h reads `0.0`,
  as a finished crawl at the cap does today.
- **Too little** (under 8 kills, not finished): every rate cell is `—` with the tip "No rate: under 8 kills and not
  finished." Verdict: `too short` (`Grouped · too short` grouped), same tip. "Hide visits too short to judge" hides it,
  and an empty list reads "3 visits too short to judge are hidden: it starts at 8 kills, or when the crawl is
  finished." All of these are today's below-the-cap words.
- **Gone:** the `—` with the tip "No rate: kills at level 50 aren't counted yet."
- **Raids:** unchanged (judged by the boss, never hidden).
- **The tile** "T5-eq per active hour" now includes cap visits with 8 kills; its words don't change.

**The overlay at the cap** (farming):
- **Rows:** T5-eq/h, then Kills/min with its usual tick, the two rows a farming crawl has below the cap. Kills/min
  was dropped at the cap by the "nothing on either side" filter; after the re-read past cap visits have kills, so it
  shows. Before 8 kills its value is `—` like T5-eq/h's.
- **Note, under 8 kills and not finished:** "The rate starts at 8 kills: 4.5 T5-eq so far." Compact: no note.
- **Note once rated:** "T5-eq/h against your usual here: {basis}, {N} visits." or "No usual here yet: three visits of
  this kind make one." (today's words).
- **Gone:** "No rate: kills at level 50 aren't counted yet. 4.5 T5-eq so far."
- The session line and the "Reading your logs" message during the re-read don't change.

**Home's farming card:** unchanged words. "Too short for a rate yet: the rate starts at 8 kills, or when the crawl is
finished." is now true at the cap and gives way at the 8th kill to "{N} T5-eq/h here, your usual {U} at {zone}
({basis})."

**Below the cap, wherever a visit's kills show** (Visits, the overlay, Home's and History's relevel kill speed, Ask's
kill-speed and XP-per-kill bars): same words, new numbers. Kills rise where the mob gave no XP; quest and task hand-ins stop counting. XP per kill leaves out kills whose XP the log doesn't size and counts a
no-XP mob as 0, so it reads lower where a player kills grey mobs among the rest. Its warning ("Less XP per kill than
this place gives…") compares with a usual re-read the same way: the review checks it doesn't fire on a normal run.

**Words that must go with the exception** (the grep: no Help, site or page text says it besides these):
- `OverlayWindow.NoRateNote`'s `AtCap` branch; `crawls.js` `noRateChip`'s `—` branch (and its comment, and the
  filter's "not at the cap" comment); `VisitStats.TooShort`'s `!s.AtCap` and `AtCap`'s summary.
- Stale comments that say the log names no kills at the cap: `OverlayWindow.xaml.cs` above `shown`,
  `RouteAnalysis.Usable`, `Records.cs` (idle at the cap). `docs/community-job.md` "visits with no kills, which are
  never shared": at the cap they now have kills (the architect's re-send decision).

**What the release note should tell players** (the engineer writes it; the README decides the re-read line):
- Headline: "Kills at level 50 now count, and every level counts a kill the same way: by its slain line."
- At 50: Kills, kills a minute, Kill s and idle fill in on Visits and the overlay. Idle is now taken out at 50 too,
  so time standing in a crawl no longer counts as active: T5-eq an hour reads higher, and so do your usuals and the
  farm rate Ask and Upgrades use.
- Below 50: kills of mobs that give no XP now count; quest and task hand-ins
  no longer do. Kill counts and kills a minute change at every level. XP per kill leaves out kills whose XP the log
  doesn't show.
- A visit at 50 under 8 kills now reads "too short" like any other, and "Hide visits too short to judge" hides it.
  The "kills at level 50 aren't counted yet" note is gone.
- Players who share: the community numbers settle as players update. (One more line if the architect decides
  shared visits are sent again.)

## Done when

- [x] A kill at the cap with no XP line counts - `KillsAtCapTests.AKillAtTheCapWithNoXpLineCounts`
- [x] A kill whose XP line has no `(x%)` counts once, whichever line comes first - `AKillWhoseXpLineHasNoPercentCountsOnceInEitherOrder`
- [x] Outside an instance, another player's kill counts only with party XP in its second - `InTheOpenWorldAnotherPlayersKillCountsOnlyWithPartyXp`
- [x] Inside an instance every mob slain counts once, and a groupmate's death doesn't - `InsideAnInstanceEveryMobSlainCountsOnce`
- [x] Two kills in one second with one XP line are two kills - `TwoKillsInOneSecondCountTwiceWithOneXpLine`
- [x] An XP line with no slain line is a reward, not a kill, and still fills the bar - `AnXpLineWithNoSlainLineIsAQuestRewardNotAKill`
- [x] XP per kill counts a no-XP kill as 0 and leaves out one whose XP isn't in the log - `XpPerKillCountsAKillThatGaveNoXpAsZeroAndLeavesOutOneWhoseXpIsntInTheLog`
- [x] Shared kill rows say 0 for no XP and that the XP isn't in the log for the rest - `SharedKillRowsSayZeroForNoXpAndNullForXpNotInTheLog`
- [x] The community job leaves kills with unknown XP out of XP per kill - `TheCommunityJobLeavesKillsWithUnknownXpOutOfXpPerKill`
- [x] Idle at the cap is measured between kills - `IdleAtTheCapIsMeasuredBetweenKills`
- [x] A cap visit with no rate reads "too short" and the box hides it - `RatePageTests.ACapVisitWithNoRateReadsTooShortLikeAnyOther`, `RateRuleEverywhereTests.TheBoxHidesNoRateVisitsExceptRaids`
- [x] No page says kills at the cap aren't counted - `NoPageSaysKillsAtTheCapArentCounted`
- [x] A visit at 50 with 8 kills has Kills, K/min, Kill s and (when 0.5 min or more) Idle filled on Visits - seen in cap-crawls.png (the designer's render: Crawls narrowed to visits at 50 and zoomed to show every column; main-crawls.png has none at 50)
- [x] A visit at 50 under 8 kills, not finished, reads "too short" in Verdict and `—` in its rates, and no cell or tip says "aren't counted yet" - seen in cap-crawls.png (the same render, box unticked; the words are in no page script)
- [x] The overlay farming at 50 shows the Kills/min row under T5-eq/h - seen in overlay-farming-standard.png
- [x] The overlay at 50 under 8 kills says "The rate starts at 8 kills: … T5-eq so far." - seen in overlay-early-farming.png
- [x] A visit below the cap where no kill moved the bar is left out of relevels and sharing, and stays in History - `AVisitBelowTheCapWhereNoKillMovedTheBarIsLeftOutOfRelevelsAndSharing` (the engineer writes it first)
- [x] Old logs are read again: `EngineState.CurrentVersion` is 12 and the note says `reread: true` - `RereadGuardTests` (it hashes a crawl at 50 in each XP line form, and fails until the version is raised)
- [x] `docs/releases/next/kills-at-cap.md`: `reread: true`, `job: true`, and kill counts change at every level

## Edge cases

- **Same name, different thing:**
  - Two kills in one second share one XP line: two kills, the XP goes to one.
  - `X has been slain by Y`: which side is the player decides whether it's a kill or a death.
  - Your pet's name on a slain line is yours. A charmed mob: see Decisions.
- **Line order:** the game writes the XP line before the slain line, in the same second. Older clients wrote the slain
  line first. 87 of 3,062 seconds had the XP line a second off. Either order, one kill.
- **A reward in a kill's second** (a hand-in mid-fight, a crawl's completion): it pairs with that kill. It's rare, and
  the bar still fills right.
- **Too little:** a cap visit under 8 kills and not finished is "too short", as below the cap.
- **The rules change:** a bonus event's no-`(x%)` line is the same line with a bonus: still a kill, still bonus-marked.
  The AA-cap line on its own isn't a kill.
- **Old versions in the field:**
  - Older apps keep sending kill rows without the unknown-XP kills, and with rewards counted as kills.
  - The community job reads both, and older apps may read the community file.
  - A field is added, never given a new meaning: see Decisions on the `xp` of a kill row.
- **Re-sends and limits:**
  - The re-read changes every visit's kills.
  - Whether already-shared visits are sent again, and how many that is against the server's daily limit: see
    Decisions. A math change once re-sent every player's history and hit the limit.
- **Starting and stopping:** a close during the re-read is handled by the re-read as today.

## Decisions

(The architect, 11-18.)
- **A shared kill row whose XP the log doesn't size: `xp: null`, no new field, no schema raise.** The column keeps its
  meaning (the kill's XP, % of a level); null says the log didn't give it. Every reader parses rows with `Num(row, 1)`,
  which reads null as no number: the new job leaves it out of XP per kill and adds 0 to levels (right: a no-`(x%)` line
  means the regular bar didn't move). The old job, for the hour before `job: true` lands, reads it as a 0, the same as
  a new field with `xp: 0` would give it, so a field buys nothing. The server stores facts as an opaque string and never
  reads rows; older apps read only the community file, whose fields keep their meaning. A schema raise would re-send
  everything a second time for nothing. `site/privacy.html` doesn't change: no new value leaves the PC.
- **What's stored:** `KillRecord` keeps `Percent` (0 when unknown) and gains `bool XpUnknown`, so qa's tests compile
  as written. `kills.pct` is `REAL NOT NULL` and `CREATE TABLE IF NOT EXISTS` won't change it, so the store adds
  `xp_unknown INTEGER NOT NULL DEFAULT 0` with `ALTER TABLE` when `pragma table_info(kills)` lacks it (the first
  `ALTER` in the store: a test opens a database made with the old table). Every `INSERT INTO kills` names its columns,
  so a build after a revert still writes to the wider table. `Visit` gains the count of unknown-XP kills (its JSON,
  no column); `LevelingSegment` gains `XpKills` (kills whose XP is known), from the visit or, in the job, from the
  rows' non-null `xp`. Every reader that divides by kills must use the flag: a site that sums `Percent` alone treats
  unknown as zero, which Modeling 11 forbids.
- **The version:** `EngineState.CurrentVersion` 11 to 12, one raise for the whole branch; a close mid-re-read is
  today's. It can't be taken back except by another raise, and that's fine: it only makes the reading truer.
- **XP per kill and its usual are one rule:** `RunPace` line 70 (`xpk`) and `ExpectedXp`'s points both divide kill XP
  by `XpKills`, and the 8-kill floor counts `XpKills`. The usual is the player's own history, re-read the same way at
  12, so a normal run with its usual share of grey mobs matches its usual, and `XpBand` takes the noise. Where a
  history includes community runs, those from apps before this one have no 0s and count rewards as kills, so they read
  higher; they're replaced as sharers update (below), and nothing is done for the gap.
- **Re-sends: yes, once, and nothing to throttle.** The re-read changes kill rows in nearly every crawl, and a crawl
  added at the cap shifts `seq` for everything after it, so a sharer's whole history goes again: about 1,500 units
  for the biggest (1,225 crawls, 86 hunts, plus reincarnations and relevels), under the 3,000 a day; a bigger one
  finishes the next day after the server's 429. That's the design working (Modeling 1: a fix to how the log is read
  changes the facts), and the server's content hash would count them anyway (it includes `engine`). The release note
  adds: "If you share, your history is sent again once, over a day or so, so the community numbers count it the new way."
- **Not re-sent, left behind:** crawl ids are the zone-entry time and don't move. A hunt's id is its first kill's time,
  which moves where a hand-in or a no-XP mob opened it; the old unit stays on the server (debt line). Below-cap only.
  The user (11-18): accepted for now, as long as it's tracked and fixed later.
- **`docs/community-job.md` "visits with no kills, which are never shared":** now true only of visits where nothing
  was slain; a cap instance with kills is shared as an `instance` unit, the same fields as today. The engineer fixes
  the line.
- **Pairing window: one second either side, as today** (`OnLine`'s flush, `OnSlain`'s and `OnExperience`'s `<= 1`),
  for your XP line and for party XP outside an instance. One XP line pairs with at most one slain line; a second
  slain line in that second is a no-XP kill. An unpaired slain line of yours becomes a kill at the flush; an unpaired
  XP line becomes a reward at the flush.
- **An unpaired XP line's percent** stays in the bar (`KillPercentInLevel`) and in the visit's levels, where the
  tracker keeps non-kill XP, so it reaches the job as a reward row (`[t, share, …]`, today's columns), never as a kill
  row. The engineer adds an assertion that the visit's `LevelsGained` doesn't change when a hand-in stops being a kill.
  An unpaired no-`(x%)` line adds nothing.
- **Pets and charm: the fight meter's rule, already in the parser.** `MobSlain.ByYou` (`LineParser.ParseSlainBy`) is
  you, your named pet, or a charmed mob while the charm holds; use it, don't re-derive. A slain line whose victim is
  your pet (`EndCharm` fires there) isn't a kill.
- **Whose slain line:** a kill when the killer is you or your pet; inside an instance also when the killer is any one-
  word capitalized name (a groupmate or their pet) and the victim isn't; outside one, that needs party XP in the
  window. A one-word victim slain by a non-player is a death. Both one-word: not a kill (rare; the log can't tell).
- **"Inside an instance" is `Tracker.Inside != null` at the slain line's time,** the same source as the kill's
  `VisitId`, so a kill's instance-ness and its visit can't disagree.
- **The loadout swap (visit-loadout-swap):** this makes it more visible. A plane visit on the main between a relevel's
  dings had no kills and no unit; now its kills count, at the relevel's level, with unknown XP: XP per kill is safe
  (they're left out), but its kill speed joins the relevel's and it may be shared as a below-cap instance with ~0
  levels. See the question in the architect's report.
- **Stopgap until [swap-zones](swap-zones.md) (the user, 11-18):** a visit the app files below the cap whose kills had
  XP lines, none of them with `(x%)`, was played on a loadout at the cap: below 50 every kill moves the bar. It's left
  out of relevels (leveling runs, their pace and kill speed) and out of sharing, and still shows in History. A visit
  of only no-XP mobs (no XP lines) isn't touched. swap-zones files such visits to the right loadout and lifts this.

## Not doing

- Telling a groupmate's kill of a no-XP mob from a stranger's outside an instance: the log can't.
- Sizing an unknown-XP kill from the next ding.
- Telling which loadout played a crawl no /who saw (above, Findings).
- Sending facts only (share-without-derived) and how every number is worked out (calc-docs).

## Where

- `src/EqlPathfinder.Engine/Parsing/LineParser.cs`, `LogEvents.cs`: `ExperienceGained` needs the percent today; the
  no-`(x%)` form
- `src/EqlPathfinder.Engine/Tracking/Tracker.cs`: `OnSlain`, `OnExperience`, `EmitKill`, `FlushPendingKill`
- `src/EqlPathfinder.Engine/Tracking/EngineState.cs`: `CurrentVersion`
- `src/EqlPathfinder.Engine/Tracking/VisitStats.cs`: `TooShort`'s cap exception
- `src/EqlPathfinder.Engine/Sharing/SharePayload.cs` (`ShareBuilder`), `Community/CommunityBuilder.cs`: kill rows and
  XP per kill
- `src/EqlPathfinder.App/OverlayWindow.xaml.cs`: `NoRateNote` at the cap; `src/EqlPathfinder.App/web/js/crawls.js`:
  the cap tip
- `tests/EqlPathfinder.Engine.Tests/`: `KillsAtCapTests`, `RereadGuardTests`, `RatePageTests`

## Findings that led here

- **Chestless-crawls (11-18)** said a full AA pool writes `You gain experience!` with no `(x%)`. The logs show the
  no-`(x%)` line comes whenever the regular bar doesn't move: on a loadout at 50 with the bar full. Both bars full
  still writes it, with the AA-cap line. A mob that gives no XP writes no XP line at all.
- **The user asked whether the 3,791 "below 50" were quest rewards.** They aren't: 2,975 of their 3,062 seconds hold
  your slain line and the rest are a second off, and an NPC's `says` is near 21% of them against 71% of rewards.
  They were in Planes of Fear, Sky and Hate, which need level 46 (the user): the main at 50, swapped in between a
  relevel's dings with no /who before the plane. A first guess, an AA slider, was wrong.
- **The app has the same blind spot** (visit-loadout-swap, "Not doing: guessing a trio for a crawl no /who saw"): a
  plane visit on the main with no /who in it counts to the relevel played before. A no-`(x%)` kill line says the
  loadout played is at the cap; using that is its own draft, not this branch.

## Reviews
- architect: one must fix and two should fixes, each fixed. (1) `StoreUpgradeTests` opens an old 10-column `kills` table with a row twice, reads the old row back with `XpUnknown` false and an unknown-XP kill back true. (2) The stopgap now covers open-world hunts: `CapLoadout.Played` is the one rule, used by `Visit.PlayedOnACapLoadout` and `FieldStretch.PlayedOnACapLoadout`, and `FieldHunts.From` drops such a stretch, so relevels, sharing and Journeys all agree (`AnOpenWorldHuntWhoseKillsAllHadNoPercent...`; the job's `Hunt` is untouched, new apps no longer send those stretches). (3) Comments beside the `ALTER` and the `INSERT` say a revert must keep the named column list.
- designer: both Crawls boxes held in cap-crawls.png. The 50 to 50 Gained 0.22 and Lv/h 0.2 to 1.9 is not this branch: main's `Tracker.OnExperience` sums `(x%)` kill XP into a visit's levels at 50 too. Logged in docs/debt.md (at the cap Gained and Lv/h should read 0).
