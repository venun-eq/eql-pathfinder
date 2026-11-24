# The new planner is out of beta

**Why:** The user, 11-20: "Take the new Plan features out of beta." Everyone gets the new planner and its Class
roles tab. On old plans: "We can ignore any old plans. Maybe with a one-time notification for users that had an old
plan stating that the planner is new, and any old plans go removed. Sorry for the inconveience, and perhaps a tldr of
how to use the new ux?" The user approved the scope on 11-20 and reads the card's words before it ships.
**Players:** everyone with a plan on the old planner (most players with a plan); beta players on the new planner, who
keep their plans; players with no plan, who see no card.

## Picture

Plan → Campaign, the top of the page. Same place, size and style as today's came-across card (`campAcross`):
a text wireframe, no mock-up, because the layout doesn't change.

```text
BEFORE (beta player, switch just turned on)
┌──────────────────────────────────────────────────────────────────────────────┐
│ Your plan came across                                     [New planner (beta)]│
│ Your goals, runs, pins and the runs you've played are as you left them. ...  │
│ WHAT DIDN'T COME ACROSS                                                      │
│ · ...                                                                        │
│ [Got it] [Back to the old planner]  Your old plan stays as you left it. ...  │
└──────────────────────────────────────────────────────────────────────────────┘
 More options: │ Try the new planner (beta)   Start again from your old plan  (o─)│
 Everyone else: no Class roles tab, the old planner, More options' "prefer" toggles

AFTER (every player who had a plan on the old planner, once)
┌──────────────────────────────────────────────────────────────────────────────┐
│ Campaign has a new planner                                                   │
│ Plans made on the old planner have been removed: they don't carry over to    │
│ the new one. Sorry for the inconvenience. Your ranks and the runs you've     │
│ played are untouched, and so is any plan you made with the new planner in    │
│ the beta.                                                                    │
│ THE SHORT VERSION                                                            │
│ · Your goals: pick a ready-made goal, or click a pip for the rank you ...    │
│ · Classes you want first and Races you want first: ...                       │
│ · Your combos: ...                                                           │
│ · Class roles, the tab beside Campaign: ...                                  │
│ [Got it]                                                                     │
└──────────────────────────────────────────────────────────────────────────────┘
 More options: no switch row. Tabs for everyone: Campaign | Class roles | Roster
```

## What the player sees

**The card's words, exactly** (`section.card.camp-across`, `aria-label="The new planner"`; `.h2` title, `.muted`
paragraph, `.lbl` heading, `.muted` "· " lines, `.btn.primary` as today; no chip):

> **Campaign has a new planner**
>
> Plans made on the old planner have been removed: they don't carry over to the new one. Sorry for the
> inconvenience. Your ranks and the runs you've played are untouched, and so is any plan you made with the new
> planner in the beta.
>
> THE SHORT VERSION
> · Your goals: pick a ready-made goal, or click a pip for the rank you want. Pathfinder plans the cheapest way
>   there: the fewest runs, then the least XP to level.
> · Classes you want first and Races you want first: click one to earn it first, or Unlock now to play its runs
>   back to back.
> · Your combos: classes you want together or apart, or a trio to play. A Must is held at the price shown; a Prefer
>   is kept wherever it costs nothing.
> · Class roles, the tab beside Campaign: your carries, best first, and the survival check: each trio holds up
>   where you level.
>
> [Got it]

Every name in the short version is a label on the page today: "Your goals", "ready-made goal", "Classes you want
first", "Races you want first", "Unlock now", "Your combos", "Must", "Prefer", "Class roles", "Your carries, best
first", "The survival check". The paragraph is true for both groups: a player who never joined the beta lost only
old plans; a beta player keeps the new plan they had and loses the old one under it.

**States**
- **Had an old plan, never in the beta:** Campaign opens with the card in `.camp-head`, above the side column's Your
  goals (empty: the zero state) and "Plan every reincarnation" with its ready-made goals in the runs column.
- **Beta, on the new planner, an old plan under it:** the card sits where the came-across card does today, under the
  hero and above the fixes; their plan is as they left it.
- **More than one character with an old plan:** one card for the copy, on whichever character Campaign opens on
  first. Got it removes every character's old plan. The words name no character, so they're true on any of them.
- **No old plan anywhere** (a new player, or no plan ever made): no card. Campaign is the new planner and the Class
  roles tab shows between Campaign and Roster.
- **Got it:** the card closes at once (rule 9) and never comes back, on any character. Until Got it, it shows on
  every visit to Campaign, as today's card does.
- **Narrow, 150%:** the lines wrap inside the card's 860 px measure; Got it stays below them, as today.

**What goes** (every place a player reads the beta switch or "(beta)" today):
- More options' first row: "Try the new planner (beta)", its line "Plans trios that hold up where you level, with
  your own carries. Your plan comes with you, and turning this off gives back your old plan as you left it.", the
  switch, "Start again from your old plan", and its "Replace this plan with a fresh copy of your old one?" Replace /
  Cancel (`campSwitchRow`).
- The came-across card in both forms (`campAcross`): "Your plan came across", "Nothing to bring across yet", the
  "New planner (beta)" chip, "What didn't come across", "What came across as your ranked lists", "Back to the old
  planner", "Your old plan stays as you left it. The switch is in More options."
- The flash "That change was made before you switched planners, so it wasn't saved. Make it again here."
  (`campDropped`): there's no switch to race.
- The old planner's own parts, now no one's: More options' "Earn these first" and "Between plans that cost the
  same, prefer" toggles. Everyone gets "Between plans that cost the same" with its Class roles link.
- Help → The campaign, two sentences that describe the old planner:
  - "Between plans that cost the same, it picks what you prefer: a carry, heals, or both (More options)." becomes
    "Between plans that cost the same, each trio holds up where you level and gets a carry from Plan → Class roles,
    best first."
  - "A class you put under "Earn these first" rides along from the first run whenever that costs no extra run."
    becomes "A class or race you rank under Classes you want first or Races you want first is earned first wherever
    that's free; Unlock now plays its runs back to back, at the price shown."
- Stays: Settings' and Home's "Join the beta" (the beta update channel) and its words. Nothing new is sent.

**For the release note** (the engineer's to use):
> The new planner is out of beta. Everyone now plans on Plan → Campaign with Your goals, Classes and Races you want
> first, Your combos and the new Class roles tab. Plans made on the old planner don't carry over and have been
> removed, sorry for the inconvenience: pick a ready-made goal to start a new one. Your ranks and the
> runs you've played are untouched.

## Done when

- [x] A player with an old plan sees "Campaign has a new planner" above the start page, with the paragraph, "The
  short version", its four lines and Got it, word for word, and no chip - seen in campaign-planner-new-empty.png
- [x] A beta player sees the same card under their plan's hero, their plan as it was - seen in campaign-planner-new.png
- [x] Narrow, the card's lines wrap and Got it sits below them, nothing touching - seen in campaign-planner-new-narrow.png
- [x] More options opens with no switch row, no "Earn these first" and no prefer toggles; "Between plans that cost
  the same" links to Class roles - seen in campaign-more.png
- [x] A player with no old plan sees no card, and the Class roles tab between Campaign and Roster - seen in campaign-start.png
- [x] Help → The campaign has the two new sentences - seen in main-help.png (the designer rendered it: main-help.png stops above it)
- [x] No "(beta)", "old planner" or "came across" left in what the page scripts draw, outside this card's "old
  planner" - `AppPageTests.NoBetaSwitchOrCameAcrossWordsAreLeftOnThePage` (it also looks for "bring across", "Earn
  these first" and "Start again from your old plan", in comments too)

What's stored (`PlannerOutOfBetaTests`, on a made-up settings.json read and written as the app does):
- [x] Everyone plans on the new planner, in the beta or not, with or without a plan: the page is told the new planner
  and Class roles saves the carries - `EveryoneGetsTheNewPlannerAndClassRolesInTheBetaOrNot`
- [x] No old plan is shown or brought across: a player with only old plans opens the empty new planner, on every
  character, and opening it writes nothing - `AnOldPlanIsNeitherShownNorBroughtAcross`
- [x] A plan made in the beta is that character's plan as it was left, whether its switch was on or off and whether
  the player is still in the beta - `APlanMadeInTheBetaIsThePlayersPlanWhetherItsSwitchWasOnOrOff`
- [x] Every edit is saved to the new plan; the old plans stay as they were, and the card stays, until Got it -
  `AnEditIsSavedToTheNewPlanForEveryoneAndLeavesTheOldPlansForGotIt`
- [x] The card shows to anyone with an old plan on any character and to no one else; Got it removes every
  character's old plan and changes nothing else in settings.json (new plans, carries, Beta, the rest); after a restart
  the card doesn't come back and every character shows the same plan - `TheCardShowsToAnyoneWithAnOldPlanUntilGotItWhichRemovesEveryOldPlanAndNothingElse`
- [x] Got it in the window reaches that removal (and saves), and nothing in the window turns a switch, starts again
  or marks a came-across card - `GotItInTheWindowRemovesTheOldPlansAndNoSwitchIsLeft` (source text: the window can't load in tests)

The tests call `PlannerSwitch.PlanFor`, `Store(s, character, plan)`, `StoreRoles`, `RolesFor`, `PageInput(...).On`, and
two new ones, stubbed for now: `HasOldPlans(s)` (the card shows) and `RemoveOldPlans(s)` (Got it). The architect may
rename them; the test changes in the same commit.

## Edge cases

- **Nothing yet:** a new player, or one who never made a plan: no card, the new planner's start page, the Class roles
  tab. Class roles saves carries for them before any plan exists.
- **States from the beta:** an entry in `NewCampaigns` with no plan (Class roles or the old card's Got it made one):
  the empty new plan, never the old one. An entry whose `Card` says "across" or "empty": no came-across card, ever. A
  plan saved by 0.10.23 (`Lists` absent) still gets its one-time race list line, now for whoever has it.
- **Same name:** a new plan's key in another case than the old one's ("standin_server"): it's that character's plan.
  Got it removes old plans under every key, in any case.
- **A second copy:** a self-test or an isolated copy has its own settings.json, so its Got it never touches the real
  one. `--snapshot` plays a player who just took the tour (no old plan, no card): the card's shots need a made-up old
  plan in `DevSnapshots`, never the real settings.
- **Starting and stopping:** Got it before the tracker has started (today `plannerCard` waits for `_service`): the
  removal needs no character, so it works then. Closed between Got it and the save: the card shows once more, nothing
  lost. A carries rebuild in flight (`RebuildAround`) saves to the new plan whatever Got it did.
- **Old versions in the field:** an older app on this PC (a rollback) reads `Campaigns`: after Got it it shows an
  empty old planner, and `NewCampaigns` is as it left it. If it writes an old plan again, the card shows again on the
  next upgrade, which is right. `Beta`, `NewPlanner` and `Card` get no new meaning; the card's state is "any old plan
  stored", so it needs no new field. `First` is still written beside the ranked lists for older apps.
- **Something leaving the PC:** nothing new is sent. Usage's `beta` is the update channel and stays;
  site/privacy.html doesn't change.
- **A message that isn't the page data:** `campaignDropped` goes with the flash, from the window and from core.js's
  `onHost` both: a name one side still sends and no script reads, or the reverse, broke pages in 0.9.37 (webcheck).
- **A tab named like its group:** Class roles is always in Plan now; a stored Class roles tab opens it.
- **Window size and scaling:** the card at narrow and 150% (the designer's shot).

## Not doing

- Bringing any old plan across, Start again, the switch, "Back to the old planner" (the user, 11-20).
- Removing old plans before Got it, or at start-up: they stay in settings.json until the player has read the card.
- Changing the beta update channel ("Join the beta" in Settings and Home, usage's `beta`).
- A page test of the card's drawing: the designer's shots prove the words; the engine holds who sees it and what Got
  it removes.

## Where

- `src/EqlPathfinder.Engine/Analysis/PlannerSwitch.cs`: `PlanFor`, `Store` (both), `WithLists`, `StoreRoles`,
  `RebuildAround`, `PageInput` lose the beta gate (`IsOn`); `HasOldPlans`, `RemoveOldPlans` (stubs). Go: `SetOn`,
  `SetBeta`'s switch, `ComeAcross`, `StartAgain`, `Card`/`ShowsCard`/`CardSeen`, `PinLines`, `RankLines`,
  `AcrossLinesFor`; in `NewPlannerView`: `HasOld`, `CardLines`, `PinsBecameCombos`, `AcrossLines`.
- `Live/TrackerService.cs` ~793 (`Planner`, `NewPlanner` for everyone), `Pages/PageData.cs` (the `planner` param).
- `src/EqlPathfinder.App/App.xaml.cs`: `ApplySetting`'s `newPlanner`, `plannerStartAgain`, `plannerCard`; `SetCampaign`
  (`wasOn`), `SetRoles`, `SetBeta`. `MainWindow.xaml.cs` ~280, ~295, ~504 and `campaignDropped`. `AppSettings.cs`
  (`NewPlanner`, the note on `NewCampaigns`). `DevSnapshots.cs` ~340 and the card's shots.
- `web/js/campaign.js`: `campSwitchRow`, `campAcross`, `campDropped`, `campFlip`/`campAgain`, More options (~210).
  `core.js`: `newPlannerOn`, `tabsOf`, the Class roles fallback (~62). `campfirst.js` `firstOn`, `campcombos.js`
  `comboOn`, `classroles.js`. `index.html` Help (~423). `app.css` ~588.
- Tests that pin the beta, for the engineer to rewrite: `PlannerSwitchTests` (the switch, came-across, card and
  Start again tests; `TheWindowReadsAndSavesPlansOnlyThroughTheSwitch`'s `SetBeta`), `CampaignCombosTests` (~197-236),
  `FirstLaneStoreTests` (`Beta = true`, `SetOn`), `CampaignLayoutTests` (~17, ~181: the old planner, `planner` "old"),
  `CampaignPageTests` (~182-200: `planner` "old" or absent), `CampaignFirstLanePageEngineTests` (came-across lines),
  `PageDataTests` if the message changes.
- Docs: `docs/code-map.md` (PlannerSwitch, campaign.js, the "(beta)" on campcombos, campfirst, classroles), a line at
  the top of `docs/specs/campaign-planner-beta.md` that this spec replaced its switch.

## Decisions

- **What's stored.** `IPlannerSettings` keeps `Campaigns` and `NewCampaigns` only. `Beta` stays in `AppSettings` for
  the update channel (`App.SetBeta` sets it itself; `PlannerSwitch.SetBeta` goes). `AppSettings.NewPlanner` and
  `NewCampaignPlan.Card` go: nothing reads them, and this build drops them from settings.json at its next save. A
  hand-installed older build then reads its switch off and shows its old planner over `Campaigns`: a rollback only,
  and the card coming back after it is right (edge cases). No table, no migration: `reread: false`, `job: false`.
- **`NewCampaigns` keeps its name.** A rename means a migration in this build, and an older build drops every beta
  plan at its next save; the name is never shown. Its note in `AppSettings` and `NewCampaignPlan.Plan`'s summary
  change: null is "no plan yet", the empty new plan, never the old one.
- **Only Got it writes `Campaigns`.** `Store` writes `NewCampaigns` only (`WithoutCombos` goes). `PlanFor` is
  `Read(Entry).Plan ?? new()` and keeps its `!Ranked` line for plans saved before the lists. `HasOldPlans` is
  `Campaigns` holding any entry (null reads as none): an empty old plan was still saved by the player, and it costs one
  read of the card. `RemoveOldPlans` empties `Campaigns` and touches nothing else; the property stays, so older builds
  and `HasOldPlans` read `{}`.
- **When the old plans go: Got it only.** Not at start-up, on an edit, or when a character's first new plan is saved,
  so no player loses a plan before reading why. A player who never opens Campaign keeps them in settings.json, which
  is harmless, and the card waits for them.
- **A plan on both sides:** the `NewCampaigns` entry (any case) is the plan, whatever `Beta` and the old switch said.
  The card shows above its hero while any old plan is stored, on any character; Got it removes the old ones only.
- **The page's input.** `PageInput` never returns null. `PlannerInput` becomes `(CampaignRoles Roles, bool OldPlans)`
  plus `RaceListEmptied`; `On`, `HasOld`, `Card`, `OldPrefer`, `PinLines` and `AcrossLines` go, and so do the payload's
  `planner` string and `NewPlannerView`'s `HasOld`, `Card`, `CardLines`, `PinsBecameCombos` and `AcrossLines`.
  `NewPlannerView.OldPlans` tells the page to draw the card (rule 4: the engine decides); its words stay in
  campaign.js. `ReincarnationData` builds the new planner from `planner ?? (Pathfinder's carries, no old plans)`, so
  `--snapshot`, the tests and every payload have `newPlanner`, and the page keeps no old-planner branch: `newPlannerOn`
  goes and Class roles is always in Plan. For this, qa's `input is { On: true }` became `input is { Roles: not null }`.
- **Got it in the window:** `plannerCard` "seen" calls `RemoveOldPlans`, saves and refreshes, with no `_service` guard
  (it needs no character, so it works before the tracker starts). "back" goes.
- **The save path and the rebuild race.** With no switch there's no other side to save into. `SetCampaign` loses
  `shownNew`, `wasOn` and `CampaignDropped`, and always reads `RolesFor` and runs `WithLists`; the window stops reading
  the message's `planner`, and `campaignDropped` goes from MainWindow and core.js both (webcheck). `RebuildAround`
  loses its `IsOn` check and saves with the plain `Store`; the `Store(..., shownNew)` overload goes. Got it can't race a
  rebuild: Got it writes only `Campaigns`, a rebuild reads and writes only `NewCampaigns`, both run on the UI thread,
  and since `PlanFor` no longer falls back to the old plan, Got it doesn't change what `RebuildAround` compares.
  `GotItDuringACarriesRebuildLeavesTheRebuiltPlanSaved` pins it (added with these decisions).
- **What goes with the switch:** `IsOn`, `SetOn`, `SetBeta`, `ComeAcross`, `StartAgain`, `Card`, `ShowsCard`,
  `CardSeen`, `CardFor`, `PinLines`, `RankLines`, `AcrossLinesFor` and `PinsAreCombos`; `Campaign.PinsAsCombos` too if
  nothing else calls it. `RaceListEmptiedSeen` and `Read`'s emptied race list stay, now for everyone.
- **What's sent, and taking it back:** nothing new leaves the PC (usage's `campaign-edit`/`campaign-build` as today).
  Got it can't be undone once it ran, which the user chose (11-20). Until a player presses it, every old plan is in
  settings.json as they left it.

## Reviews

- designer: 11-20: the card matches the spec word for word in all three states (on the start page, under a plan, narrow); More options, the start page and Help are as designed. Should fix, done: the start page's 'carry and heals' sentence, the empty-card shot's goals, and the user's wording for the Class roles line. The run-row overlap on the side column is on main: debt.
- architect: 11-20: one must fix, done: a page test that Got it posts plannerCard: seen and closes the card. Should fix, done: the options/shortView shots pass the carries, and the old planner's tie-break, preferences and Earn-these-first handlers went with roles required. The delete path, a beta plan surviving and the settings.json drops are sound. Debt: the stored Prefer and the CLI's tie-break.
