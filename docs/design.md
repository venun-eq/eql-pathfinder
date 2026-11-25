# Design: what the app owes the player

The `designer` agent's page. It designs what players see before code is written, and checks the rendered result
afterwards (docs/roles.md). Every rule here came from something that went wrong, and its case is beside it. When the
user corrects a design decision, add the rule here in the same branch, so no session makes that mistake again.

## Who plays

Check every screen as each of these. Most design mistakes so far were right for the first player and wrong for another.

| Player | What's different for them |
|---|---|
| **The regular** | Logs every session, has months of history, shares. Every page has plenty to show |
| **New, log just turned on** | Installed today and turned `/log` on today. Almost nothing to show. A page must say something true and useful with no data, and never a zero that reads as a fact |
| **New to the app, old to the game** | Played for years before logging. The log shows no reincarnations though they have many, so their ranks are a guess until they set their counts |
| **Not sharing** | Reads the community numbers, sends nothing. None of their own play is in what they compare against |
| **Sharing, among few sharers** | Their own play is a large part of "players who share" unless it's taken out |
| **Casual** | Doesn't know the game's math: what a fragment is worth, how high to take gear. Wants the recommended answer without having to understand the question |

## Rules

1. **Say what a number is and where it comes from.** Upgrades said "2 Greater → 1 Superior" without saying motes or
   fragments. The farm rate now names its source on every page that uses it.
2. **Never show a guess as a fact.** Leave it out, or say it's a guess and how to make it right. Hours for a trio
   the player had never played were removed in 0.9.37: "it feels wrong to even guess at it".
3. **Warn only when certain.** "Your log is off" fired during patching and at character select. It took two fixes
   to reach the rule: the tracked character is in the world and its log is silent, and only then.
4. **Show partial progress, never only a count of finished things.** "0 of 16 classes at your goal", with every
   class at 1/3, read as data that hadn't synced. Show ranks earned (pips, "18 of 48 ranks") beside any such count.
5. **Count what the player has already done.** "Run 1 of 42" for a player six runs in was misleading. Played runs
   are counted and locked: "Run 7 of 48".
6. **Don't ask for what the app can find.** The "Other copies" box asked players to list duplicate items the
   inventory export already held.
7. **A question that needs game math gets a recommended answer as its default.** "How high do you take gear?"
   became "Not sure: recommended", which follows the app's own advice.
8. **The default is the cheapest way to what the player asked for, and time comes first.** In Campaign: the player's
   Must combos, then fewest runs, then least XP to level, then classes before races. A Must is the player's explicit
   choice: it's held above the cheapest path, and its price shows before it's added and in the plan. Unlock now on a
   trio, class or race is the same kind of choice (the user, 11-19: "Unlock now -> must: yes"). When one asks more of
   a class than its goal, the choice wins and the goal says so, with one click to remove or raise it: a goal is never
   raised silently, and no run is spent for it. Everything else,
   the player's preferences and Pathfinder's suggestions, acts only where it costs no run and no XP. Hand edits stay
   possible. The user, 11-19: Must combos "must be held above all else"; and on keeping PAL in every run of a 48-run
   plan, which would reach 200–300% more XP to level a run: "Time is a users most valuable commodity. Our whole motto
   is 'relevel faster', we should lean heavily towards 'time' unless the user explicitly tells us otherwise."
9. **A choice shows that it's chosen, and a click shows its result at once.** A goal the plan already met looked
   the same as no goal. While an answer is on its way, keep what was there, faded. Never blank the page.
10. **The XP penalty is more XP to level, never less XP per kill.** The bar is bigger and a kill gives what it
    always did. "+40% XP to level", never "XP ×1.4". The penalty is by level: a level needs it only for the
    reincarnations made after the class or race reached that level, so a new cap's levels need their usual XP at
    first (the user, 11-24: "Level 51-55 however, levels I've never done before, have 0% penalty"). Show the penalty
    of the level the player is at, and say when it changes along the way.
11. **Pick from what's shown before reaching for drop-downs.** The loadout builder was "a lot of drop downs".
    Goals first, then a picker for hand edits.
12. **Nothing touches.** The spin arrows sat against the number in Your motes. Controls get room, and a box whose
    value is typed selects it on click.
13. **Explain a number where the player is looking**, with an (i) beside it, and keep the long version in Help.
14. **Use the game's words.** Motes, fragments, +9, and the game's race abbreviations (HUM, BAR, ERU, ELF, HIE,
    DEF, HEF, DWF, TRL, OGR, HFL, GNM, IKS, KRN, FRG, HDE) where space is tight.
15. **Names, chat and clock times never leave the PC** (CLAUDE.md). Anything that sends or captures the screen is
    designed around that first.
16. **Home follows the character that's on, and only what's known about it.** Home showed "relevel 7, WAR/CLR/RNG
    32 / 50" while the player was on PAL/MNK/SHM at 50 (report #7): it took the first open relevel. A relevel shows
    only once the loadout on now is known and below the cap; at the cap Home opens on Farming, always; open relevels
    that aren't on are listed as parked, never shown as the one on. The cap comes from the data (50, then 55 from
    Kunark), never a literal.
17. **Campaign says "leveling" for a reincarnated run below the cap, whatever loadout you are on.** Home's "Parked"
    and "Open relevels" are about the loadout on now; Campaign's chip, "Leveling: ...", "Also leveling" and "2 leveling"
    are about the run (the designer proposed "open"; the user chose "leveling", 11-18).
18. **Compare relevels like for like, by difficulty, and never fold loot into hours.** Home said "To beat: 7.3 h" from
    a relevel played 71% at D0 to a player on a D4 motes route (report #5). The goal picks the route, the route's
    difficulty picks the relevels it's compared with ("mostly D4": over half the active hours there), in plain hours
    with T5-eq beside them; with none of that kind, say so. Net hours (loot turned into farming time saved) went to
    −116 h on 11-18, because a D4 relevel loots ten times what an hour of this player's farming does.
19. **Read what the player is doing from their play; only Ask asks what they want.** Home's Fastest | Counting motes
    switch (0.10.10) and Settings' goal were scrapped on 11-18: "that should be inferred by the behavior of the
    character through logs". Difficulties the player farms (default D3, D4), grouped play, unfinished instances and the
    cap are judged by T5-eq/h; the rest by levels/h; raids by boss kills. A visit is compared only with the same kind
    at the same difficulty: report #9's D4 rows all read "Slower place" against D0.
20. **"Typical" is the middle of your own plays; "usual" is only a verdict's baseline.** The median of your farming crawls at 50 since the patch reads "Your typical farm at 50 since the patch" on Home, Ask and the farm rate's (i) alike (the user, 11-18: "usual" there clashed with the baseline a verdict is judged against, and "median" is jargon).
21. **A colour is a verdict's.** A bar, a number or a word is green or red only when a verdict judges that very number,
    with the verdict's own usual and band; what no verdict judges is grey, with its tick. Amber is the place (Fading,
    Slower place). The overlay's bars were coloured in the App from other numbers and an amber line at "0.85 − band"
    that no verdict knew, so a bar could be green beside "24% slow" (the user, 11-19: "the bars follow the verdict").
    A price is a fact, not a verdict: "Free." and "+1 run" are plain text. `--warn` is for something the player asked
    for that can't happen or didn't: a clash, a goal the plan misses, a Must a hand edit broke (campaign-your-combos,
    11-19: a green "Free." beside a plain "+1 run" would make the player's own Must look like a mistake).

- **A line that only says all is well is noise** (the user, 11-19: "why do we need the comment at all? Even reading
  this, without context as another player, I wouldn't know what this means"). Words appear for a cost, a broken
  promise or a choice, in terms a player without context understands. A thing that costs nothing says nothing: no
  "Free.", no "Your combos cost nothing".
- **Players know their classes.** What each class brings (tank, heals, slow) isn't spelled out on every row; why the
  plan chose a trio is for a switch the player turns on, if it's built (the user, 11-19, parking campaign-run-chips:
  "I think the people using this know what roles the classes have").

22. **The player's choices are never marked wrong, and the app doesn't flag its own.** A red "falls short" on a Must
    or Prefer reads as "your choice is bad" (the user, 11-19: "a MUST or PREFER (something the user asked for)
    shouldn't come back in red saying 'your choice is bad' basically"). What the app knows goes into how it plans,
    where it costs nothing; a fault it finds in its own plan is a bug to fix, not a warning ("If it something the
    application gave, why did it if it knows the rules"). A verdict never assumes the player will play every run to
    come at today's difficulty ("What if someone doesn't want to level at D4 for these ones?").
23. **The zero state is the default, and the plan holds only what the player overrides.** A fresh plan, and one a
    ready-made goal just set, starts with every list empty: nothing ranked, no combo, no timing, so Pathfinder places
    everything the cheapest way and whatever is in a list is the player's own. A goal says how far; a rank, a timing
    or a combo is an override, added one click at a time. In 0.10.23, Everything 3/3 ranked all 16 races, because a
    race's goal and its rank were one setting (the user, 11-19: "It should start like the classes: nothing ranked,
    and Pathfinder does its thing. The rule: the zero state is the default, and the plan holds only what I
    override.").

## Every screen, in every state

Design and check each of these, for each player above that the change touches:

- **Nothing yet:** first run, or the log has no line of the kind this screen needs.
- **Too little:** below the smallest sample the number needs. Say what's there and when the number starts, as Home
  does before 6 minutes in crawls.
- **Plenty:** the usual case, and a long history where lists get long.
- **Waiting:** the first read of the logs, a re-read, an answer on its way.
- **Wrong:** offline, the server said no, a second copy has the database.
- **The window:** narrow, and at 150% display scaling. Players' reports arrive at their own pixels.

## How to look

The `app-screenshots` skill renders every page from real logs without opening a window, draws a mock-up in the real
window, and runs a page in Chrome for typing and clicks. A screenshot shows no hover or focus state. Players' own
screenshots arrive with reports (the `app-feedback` skill).

## What a design says, and what a review returns

- **A design** is the spec's "What the player sees": each state above that applies, the exact words on screen, and
  which existing parts it reuses (docs/code-map.md lists the classes in `app.css`). For a new layout, a mock-up.
  For an open question, two or three options with a pick. The user chooses between designs; the designer doesn't.
- **Every change is told from the player's side** (the user, 11-19): what they see today and what they'll see
  after, with a picture when anything on screen changes (docs/specs/README.md, "Pictures"). Code terms are for what
  players can't see, and even then a line says why it matters to them.
- **A picture is a foundation, not a contract** (the user, 11-19). A draft's wireframe or a mock-up is where the
  design starts. A better way found while designing or building is taken, with a line under the picture saying what
  changed and why.
- **A review** is a short list ranked **must fix** (breaks a rule here, or the player can't tell what happened),
  **should fix**, and **idea**. Each names the screenshot, the place on it, the rule and the fix.
