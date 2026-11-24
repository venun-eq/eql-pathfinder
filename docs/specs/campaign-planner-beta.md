> Replaced 11-20: the switch, the bring-across and Start again were taken out when the new planner left the beta ([planner-out-of-beta](planner-out-of-beta.md)).

# The new planner is opt-in until it's ready, and your plan comes with you

**Built 11-19: the hidden switch, the new plan stored beside the old, the bring-across and the way back**
([campaign-combos](campaign-combos.md)). **Tier:** risky: a second plan stored beside the first. No designer: nothing
on screen changes; campaign-class-roles shows the switch and the card, with its designer. **Unblocks:** every other
campaign draft, each built behind this switch.

**Why:** the user, 11-19: "I would like to put this behind beta. Or a feature flag. It's important that users that are
currently using the 'Plan' feature to some extent are not impacted. And we have a clear path to migrate them." Today
whatever is finished ships to every player within the hour, so a half-built planner would change everyone's Campaign
at once. After, a player in the beta turns on "Try the new planner" on Campaign and their plan comes across;
turning it off gives back the old plan exactly as they left it. Everyone else sees no change until it's ready.
In this branch players see no change, beta or not: the switch is stored, not shown, and brings today's plan across.
**Players:** everyone with a campaign. Worst for a player 20 runs into a plan with pins and hand edits, who must find
it as they left it.

```text
Plan → Campaign, a player in the beta (a made-up plan: 16 runs, runs 1–3 and 9 pinned)

In this branch, switch on or off: the same page as today, from the same plan (on: an exact copy of it)
[Class passives] [Race passives] [Compl.]
Run 1  IKS  SHD / ROG / SHM   pinned
Run 2  IKS  SHD / ROG / SHM   pinned
…
More options                                   (no switch here yet: campaign-class-roles adds it, and the card)

Later, the first time the switch is on (campaign-class-roles, your-combos, first-lane each add their lines)
⟦Your plan came across. Runs 1–3 (SHD / ROG / SHM, pinned) are now the combo "SHD / ROG / SHM, 3 times" (Must).⟧
⟦· Run 7 was edited by hand, not pinned: it may change. Make it a combo⟧   ⟦Got it   Back to the old planner⟧
```

## Done when
- [x] Today's settings.json, read and saved with the switch off, then on with both characters' new plans edited, then
  off again, keeps every field of every old plan exactly as a plain read and save does - `TodaysPlansComeBackExactlyAfterTheSwitchIsTurnedOnThenOffAgain`
- [x] Turning it on brings each character's plan across as Campaign showed it (pins, First, Prefer, goals, `Since`,
  ticked-off runs), kept as that character's new plan; the old ones untouched - `TurningTheSwitchOnBringsAPlanAcrossAsTheOldPlannerShowedIt`
- [x] An edit with the switch on goes only to the new plan, one with it off only to the old; each is there as left when
  the switch comes back, and a plan is brought across once - `EachPlanIsKeptWhileTheOtherIsInUse`
- [x] "Start again from your old plan" replaces the new plan with a fresh copy of the old, which it never changes - `StartingAgainCopiesTheOldPlanOverTheNewOne`
- [x] The switch is read only in the beta, can't be turned on outside it, and leaving the beta turns it off; back in,
  it's off until turned on, and the new plan is as left - `TheSwitchIsReadOnlyInTheBetaAndLeavingTheBetaTurnsItOff`
- [x] A character with no plan has nothing brought across and nothing stored; a plan made with the switch on is only
  a new plan - `ACharacterWithNoPlanHasNothingBroughtAcrossAndItsNewPlanStaysNew`
- [x] A run being played (and runs played since the plan began) keeps its place and state when the switch turns on - `ARunBeingPlayedWhenTheSwitchTurnsOnKeepsItsPlace`
- [x] The window reads, saves and leaves the beta only through the switch's operations - `TheWindowReadsAndSavesPlansOnlyThroughTheSwitch`
- [x] Campaign.Evaluate's `l = l!;` is `l = ls[i];` (docs/debt.md's row goes): the engine builds with no CS1717 or
  CS8602 in Campaign.cs - `dotnet build src/EqlPathfinder.Engine` output

## Edge cases
- **Nothing yet:** a character with no plan gets an empty plan and nothing stored ("Nothing to bring across yet" is
  campaign-class-roles' card). Its old plan, made later with the switch off, comes across the next time it's on.
- **Play the log never saw:** runs ticked off by hand (`Marked`) and `Since` come across, so the same runs read done.
- **Starting and stopping:** a run being played when the switch turns on stays "leveling" in the same place. Both
  plans are in the one settings.json, saved whole as today (written aside, then moved), so a close mid-save loses
  neither.
- **A hand-edited settings.json:** a bad old plan (a class that isn't one, rank 7, a two-class loadout) comes across
  as Campaign showed it, normalized, and stays in the file as written. `NewPlanner` true without `Beta` is off.
- **Same name, different thing:** a character's plans are matched ignoring case, as today; a save under another case
  replaces neither plan's other copy.
- **A second copy:** a copy in `EQLPATHFINDER_DATA` keeps its switch and new plans in its own settings.json; a copied
  file with the switch on is on only in the copy, and the real file is never written.
- **Old versions in the field:** an older app reads the file without the new fields and its next save drops them.
  The old plan is intact, and the new one is brought across again. `Campaigns` keeps its meaning; fields are added.
- **Something leaving the PC:** nothing new is sent; the usage counts stay as they are.

## The bring-across: an exact copy here, a row for each later draft
| Today's plan | In this branch | Changed by |
|---|---|---|
| Pinned rows, their races | Pinned, as they are | campaign-your-combos: a Must "Play this trio N times"; the race ranked |
| `First`, race goal pips | As they are | campaign-first-lane: the top of the ranked lists, 3/3 goals before 2/3 |
| `Prefer` (carry, heals) | As it is | campaign-class-roles: the carries at their default; heals, the survival check |
| Class goals, `Locked`, ticked-off runs, `Since` | As they are | Nobody: they stay |

Each later draft adds its "didn't come across" line with its row, on campaign-class-roles' card.

## Not doing
- The switch on screen, the "Your plan came across" card and its "didn't come across" lines: campaign-class-roles,
  with its designer. The page isn't told the switch's state until then.
- A usage count that says the new planner is on: it would change what's sent and site/privacy.html. A later branch
  before graduation adds it if wanted.
- Graduation, and how long the way back stays after it: decided then, not here.

## Where
- `src/EqlPathfinder.Engine/Analysis/PlannerSwitch.cs` (qa's stub): `IPlannerSettings`, `NewCampaignPlan` and
  `PlannerSwitch` (`IsOn`, `SetOn`, `SetBeta`, `PlanFor`, `Store`, `StartAgain`). The names and the stored shape are
  the architect's; the tests read only these operations and whether `NewCampaigns` has a character.
- `src/EqlPathfinder.App/AppSettings.cs`: implements `IPlannerSettings` (`NewPlanner`, `NewCampaigns` beside
  `Campaigns`); `CampaignFor` goes to `PlannerSwitch.PlanFor`.
- `src/EqlPathfinder.App/MainWindow.xaml.cs` `Refresh` (~line 462): the plan from `PlannerSwitch.PlanFor`.
- `src/EqlPathfinder.App/App.xaml.cs`: `SetCampaign` (~line 754) saves through `Store`; `SetBeta` (~line 879) through
  `PlannerSwitch.SetBeta`; a `newPlanner` case in `ApplySetting` (no script posts it yet).
- `src/EqlPathfinder.Engine/Analysis/Campaign.cs` ~line 581: the debt row. `docs/releases/next/campaign-planner-beta.md`.

## Decisions
- **What's stored: a wrapper beside the old plan.** settings.json gains `"NewPlanner": false` and
  `"NewCampaigns": { "<Name_server>": { "Plan": { ...a CampaignPlan... } } }`: `NewCampaignPlan` has one property,
  `Plan`, a `CampaignPlan`. Later drafts add their fields (Musts, combos) to `NewCampaignPlan`, never to `CampaignPlan`,
  which `Campaigns` and every older app read. A draft's field that's absent means "brought across before that draft",
  which is how its conversion finds what to convert; no version number until a draft can't tell from its own field.
  Local only: no table, no migration, no re-read (`reread: false`, `job: false`).
- **The bring-across happens when the switch turns on.** `SetOn(true)` (in the beta only) copies `old.Normalized()`
  for every key in `Campaigns` with no match ignoring case in `NewCampaigns`, taking the first of duplicate keys as
  `PlanFor` does. `Normalized()` builds new lists and loadouts, so the copy shares nothing with the old plan. `PlanFor`
  stays a read that never writes: on, it gives the new plan's `Plan.Normalized()`, or, if a hand-edited file has none
  for a character with an old plan, the old plan normalized, which the first `Store` keeps. Not on first read: the
  window refreshes often and doesn't save after a read, so a copy made there would be made again until an edit saved it.
- **Each operation writes one side.** `IsOn` is `Beta && NewPlanner`. `Store` writes `NewCampaigns` when on and
  `Campaigns` when off, removing other-case keys on that side only. `StartAgain` replaces the new plan with
  `old.Normalized()` (removes it when there's no old plan). `SetBeta` sets `Beta` and clears `NewPlanner` both ways,
  so the switch is only ever on after a `SetOn` inside the beta, a hand-edited `NewPlanner` included. Nothing deletes
  `NewCampaigns`: leaving the beta keeps the new plans for coming back.
- **Two things at once:** `App.SetCampaign` awaits a rebuild before it saves. It notes `PlannerSwitch.IsOn` before the
  await and drops the save (and refreshes) if it changed by then, so a plan read from one side is never stored in the
  other. Unreachable in this branch (nothing posts `newPlanner`), but it's the user's constraint, so it goes in now.
- **Older apps: accepted, as the edge case says.** `AppSettings` has no `[JsonExtensionData]`, so every build before
  this one drops `NewPlanner` and `NewCampaigns` at its next save. That takes a hand-installed older build (Velopack
  never moves back, and leaving the beta keeps the build); the old plan is untouched, which is what the user asked for;
  the switch then reads off, and turning it on brings the plan across again. A beta player loses only new-plan edits.
- **What's sent, and limits:** nothing new. Usage keeps `campaign-edit`/`campaign-build` as today, whichever plan is
  edited; no settings field leaves the PC. A copy on `EQLPATHFINDER_DATA` has its own settings.json.
- **Taking it back:** a later build without the fields drops them at its first save, and every old plan is as the
  player left it. Graduating is the same bring-across for every player who never turned it on, decided then.

## Reviews
- architect: 11-19 (b0b6250), no must fix. Switch off, every read and save is today's (`PlanFor` is the old
  `CampaignFor`, `Store` the old save); settings.json only gains `"NewPlanner": false, "NewCampaigns": {}`, which older
  apps ignore. Tests unchanged since 3f7abc2 and each fails if its side breaks; the engine rebuilds (`--no-incremental`)
  with 0 warnings. For campaign-class-roles, when something posts `newPlanner`: the `wasOn` guard only covers the
  rebuild's await. A `campaign` edit from a page drawn before the switch flipped still lands on the other side (switch
  just turned off: the new plan's edit overwrites the old plan). Drop a `campaign` message read from the other side
  (the page echoes the side it was sent, or edits are ignored until the refresh after a flip), with a test.
