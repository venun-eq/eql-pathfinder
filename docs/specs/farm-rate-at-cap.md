# The farm rate at the cap counts your farming, from the patch's real hour

**Started 11-19** ([numbers-audit](numbers-audit.md), from sample-gates' review). **Tier:** standard, about 250k with
the designer. Nothing sent or stored changes. Pays docs/debt.md's top row (`FarmRates`, `FarmPatch`).

**Why:** on this player's database (11-19) the farm rate is 3.3 T5-eq/h, the median of 6 crawls at 50 since
`FarmPatch` (11-10 12:00). Four of them are D0, at 0 to 4.7 (Permafrost Keep D0 looted nothing), while their D4 farming
runs 57 to 101. Ask's "Count motes too" and Upgrades' hours price motes from this rate, at about a twentieth of what
this player farms.

**The user, 11-19:**
- "The 11/10 patch landed (the servers unlocked) at 2:50 PM EST": 19:50 UTC, not noon.
- A farming crawl at the cap is one at the player's farming difficulties: the setting "Which difficulties do you farm
  for motes?" (default D3+D4), the same rule Home and History use to read what a player is doing (docs/pace.md, the
  user's 11-18 "inferred by the behavior of the character through logs").

## What the player sees

The designer, 11-19, from the code (no render: another branch's reviews were rendering). Nothing new is laid out:
the same lines on the same pages, with new words. Reused: `.dim`, `.muted`, `.link`, `.stale`, `cmpRow` on Home's
farming card, the `infoTip` popover. Nothing new in `app.css`.

**What moves.** The farm rate is taken from crawls at the cap at the difficulties the player farms for motes (the
setting, default D3 and D4), from the moment the servers came back on 11-10. On this PC it goes from 3.3 T5-eq/h
(six crawls, four of them D0) to its D3 and D4 farming, which ran 57 to 101: Ask's "Count motes too", Every place's
ranking and Upgrades' hours all move with it.

**The patch is named, never timed.** It reads "the 11-10 patch" everywhere, the spelling every other page already
uses. The page stops formatting the patch's date from the engine's instant (`farmPatch()` in core.js and `patch` in
home.js's `farmList` both call `dt`): once it's a real instant, `dt` would turn it into "Nov 11" for a player in
Sydney, and today it prints "Nov 10" beside "11-10" on the same page (docs/debt.md's row, paid here). The patch's
hour shows nowhere, on screen or in the notes: a player can't act on it, and in their own clock it would be a
different time on every PC. `HomePageTests`' "Too little at 50 since the Nov 10 patch" becomes "11-10".

**How the difficulties are named.** In the words Settings, Crawls and Home already use (`farmWords()`): "D3 and D4",
"D4", "D1, D3 and D4". A "none" sentence needs "or": "D3 or D4" (`farmWords` takes the joining word). After a count,
the difficulties go at the end of the clause, so a number never sits against a D ("3 D3 and D4 crawls"); after "of
your" they go before "crawls". The words name the difficulties **the engine counted** (sent beside the rate in
`reinc.farm`), not `data.settings.farmDifficulties`, so a just-clicked difficulty never puts new words on an old
number.

### Where the farm rate names its source (`farmFrom`, core.js)

It follows "Farm rate **N T5-eq/h** (i), " on Ask (the motes answer, and Count motes too's help), Upgrades ("The
hours are at …"), Settings and Every place. With 50 as the cap and the default setting:

| State | Words |
|---|---|
| Best (the setting's default) | your best farm, Plane of Hate D4 (5 crawls at 50 since the 11-10 patch) |
| Typical | your typical farm (the middle of your 6 farming crawls at 50 since the 11-10 patch, at D3 and D4) |
| Set, as the fallback | the rate you set in Settings, used until 3 of your D3 and D4 crawls at 50 since the 11-10 patch have a rate |
| Set, chosen | the rate you set in Settings (unchanged) |
| Too little: some have a rate | the default: only 2 of your D3 and D4 crawls at 50 since the 11-10 patch have a rate, and it takes 3 ("only 1 … has a rate") |
| Too little: none has a rate | the default: none of your 4 farming crawls at 50 since the 11-10 patch, at D3 and D4, has a rate yet (8 kills, or a finished crawl), and it takes 3 |
| **Crawls at 50, none at a farmed difficulty** (the player who plays 50 on D0 to D2) | the default: none of your 8 crawls at 50 since the 11-10 patch were at D3 or D4, the difficulties you farm for motes (Settings → How you play) (one: "your 1 crawl at 50 since the 11-10 patch wasn't at D3 or D4, …") |
| Nothing at 50 | the default: your logs have no crawls at 50 since the 11-10 patch |
| No difficulty ticked in How you play | the default: you farm no difficulty for motes (Settings → How you play) |

- Best needs no difficulty words: its place carries its own D, and it can only be a farmed one now.
- The 8 in "none of your 8 crawls" is new from the engine: crawls at the cap since the patch, with 3 active minutes,
  at a difficulty the player doesn't farm. Without it the line would say "no farming at 50" to someone with eight
  crawls there, a zero that reads as a fact. Open-world hunts at 50 (no difficulty) aren't crawls and aren't counted
  in it or in the farm rate.
- The 3 and the 8 kills stay the engine's (`farmNeeds()`, `rateFromKills()`), never typed in the page.
- Settings adds " Pick Set it to use your own." after a default, as now. That's how the D0 to D2 player gets their
  own rate if they don't want to tick D2.

**Your farming, after the source** (`farmLine`, Settings and Every place): " Your farming at D3 and D4 since the patch:
typical 80 over 6 crawls, best Plane of Hate D4 101." (was "Your farming since the patch: …"). "Before it: typical …,
best …" is unchanged in words; its numbers come from the same difficulties.

### Settings, under Your farm rate (Set it)

"Also used until 3 of your D3 and D4 crawls at 50 since the patch have a rate." (was "…until your logs have 3 farming
crawls with a rate since the patch."). Hidden once met, as now.

**Waiting:** after a difficulty is clicked in How you play, Settings' farm line (`farmLine`) and Home's typical row
are faded (`.stale`, while `farmWaiting`) and keep their old number and their old difficulty words until the next
data message. Never blanked.

### Home, the farming card (at the cap)

The row keeps its label, "Your typical farm at 50 since the patch" (design.md rule 20). Its note, right of the number:

| State | Note |
|---|---|
| Has a typical | 6 crawls, D3 and D4 (the "2 crawls, 3 players" pattern of the row above it) |
| None at a farmed difficulty | no D3 or D4 crawls yet |
| No difficulty ticked | no difficulty farmed |

A player at 50 on D1 sees their D1 session in "This session" and "no D3 or D4 crawls yet" beside the typical: true,
and it says why the row is empty. Home's "Yours, since the 11-10 patch" farm list isn't the farm rate: it ranks every
place at 50 at its own difficulty and doesn't change, only its patch spelling.

### The (i) beside the farm rate (`INFO.farmRate`)

> What an hour of farming at the cap earns, in T5-eq. Ask weighs levels against it, and Upgrades turns what's left to
> farm into hours with it.
>
> It's your best or typical farm at the cap since the 11-10 patch, from your crawls at D3 and D4: the difficulties you
> farm for motes, in Settings → How you play. It takes 3 of those crawls with a rate (8 kills, or a finished crawl).
> Until then it's the rate you set in Settings, or 50.

With no difficulty ticked: "…from your crawls at the difficulties you farm for motes (none ticked, in Settings → How
you play)."

### Help, The farm rate (index.html)

"Taken from your own crawls at the cap since the 11-10 patch, at the difficulties you farm for motes (Settings → How
you play; D3 and D4 unless you change them): your best place or your typical, once three of them have a rate."
(Was "your best zone or your usual", the word rule 20 retired.)

### Players

- **The regular** (this PC): the rate rises about twentyfold, and every line names D3 and D4.
- **New, log just turned on / old to the game:** "no crawls at 50 since the 11-10 patch", as before, in clearer words.
- **Casual, plays 50 on D0 to D2:** told their crawls weren't at the difficulties they farm and where that's set,
  never "no farming".
- **Not sharing / among few sharers:** nothing here is shared or compared with players who share.
- **Another time zone:** the same crawls count, by the instant; the page still says "11-10".

## Release note (docs/releases/next/farm-rate-at-cap.md)

```markdown
---
title: The farm rate counts your farming
reread: false
job: false
bump: patch
---
The farm rate now comes from your crawls at 50 at the difficulties you farm for motes: D3 and D4, unless you change
them in Settings → How you play.

- A quick D0 run at 50 no longer drags it down. Ask's Count motes too and Upgrades' hours now price motes at what you
  really farm.
- Ask, Upgrades, Settings and Home's farming card say which difficulties it's from, and say so when none of your
  crawls at 50 were at them.
- The 11-10 patch starts when the servers came back up, wherever you play, not at a fixed hour of your clock.
```

`reread` stays false if nothing stored changes with `FarmPatch` (the engineer confirms; the proposal says nothing is).

## Proposal

- `FarmPatch` is one instant, 11-10 19:50 UTC. Log times are each player's own clock, so it is compared in the
  player's local time (`TimeZoneInfo.Local`), never as a bare `DateTime`. Every use moves with it:
  `RouteAnalysis` (`PreNerfDdd`, `FarmRates`), `PlayFacts` (:116), `MeterViews` (:63). `CommunityBuilder` (:125)
  compares dates only, so it doesn't change (and it is on the risky list: leave it).
- `RouteAnalysis.FarmRates` counts crawls at the cap at the player's farming difficulties only, before and after the
  patch alike, and its source says so. Below the cap nothing changes.
- Tests: a crawl at 14:00 local on 11-10 in EST is before the patch, and one at 15:00 after; the same crawls in
  another time zone fall by the instant, not the clock; a D0 crawl at 50 is left out of the farm rate and a D4 one
  counts; a player who farms D2 by the setting has their D2 crawls counted.

## Where

- `src/EqlPathfinder.Engine/Analysis/RouteAnalysis.cs` (`FarmPatch` :92, `PreNerfDdd` :79, `FarmRates` :161),
  `PlayFacts.cs` (:116), `Combat/MeterViews.cs` (:63); the farming-difficulties setting where Home and History read it
- `src/EqlPathfinder.App/web/js/core.js` and `settings.js` (the farm rate's source words), `ask.js` (the farm line)
- docs/pace.md (the farm rate), docs/debt.md (the row this pays, and the "Nov 10" / "11-10" spelling row if it's
  touched anyway)

## Done when

Renders in an isolated copy (`EQLPATHFINDER_DATA`, `--snapshot` with a copy of the database); mock shots click Home's
farming card and open the farm rate's (i) as the app-screenshots skill says. The words with no render (too little,
no difficulty ticked, one crawl) are proved by page tests on the strings above.

- [x] Ask's farm rate no longer reads 3.3: on this PC (one D4 crawl with a rate since the patch's hour) it reads "Farm rate 50 T5-eq/h, the default: only 1 of your D3 and D4 crawls at 50 since the 11-10 patch has a rate, and it takes 3." - seen in main-ask-motes.png
- [x] Settings' farm line says "Your farming at D3 and D4 since the patch", and no "Nov 10" anywhere on it - seen in main-settings.png
- [x] Home's farming card: "Your typical farm at 50 since the patch", the same number as the farm rate's typical, and "N crawls, D3 and D4" - seen in home-farming.png
- [x] The farm rate's (i) names D3 and D4 and Settings → How you play - seen in info-farmrate.png
- [x] With How you play set to D2 only (a copy's settings.json), Settings' farm line reads "the default: none of your N crawls at 50 since the 11-10 patch were at D2, the difficulties you farm for motes (Settings → How you play)" with N over 0, and Home's note reads "no D2 crawls yet" - seen in settings-d2.png and home-farming-d2.png
- [x] Set it, with the rate a fallback: "Also used until 3 of your D2 crawls at 50 since the patch have a rate." - seen in settings-d2-set.png

## Built (engineer, 11-19)

- `reread` stays false: nothing stored changes. `CommunityBuilder` (:125, :276) compares dates only and reads `FarmPatch.Date`, now the patch's date on the PC's own clock; left alone (risky list).
- `FarmPatch` is a property (11-10 19:50 UTC in local time); `FarmPatchIn(zone)` is what the tests use. `FarmRateInfo` and `FarmRateSource` carry `Difficulties` and `NotFarmed`.
- On this PC the farm rate reads 50 (the default), not 57 to 101: only one D3 or D4 crawl at 50 with a rate falls after 14:50 EST on 11-10 (Nagafen's Lair D4, 57); the Plane of Fear D4 crawls are before the patch. First box left unticked for that reason.
- Not rendered: Home's farming card (the real player is leveling at 10) and the D2 shots (the copy's settings.json key didn't take). Their words are proved by page tests (FarmRateAtCapPageTests) only for the Settings lines; Home's note and the D2 line are untested.
- 11-19 (second engineer): Home note, D2 words and the fade have page tests (FarmRateAtCapPageTests.cs); the fade needed Settings' click to redraw Home (settings.js). Shots rendered through a mock that stubs the farm data. Box 1 stays open: this PC has one D3/D4 crawl with a rate since the patch.

## Reviews
- **Architect:** three must fix, one should fix, two debt rows (67f7e96). Done: `FarmRates` takes a zone and a Sydney-against-Eastern test proves the instant (fails with a bare `DateTime`, seen); Home's note says "N crawls at D3 and D4, no rate yet" when crawls are unrated; Done when line 1 rewritten to what this PC shows; the per-crawl list search is now a difficulty test.
- **Designer:** no must fix, two should fix, an idea taken. Done: "the difficulty you farm for motes" with one difficulty (the none line and the (i)); Settings reads "57 from 1 crawl", not "typical 57 over 1 crawl". Page tests for each; two D2 tests' expected words moved to the singular (their own wording, same commit).
