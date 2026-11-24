# Home sees relevels through your goal

## Why

Feedback report #5 (0.10.3, Home): the goal is motes in Settings and in Ask, the route is the mote route, and yet
Home says "To beat: 7.3 h". "That's a measurement of speed, not mote farming. When our goals are set to a specific
thing, the game should holistically view it through that lens." And: the 7.3 h was a relevel played almost all at D0,
"the fastest I could do it. Now, with my goal set to motes, I'm doing D4. Is it a fair comparison? ... Should
comparisons only be made to equivalent runs?" Also wanted: a quicker way to change the goal, since goals change within
a session.

Decided with the user (option A, 11-18): **like for like.** The goal picks Ask's route; the route's difficulty picks
which relevels Home compares with. A relevel is compared only with relevels played mostly at the same difficulty, in
plain hours as played (re-figured for the XP penalty as today), with their T5-eq beside them. With none of that kind
yet, Home says so; it never falls back to another kind. No number depends on the farm rate.

Tried first and dropped: net hours (active hours minus the farming time the loot saves at the farm rate, Ask's "Count
motes too" rule). At this player's farm rate, 4.9 T5-eq/h, a D4 relevel loots about 50 T5-eq an hour, so every relevel
came out 48 to 116 hours below zero ("To beat: −116 h").

## Like for like, by difficulty

- **Where a relevel's hours went:** its active hours in crawls at each difficulty (D0 to D4), out of all its active
  hours. Open world and other instances count in the whole but are no difficulty.
- **Mostly Dn:** more than half of its active hours were in crawls at Dn. At most one difficulty can be.
- **Mixed:** no difficulty has more than half.
- **The route's difficulty:** the same rule on Ask's route, by each leg's minutes (the leg's place name carries its
  difficulty, as `askSegs` reads it; open-world legs and gaps carry none). The route Home has: from the level now to
  the cap while leveling, from 1 to the cap at the cap (AskPage's default, the next relevel's).
- **Comparable:** the finished relevels with the same word as the route (mostly D4 with mostly D4, mixed with mixed).
  "Your best" is the fastest of those after the XP re-figuring, "typical" their middle, "the middle half" their band.

On the player's six relevels (`--snapshot` of a copy of their database, 11-18):

| Relevel | Rank | Active h | T5-eq | Where the hours went | Word |
|---|---|---|---|---|---|
| SHD/ROG/BER | 2 | 7.3 | 39 | D0 71%, D1 19%, D2 8%, open world 2% | mostly D0 |
| RNG/DRU/BRD | 1 | 9.3 | 461 | D4 93%, D1 4%, D2 3% | mostly D4 |
| PAL/MNK/ENC | 1 | 9.7 | 629 | D4 83%, D2 10%, open world 6% | mostly D4 |
| NEC/MAG/BST | 1 | 10.2 | 493 | D4 94%, D1 5% | mostly D4 |
| SHD/SHM/WIZ | 1 | 10.8 | 296 | D4 66%, D2 20%, D1 10%, D3 3% | mostly D4 |
| WAR/CLR/ROG | 1 | 11.2 | 478 | D4 92%, D2 4%, open world 4% | mostly D4 |

None is mixed; the closest is SHD/SHM/WIZ at 66%. Their motes route (report #5's: Befallen D1, then D4 to 50) is
mostly D4, so the best becomes RNG/DRU/BRD, 9.3 h · 461 T5-eq. On a mostly-D0 Fastest route it stays SHD/ROG/BER,
7.3 h · 39 T5-eq.

## What the player sees

Mock-up, between relevels: both switch positions and nothing to beat, on the player's real relevels (route numbers
made up). Path in the designer's report: `mock-goal-on-home.png` in its scratchpad.

### The goal switch, wherever Home draws Ask's route

A `.seggroup` of two, **Fastest** | **Counting motes**, on Next relevel, Leveling now and Leveling (below the cap, no
relevel). It is Settings → "Your goal" (`data.settings.goal`, the `"goal"` setting): the one Home's route already
follows (AskPage: anything but `speed` counts motes). So the overlay and Ask's questions that have no pick of their own
follow it too. Ask's per-question "What counts" is untouched; merging the two is not this branch.

- **Fastest** is on when the goal is `speed`; **Counting motes** when it's `balanced`, `motes` or `rare`.
- Clicking Fastest sets `speed`. Clicking Counting motes sets back the motes-counting goal the player had before
  Fastest in this run of the app, or `motes` when there was none. A click on the one that's on does nothing.
- Under it at full width (beside it when Leveling now has room), dim: `Your goal: Most motes` (the `GOAL_NAMES` word), so the link to Settings is plain.
- **Waiting (rule 9):** the click turns the new button on at once. The route strip, the route's numbers and every
  number compared with your best (To beat, the pace line, splits, race, Your relevels) get `.stale` until the data
  message with the new route arrives. Never blank.

### Next relevel (at the cap, between relevels)

The route row replaces "Ask's route, counting motes / hover a stop for its time":

`ASK'S ROUTE  [Fastest | Counting motes]  ≈ 8.4 h · 452 T5-eq (i) · mostly D4 (i)`, and under it, dim: `Your goal: Most motes · hover a stop for its time`

- Hours and T5-eq are the route's at this trio's XP (the strip's own scale), with `≈` as Ask writes it. With gaps:
  `≈ 8.4 h for 45 of 49 levels`, as Ask does.
- The first (i) is the existing `t5eq`; the second is new, `likeforlike` (below).
- Mixed route: `· mixed (i)`. No route (no legs): the row has the switch and `Ask has no route to 50 yet.`, and there
  is no To beat line.

The To beat line:

- A comparable best: `To beat: 9.3 h · 461 T5-eq (RNG/DRU/BRD, mostly D4), which is ≈ 10.2 h at this trio's +20% XP to
  level` (the "which is" part only when the XP differs, as today).
- None comparable: `Nothing to beat at D2 yet: none of your 6 relevels was mostly D2.` Mixed route:
  `Nothing to beat on a mixed route yet: none of your 6 relevels was mixed.`
- No finished relevels (new player, or old to the game and new to the app): no To beat line, as today.

### Your relevels (below Next relevel)

- Header: `Mostly D4, like Ask's route: best 9.3 h · typical 10.2 h`, in hours as played, the same hours as the bars.
  (Today's header re-figures to the latest relevel's XP while the bars don't: it would say 10.2 beside a 9.3 bar.)
  Mixed route: `Mixed, like Ask's route: …`. None comparable: `None mostly D2 like Ask's route yet`.
- Sub line: `One bar per relevel, as long as its active hours, coloured by where the hours went. Faded: mostly at
  another difficulty, so not compared.` (keep today's ": your best and the latest five" when it applies).
- Each row: trio · bar · `mostly D4` (dim) · `461 T5-eq` (muted, right) · `9.3 h`. `.timebar`'s columns become
  `112px minmax(0, 1fr) 70px 78px 50px`. A row that isn't comparable is at `opacity: .5` (a new modifier, e.g.
  `.timebar.other`). The ★ and gold go to the comparable best; with none comparable, no ★.
- Rows shown: the comparable best, then the latest five, as today.

### Leveling now (a relevel on)

- Under the strip, right side, two lines right-aligned: the switch, then `dashed = Ask's route from here · mostly D4 (i)`.
- Pace line: `12 min behind your best at D4` / `… ahead of your best at D4` / `Even with your best at D4`.
- Sentence: `Your best at D4, RNG/DRU/BRD, reached 34 after 3.6 h needing +10% XP to level: about 3.9 h at your +20% XP
  to level. Typical at D4: 4.4 h.` (numbers illustrative)
- None comparable: no delta (the kill-speed chip stays: it is already like for like by place), and the sentence is
  `Nothing to race at D0 yet: none of your 6 relevels was mostly D0.`
- **Splits** card: title `Splits against your best at D4`, sub `Active hours to reach each level · best at D4 is
  RNG/DRU/BRD (9.3 h · 461 T5-eq)`. None comparable: no card, as today with no best.
- **Race** card: title `The race at D4, all at your +20% XP to level`. Legend `Your best at D4: RNG/DRU/BRD 9.3 h`,
  `Middle half of your 5 relevels at D4` (only with 2 or more, as today), gap `2.1 h ahead of your D4 best at 46`. Foot
  note `Your D4 relevels are re-figured for this trio's +20% XP to level. History → Relevels has their hours as played.`
  None comparable: you and your projection only, and the legend adds `No relevel of yours mostly at D0 to race yet`.

### Farming (at the cap)

"When you're done": `Relevels: best at D4 9.3 h, 6 done` (the route is the next relevel's, 1 to the cap). None
comparable: `Relevels: 6 done, none mostly D0 like Ask's route`. No route: `Relevels: 6 done`.

### The (i) `likeforlike` (core.js), and Help

Title `Mostly D4` (the word it sits beside). Body: "More than half of a relevel's active hours were in crawls at that
difficulty. Home compares a relevel only with yours played mostly at the same difficulty as Ask's route: a D4 relevel
loots far more and levels slower than a D0 one, so their hours don't compare. When no difficulty has more than half,
the relevel is mixed, and compares only with other mixed ones. Fastest or Counting motes changes the route, and with
it what you're compared with." Help → "Am I going at a proper speed?" (`help-pace`) gets the same as one paragraph.

### What stays

History → Relevels (its table, ★ and race) compares every relevel as today: not this branch. The XP-to-level
re-figuring stays wherever it is. Ask's page is unchanged.

### The window

At 1150px and below the route row wraps the same way: the switch and the numbers on one line, `Your goal: …` under them. Your
relevels keeps all five columns; the trio column already ellipses.

## Done when

- [x] Next relevel on a motes goal: Counting motes on, `≈ … h · … T5-eq · mostly D4`, `Your goal: Most motes`, To beat names RNG/DRU/BRD with its T5-eq and "mostly D4" - seen in main-home-goal-motes.png
- [x] Your relevels: header "Mostly D4, like Ask's route", each row's "mostly Dn" and T5-eq, SHD/ROG/BER faded, ★ on RNG/DRU/BRD - seen in main-home-goal-motes.png
- [x] Next relevel after Fastest: Fastest on, the route's own difficulty, To beat from relevels of that difficulty only - seen in main-home-goal-fast.png
- [x] Nothing comparable: "Nothing to beat at Dn yet: none of your 6 relevels was mostly Dn." and no ★ - seen in main-home-goal-none.png
- [x] Waiting: after a click, the new button on and the route and To beat faded, nothing blank - seen in main-home-goal-wait.png
- [x] Leveling now: the switch under the strip, "behind/ahead of your best at D4", the sentence naming the D4 best - seen in main-home.png
- [x] Splits titled "at D4" with the best's hours and T5-eq; race titled "The race at D4", legend "Your best at D4" - seen in main-home.png
- [x] The `likeforlike` (i) open - seen in main-home-goal-info.png
- [x] Farming's "When you're done" says "best at D4" - proved by FarmingsWhenYoureDoneSaysBestAtTheRoutesDifficulty

## Edge cases

- **Nothing yet / too little:** no finished relevel: no To beat line, no Your relevels card, as today. A route with no
  legs: no word, the switch stays, `Ask has no route to 50 yet.` and no To beat. A relevel on with no route: it is
  compared with relevels of its own word so far, never with all of them.
- **Mixed:** `mixed` is a kind of its own; a mixed route compares with mixed relevels only. Exactly half is mixed.
- **Open world and other instances:** in the whole, never a difficulty (48% D4 plus open world is mixed).
- **Different ranks:** the XP re-figuring is per relevel as today; kinds are decided on hours as played.
- **Old apps / the data message:** `mostly` is a new field on a journey and on Ask's route, never a changed meaning.
- **Waiting (rule 9):** waiting = the route's own goal is not the goal the setting now says; no extra state.
- **A new goal setting value** (`balanced`, `rare`) counts motes, as AskPage does.
- Nothing new is sent off the PC.

## Where

- `Engine/Analysis/DifficultyMix.cs` (the word), `Journey.Mostly` (Journeys.cs), `AskRoute.Mostly` (AskPage.cs)
- `web/js/home.js` (switch, route row, To beat, Your relevels, Leveling now, splits, race, farming), `race.js`
  (`comparable(xp, kind)`, `kindWord`), `core.js` (`likeforlike` (i)), `index.html` (`help-pace`), `app.css`
- Tests: `LikeForLikeTests` (the word), `HomeGoalPageTests` (what Home draws, from the player's six relevels' shape)

## Reviews
- architect: one must fix (the route's `mostly` was proven only by reflection; a test now proves the built data message carries it) and one should fix (refresh the window when the goal changes, so Waiting ends at once), both fixed. The engine's word, the edge cases and privacy are sound.
- designer: one must fix ("Your best and the latest five" with nothing comparable, now "Your latest five.") and four should fix: Splits' unlabelled recalculated hours, units split from numbers beside the switch, and "typical" from a single relevel are fixed; the shots patched by hand are logged in docs/debt.md. The rest matches the design.
