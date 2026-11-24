# Trios and pairs you love stay in your plan through every rebuild

**Built 11-19, behind the beta switch** ([campaign-combos](campaign-combos.md), the design page's "B · Your combos"). The
`designer` goes first, then `qa` writes the rest of the spec and the failing tests, then the architect's Decisions.
**Tier:** risky: it stores each character's combos and changes the planner's order (a Must ranks above the cheapest
path). **Built behind** [campaign-planner-beta](campaign-planner-beta.md)'s switch: without it, Campaign is as today.
**Waits for:** campaign-planner-beta, campaign-class-roles (Never SHM with ENC comes from its one strong slow; the
picker's labels are its roles). **Unblocks:** campaign-first-lane, campaign-synergies.

**Why:** the user wants to say "SHD/SHM/ROG 3 times no matter what" and "PAL and MNK together". Today the only way is
to pin a run, and a pin holds one row: changing a goal, rebuilding, a fix or a swap can undo what the player meant.
After, the player writes a combo once (Must, Prefer or Never), sees its price before adding it, and every rebuild
keeps it. A combo that asks more of a class than its goal wins, and the goal says so.
**Players:** everyone with a campaign, most of all a player with trios they love or classes they want to play together.

The new card is the Campaign Combos design notes' section "B · Your combos".
What changes on the goal tile:

```text
Class passives (made-up plan: SHD's goal 2/3, the Must "SHD / ROG / SHM, 3 times")

Today                                       After
SHD ●●○  (goal 2/3; nothing says a pin      SHD ●●◐  ⟦third pip planned, as any planned rank⟧
          took it further, or didn't)       ⟦SHD: your combo SHD / ROG / SHM takes it to 3/3, past your goal of 2/3,⟧
                                            ⟦so this goal has no effect.   Remove the goal   Make it 3/3⟧
```

## The cause

A pin (`CampaignLoadout.Pinned`) holds one row of `CampaignPlan`, not an intent. `Generate`, `Improve`, `Fixes` and
`Options` (Swap…) keep pinned rows but know nothing of "these classes together" or "this trio 3 times", and a goal
change regenerates around them. The design page calls a combo a rule written once: it holds an intent, where a pin
holds a row.

## What the user decided (11-19)

- **"'must' combos, or combos the user avidly wants to play, must be held above all else."** A Must ranks above the
  cheapest path, and shows its price before it's added.
- **Prefer combos hold only where free.** Never combos keep classes apart.
- **Time first** (relevel faster): on keeping PAL in every run of a 48-run plan, which would reach 200–300% more XP to
  level a run: "Time is a users most valuable commodity. Our whole motto is 'relevel faster', we should lean heavily
  towards 'time' unless the user explicitly tells us otherwise." So a Must holds in the runs that earn its classes'
  ranks; past 3/3 every combo is a Prefer.
- **Spread out is the default; "Unlock now" is an option** beside each trio combo: it plays the trio's runs back to
  back ("I want to unlock the actives"). 3/3 unlocks a class's active, on the reincarnation after the third relevel.
- **Unlock now is held like a Must** (later on 11-19: "Unlock now -> must: yes"): above the cheapest path, with its
  price shown before it's set.
- **A Must that asks more than a class's goal wins, and the goal says so:** "Basically on the SHD 2/3 goal could be
  'You have SHD in the SHD/ROG/SHM build for unlock now, so this is ignored, you can remove it' or something like that
  with a better user experience." The plan never spends extra runs for the goal and never raises it silently.
- **Pins come across as Musts** (answer 1): an old pinned loadout with runs to go becomes a Must combo when the plan comes
  across to the new planner, and Pathfinder picks its race (races get their own lane in campaign-first-lane). It isn't
  kept as a pin.
- **A new combo starts as Prefer** (answer 2, time first); the Add form always shows what Must would cost.

- **"Free" means the same total runs and the same total XP to level as without the combo** (answer A, the user,
  11-19). Moving XP between runs (run 11 needs 10% less, run 12 10% more) is free. The engine decides it
  (`CampaignComboPrice.Free`).
- **A free combo's row has no price line** (answer B). "Why do we need the comment at all? Even reading this, without
  context as another player, I wouldn't know what this means." A row says something only when the combo costs (runs,
  XP to level, classes or Completionist later), a Prefer isn't kept, or a hand edit or pin broke it. The Add form still
  says what adding does, in runs and time to relevel ("Your campaign stays 16 runs and takes no longer."), never
  run-by-run XP shifts. The cheapest-path line names the combos only when they cost (`CampaignView.CombosCost`).
  The rule is on docs/design.md.
- **Plans brought across in 0.10.21's beta keep their pins as pins** (answer C): no conversion, no marker.

## What the player sees

Where the lines below show "Free." or "Your combos cost nothing", answer B above replaces them: a free combo's row is
just its name, kind and ⋯, and the cheapest-path line doesn't mention free combos.

Only a player with Settings → "Join the beta" on and the new planner switched on (More options) sees any of this. With
either off, Campaign is exactly today's: no card, no line on the goal tile, no new item in a loadout's ⋯ menu.

![Your combos on Campaign, with the goal tile's line above it](.webp)

*Mock-up in the real window, made-up combos on a real 48-run plan. Changed from the design page: the card sits on
Campaign between the campaign card and Your runs, and Add a combo opens inside it on a click, so Campaign stays short
for a player with no combos. "Free." is plain text, not green, and Never is gold like Must (it's held like one), not
red: rule 21, see "Colour" below. The page's "Never SHM with ENC" row is gone: that's Class roles' "One strong slow
per trio", which is kept only where it's free, so listing it as a Never would say it's held when it isn't; a foot line
points to it instead. Pathfinder's notes on a pair are campaign-synergies' and aren't here. The goal tile's line names
each combo once, with all the classes it takes past their goals.*

### 1. Your combos (a `.card` on Campaign)

Between the campaign card (and its fixes, when there are any) and **Your runs**. On the "no plan yet" page
(`campStart`) it sits under the ready-made goals, so a player can write "SHD / ROG / SHM 3 times" before the first plan.

- Title **Your combos**; right of it, dim: "Kept through every rebuild and goal change".
- Under it, dim: "Classes you want together or apart, or a trio to play. A Must is held above the cheapest path, at
  the price shown. A Prefer is kept wherever it costs nothing."
- A row for each combo, in the order added: a kind tag, what it is, ⋯ (`.iconbtn`, aria-label "More for SHD / ROG /
  SHM"), and the price line under it.

| Combo | Tag | What it says |
|---|---|---|
| Two classes together, every run | Must or Prefer | "PAL with MNK" `.chip` "every run" |
| Two classes together, at least once | Must or Prefer | "PAL with MNK" `.chip` "at least once" |
| A trio | Must or Prefer | "Play SHD / ROG / SHM" `.chip` "3 times" (or "once", "2 times"), then `.seggroup.sm` **Spread out** \| **Unlock now** |
| Two classes apart | Never | "WAR with BER" |
| Three classes apart | Never | "No two of WAR, BER, ROG" |

Tags (`.kind`): **Must** and **Never** gold-edged on `--gold-bg` (held above the cheapest path, like a pin, which is
gold too); **Prefer** plain, `--chipline` and `--soft`. "Every run" means the runs that earn the two classes' ranks;
past 3/3 a combo is kept only where it's free (time first).

The trio's Spread out | Unlock now works in the row: a click switches it at once, the plan rebuilds, the row keeps
its place. Unlock now is held like a Must: a Prefer trio switched to Unlock now becomes Must, and its tag says so.

**The price line** (`.price`, 12px `--muted`; the engine gives every number, campaign.js draws the words). Runs first,
then XP to level, then when classes and Completionist land, the order Swap… already uses (`campCost`):

| State | Words |
|---|---|
| Held, free (together) | "Free. Together in runs 11, 14 and 16." |
| Held, free (trio, spread out) | "Free: runs 10, 13 and 16. **Unlock now** would play them back to back, runs 10–12: the same runs and XP to level, but Completionist 2 comes at run 14, not 11." |
| Held, trio unlocked now | "Unlock now: runs 10–12, back to back. The same runs and XP to level, but Completionist 2 comes at run 14, not 11. **Spread out** is free: runs 10, 13 and 16." |
| Held, free (apart) | "Free. No run has both." |
| A plan past 3/3 (more runs than the classes need) | the held line, then "Past 3/3 they sit together only where that's free: 4 of the 32 runs after." |
| Held, with a price | "+1 run: 17 runs, not 16. Runs 2 and 3 need +20% and +30% XP to level, not +10%." Only the parts that change, each with what it would be without the combo. Never "XP ×" (rule 10) |
| Prefer, partly kept | "Kept in 2 of 3 runs (9 and 15). The third would cost +1 run, so it isn't kept." |
| Prefer, not kept | "Not kept: it would cost +1 run." then `.link` **Make it a Must** |
| Every run of it past 3/3 | "PAL and MNK are 3/3 already, so it's kept only where it's free: 4 of the 32 runs left." |
| A played run broke it | "Run 3, played, had PAL without MNK. The 2 runs to go keep them together." Played runs are history, never a clash |
| A hand edit or a pin broke a Must | `.warn`: "Not held in run 12: you changed it by hand." (or "…: loadout 12 is pinned.") then `.link` **Rebuild the rest to keep it** (or **Unpin it**) and **Make it a Prefer** |
| A Prefer a hand edit broke | plain: "Not kept in run 12: you changed it by hand." |

Two Unlock now trios that share no class don't clash: the second follows the first, and its line says "Unlock now:
runs 13–15, after SHD / ROG / SHM."

- Foot, dim, while Class roles' "One strong slow per trio" is on: "Also kept where free: one strong slow per trio, from
  Class roles." (Class roles is a `.link` to the tab.)
- Then `.btn` **Add a combo** and, dim, "Or a loadout's ⋯ → Make this a combo."

**States**
- **No combos:** the title, then in place of the rows, `--muted`: "Nothing here yet. A combo is a rule you write once:
  two classes together or apart, or a trio to play once, twice or three times. Every rebuild and goal change keeps it."
  Then Add a combo.
- **Waiting** (a combo added, changed or removed, the plan rebuilding): the rows stay with their last price lines,
  faded (`.stale`), and the title gets today's `updating…` chip. A new combo's row shows at once, its price line
  "Working it out…". Never blank (rule 9).
- **Many:** every row, no cap; the list grows down. Nothing scrolls inside the card.
- **Narrow (under 1150 px):** the Spread out | Unlock now group wraps under the name; the price line keeps the full
  width under the tag. Nothing touches (rule 12).

**The ⋯ menu** (`.cmenu` in `#campPop`, as a loadout's):
- **Change…**: opens the form (2) filled in, titled "Change this combo", its button **Save combo**.
- **Make it a Must** / **Make it a Prefer** (not on a Never). On an Unlock now trio, Make it a Prefer is disabled, its
  line "Unlock now is held like a Must".
- **Remove combo** (`.danger`), its line "Pathfinder plans the cheapest way without it". It goes at once; the card
  flashes (`.flash`) "Removed "PAL with MNK". Runs you've played or pinned stay as they are."

### 2. Add a combo

![Add a combo: two classes picked, together, every run, Prefer, and what it does](.webp)

**Add a combo** opens a form inside the card, under the list (`.combo-add`, `--inset`), and the button hides. Focus
goes to the first class.

- Title **Add a combo**; right, dim: "Pick 2 or 3 classes".
- The 16 classes in the game's class order (WAR CLR PAL RNG SHD DRU MNK BRD / ROG SHM NEC WIZ MAG ENC BST BER), 8 a
  row, each a `.combo-pick` button (`aria-pressed`, gold when picked) with a small line of roles from Class roles, at
  most two, in this order: "carry" (in the player's own carries), "tank", "heals", "mez" (crowd control 1st or 2nd), "slow"
  (a counted slow). So WAR "tank", PAL "tank · heals", SHM "heals · slow", ENC "carry · mez", BRD "mez · slow"; MNK,
  ROG, WIZ and BER have none. With three picked, the other 13 are disabled. A click on a picked class unpicks it.
- **0 or 1 picked:** nothing under the classes but Cancel.
- **2 picked:** "PAL and BER:" then `.seggroup.sm` **Together** | **Apart**; with Together, **Every run** | **At least
  once** and **Must** | **Prefer**. With Apart, no strength: dim "Held like a Must."
- **3 picked:** "WAR / CLR / MNK:" then **Together** | **Apart**; with Together, "Play this trio" **1** | **2** | **3**
  "times", **Spread out** | **Unlock now**, **Must** | **Prefer**. Unlock now picks Must, and Prefer is disabled
  (title "Unlock now is held like a Must").
- Defaults: Together, Every run, 3 times, Spread out, **Prefer** (time first: a Must is the player's explicit step).
- **What it does** (`.cheap`), the price before it's added (rule 8), drawn from the engine's answer for this form:
  - Free: "**What it does:** costs nothing. Still 48 runs and the same XP to level. PAL and BER play together in runs
    11, 14 and 16, the runs that earn their ranks. Past 3/3 they sit together only where that's free."
  - A Must that costs: "**What it does:** +1 run: 17 runs, not 16. Runs 2 and 3 need +20% and +30% XP to level, not
    +10%. A Must is held above the cheapest path, at this price."
  - A Prefer that can't be kept everywhere: "**What it does:** as a Prefer it's kept in 2 of 3 runs, at no cost. As a
    Must it's kept in all 3, for +1 run."
  - Past a goal (a trio): add "It takes SHD to 3/3, past your goal of 2/3." (see 5).
  - Every run of it past 3/3: "SHD, ROG and SHM are 3/3 already, so it's kept only where it's free." Must is
    disabled, title "Past 3/3 a combo is kept only where it's free".
  - The same as a combo you have: "You have this combo already." Add combo disabled.
  - Waiting for the answer: "Working it out…", the last answer faded beside it while it comes.
- `.btn` **Cancel** and `.btn.primary` **Add combo**, right-aligned. Add combo closes the form, the row appears at the
  bottom of the list at once (Waiting, above), and focus goes to its ⋯. Cancel or Escape closes the form and focus
  goes back to Add a combo.

### 3. A clash, found while adding

![Adding WAR / CLR / MNK 3 times as a Must clashes with "PAL with MNK"](.webp)

A clash is found when the form changes, never later in the plan. It takes the place of "What it does": a `.warn` lead,
then why in plain words, then two `.btn.sm` fixes. **Add combo** is disabled (title "Settle the clash first").
Changing the form so it no longer clashes takes the box back to "What it does" at once.

| Clash | Words | Fixes |
|---|---|---|
| Two Musts need more of a class's runs than it has | "**Clashes with your combo "PAL with MNK" (Must):** MNK has 3 runs left to earn its ranks, and this trio takes all of them, so PAL would never sit with MNK." | **Add it and drop "PAL with MNK"** · **Add it as Prefer** |
| Together against a Never | "**Clashes with your combo "Never WAR with BER":** a trio can't have WAR and BER both together and apart." | **Add it and drop "Never WAR with BER"** · **Add it as Prefer** |
| A pinned loadout with runs to go | "**Clashes with your pinned loadout 12 (Half Elf · WAR / RNG / SHM):** it seats WAR in a run this combo needs." | **Add it and unpin loadout 12** · **Add it as Prefer** |

The same box, the same fixes, show for **Change…** and for **Make it a Must** (in the ⋯ menu that one opens the form
with the clash showing). A played run never clashes: what's played counts as it was.

### 4. Make this a combo (a loadout's ⋯ menu)

A new item after **Move down**: **Make this a combo**, its line "Keeps SHD / ROG / SHM through every rebuild". It
opens the form (2) with the trio picked, Together, the loadout's runs as times, Spread out, Must, and scrolls it into
view. On a pinned loadout, a dim line under What it does: "Loadout 12's pin goes: the combo keeps the trio, and
Pathfinder picks its runs and race." When a combo already keeps that trio: disabled, line "Already your combo".

### 5. The goal tile, when a combo goes past a goal

Under the Class passives pip grid, in the tile's `.psub`, after today's "0 of 16 at your goal · all by run 16": one
line for each combo that takes classes past their goals (`--muted`), then two `.link` buttons.

- One class: "SHD: your combo SHD / ROG / SHM takes it to 3/3, past your goal of 2/3, so this goal has no effect."
  **Remove the goal** · **Make it 3/3**
- More than one: "SHD and ROG: your combo SHD / ROG / SHM takes them to 3/3, past your goals of 2/3 and 1/3, so these
  goals have no effect." **Remove the goals** · **Make them 3/3**

SHD's cell shows its third pip as planned, as any planned rank past a goal does today (`gpip plan past`). The cell's
tooltip ends "; your combo SHD / ROG / SHM takes it to 3/3". Either link changes the goal at once and the line goes
(rule 9); no run changes. Muted, not amber or red: nothing is wrong, the player's own choice won (rule 21). Clicking
SHD's pips works as today. A class with no goal gets no line: its dimmed planned pips already show where the plan
takes it, and the combo's price said so when it was added.

### 6. The cheapest-path line (the campaign card)

- **No combos:** today's line.
- **Combos, all free:** "**Cheapest path with your 4 combos** (i): 39 runs to go is the fewest that reaches your goals
  (39 race ranks to earn, one a run). Your combos cost nothing." then today's sentence about classes.
- **Musts or Unlock now with a price:** "**Cheapest path with your 4 combos** (i): 17 runs to go is the fewest that
  reaches your goals and keeps your Musts. Without them it would be 16 (48 class ranks to earn, three a run)."
- **The plan is longer than that:** today's `.warn` line, with "the cheapest path with your combos" in place of "the
  cheapest path".
- "Cheapest path" stays green, as today: it's the plan judged against its floor. The (i) for cheapest gains: "Your
  Must combos and anything set to Unlock now come first, at the price they show. Everything else only where it's
  free." Help's "How Pathfinder counts" → campaign gets the same sentence.

### 7. "Your plan came across": pinned loadouts

The card (campaign-class-roles) gets one more line under **What didn't come across** when the old plan had pinned
loadouts with runs to go:
- "· Your pinned loadouts are Must combos now: SHD / ROG / SHM 3 times and WAR / CLR / RNG once. Pathfinder picks
  their runs and races, the cheapest way."
- For a pin that would clash with one before it: "· Loadout 9 (Wood Elf · MNK / SHM / MAG) stays pinned: as a combo
  it would clash with SHD / ROG / SHM."

### Colour (the draft's open check, settled)

"Free." and every price are plain text: a price is a fact about the plan with the combo against the plan without it,
and no verdict judges it (rule 21, and the line this branch adds to it). Green "Free." beside a plain "+1 run" would
also make the player's Must look like a mistake, when it's their own choice. `--warn` is only for a Must that isn't
held (a hand edit or a pin broke it) and for a clash: something the player asked for that can't happen.

### Reused and new

- **Reused:** `.card`, `.h2`, `.lbl`, `.dim`, `.muted`, `.soft`, `.warn`, `.chip`, `.seggroup.sm` (`button.on`),
  `.iconbtn`, `.cmenu` in `#campPop`, `.cheap` and `.stale`, `.btn` (`.primary`, `.sm`), `.link`, `.flash`, the
  `updating…` chip, `.psub`, `.gpip.plan.past`, `infoTip("cheapest")`.
- **New:** `.combos` (the card), `.combo-row` (tag, what, ⋯ on one line, the price under), `.kind` and `.kind.must`
  (the tag), `.combo-add` (the form's inset), `.combo-picks` and `.combo-pick` (8 a row, gold when `aria-pressed`),
  `.psub .over` (the goal tile's line). The sizes in the pictures are
  the ones to use.

### Done when (what a screenshot shows)

- [x] Beta on, new planner off: Campaign as today, no Your combos card, no new ⋯ item - seen in main-campaign-beta.png
- [x] Your combos with a Must trio set to Unlock now (its cost, and where it lands), a free Must pair with no line, a Prefer
  kept in 1 of 2, a Never, the Class roles foot line and Add a combo - seen in main-campaign-combos.png
- [x] No combos: "Nothing here yet…" and Add a combo - seen in main-campaign-combos-empty.png
- [x] No plan yet: the card under the ready-made goals - seen in main-campaign-start-combos.png
- [x] Add a combo with two classes picked: the role lines, Together, Every run, Prefer, and "What it does:" with
  what it costs, or "Still N runs to go, and no more XP to level." when it is free - seen in main-campaign-combo-add.png
- [x] Three classes picked: the other 13 disabled, "Play this trio 1 2 3 times", Spread out | Unlock now, Must - seen
  in main-campaign-combo-clash.png
- [x] The clash: the warn lead, why, "Add it and drop …", "Add it as Prefer", Add combo disabled - seen in
  main-campaign-combo-clash.png
- [x] A Must with a price: its price line and the cheapest-path line "… keeps your Musts. Without them it would be 16"
  - seen in main-campaign-combos-cost.png
- [x] A Must broken by a hand edit: the warn line with "Rebuild the rest to keep it" and "Make it a Prefer" - seen in
  main-campaign-combo-broken.png
- [x] Free combos: their rows have no price line (answer B) - seen in main-campaign-combos.png
- [x] The goal tile's line under Class passives, SHD's third pip planned, "Remove the goal" and "Make it 3/3" - seen
  in main-campaign-combos.png
- [x] A loadout's ⋯ menu with "Make this a combo" - seen in main-campaign-combo-menu.png
- [x] "Your plan came across" with the pinned loadouts' line - seen in main-campaign-across-pins.png
- [x] At 1000 px wide: the card's rows wrap, the picker keeps 8 a row, nothing touches - seen in
  main-campaign-combos-narrow.png

## Proposal

- Combos are stored in each character's new plan (campaign-planner-beta): kind, classes, together or apart, how
  often, Must or Prefer, spread out or unlock now. A plan with no combos plans exactly as the new planner without them.
  Pinned trios brought across from the old plan become Musts: this draft adds that row to the bring-across
  (campaign-planner-beta copies the pins as they are) and its "didn't come across" line, the clash.
- A Must past a goal is played as the Must says; its runs are in the combo's price, shown when it's added. The page
  data says which goals a Must goes past, so campaign.js only draws the line (engine-owns-numbers).
- The planner holds every Must in the runs that earn its classes' ranks, then finds the cheapest path inside them,
  then keeps Prefers where free (the index's "How a plan ranks", items 1 and 5). Generate, Improve, Fixes and Swap
  never break a Must; one that breaks a Prefer says so.
- A combo's price is the plan with it against the plan without it: runs, XP to level, when classes and Completionist
  land.

## Done when

Engine (`CampaignCombosTests`, `CampaignCombosPageTests`); the page's words are the designer's screenshots above.
- [x] A Must trio is played as many times as it says, at no cost where that's free -
  `AMustTrioIsPlayedAsManyTimesAsItSaysAndCostsNothingWhereItsFree`
- [x] Unlock now plays it back to back, for the same runs; any XP it costs is in its price -
  `UnlockNowPlaysTheTrioBackToBackForTheSameRunsAndXp`
- [x] A Must that costs is held above the cheapest path, and its price (+2 runs, 3 of 3) is known before it's added -
  `AMustThatCostsIsHeldAboveTheCheapestPathAndShowsItsPriceFirst`
- [x] A Prefer is kept only where free (1 of 3) and says what a Must would cost (+2) -
  `APreferIsKeptOnlyWhereFreeAndSaysWhatAMustWouldCost`
- [x] A Must pair sits together in every run that earns their ranks; past 3/3 it costs no run and no XP; "at least
  once" seats them once - `AMustPairSitsTogetherInEveryRunThatEarnsTheirRanksAndPastThreeOnlyWhereFree`
- [x] A Never (two or three classes) keeps them apart in every run - `ANeverKeepsTheClassesApartInEveryRun`
- [x] A Must past a goal wins, the goal isn't raised, and the view names the combo and the classes (none without a
  goal) - `AMustPastAGoalWinsAndTheGoalSaysSo`
- [x] Each clash is found when adding (two Musts, together against a Never, a pin with runs to go), and a Prefer never
  clashes - `AClashIsFoundWhenTheComboIsAddedWithBothWaysOut`
- [x] Swap… never offers a change that breaks a Must - `SwapNeverOffersAChangeThatBreaksAMust`
- [x] Combos are kept with the plan; a hand-edited list is cleaned; Unlock now is a Must -
  `CombosAreKeptWithThePlanAndAHandEditedListIsCleaned`
- [x] Only the new plan keeps combos; the old plan never gets one - `OnlyTheNewPlanKeepsCombos`
- [x] A pin with runs to go comes across as a Must (two pins of a trio are one combo, a clashing pin stays pinned, a
  played-out pin is history) - `APinnedLoadoutWithRunsToGoComesAcrossAsAMustCombo`
- [x] The card shows only with the new planner, above Your runs, and on the no-plan page; none: "Nothing here yet" -
  `TheCardShowsOnlyWithTheNewPlannerAndSaysWhatACombosIsWhenThereAreNone`
- [x] With the switch off or no combos, every plan is today's - the existing `CampaignTests`, `ClassRolesTests` and
  `PlannerSwitchTests` stay green (one is corrected, below)

## Edge cases

- **Nothing yet:** combos written on the no-plan page are held by the first plan. A Must with no goals still plans its
  runs (it's the player's explicit ask); a Prefer with no goals adds none.
- **Played runs:** history, never a clash and never replayed. A trio's times count its runs since the plan began; a
  Must is held only in runs that earn a rank for one of its classes, so SHD / ROG / SHM 3 times with all three at 2/3
  holds one run and the rest is a Prefer. Counts entered on Roster count the same.
- **A state the app never builds:** a combo whose classes are all 3/3 already: Must disabled in the form, held only
  where free; a test builds the ranks as Roster gives them, not by hand.
- **Same name, different thing:** "PAL with MNK" and "MNK with PAL" are one combo ("You have this combo already"); the
  same classes together and apart clash; two pins of one trio are one Must, 3 times at most.
- **Pins and combos:** a pin that breaks a Must shows the warn line (Unpin it); Make this a combo on a pinned row
  unpins it. A pin that would clash with an earlier one when brought across stays pinned, with the designer's line.
- **Starting and stopping:** an edit while the plan rebuilds keeps the rows faded; the edit saved last wins, as Class
  roles' rebuild does. A page drawn before the switch flipped never saves combos into the other plan (`planner`).
- **Start again from your old plan:** keeps the player's combos (as it keeps the carries, the user 11-19), then adds
  the old pins as Musts, dropping any that repeat or clash.
- **Old versions in the field:** older apps read only the old plan, which never holds a combo; an older build drops the
  new plans at its next save, combos with them (planner-beta's Decisions). A hand-edited list is cleaned on read.
- **The rules change:** a price is worked out each time from the plan, never stored; a goal change reprices every row.
- **Something leaving the PC:** nothing. Combos stay in settings.json; no new page name or usage key.
- **Window size:** 1000 px wide, the picker keeps 8 a row and nothing touches (the designer's narrow shot).

## For the architect's Decisions

- Where combos are stored: the tests use `CampaignPlan.Combos` (null and unwritten when none), so `Generate`, `Fixes`,
  `Options` and the page's `campaign` message carry them without new parameters. planner-beta's rule puts new fields on
  `NewCampaignPlan`; decide which, and keep the old plan's JSON unchanged either way.
- The bring-across reads pins' runs to go from `Marked` only (`SetOn` has no log). A pin whose runs the log played is
  self-limiting (a Must holds only runs that earn ranks), but say whether that's enough.
- The engine's answer for the form (`Campaign.ComboPrice`: price, runs, kept of, as a Must, past goals, clash) and for
  each row's state (held, broken by hand, broken by a pin, partly kept) and the cheapest-path line's "without them it
  would be 16": the shapes and the message that asks for a form's price while it's open (as Swap… does).
- `PlannerSwitchTests.TurningTheSwitchOnBringsAPlanAcrossAsTheOldPlannerShowedIt` asserts pins come across as pins:
  it's corrected in a commit of its own (quality.md rule 5), by answer 1.

## Not doing

- Ranked classes and races, and the race lane (campaign-first-lane); pair notes and the library (campaign-synergies).
- Role chips on run rows (campaign-run-chips). A Never for SHM with ENC: that's Class roles' one strong slow.

## Where

- `src/EqlPathfinder.Engine/Analysis/Campaign.cs`: `CampaignCombo`, `CampaignComboPrice`, `CampaignClash`,
  `CampaignPastGoal` (stubs from qa), `CampaignPlan.Combos` with `Normalized` and `Copy`, `Generate`, `Improve` and its
  `Cost`, `Floor`, `Fixes`, `Options`, `ComboPrice` (a stub), `Evaluate` (`CampaignView.PastGoals`)
- `src/EqlPathfinder.Engine/Analysis/PlannerSwitch.cs`: `SetOn` and `StartAgain` (pins → Musts), `Store` (the old plan
  never keeps combos), `NewPlannerView` (the came-across line)
- `src/EqlPathfinder.App/TrackerService.cs`: `CampaignData`, `CampaignOptions`, `RebuildCampaign`; `MainWindow` for the
  form's price message
- `src/EqlPathfinder.App/web/js/campaign.js`: a new card in `renderCampaign` and `campStart`, `campCheap`,
  `campRenderPop` (⋯), `campGoalTiles` (the line under the pip grid); `app.css` (the designer's new classes)

## Decisions

Architect, 11-19 (from 330328d). The questions engineering.md asks, then the shapes of the engine's answers, then the split.

- **What's stored: `CampaignPlan.Combos`, as the tests have it, and only ever on the new side.** An exception to
  planner-beta's "new fields on `NewCampaignPlan`", for one reason: a combo is part of the plan, as a pin is. On the
  plan, every combo edit is a plan edit through the `campaign` message the page already sends (whole plan, `planner`,
  `rebuild`), so the edit saved last wins and `App.SetCampaign`'s `wasOn` guard covers a flip; `Generate`, `Fixes`,
  `Options`, `Evaluate` and the `campaignOptions` message get them with no new parameter. On `NewCampaignPlan` it would
  need its own store and `RebuildAround`'s retry loop, as carries did. The old side never holds one: `Store` writes
  `Combos = null` on the old side, and `PlanFor` drops them with the switch off (a hand-edited old plan), so the old
  planner never plans with a combo and `Campaigns` serializes as today (`WhenWritingNull`). `Normalized` and the
  private `Copy` carry `Combos`, cleaned as the test says, an empty list written as null. Fix `NewCampaignPlan`'s
  comment ("never there") to name this exception. Local only: no table, no migration, `reread: false`, `job: false`.
- **A fuse:** `Normalized` keeps the first 32 combos (`CampaignPlan.MaxCombos`, beside `MaxLoadouts`), because each
  row's price is a rebuild without it, and a hand-edited list of hundreds would stall every rebuild. At 32 the form's
  answer says `Full` and Add combo is disabled (the designer's words, page part).
- **Who else reads it:** builds before planner-beta drop `NewCampaigns` whole (accepted there). 0.10.20–0.10.21 read
  the new plan as a `CampaignPlan` and skip the unknown `Combos` (System.Text.Json's default; nothing here sets
  `UnmappedMemberHandling`), then drop it at their next save: the same accepted loss, a hand-installed older build
  only. The NAS job and the server never see settings.json.
- **What's sent:** nothing. The form's price is an in-process message; no usage key, no page name. privacy.html stands.
- **Taking it back:** a build without combos drops them at its first save; the old plan, pins and all, is untouched,
  so Start again (or switching off) gives back every pin. Undoable.
- **Plans already brought across (0.10.21's beta) keep their pins as pins.** The conversion runs only where a plan
  comes across (`SetOn`'s copy, `StartAgain`): converting stored new plans would need a marker field and would change
  a plan the player has seen and maybe edited. A pin still holds in the new planner, and Start again converts it.
- **Pins → Musts: one pure function,** `Campaign.PinsAsCombos(old)` → (Musts, StayPinned), used by `SetOn`,
  `StartAgain` and the card's line, so nothing new is stored for the card. In loadout order:
  - "Runs to go" is `Times − Marked` (`SetOn` has no log), and it only decides converted (> 0) or history (0, stays
    pinned). That's enough because a Must counts **every run of its trio played since `Since`**: done runs of
    loadouts with that trio (`DoneCounts`, the log's and the ticked) plus off-plan reincarnations with it. So a pin
    the log played out but nobody ticked asks for nothing more.
  - A Must's `Times` is the sum of `Times` over every pin of that trio, history pins included, at most 3: their played
    runs count toward it, so leaving them out would undercount. Two pins of a trio are one combo.
  - A converted pin's loadout stays in the plan **unpinned**, not removed: its played runs keep matching the log
    (removed, they'd turn off-plan), and the next rebuild keeps its done runs and picks the rest and the race.
  - The clash, without ranks (`SetOn` brings every character across and has none): a pin stays pinned when a class it
    seats would be in more than 3 Must runs to go, counting the Musts before it. The planner's own rule (a Must holds
    only in runs that earn its classes' ranks) trims the rest once ranks are known.
  - `StartAgain` keeps `e.Plan.Combos` (as it keeps carries), then adds these Musts, dropping one equal to a kept
    combo (same classes in any order, same together or apart) or clashing with a kept one by the rule above.
  - The card's line names only converted Musts still in the new plan, and `StayPinned` loadouts still pinned.
- **How the planner holds them (`Campaign.cs`),** the index's "How a plan ranks", items 1 and 5:
  - A Must or Unlock-now trio holds in min(`Times` − played, the runs its classes still earn a rank in). A Must pair
    "every run" holds in every run that seats either class while either earns; "at least once", one run. A Never holds
    in every run. Past that each is a Prefer. `Generate` seeds the Must runs as it seeds pins; the greedy and `Improve`
    choose around them and never take a candidate or a trade that breaks a Must or a Never.
  - Prefers (and Musts past 3/3) are a new `Cost` term, `Kept` (runs where one holds), between `ClassSoon` and the
    quality term, and the same in `Priced`: free means no run, no XP and no class rank later.
  - One check, `Campaign.Broken(plan, …)` (which Must or Never a plan breaks, and in which run), is the only test of
    "held": `Improve`, `Fixes` and `Options` drop every candidate it flags, and the rows' state reads it.
  - Spread out lets the greedy place the trio's runs; Unlock now puts them in consecutive run numbers (one loadout or
    adjacent ones), and a second Unlock-now trio that shares no class follows the first.
- **The engine's answers.** Append to `CampaignComboPrice` after qa's seven, so the tests stand: `Other` (a trio's
  price in the other placement, Spread out ↔ Unlock now; its own `Other` and `Clash` null); `XpRuns`
  (`CampaignRunXp(int N, double With, double Without)`, the runs whose XP to level differs); `Lands`
  (`CampaignLanding(string What, int? With, int? Without)`, "completionist:2" or "classes", only those that move);
  `PastKept` and `PastOf` (past-3/3 runs it sits in, of the runs after); `AllPast` (every run past 3/3: Must
  disabled); `Same` (the index of an equal combo); `After` (the Unlock-now trio it follows); `Full`. A price is a
  rebuild with the combo against a rebuild without it, from the plan's pins and done runs.
  - **The form:** a new message `comboPrice` `{ value: plan, combo, index, n }` (`index`: the combo being changed, left
    out of the plan first; null when adding). `MainWindow` answers `{ kind: "comboPrice", n, price }` through
    `svc.Query(s => s.ComboPrice(plan, combo, index, roles))`, as `SendCampaignOptions` does, with a null price when
    the switch is off. The page draws only the answer whose `n` is its latest.
  - **The rows:** `CampaignView.ComboRows` (null with no combos): `CampaignComboRow(int Index, CampaignComboPrice
    Price, string State, int? Run, int? Loadout, IReadOnlyList<int> PlayedBroke)`. `State` is "held", "partial",
    "notKept", "hand" or "pin" (`Run`: where it isn't held; `Loadout`: the pin). Hand or pin is read from the stored
    plan at view time (the breaking run's loadout is pinned or not); nothing marks an edit.
  - **The cheapest-path line:** `CampaignView.ComboFloor` (int?), the runs a rebuild with the combos gives, set when a
    Must or Unlock now exists. "Without them it would be" is today's `Floor` (Runs, Why), unchanged. The warn line
    compares `Left` with `ComboFloor ?? Floor.Runs`.
  - **The goal tile:** `CampaignView.PastGoals`, set by `Evaluate`: each Must (or Unlock-now) combo's classes that have
    a goal above 0 and whose `AtEnd` passes it. The goal is never written.
  - **Cost:** `ComboRows` and `ComboFloor` are built in `TrackerService.CampaignData` and cached as `_fixCache` is (the
    plan's JSON, the ranks key, the events' count), so a refresh never rebuilds. About two rebuilds a combo.
- **Two things at once:** a combo edit is a plan edit (above). A form's price that arrives after the form changed is
  dropped by `n`; one asked on one side and answered after a flip is null.
- **Where the engine ends and the page starts** (one branch, finished once, so Musts from pins never ship without
  their card):
  - **Engine, first engineer:** `Campaign.cs`, `PlannerSwitch.cs` (the card's line in `NewPlannerView.Of`),
    `TrackerService` (`CampaignData`, `ComboPrice`), `MainWindow`'s `comboPrice` case. Done when every
    `CampaignCombosTests` test passes, the `PlannerSwitchTests` correction is its own commit (pins with runs to go come
    across unpinned with their Must; the rest as shown), and `CampaignTests`, `ClassRolesTests` and
    `PlannerSwitchTests` are green. Also move qa's `ComboPrice` stub off `Options`' doc comment, which it split.
  - **Page, second engineer:** `campaign.js` (the card, the form, the clash, the ⋯ items, the goal tile's line, the
    cheapest-path line, Help's sentence), `app.css`, `CampaignCombosPageTests`, the renders the designer's Done when
    lists, and the release note. It draws the shapes above and adds no "if" about the plan.

## Reviews
- architect: 11-19, two rounds. First (e31beae): four must fixes, all fixed (52431e8, 210fc72), each with a test seen
  failing first or reading a field that didn't exist: a Prefer trio played once read "changed by hand" (`KeptOf`
  shared by the row and the rebuild); a later Must could come between an Unlock-now trio's runs; `Generate` re-planned
  a Must trio played outside the plan (`off`); "free" and "do the Musts cost" were decided in the page (the engine
  sends `Free`, `CombosCost`, totals and the clash's class). Should fix, all fixed: `Other` reuses `now`, the form
  driven in Chrome (Escape closes a ⋯ menu first). Second (210fc72): M3 still open in `Fixes`, fixed with a test
  through `Fixes` (a2d1780); `CombosCost` now uses `Free`'s test, `ComboCostWhat` words the cheapest-path tail,
  `PinsBecameCombos` replaces matching the pin line's text. The lead read the last fix; no third review (nothing
  changed what's stored or sent). Found in the shots by the fix engineers, both with a test that failed first:
  `Generate`'s fallback could seat a Never's classes together (120-state sweep), and a Must written before the plan
  had runs read "changed by hand". Debt: e31beae (`Generate`'s combo rules are closures).
- designer: 11-19, two rounds. First: eight must fixes, all fixed: run numbers in prices 2 low, goals already 3/3
  named past their goal, XP moved between runs read as a price (the user: "free" is the same total runs and XP, and a
  free row says nothing, "why do we need the comment at all?", now a rule in design.md), Prefer wording on a Prefer,
  runs listed that weren't counted, "cost nothing" beside a cost, a garbled price line, three shots not showing their
  state. Second (r7): two must fixes (a pair's played-run line now says what the runs to go do; a trio's later
  Completionist says it's the combo's doing) and four should fixes (a Prefer names the runs it misses and their XP,
  the cheapest-path line has its unit and singular, "Still N runs to go", a clash names what each combo needs), all
  fixed (a2d1780, 3ae3da8), shots in r8. Left out as ideas: dim Spread out / Unlock now with one run left, "When you
  build it:" on the no-plan page.
