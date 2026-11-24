# The ready-made goals stay on the page, so you can change your mind

**Built 11-20 in campaign-page-layout's branch, shipped in 0.10.24** ([campaign-page-redesign](campaign-page-redesign.md)). **Built in
[campaign-page-layout](campaign-page-layout.md)'s branch** (the user, 11-20), with campaign-goal-tiles: no branch of
its own. The engine already sends the presets with every plan (`CampaignView.Presets`, cached on your ranks); this
draws them and changes what a click on one does, which may need each button's runs to count the plan's pins (the
first open check). **Built behind** the beta switch.

**Why:** the ready-made goals (Everything 3/3, Every class 3/3, Choose my own) show only on an empty plan. After one
click they're gone: the tools row keeps "✓ Every class 3/3" and "✓ Every race 3/3" toggles, with no Everything 3/3 and
no run counts, and the only way back to the choice is Start over, two clicks, which throws the plan away. The user:
"Everything 3/3 goes away once chosen, so I can't easily change my mind or pick another." On the empty plan Every class
3/3 shows twice, as a tile and in the tools row. After, the ready-made goals are always at the top of Your goals, each
with its runs, the one that matches lit; a click on another changes the goals and nothing else.
**Players:** everyone on the beta with a campaign. Worst for a new player, who picks before knowing what each costs.

```text
Your goals (made-up numbers; mock-up: page-b-full.png, top of the side column)

Today, after one click                          After
(no ready-made goals)                           Your goals
[✓ Every class 3/3] [✓ Every race 3/3]          [✓ Everything 3/3 · 39 runs] [Every class 3/3 · 7 runs] [Clear goals]
[Clear goals] More options                      More options
```

## What the user decided (11-19)

- The ready-made goals stay, so the player can change their mind or pick another.
- Rule 23: a ready-made goal sets goals only. It ranks nothing and adds no combo.

## What the player sees, in short

The designer's words and states are in [campaign-page-layout](campaign-page-layout.md#what-the-player-sees),
with this draft's Done when; the lines below are the draft's.

- **Everything 3/3 · N runs**, **Every class 3/3 · N runs**, **Clear goals**, at the top of Your goals, on every plan,
  empty or not. The one the goals match is lit (✓, gold edge); a hand-set mix lights none.
- **A click on one that isn't lit** sets those goals and rebuilds the runs still to play around your pins, played and
  leveling runs, combos, ranks and timings, as a pip click does. It is never Start over.
- **Its runs** are what the plan would have with that goal, from here: today's empty-plan wording ("39 runs from
  here"), shortened to "39 runs" on the button, the XP line in its title.
- **The empty plan** keeps "Plan every reincarnation" with the three larger tiles where the runs will be
  (page-b-empty-full.png), and Your goals has no second copy of them.

## Open checks

- Today a preset click empties the loadouts (`campClick`: `p.loadouts = []`), pins included. After, it keeps pins and
  played runs, so its runs count must be worked out the same way, or the button's number and the plan's disagree.
  `Campaign.Presets` builds from scratch today; a pinned plan needs the count with its pins.
- Clicking a lit one: nothing happens (it's a state, not a toggle). Clear goals is the way to none.
- Today's "Every race 3/3" toggle goes. A player who wants every race and no class goals sets the race pips (with
  campaign-race-goals, they're buttons).

## Where

- `src/EqlPathfinder.App/web/js/campaign.js`: `campClick`'s `data-preset`, `campPresetBtn`, `campStart`, the tools row
  in `campGoalTiles`
- `src/EqlPathfinder.Engine/Analysis/Campaign.cs`: `Presets` (the count with pins); `TrackerService.cs`: `_presetCache`
