# A plan brought across offers a rebuild with the new rules

**Dropped 11-20:** the new planner came out of beta with no plan brought across ([planner-out-of-beta](planner-out-of-beta.md)),
so there is nothing left to rebuild. Kept as the record of the idea. **Was: draft, 11-19, not started; parked by the user 11-20** ([campaign-combos](campaign-combos.md)). Found while sizing campaign-run-chips, which
the user parked. The `designer` goes first (new words on the "came across" card). **Tier:** the lead sizes it before
building; it changes what the new plan stores when the player clicks, so likely risky.
**Built behind** [campaign-planner-beta](campaign-planner-beta.md)'s switch: without it, Campaign is as today.
**Waits for:** nothing (planner-beta, class-roles and your-combos are on main).

**Why:** turning the new planner on brings each old plan across as an exact copy (`PlannerSwitch.SetOn` →
`ComeAcross`, no rebuild), so its trios were seated by the old rules, a carry and heals, and stay that way until
something rebuilds the plan: a goal change, a carries edit, Rebuild. Until then the new planner's survival tie-break,
one strong slow, haste beside a melee class and the player's carries have touched nothing, and a player who turns the
switch on sees the same trios and can fairly think the new planner changed nothing. The user, 11-19, on trios that
would fall short: "Is this more a side effect of the original plans generated?"
After, the "Your plan came across" card says what a rebuild with the new rules would change, at no cost, and offers
it. One click, never automatic: players who use Plan today aren't changed without their say (campaign-planner-beta).
**Players:** beta players who turned the new planner on with a plan already made.

```text
Plan → Campaign, the "came across" card (made-up numbers; the designer writes the words)

Today                                          After
Your plan came across        New planner (beta)  Your plan came across        New planner (beta)
<what came across, what didn't>                <what came across, what didn't>
                                               ⟦Rebuilt with the new rules, your plan keeps its 16 runs and the same⟧
                                               ⟦XP to level, and 4 trios change. Your combos stay.       Rebuild⟧
```

## Open checks

- Confirm first that nothing rebuilds a copied plan before the player edits it (`App` "newPlanner" only refreshes).
- Offered only when a rebuild changes a trio and costs no run and no XP; otherwise the card says nothing about it
  (design.md: a line that only says all is well is noise).
- Played and leveling runs stay as they are (rule 5); Musts (pins came across as Musts) and Unlock now stay.
- What the card says changes, in the player's terms, without grading the copied trios (rule 22).
- After the click: the card's line goes, and the plan saves like any rebuild (`RebuildCampaign`).

## Where

- `src/EqlPathfinder.Engine/Analysis/PlannerSwitch.cs`: `SetOn`, `ComeAcross`, the card's lines (`NewPlannerView`)
- `src/EqlPathfinder.Engine/Live/TrackerService.cs`: `RebuildCampaign`
- `src/EqlPathfinder.App/web/js/campaign.js`: the "came across" card
