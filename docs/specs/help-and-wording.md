# Help links each setting it names, and stops pointing at things that aren't there

## Why

Four reports from one player on 11-20 (0.10.24, all from Help), and five Help lines found wrong since:

- **#16** (confusing), on "T5-eq counts a fragment at the value Settings gives it: What's a fragment worth?, below":
  "We're in Help, not in settings. 'Below' means nothing. We should link to the setting from here."
- **#17, part 1** (confusing): "The below referred to another area of Help ... and then in here, there is a 'Pick that
  in Settings → Counting.' ... when encountering that first sentence above, it was confusing." (Part 2, adding to a
  sent report, is the report-threads row.)
- **#18** (idea): "It would be great if we could link out to all of the settings from the Help section whenever one is
  mentioned."
- **#19** (bug), on "Planes below 46": "if there are planar visits <46 that is for sure a bug. The game has NEVER
  allowed characters to enter the planes before level 46." There was never a "before that rule": every plane visit
  read below 46 is a loadout at the cap the player swapped to with no /who (docs/log-formats.md, 11-20).
- docs/debt.md: Help names Home's **Fastest | Counting motes** switch, scrapped 11-18 (design rule 19); Help's chip
  says **Slower place** where every page says "<place> levels faster"; the farm line prints **"typical 0"** for 0.27
  (rule 1, a zero that reads as a fact); the **XP per kill table** cuts its Reincarnation row off behind a sideways
  scroll bar at the window's usual width.

## What the player sees

Wording and links only: no new layout. Every link is the existing inline link (`button.link`, gold, as
classroles.js's "Settings → How you play"), with `data-go` and `data-anchor`: core.js's handler already opens the page,
scrolls the spot into view and flashes it (`.flash` / `.card.flash`). In Help's prose the link takes the sentence's
size (`.link` is 12px; add `.help .link { font-size: inherit; }`), so a link reads as part of the line.

### Links from Help to Settings

`[...]` is the link. Anchors that exist: `shareCard`, `howYouPlay`, `farmRateSetting`. New: `fragmentSetting` on the
"What's a fragment worth?" row in Settings → Counting (settings.js), and `meterSettings` around the fight meter's part
of Settings → Overlays, from its "Fight meter" label to its last row (settings.js, `overlaysCard`). The DPS counts row
is only there while the meter is shown, so the link lands on the meter's part, whose first row is "Show the fight
meter".

| Card | Before | After | Goes to |
|---|---|---|---|
| What it reads and sends | Your play, as numbers, only if you turn on sharing. Settings → Sharing shows exactly what goes. | Your play, as numbers, only if you turn on sharing. [Settings → Sharing] shows exactly what goes. | `settings` / `shareCard` |
| Numbers from other players | Leaving sharing stops new uploads; Settings → Sharing can also delete everything you sent. | Leaving sharing stops new uploads; [Settings → Sharing] can also delete everything you sent. | `settings` / `shareCard` |
| Motes, fragments and T5-equivalent (#16, #17) | … so they belong on your highest gear. T5-eq counts a fragment at the value Settings gives it: What's a fragment worth?, below. | … so they belong on your highest gear. T5-eq counts a fragment at the value you pick in [Settings → Counting]; the next card, [What's a fragment worth?], says which to pick. | `settings` / `fragmentSetting`; `help` / `help-fragment` |
| What's a fragment worth? (#17) | Take gear higher or lower with motes? Pick that in Settings → Counting. | Take gear higher or lower with motes? Pick that in [Settings → Counting]. | `settings` / `fragmentSetting` |
| How you play and the farm rate | Pathfinder judges a visit at the difficulties you farm (Settings → How you play) by T5-eq per hour … | Pathfinder judges a visit at the difficulties you farm ([Settings → How you play]) by T5-eq per hour … | `settings` / `howYouPlay` |
| How you play and the farm rate | … at the difficulties you farm for motes (Settings → How you play; D3 and D4 unless you change them): … | … at the difficulties you farm for motes ([Settings → How you play]; D3 and D4 unless you change them): … | `settings` / `howYouPlay` |
| How you play and the farm rate | Until then, or if you prefer, the rate you set in Settings. Without either, a default of 50 T5-eq/h. … | Until then, or if you prefer, a rate you type yourself: Set it, in [Settings → Counting → Farm rate]. Without either, a default of 50 T5-eq/h. … | `settings` / `farmRateSetting` |
| DPS and kill time | … and the fight meter can use either (Settings → Overlays → DPS counts). | … and the fight meter can use either ([Settings → Overlays → Fight meter], DPS counts). | `settings` / `meterSettings` |
| Reincarnation counts | Any from while your log was off are invisible to it. Set them under Plan → Roster (Set counts…). | Any from while your log was off are invisible to it. Set them under [Plan → Roster] (Set counts…). | `roster` |

Every other mention of a setting in Help is one of these: after this change, Help names no setting without a link.

### Words that point at things that aren't there

**Planes below 46** (What's left out, #19):
- Before: "**Planes below 46:** Hate, Fear and Sky open at 46, so Ask never offers them lower, and visits from before
  that rule are left out."
- After: "**Planes below 46:** Hate, Fear and Sky open at 46, so Ask never offers them lower. A plane visit your
  history puts below 46 was a loadout at the cap you'd swapped to without a /who since, so its level is the last one
  Pathfinder saw: Ask leaves it out."
- What's true behind it: Ask's `IsUsable` drops a visit that starts below its zone's gate, so "Ask leaves it out"
  holds. `ZoneGates.cs`'s comments say the same: the planes have always needed 46; a visit read below that is a cap
  loadout swapped to with no /who (docs/log-formats.md, 11-20). Its summary's stray last line ("numbers (they can't
  be repeated).") goes.

**Fastest or Counting motes on Home** (Am I going at a proper speed?, "Mostly D4", and the same text in core.js's
`likeforlike` (i), about line 295):
- Before: "… When no difficulty has more than half, the relevel is mixed, and compares only with other mixed ones.
  Fastest or Counting motes on Home changes the route, and with it what you're compared with."
- After: "… When no difficulty has more than half, the relevel is mixed, and compares only with other mixed ones. Ask's
  route counts motes when this relevel, or your last, was mostly at difficulties you farm ([Settings → How you
  play]), and is the fastest otherwise. Home says which, with a link to ask it the other way."
- In the (i) the same sentence, with "on Home" kept out of it (the (i) is on Home) and the same link; core.js already
  closes the popover when a link in it is clicked.

**Slower place** (Am I going at a proper speed?, the chips):
- Before: `[Fading]` "Your best place 3 levels back, clearly passed by another at your level. …" and `[Slower place]`
  "The same test for a place that was never the best."
- After, as Home and History say it (amber `chip`, an example place):
  - `[Fading · Kedge Keep levels faster]` "Your best place 3 levels back, clearly passed by another at your level. …"
    (the rest of that line as it is)
  - `[Kedge Keep levels faster]` "The same test for a place that was never your best: another place at the same
    difficulty is clearly faster at your level."
- Ask's own "Fading" tags stay as they are: Ask shows "Fading" alone, and the first chip says what it means.

**The XP per kill table** (Am I going at a proper speed?, "What changes XP per kill"): the second column gets the
existing `wrap` class (`.help .tablewrap td.wrap`, as the Rank by table has), so the Reincarnation row wraps at the
window's usual width and no sideways scroll bar hides it. Words unchanged.

**"typical 0"** (the farm line: Settings → Counting under Farm rate, and Ask's Every place):
- Before: "… Before it: typical 0, best Kedge Keep 1."
- After: a farm number under 1 shows one decimal: "… Before it: typical 0.3, best Kedge Keep 1.2." Under 0.05 it
  reads "under 0.1", never "0.0". The same for every number in `farmLine` (since the patch and before it, typical and
  best). Numbers of 1 and over stay whole.

### States

- **Plenty, the regular:** every link above is there, whatever the data: Help is static text.
- **New, log just turned on:** the same. The farm line has no "Before it" when there are no crawls before the patch,
  as today.
- **Not sharing:** Settings → Sharing lands on the sharing card, which shows how to join; the link is right for them.
- **Fight meter off:** the link lands on the fight meter's part of Overlays, on "Show the fight meter".
- **The window, narrow and 150%:** links wrap with their sentence; a link is one button, so "Settings → Counting →
  Farm rate" may wrap inside itself, which is fine.

## Done when

New shots in DevSnapshots: `help-links` (Help scrolled so the Motes card's rules and the fragment card's "Not sure?"
list show) and `settings-from-help` (the window after clicking the fragment row's link in Help).

- [x] The Motes card reads "T5-eq counts a fragment at the value you pick in Settings → Counting; the next card, What's a fragment worth?, says which to pick.", both gold links at the text's size - seen in main-help-links.png
- [x] "Pick that in Settings → Counting." is a gold link - seen in main-help-links.png
- [x] Clicking it opens Settings with "What's a fragment worth?" in view - seen in main-settings-from-help.png
- [x] The farm rate card's three Settings mentions are links - seen in main-help-counts-*.png (the one with "How you play and the farm rate")
- [x] The chips read "Fading · Kedge Keep levels faster" and "Kedge Keep levels faster"; no "Slower place" in Help - seen in main-help-counts-*.png (the one with the chips)
- [x] "Mostly D4" names no Fastest or Counting motes switch, and links Settings → How you play - seen in main-help-counts-*.png
- [x] The XP per kill table's Reincarnation row wraps whole, with no sideways scroll bar - seen in main-help-counts-*.png
- [x] "Planes below 46" reads as above, with no "before that rule" - seen in main-help-counts-4.png
- [x] DPS and kill time and Reincarnation counts link Settings → Overlays → Fight meter and Plan → Roster - seen in main-help-counts-4.png
- [x] No farm number under 1 reads "0": a test of `farmLine` with a before-the-patch usual of 0.27 reads "typical 0.3" (page test)
- [x] `ZoneGates.cs` says the planes always needed 46 (code read, no shot)
- [x] A release note in docs/releases/next/help-and-wording.md

After it ships: reports #16, #17, #18 and #19 marked done with the version (the lead; #19 is the Planes line and ZoneGates.cs).

## Reviews

- Test corrected: the spec's farm example read "best Kedge Keep 1.2", but its rule says numbers of 1 and over stay whole; the rule wins and the test reads 1.
- architect: no must fix; three should fix (farm test regexes, 0.95 cut, the (i) check), all fixed.
- designer: one must fix (chips overlapping, grid column), should fixes (nowrap "(" links, 0.95 cut, farm-card wording, release note), all fixed.
