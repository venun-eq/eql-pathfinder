# You rank your carries, and the plan checks each trio can survive where you level

**Built 11-19, behind the beta switch: the switch and the "came across" card shown, a Class roles tab with your ranked carries and Pathfinder's survival ranks, the survival check where you level (read as Home reads it), one strong slow per trio and haste for weapon classes** ([campaign-combos](campaign-combos.md), the design page's "A · Class roles"). The
`designer` goes first, then `qa` writes the rest of the spec and the failing tests, then the architect's Decisions
(docs/specs/README.md). **Tier:** risky: it stores each character's carries and changes how the planner breaks ties.
**Built behind** [campaign-planner-beta](campaign-planner-beta.md)'s switch: without it, Campaign is as today.
**Waits for:** campaign-planner-beta. **Unblocks:** campaign-your-combos, campaign-run-chips.

**Why:** today a plan can seat RNG / DRU / BER together for a run at D4: heals and nothing else, no warning. It can put
SHM and ENC in one trio, wasting a strong slow and a haste, and it calls BST a carry whatever the player thinks. After,
the player ranks their own carries, and Pathfinder checks that every trio leveled at a difficulty they farm for motes
can hold up (ENC, or two of a tank, heals, a slow and crowd control), keeps one strong slow per trio, and gives haste
to the classes that hit hard with it, all without adding a run or XP to level.
**Players:** everyone with a campaign. Worst for a player leveling at the difficulties they farm for motes (D3 and D4
by default), where a trio with too little survival struggles.

The new layout is the Campaign Combos design notes' section "A · Class roles".
What changes on Campaign:

```text
Plan → Campaign                                After: a Class roles tab beside it
                                               ⟦Your carries, best first:  1 MAG ▲▼✕  2 NEC ▲▼✕  3 ENC ▲▼✕  4 RNG ▲▼✕⟧
(today: no tab; the carries are fixed,          ⟦Add: WIZ CLR …   Back to Pathfinder's⟧
 BST among them)                               ⟦How Pathfinder ranks survival: tank · heals · slow · mez, charm · haste⟧
                                               ⟦You level at D4, a difficulty you farm for motes (Settings → How you⟧
                                               ⟦play), so trios are checked.⟧
```

## The cause

The planner's free seating choices are large (16 runs for every class 3/3 is 48 seats, each class in 3, and nearly any
seating costs the same runs and XP), and today it spends them on two checks only: a carry and heals in each trio
(`Campaign.Quality`, from `CampaignPlan.Prefer` = "carry", "heals"). It knows nothing of tanks, slows, charm or haste.
The carries are fixed in code (`ClassNames.Carries`), BST among them; heals is the only role (`IsHealer`).

## What the user decided (11-19)

- **Pathfinder is opinionated on tank, heals, slow and charm,** and ranks the classes in each, with sources (the page's
  research section).
- **Carries are the player's:** "a few hardcoded, fully editable, ranked by the user". Pathfinder starts the list with
  MAG, NEC, ENC and RNG. BST is off it: "I'm not sure BST would be considered a carry by anyone". Carries win ties over
  Pathfinder's suggested pairs: "carries should be able to CARRY the trio".
- **The survival check is for relevels at the difficulties the player farms for motes** (later on 11-19: "Follow
  their 'mote farming' settings. (default D3/D4)"): a trio holds up if it has ENC, or two of a tank, heals, a slow and
  crowd control. "At D0 any trio makes it": composition barely matters there, and the check stays quiet at every
  difficulty the player doesn't farm. WAR / CLR / RNG with no slow was fine for the user (best tank, best healer).
  The setting is the existing one, never a new switch: Settings → How you play, "Which difficulties do you farm for
  motes?" (`AppSettings.FarmDifficulties`, default `[3, 4]`; the page reads it as `data.settings.farmDifficulties`).
- **Crowd control counts for survival, and ENC is the best of it** (later on 11-19): "Any build with an enchanter can
  get through D4 pretty easily. CC is probably also just as important as tank/heals/slows for survival. Sometimes
  more-so. Enchanters early level aoe mez can mez up to 30 mobs, allowing them to chip away one at a time. Almost
  impossible to die." Crowd control is mez or charm: ENC 1st (area mez, best charm), BRD 2nd; NEC's charm counts on
  undead only, DRU's on animals only.
- **Never SHM with ENC** (one strong slow per trio), on by default. Since ENC alone makes a trio hold up, ENC's 3 runs
  are best given to trios that would otherwise fall short. ENC stays in the default carries (charm).
- **Haste is a ranked role after all:** a big boost for physical DPS (RNG, MNK, BER, ROG; less for BST, BRD, WAR, PAL,
  SHD), and the earlier a class gets it the better. ENC Quickness at 16, SHM at 29, BRD 10% at 10; PAL at 44 is too
  late. A fresh loadout's item haste is up to 51% (Plane of Sky belt, fully upgraded) or 46% (Cloak of Flames 10/10),
  and Distillate of Alacrity VI from 40. The cap is 55% to 30, 75% at 50; only the best item counts.

## What the player sees

Only a player with Settings → "Join the beta" on sees any of this. With the beta off, or the switch below off, Campaign
is exactly today's: no switch, no tab, no card, the same More options.

![Campaign the first time the switch is on: the card, and More options with the switch](.webp)

*Mock-up in the real window, on a real 48-run plan. Changed from the design page: the switch sits first in More
options, the whole width, because it decides what the rest of that panel means; and the survival check's status now
sits beside the carries, above the table, since "is my plan being checked?" is the first thing a player looks for and
the table is reference.*

### 1. The switch: Campaign → More options

- **Beta on, switch off:** a new first row in `.camp-more`, across all three columns, a line under it:
  **Try the new planner (beta)** / "Plans trios that hold up where you level, with your own carries. Your plan comes
  with you, and turning this off gives back your old plan as you left it." A `.switch` on the right. The rest of More
  options is today's, "Between plans that cost the same, prefer" and its two toggles included.
- **Switch on:** the switch lit. Left of it, a `.link` **Start again from your old plan** (hidden when the character
  has no old plan). The third column becomes **Between plans that cost the same** / "Each trio holds up where you
  level, and gets your carries, best first." / `.link` **Class roles**. The Class roles tab appears between Campaign
  and Roster.
- **Start again:** the link's place becomes "Replace this plan with a fresh copy of your old one?" with `.btn.sm`
  **Replace** and **Cancel**. Replace brings the old plan across again and shows the card (2).
- **Waiting:** the switch moves at once. While the other plan is read or brought across, Campaign keeps what it showed,
  faded, with today's `updating…` chip on the campaign card. Never blank (rule 9).
- **Switch turned off on the Class roles tab, or a stored tab of Class roles with the switch off:** Campaign opens.
- **An edit dropped because the page was drawn before a flip** (the architect's guard): the page redraws with the plan
  it now shows and flashes one line on the campaign card: "That change was made before you switched planners, so it
  wasn't saved. Make it again here." The player must never think it landed.
- **Settings → Join the beta, while the new planner is on:** the hint gains "Leaving turns the new planner off; your
  old plan is as you left it."

### 2. "Your plan came across": top of Campaign

Shown the first time the switch is turned on for this character, and after Start again. It stays at the top of
Campaign (above "Plan every reincarnation" when there are no runs) until **Got it** or **Back to the old planner**.
A `.card` with a `--gold-dim` border; `.chip.gold` **New planner (beta)** on the right of the title.

- Title **Your plan came across**. "Your goals, runs, pins and the runs you've played are as you left them. From here
  the new planner also checks that each trio can hold up where you level, and seats your carries in the order you set:
  both are in Class roles." (Class roles is a `.link` to the tab.)
- `.lbl` **What didn't come across**, then one line for each that applies (later drafts add theirs here):
  - "A carry" was on (the default): "· "A carry" is now your own list, starting with MAG, NEC, ENC and RNG. BST isn't
    a carry any more."
  - "A carry" was off: "· You had "A carry" turned off. The new planner starts your carries at MAG, NEC, ENC and RNG:
    clear the list in Class roles to plan without them."
  - "Heals" was on: "· "Heals" is now part of the survival check, with tanks, slows and crowd control."
  - "Heals" was off: "· You had "Heals" turned off. Heals are now part of the survival check, which follows where you
    level: see Class roles."
- `.btn.primary` **Got it**, `.btn` **Back to the old planner**, then dim: "Your old plan stays as you left it. The
  switch is in More options."
- **Nothing to bring across:** title **Nothing to bring across yet**; "You had no plan on the old planner. Pick your
  goals below and the new planner builds the cheapest way there." No "What didn't come across". Same two buttons.
- **Got it:** the card goes and doesn't come back for this character. **Back to the old planner:** the switch turns
  off and Campaign is today's, with the old plan as left.

### 3. The Class roles tab

![Class roles: carries, the survival check, and how Pathfinder ranks the classes](.webp)

Two columns: **Your carries, best first** (300–420 px) and **The survival check**; **How Pathfinder ranks the
classes** below, the full width. Narrower than 1150 px: one column, the table scrolls inside its `.tablewrap`.

**Your carries, best first** (`.card`)
- Right of the title: dim "Pathfinder's list" while the list is MAG, NEC, ENC, RNG in that order; otherwise
  `.link` **Back to Pathfinder's**.
- Under it, dim: "The classes that do your killing. Between plans that cost the same runs and XP, each trio gets a
  carry, the best ones spread out, in this order."
- A row each, gold-edged: the rank, the class, a dim word for what it kills with (MAG pet, NEC pet, ENC charm, RNG bow,
  BST warder, ROG backstab, MNK melee, BER melee, WIZ nukes, BRD songs, SHD pet, DRU nukes; WAR, CLR, PAL, SHM none),
  then `.iconbtn` ▲ ▼ ✕ (aria-labels "Move MAG up", "Move MAG down", "Remove MAG"). ▲ is faded and does nothing on
  the first row, ▼ on the last. Focus stays on the arrow that moved.
- `.lbl` **Add a carry**, then a `.chip` button for every class not in the list, in the game's class order. A click
  adds it at the bottom.
- **Empty:** "No carries. Between plans that cost the same, the survival check and haste decide." With the check off
  (below): "No carries, and the check is off: any seating that costs the same will do."
- Foot, dim: "Your plan follows this list at once. Runs you've played or pinned never move."
- **Waiting:** the list moves at once; while the plan rebuilds, a `.chip` **updating…** sits right of the title.

**The survival check** (`.card`). First a status box, gold-edged while trios are checked, grey while not:

| Where the player levels | Status (bold part in gold when checked) | Line under it, dim |
|---|---|---|
| A difficulty they farm for motes (over half the active hours of your latest relevel, as Home reads it) | "**You level at D4**, a difficulty you farm for motes, so trios are checked." | "D4 had over half the hours of your latest relevel. What you farm is in Settings → How you play." |
| A difficulty they don't farm | "**You level at D0**, which you don't farm for motes, so the check stays quiet: any trio makes it." | the same |
| No difficulty over half | "**No one difficulty** had over half the hours of your latest relevel, so trios are checked. It only chooses between seatings that cost the same." | "What you farm is in Settings → How you play." |
| No relevel yet | "No relevels yet, so trios are checked as if you level where you farm for motes." | "You farm D3 and D4 for motes (Settings → How you play)." |
| Farms no difficulty | "You don't farm any difficulty for motes, so the check is off: any trio makes it." | "Pick what you farm in Settings → How you play." |

"Your latest relevel" is the one on now once it has crawl minutes, else the last finished (Home's own pick, `DifficultyMix.HomeKind`). "Settings → How
you play" is a `.link` that opens Settings at that row. Then the rules, each a bold sentence and a muted one:
- **A trio holds up with ENC in it, or with two of: a tank, heals, a slow, crowd control.** ENC's area mez holds up to
  30 mobs, so you take them one at a time. BRD slows and mezzes, so a trio with BRD holds up too. WAR / CLR / RNG
  holds up (tank and heals); RNG / DRU / BER doesn't (heals only).
- **ENC and BRD go where they're needed most.** Each has 3 runs and holds a trio up on its own, so they go to trios
  that would otherwise fall short.
- **Haste goes to the classes that hit with weapons.** A trio with RNG, MNK, BER or ROG gets ENC, SHM or BRD where it's
  free, the earliest haste first.
- **The best go where they're missing.** A trio with a top tank gets heals or a slow before a trio that has both.

Then a `.tog`, on by default: **One strong slow per trio** / "SHM and ENC never share a trio, so each slows, heals and
hastes for another one." Last, dim: "None of this adds a run or XP to level: it only chooses between seatings that cost
the same." While the check is quiet or off, the rules are faded (`.dim`); the toggle still works and is kept.

**How Pathfinder ranks the classes** (`.card`, `.tablewrap`). Right of the title, dim: "From the EQL wikis and
guides, checked against the patch notes to Nov 10". Columns: Class, Tank, Heals, Slow, Mez, charm, Gives haste, Wants
haste, Notes. A rank is a `.chip` ("1st" is `.chip.gold`); none is a dim "–"; a partial one is a faint, dashed chip.

| Class | Tank | Heals | Slow | Mez, charm | Gives haste | Wants haste | Notes |
|---|---|---|---|---|---|---|---|
| WAR | 1st | | | | | some | The strongest tank |
| CLR | | 1st | | | | | The strongest healer |
| PAL | 3rd | 4th | | | *at 44: too late* | some | Tank and heals in one seat |
| RNG | | | | | | a lot | Bow carry |
| SHD | 2nd | | | | | some | Heals itself with lifetaps |
| DRU | | 2nd | | *animals only* | | | |
| MNK | | | | | | a lot | Raises its own haste cap |
| BRD | | | 3rd | 2nd | 3rd · from 10 | some | Area slow from 20, and mez: holds a trio up alone. A weaker haste song |
| ROG | | | *poison: not a slow* | | | a lot | |
| SHM | | 3rd | 1st | | 2nd · from 29 | | Slow from 5; the strong one, Togor's Insects, at 38 |
| NEC | | | *undead only* | *undead only* | | | |
| WIZ | | | | | | | |
| MAG | | | | | | | |
| ENC | | | 2nd | 1st | 1st · from 16 | | Area mez; charm from 11; slow from 9, the strong one, Shiftless Deeds, at 41. Holds a trio up alone |
| BST | | | *weak: not counted* | | | some | Weak slows only below 50 (Drowsy at 20, Languid Pace at 35) |
| BER | | | | | | a lot | Berserker stance |

(*Italic*: the faint, dashed chip. "a lot" is `.chip.amber`, "some" a plain `.chip`.) Under it, dim: "1st is the best
at it. A dashed cell counts for less, or not at all: NEC's charm works on undead only and DRU's on animals only, and
ROG's poison and BST's weak slows don't count as a slow. **Haste: the earlier, the better.** "From 16" is the level the class gets it.
Item haste and spell haste add up, but two spell hastes don't, so SHM and ENC together waste one. From 40, Distillate
of Alacrity VI hastes any trio." No haste percentage for the player anywhere (the user's answer, below).

### Reused and new

- **Reused:** `.card`, `.h2`, `.lbl`, `.dim`, `.muted`, `.soft`, `.link`, `.btn` (`.primary`, `.sm`), `.switch`,
  `.tog` with `.box` and core.js `CHECK`, `.chip` (`.gold`, `.amber`) in `.chips`, `.iconbtn`, `.tablewrap` with
  `th.l`/`td.l`, `.between`, `.row`, `.camp-more`, the `updating…` chip, `.flash`; the tab through core.js `GROUPS`.
- **New:** `.camp-switch` (the full-width first row of `.camp-more`), `.camp-across` (the card's gold border),
  `.roles-grid` (two columns, one under 1150 px), `.carry-row` (the gold numbered row), `.check-state` (the status box,
  gold-edged or grey), `.chip.part` (faint and dashed).

### Done when (what a screenshot shows)

- [x] Beta off, and beta on with the switch off: Campaign and More options exactly as today, no Class roles tab - seen
  in main-campaign.png
- [x] Beta on, switch off: "Try the new planner (beta)" first in More options, its line, the switch off - seen in
  main-campaign-beta.png
- [x] Switch on: the switch lit, "Start again from your old plan", the third column pointing at Class roles, the tab
  between Campaign and Roster - seen in main-campaign-beta-on.png
- [x] "Your plan came across" with both "didn't come across" lines, Got it and Back to the old planner - seen in
  main-campaign-across.png
- [x] "Nothing to bring across yet" on a character with no old plan - seen in main-campaign-across-empty.png
- [x] Class roles: the carries MAG, NEC, ENC, RNG with "Pathfinder's list", ▲ faded on the first and ▼ on the last,
  the Add chips, the checked status in gold, the rules, the toggle on, the 16-row table and its foot - seen in
  main-classroles.png
- [x] Carries changed and one removed: "Back to Pathfinder's" shows; the empty list's words with none - seen in
  main-classroles-edited.png and main-classroles-empty.png
- [x] Each status row of the survival check (quiet at D0, no difficulty over half, no relevels yet, farms none), the
  quiet and off ones grey with the rules faded - seen in main-classroles-quiet.png, -spread.png, -norelevel.png,
  -nofarm.png
- [x] At 1000 px wide, Class roles in one column and the table scrolling inside its card; nothing touches - seen in
  main-classroles-narrow.png

## Proposal

- One table of roles in the engine: each class's rank for tank, heals, slow (strong: SHM, ENC; BRD counts too; BST's
  weak slows, ROG's poison and NEC's undead-only slow don't), crowd control (mez or charm: ENC 1st, BRD 2nd; NEC undead only and
  DRU animals only, partial), gives haste (with the level), wants haste. It replaces `ClassNames.Carries` as the rule
  and `IsHealer` as the only role, in the new planner only (the old one keeps them while the switch is off).
- The survival check: ENC in the trio, or two of tank, heals, slow, crowd control. Where it's free, ENC's runs go to
  the trios that would otherwise fall short.
- Carries become a ranked list stored with each character's new plan (campaign-planner-beta), defaulting to MAG, NEC,
  ENC, RNG. This draft changes the bring-across's `Prefer` row: a plan brought across gets the default, and its
  "didn't come across" line says so.
- This draft first shows the switch, "Try the new planner (beta)" under More options while "Join the beta" is on, and
  the "Your plan came across" card ("Got it", "Back to the old planner"; "Nothing to bring across yet"), designed by
  its designer. campaign-planner-beta built them hidden. Once the page can flip the switch, a Campaign edit from a
  page drawn before the flip must never be saved into the other plan (planner-beta's review): the page sends back
  which plan it showed, or edits are ignored until the refresh after a flip, with a test.
- The tie-break below the cheapest path, in this order (the index's "How a plan ranks", items 6 and 7): the survival
  check for relevels at the difficulties farmed for motes and haste for physical DPS, then the carries in the player's
  order. Nothing here may add a run or XP to level.
- Which difficulty the player levels at is read from their relevels (design.md rules 18 and 19), never asked; the
  check is on when that difficulty is one of `FarmDifficulties`.

## Open checks, answered by the user (11-19)

- **Item haste and spell haste stack** ("YES item haste and spell haste stack"). Spell hastes don't stack with each
  other, except a high-level bard song ("Melody of Ervaj or something like that"), which stacks on top.
- **Reading or projecting item haste is not the planner's job, yet:** "trying to read item hastes, and trying to
  project it is not the responsibility of this campaign builder (yet)". The roles table says who gives haste, from
  what level, and who gains from it; the page claims no haste percentage for the player. Reading the inventory
  export's "+N" belt and cloak ranks (seen 11-19: `Back  White Dragonscale Cloak +9`) is a later draft.
- **"You level at D4":** the difficulty with over half the active hours of the relevel Home reads (the one on now with crawls, else the last finished: not a pool of three, which on 11-19's real data gave D4 40% and D0 46%, so "no one difficulty" while Home and the player said D4; rule 18's
  "mostly D4"). With no difficulty over half, the check runs: it only breaks ties and costs nothing.
- **A character with no relevel yet:** the check runs, and says so: "No relevels yet, so trios are checked as if you
  level where you farm for motes."
- **A player who farms no difficulty:** the check never runs, and the tab says so.
- **What counts as a slow: SHM, ENC and BRD; BST doesn't** ("BST definitely shouldn't count"). The eqlwiki spell
  lists (11-19): SHM Drowsy 5, Walking Sleep 13, Tagar's Insects 27, Togor's Insects 38 (49–70%); ENC Languid Pace 9,
  Tepid Deeds 23, Shiftless Deeds 41 (49–65%); BRD Largo's Melodic Binding 20 (area, 15–30%), Selo's Consonant Chain
  23; BST only Drowsy 20 and Languid Pace 35 below 50, Sha's Lethargy at 50. BRD is a slow and crowd control, so a
  trio with BRD holds up on its own, like ENC. Carries break ties whether the check is on or quiet ("Yes, always").
  A one-time notice that points beta players to the switch is for later, not this branch.
- **qa's questions, answered (11-19):** "Start again from your old plan" brings the runs back and keeps the player's
  carries and the "One strong slow per trio" toggle. "Back to the old planner" dismisses the "came across" card for
  good, as Got it does. The relevel being played now is the one read once it has crawl minutes.
- **`PlanPrefs` and `CampaignPlan.Prefer`:** left as they are, so the old planner is unchanged; the new plan keeps
  its own carries. What becomes of them is decided when the beta graduates. A hand-edited `settings.json` with a
  class that isn't real: the class is dropped on load.

## Done when

The engine and what's stored (`ClassRolesTests`, `PlannerSwitchTests`). The screenshots are above.
- [x] A trio holds up with ENC or BRD in it, or two of a tank, heals, a slow and crowd control; BST's slows, ROG's
  poison, NEC's undead-only and DRU's animal-only charm count for nothing - `ATrioHoldsUpWithEncOrBrdOrTwoOfTankHealsSlowAndCrowdControl`
- [x] Where trios are checked, every trio of "Every class 3/3" and "Everything 3/3" holds up, at the old planner's runs
  and XP (the test seats one by hand to show it can) - `EveryTrioHoldsUpAtTheSameRunsAndXp`
- [x] SHM and ENC never share a trio while "One strong slow" is on, checked or not - `ShmAndEncNeverShareATrio`
  (passes with no tie-break at all; seen failing on today's, which seats BRD/SHM/ENC)
- [x] Every trio with ENC, SHM or BRD has RNG, MNK, BER or ROG to haste - `EveryHasteGiverSitsWithAClassThatHitsWithWeapons`
- [x] Pathfinder's four carries get a trio each: 12 trios with one, none with two - `EachOfYourCarriesGetsATrioOfItsOwn`
- [x] With the check quiet, carries still break ties, in the player's order: of six, the first keeps trios of its own - `YourFirstCarryKeepsATrioOfItsOwnWhileTheCheckIsQuiet`
- [x] Where you level is the difficulty with over half the active hours (open world counted) of the relevel Home reads (not a pool of the last 3):
  checked at one farmed for motes, quiet at one not, checked with none over half or no relevel, off when nothing is
  farmed - `TriosAreCheckedWhereYouLevelAtADifficultyYouFarm`, `TheCheckIsQuietWhereYouLevelAtADifficultyYouDontFarm`,
  `WhereYouLevelIsWhatHomeCallsMostlyNotAPoolOfThree`, `OlderRelevelsDontSayWhereYouLevel`, `WithNoDifficultyOverHalfOrNoRelevelYetTriosAreChecked`, `FarmingNoDifficultyTurnsTheCheckOff`
- [x] The old planner plans exactly as on main, BST a carry - `TheOldPlannerPlansExactlyAsBefore` (passes; seen
  failing with BST off `ClassNames.Carries`)
- [x] A plan brought across, now or before this branch, starts with MAG, NEC, ENC, RNG and the toggle on - `ABroughtAcrossPlanStartsWithPathfindersCarriesAndOneStrongSlow`
- [x] The carries and the toggle stay with the new plan through a save, a Campaign edit and Start again; an emptied
  list stays empty; the old plans never change - `YourCarriesAndTheToggleAreKeptWithTheNewPlanAndNeverTouchTheOldOne`
- [x] They're saved only while the switch is on - `CarriesAreSavedOnlyWhileTheNewPlannerIsOn`
- [x] A hand-edited list keeps real classes once, names and any case read as the class - `AHandEditedListKeepsOnlyRealClassesOnce`
- [x] An edit from a page drawn before a flip is never saved, either way - `AnEditFromAPageDrawnBeforeAFlipIsNeverSaved`
- [x] The page sends back which plan it showed with every Campaign edit - a `CampaignPageTests` test, written with
  the field the architect names
- [x] "Your plan came across" shows the first time, and after Start again, until Got it; "Nothing to bring across yet"
  never stops a later old plan from coming across - `TheCameAcrossCardShowsUntilGotItAndAgainAfterStartAgain`

## Edge cases
- **Nothing yet:** no relevel: checked, and says so. No plan: "Nothing to bring across yet"; its Got it stores
  nothing that stops a plan made later on the old planner coming across (planner-beta's edge case).
- **Too little:** a relevel on now with a few crawl minutes is read by those minutes, as Home reads it. Exactly half isn't
  over half. No crawls anywhere yet: "No relevels yet".
- **Play the log never saw:** relevels before `/log on` aren't there; the words say "your latest relevel".
- **A state the app never builds:** an unplayed reincarnation has no journey, so it's never one of the 3. The tests'
  relevels all have crawls and hours.
- **Starting and stopping:** a flip during a rebuild is planner-beta's `wasOn` guard; a page drawn before a flip is
  refused (above) and redraws with the designer's flash line. A carry edit while a rebuild runs: the rebuild that
  lands uses the list saved last.
- **Same name, different thing:** characters match ignoring case, as planner-beta. ENC is a carry and holds a trio up:
  it counts as both. "Shaman" and "shm" are SHM.
- **The rules change:** the roles are game facts as of the patch notes to Nov 10. A nerf changes the table and so
  the tie-breaks; never a stored plan, and the player's carries stay theirs.
- **A game fact from a summary:** each level in the table comes from the class's eqlwiki spell list, the spell named
  beside it (Open checks: SHM Drowsy 5, ENC Languid Pace 9, BRD Largo's 20; haste ENC 16, SHM 29, BRD 10).
- **Old versions in the field:** an older app drops `NewCampaigns` at its next save (planner-beta's Decisions), carries
  and card flag with it. Fields go on `NewCampaignPlan`, never `CampaignPlan`. No carries stored = Pathfinder's
  list; an empty one = none.
- **A tab named like its group:** Class roles must be reachable again after Roster or Campaign is used (0.9.11).
- **Something leaving the PC:** nothing new is sent. The tab's page-open count (code-map recipe 8, `docs/usage.md`)
  would be a new usage key: the architect decides, and `site/privacy.html` must still match.

## Not doing
- Reading or projecting item haste, or a haste percentage anywhere (the user, 11-19).
- `PlanPrefs`, `CampaignPlan.Prefer`, `ClassNames.Carries` and `IsHealer`: unchanged, for the old planner.
- A notice that points beta players to the switch; role chips on runs (campaign-run-chips); combos (campaign-your-combos).
- NEC's and DRU's partial charm: shown dashed, worth nothing to the check.

## Where
- `src/EqlPathfinder.Engine/Model/ClassRoles.cs` (qa's stub): the roles table, `HoldsUp`, `DefaultCarries`
- `src/EqlPathfinder.Engine/Analysis/SurvivalCheck.cs` (stub): `Of(journeys newest first, FarmDifficulties)`
- `src/EqlPathfinder.Engine/Analysis/Campaign.cs`: `RolesQuality(roles, check)` (stub), for `Generate`, `Improve`,
  `Fixes`, `Options`, `Presets` in the new planner; `DeltaOf` (~line 720) says "a carry" from the player's list there
- `src/EqlPathfinder.Engine/Analysis/PlannerSwitch.cs`: `CampaignRoles`, `RolesFor`, `StoreRoles`, `Store(…, shownNew)`,
  `ShowsCard`, `CardSeen` (stubs). Today's `Store` replaces the whole `NewCampaignPlan`, so it would drop the carries
- `src/EqlPathfinder.Engine/Live/TrackerService.cs` `CampaignData`, `CampaignOptions`, `RebuildCampaign` (~761–803):
  the roles and the check when on, in the cache keys; `Pages/PageData.cs` and `PageDataTests` for what the page needs
- `src/EqlPathfinder.App/App.xaml.cs` `SetCampaign` (~754, `shownNew`), `ApplySetting` (`newPlanner` ~793, and the
  tab's edits); `web/js/campaign.js`, `index.html`, core.js `GROUPS`; recipe 8 in docs/code-map.md for the tab
- The names in the stubs and tests are the architect's to change

## Decisions
- **What's stored: three fields on `NewCampaignPlan`, beside `Plan`, nothing on `CampaignPlan`.** `"Carries": [...]`
  (`List<string>?`: absent or null = Pathfinder's list, `[]` = none), `"OneStrongSlow"` (`bool?`, absent = on) and
  `"Card"` (`string?`: `"across"`, `"empty"`, `"seen"`; absent = `"across"` when the entry has a plan, else
  `"empty"`; anything else = `"seen"`). `Plan` becomes `CampaignPlan?`: null means "no new plan yet", and `PlanFor`
  already falls back to the old plan there. `RolesFor` cleans on read (class or name, any case, through
  `ClassNames`; unknown dropped; first of a repeat kept) and `StoreRoles` stores the cleaned list, or null when it
  equals `ClassRoles.DefaultCarries` in order, so "Back to Pathfinder's" follows a later change to Pathfinder's list.
  Local only: no table, no re-read (`reread: false`, `job: false`). `IPlannerSettings` doesn't change.
- **Every write keeps the entry's other fields.** `Store` (on) sets `Plan` on the first entry matching ignoring case
  (or a new one) and removes the other-case duplicates; a new entry gets `Card` = `"across"` when an old plan exists,
  else `"empty"`. `StartAgain` sets `Plan` = `old?.Normalized()` (null when none) and `Card` = `"across"`/`"empty"`,
  keeping `Carries` and `OneStrongSlow` (the user, 11-19). `SetOn(true)` brings an old plan across into a missing
  entry or one with a null `Plan`, keeping that entry's carries, and sets `Card` = `"across"`. `StoreRoles` returns
  false when off, else sets the two fields on the entry (or a new `{ Plan: null }` one).
- **The card:** `PlannerSwitch.Card(s, character)` gives `"across"`, `"empty"` or null (no card), the engine's choice
  of variant (rule 4); `ShowsCard` is `Card != null`, and is false when off. No entry while on = `"empty"`.
  `CardSeen` sets `"seen"`, making a `{ Plan: null, Card: "seen" }` entry when there is none: the null plan is what
  lets an old plan made later still come across, and then its card shows as `"across"` (it's new news). "Back to the
  old planner" is `CardSeen` then `SetOn(false)`.
- **Which plan the page showed: `planner`.** The page payload's `campaign.planner` is `"new"` or `"old"`
  (`PlannerSwitch.IsOn` when the page was built), and campaign.js sends it back on every `campaign` message:
  `post("campaign", { value, rebuild, planner })`. MainWindow passes `shownNew: planner == "new"`; absent reads as
  `"old"`, right for every player outside the beta and refused (visibly) for one inside it. `SetCampaign` calls
  `Store(…, shownNew)` and keeps its `wasOn` guard; a refused edit refreshes with the designer's flash line.
- **The new planner's inputs reach TrackerService as one parameter, null = the old planner.** `CampaignData(plan,
  journeys, CampaignRoles? roles)`, `CampaignOptions(plan, index, CampaignRoles? roles)`, `RebuildCampaign(plan,
  CampaignRoles? roles)`. Null runs today's code exactly (`Campaign.Quality(plan.Prefer)`, today's keys).
  Otherwise the quality is `Campaign.RolesQuality(roles, check.Checks)` with `check = SurvivalCheck.Of(the
  character's journeys newest first, _options.FarmDifficulties)`, and both cache keys gain `"|new|" + carries +
  "|" + OneStrongSlow + "|" + check.State`, so a carries edit, the toggle, a farming setting or a new relevel
  rebuilds them. The roles are read on the UI thread with the plan (`RolesFor` while on, else null) and passed in;
  `PageData.Build` gets the same and gives the page the roles, the check and `Card`.
- **Swap's "a carry" reads the player's list:** `Campaign.Fixes(…, quality, IReadOnlyCollection<string>? carries
  = null)` and `Campaign.Options(…, quality, index, … carries = null)` pass it to `DeltaOf`; null is
  `ClassNames.Carries` (the old planner, unchanged). An empty list never says "a carry". "Heals" stays as it is.
- **A Class roles edit** stores the roles at once, then rebuilds the plan in use (`RebuildCampaign(plan, roles)`) and
  stores it only if the switch, `RolesFor` and the stored plan are unchanged after the await; otherwise it only
  refreshes (the later edit's rebuild lands instead, the edge case's "the list saved last").
- **What's sent: one new page name.** The tab sends `seen` with `page: "classroles"` (added to MainWindow's list and
  `PageNames`), counted as `page.classroles`; docs/usage.md's page list gains it. It's a page count, which
  site/privacy.html already lists ("pages opened"), so the page doesn't change. Class roles edits add no feature key.
  The user kept the count (11-19).
- **Older apps and taking it back:** as planner-beta's Decisions: an older build drops `NewCampaigns` at its next
  save, carries and card with it; the old plans are untouched. A later build without these fields ignores them.

## Reviews
- architect: 11-19 (9b5ab69), two must fix, both fixed. A carries edit landing while a Campaign edit rebuilt left the
  plan built around the old carries: `PlannerSwitch.RebuildAround` rebuilds from the plan that landed, up to three
  times (274ed3f, two tests; the lead read the fix). The page chose the card's lines, the carries' kills and "is it
  Pathfinder's list": the engine sends them, the page draws (1eb704b, `ClassRolesPageTests`). Should fix, all fixed:
  the cache-key test now asserts what the key decides, a direct `RolesQuality` case proves the "One strong slow"
  toggle (a037493), and `PAGE_NAMES` has Class roles. Switch off: the old planner, its cache keys and its stored plan
  are main's. No second review: neither fix changed what's stored or sent.
- designer: 11-19, no must fix. Should fix, all fixed (1eb704b): "No one difficulty" in gold, "Back to Pathfinder's"
  beside "updating…", carry rows capped in one column. The missing shots were rendered (f958f10): beta off with More
  options open is pixel-identical to main's render; all 16 rows and the foot; a real 1000 px window (the table is
  below its frame). On real data the check said "No one difficulty" (last 3 relevels pooled: D4 40%, D0 46%) while the
  player levels mostly at D4: it now reads where you level as Home does (8682794), which the user chose.
