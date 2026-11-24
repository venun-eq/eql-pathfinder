# Campaign plans trios you want to play, not only the cheapest

**Index, 11-19. Not a change of its own:** this page lists the drafts the Plan → Campaign "combos" redesign was split
into, and the order they go in. Each draft is solved in its own branch and session, by the tier it names. The design,
its mock-ups and the research behind it came from the "Campaign Combos" design notes; each draft points at its section
rather than copying it. All of
it is built behind one switch, [campaign-planner-beta](campaign-planner-beta.md), so players who use Plan today see
no change until they turn it on.

**Why:** today Campaign seats every class 3 times at the least cost, and nearly any seating costs the same, so the
trios it picks can be ones nobody wants to play: no slower where content is hard, a favourite pair split up, the
same trio the player wanted three times spread across the plan. After, the player says what they want ("SHD/SHM/ROG
3 times no matter what", "PAL and MNK together", ROG unlocked now, their carries), Pathfinder makes sure each trio can
survive where they level, and every choice that costs shows its price.
**Players:** everyone with a campaign. Worst for a player who levels at the difficulties they farm for motes, or who
has trios they love.

```text
Plan → Campaign (made-up plan; the full mock-ups are the design page's sections A to E)

Today                                          After
[Class passives] [Race passives] [Compl.]      [Class passives] [Race passives] [Compl.]
                                               ⟦Your combos: SHD / ROG / SHM 3 times (Must) · PAL with MNK (Prefer)⟧
                                               ⟦Classes you want first: ROG, Unlock now · Races you want: IKS, ERU⟧
Run 1  IKS  WAR / RNG / BER                    ⟦Round 1 · every class to 1 · +10% XP to level (made up)⟧
Run 2  ERU  SHD / ROG / SHM                    Run 1  IKS  SHD / ROG / SHM
…                                              Run 2  ERU  PAL / MNK / ENC
```

## The cause

The planner (`Campaign.Generate`, `Improve`) finds the fewest runs and least XP to level, then spends the free seating
choices on two checks only, a carry and heals (`Campaign.Quality`, `CampaignPlan.Prefer`). 16 runs for every class
3/3 is 48 seats with each class in 3. Pins hold one row, not an intent; the carries are fixed in code
(`ClassNames.Carries`). The user's points, 11-19: some classes are strongest together; weaker trios need a slower for
higher content; carries should be up to the player; slowers belong in separate trios.

## The user's decisions (11-19)

- **Must combos are held above everything,** the cheapest path included ("must be held above all else"), with their
  price shown. Prefer combos only where free.
- **Time first:** "Our whole motto is 'relevel faster', we should lean heavily towards 'time' unless the user
  explicitly tells us otherwise." Past 3/3 a combo holds only where it costs nothing (design.md rule 8).
- **Spread out is the default; Unlock now is an option** beside each trio, class and race, aiming at 3/3 (which unlocks
  the active).
- **Unlock now is held like a Must** ("Unlock now -> must: yes"): it's the player's explicit choice, held above the
  cheapest path, with its price shown before it's set.
- **A Must that asks more than a goal wins, and the goal says so:** "Basically on the SHD 2/3 goal could be 'You have
  SHD in the SHD/ROG/SHM build for unlock now, so this is ignored, you can remove it' or something like that with a
  better user experience." The goal tile says the combo takes SHD past the goal, so the goal has no effect, with one
  click to remove the goal or raise it to match. The plan never spends extra runs for the goal and never raises it
  silently (campaign-your-combos).
- **Pathfinder ranks tanks, heals, slows, crowd control (mez, charm) and haste; the player ranks carries** (BST isn't
  one; ENC is, for charm). The survival check follows the player's mote farming setting: "Follow their 'mote farming'
  settings. (default D3/D4)". It's on for relevels played at the difficulties the player farms for motes (Settings →
  How you play, D3 and D4 by default), read from play, never a new switch. A trio holds up with ENC ("Any build with
  an enchanter can get through D4 pretty easily"), or with two of tank, heals, slow and crowd control. At other
  difficulties any trio makes it. Never SHM with ENC, and ENC's runs go to the trios that would otherwise fall short.
- **Races and classes the player wants are ranked; unranked means don't care.** Plan for players who want only a few.
- **Suggestions break the ties that are left,** after carries, on by default and labeled.
- **Nothing guesses how fast a trio levels** (the 0.9.37 decision, design.md rule 2).
- **Behind beta or a flag:** "It's important that users that are currently using the 'Plan' feature to some extent are
  not impacted. And we have a clear path to migrate them." (campaign-planner-beta)

## How a plan ranks, once all of it is in

1. The player's explicit choices, each with its price: Must combos, and the trios, classes and races set to Unlock now,
   in the runs that earn their ranks (campaign-your-combos, campaign-first-lane).
2. Fewest runs.
3. Least XP to level.
4. Classes and Completionist sooner.
5. Prefer combos, combos past 3/3, then the order of the classes and races wanted first: a rank alone only breaks ties
   (campaign-your-combos, campaign-first-lane).
6. The survival check for relevels at the difficulties farmed for motes, and haste for physical DPS
   (campaign-class-roles).
7. The player's carries, in their order (campaign-class-roles).
8. Suggested Core pairs not hidden, Clash pairs counted against a trio, labeled "(suggested)" (campaign-synergies).

## The drafts, and what each waits for

| Order | Draft | Tier | Waits for |
|---|---|---|---|
| 0 | [campaign-planner-beta](campaign-planner-beta.md): the switch, the new plan stored beside the old, bringing a plan across as an exact copy, the way back; all hidden | Risky | nothing |
| 1 | [campaign-class-roles](campaign-class-roles.md): the switch and the "came across" card shown, survival ranks (crowd control among them), the player's carries, one strong slow, the survival check at the difficulties farmed for motes, haste | Risky + designer | planner-beta |
| 2 | [campaign-your-combos](campaign-your-combos.md): Must, Prefer and Never combos, prices, clashes, kept by the planner | Risky + designer | class-roles |
| – | [campaign-run-chips](campaign-run-chips.md): role chips, the survival check and its fix on each run row. **Parked by the user, 11-19:** players know their classes' roles (later, maybe, an optional switch), and a choice the player made is never marked red (design.md rule 22) | – | – |
| 3 | [campaign-came-across-rebuild](campaign-came-across-rebuild.md): a plan brought across offers a rebuild with the new rules, where it costs nothing | Lead sizes it (likely risky) + designer | your-combos |
| 3 | [campaign-first-lane](campaign-first-lane.md): ranked classes and races, each with a target and its timing: the store, the planner and the engine's words, nothing new on screen | Risky + designer | your-combos |
| 4 | [campaign-first-lane-page](campaign-first-lane-page.md): the two ranked lists on Campaign, from first-lane's design | Standard + designer | first-lane |
| 3 | [campaign-synergies](campaign-synergies.md): the sourced pair library, its cards, Add and Not for me | Risky + designer | your-combos |
| – | [campaign-rounds](campaign-rounds.md): runs grouped by round, free moves marked. **Replaced, 11-19,** by campaign-run-list in the next index, [campaign-page-redesign](campaign-page-redesign.md) | – | – |

```
campaign-planner-beta ─► campaign-class-roles ─┬─► campaign-your-combos ─┬─► campaign-first-lane ─► campaign-first-lane-page
                                               │                         ├─► campaign-synergies ··► (later) community ratings
                                               │                         └─► campaign-came-across-rebuild
                                               └─► (campaign-rounds: replaced by campaign-run-list)
```

**What comes next:** [campaign-page-redesign](campaign-page-redesign.md) (11-19) redesigns the page as a fresh player
meets it, and says where campaign-came-across-rebuild and campaign-synergies go in it. Its campaign-page-layout,
campaign-goal-presets and campaign-goal-tiles shipped in 0.10.24.

**A bug in what's built, not part of the redesign:** [campaign-run-gold](campaign-run-gold.md), "Rebuild the rest"
on a plan that's already cheapest leaves every run row blank (gold marks, dots, XP) until a combo changes the plan
(report 14). Built 11-19.

Every draft waits for planner-beta, through class-roles. A draft in one row may start while another in the same row
is open. One that waits starts after what it waits for is on main. No budgets yet: the lead gives them before the
first branch.

The design page's table named the combos piece `campaign-combos`; it's `campaign-your-combos` here, so this index keeps
the set's name. The page sized synergies standard; it stores hidden pairs and a switch and breaks the planner's ties,
so it's risky by CLAUDE.md.

## Questions before building (answered by the user, 11-19)

- **Where "Unlock now" ranks:** with the Musts, item 1, held above the cheapest path with its price; a rank alone only
  breaks ties (item 5). "Unlock now -> must: yes".
- **D1 to D3:** the survival check follows the difficulties the player farms for motes, D3 and D4 by default; the
  rest stay quiet. "Follow their 'mote farming' settings. (default D3/D4)".
- **A Must beyond a goal** (SHD / ROG / SHM 3 times with SHD's goal at 2): the combo wins, and the goal tile shows the
  clash with one click to remove the goal or raise it. No extra runs for the goal, and it's never raised silently.

## Later: community ratings

What guides and players rate each pair and trio ("guides rate this S at D4"), and from the sharing pool how many
sharers leveled a trio. Risky: what's sent. Waits for campaign-synergies, whose pairs keep an empty "rated by" field
for it. A note here only, not a draft and not a roadmap entry yet.
