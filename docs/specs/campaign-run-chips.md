# Every run row says what its trio brings

**Draft, 11-19, parked by the user** ([campaign-combos](campaign-combos.md)). Not to be built as first drafted. If it
comes back, it's an optional switch, off by default, for players who want to see each trio's roles or why the plan
chose it; the lead sizes it then, with the designer first.

## What was drafted

Each run row on Campaign would have shown chips for its roles ("tank: SHD · heals: DRU · slow: SHM · carry: MAG pet")
and its combos, and, when the player levels at a difficulty they farm for motes, a red "falls short" on a trio that
lacked ENC or BRD or two of a tank, heals, a slow and crowd control, with a free swap to fix it and "It's fine as it
is" stored in the new plan. The designer's version of this page (chip order and tones, narrow rows, the verdict and
its fix) is in this file at 896b9bf.

## Why it was parked (the user, 11-19)

- **The role chips:** "I think the role chips are a bit excessive. I think the people using this know what roles the
  classes have. That feels like something we can build later as an optional switch for users that want to know the
  roles, or why a trio was chosen."
- **The verdict on the player's own choices:** "a MUST or PREFER (something the user asked for) shouldn't come back in
  red saying 'your choice is bad' basically." (design.md rule 22)
- **The verdict on the planner's own choices:** "If it something the application gave, why did it if it knows the
  rules." The new planner already seats trios that hold up wherever it costs no run and no XP
  (`Campaign.RolesQuality`: holding up outweighs haste and carries), so a trio it chose falls short only where fixing
  it would cost, and a free fix on such a row would be the planner missing it: a planner bug, fixed in the planner.
- **One difficulty for every run:** "What if someone doesn't want to level at D4 for these ones?" The check reads the
  difficulty from the relevel on now and judges every run to come by it. That's harmless as a tie-break that costs
  nothing, and presumptuous as a red word on a row.
- **"Is this more a side effect of the original plans generated?"** Mostly, yes: turning the new planner on brings the
  old plan across as an exact copy, seated by the old rules, until something rebuilds it. That's
  [campaign-came-across-rebuild](campaign-came-across-rebuild.md).

## If it comes back

- A switch the player turns on (Class roles or Settings), off by default; with it off, the rows are as today.
- Facts only, grey (rule 21): what each class brings, and which of the player's combos a run holds.
- Never a verdict on a choice the player made (rule 22), and never one that assumes the difficulty of every run to come.
- Room after the combos for campaign-synergies' "(suggested)" chip, if that's built.
