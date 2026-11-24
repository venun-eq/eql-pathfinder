# The fight meter

A second overlay (`App/MeterWindow.cs`) that shows damage per second, sliced several ways, against your usual in the
same place. Its engine is `Engine/Combat/`; it reads the same logs on its own and keeps its own database
(`fights.db` in the data folder), so it doesn't depend on the crawl tracker.

## Definitions

- **Fight:** one kill. It starts with the first swing either way between you (or your pet) and the mob: a hit, a miss,
  a dodge, parry, riposte or block, or a resisted spell ("A gnoll resisted your Ignite!"). A debuff that lands writes no
  line naming the mob, so it can't start one. Group members' hits join, including the 12 seconds before you engaged. It
  ends when the mob dies and nothing hits that name for 6 seconds, when you zone, or after 8 seconds with nothing (the
  meter's "A fight ends" setting, 3 to 30 s; it was a fixed 2 minutes until 11-16, which chained same-named mobs across
  a crawl into minutes-long encounters). Measured on 10,132 kills since the 11-10 patch, the share of kills a pause
  mid-fight cuts in two: 8 s 1.5%, 5 s 3%, 3 s 13%, 2 s 27%, 1 s 63%; at 1 s or less some kills are lost outright (the
  death line arrives after the fight closed), so 3 s is the floor. 30 s keeps a short feign-death split one fight. A mob
  that never took damage isn't a fight.
- **A fight on the meter is an encounter** (11-16): every mob fought at the same time. Kills whose times overlap, or
  follow within 2 seconds of the last one ending, are one fight, timed from its first swing to its last, with
  overlapping time counted once (`MeterViews.Encounters`). Mob by mob, a pull of ten looked wrong: each mob's clock
  started when it first swung at you, so the first one you killed looked fast and the last slow. The Fight tab, the
  "by fight" list and an opened fight all use encounters, labelled "a ghoul ×3" (one name) or "a ghoul +5" (the mob
  you hit hardest and how many more). Non-stop chain pulling is one long fight until you stop: in Venun's Mistmoore D0
  on 11-16, 298 kills were 14 fights, the longest 65 mobs over 6 minutes (only 15 breaks in the whole crawl, most of them 2–13 s: even counting only mobs that overlap it would be 16 fights). "By mob" and a mob's own fights still time
  each mob from its own first swing.
- **Same-named mobs:** the log has no mob ids, so "a zombie" ×3 fought together is one fight with 3 kills. A death
  followed within 6 seconds by a hit on the same name means more of them; lists count kills, not fights.
- **Active time and kill time:** every fight keeps both. Active time is the stretches of fighting (actions within 10
  seconds of each other); kill time is first swing to the death, with feigns, mezzes and waits included. The meter leads
  with the one you choose (Settings → Overlays → "DPS counts", or a click on the other clock's line under the bars) and
  shows the other there. Your usual is worked out on both.
- **Mob or group member:** players and their pets have one-word names, but so do some mobs ("Fright", "Terror"). A name
  is a mob once you hit it or it hits you or your pet; it's a friend once it hits a known mob. Your pet hitting a friend
  (a charm that broke) and damage you do to yourself aren't fights.
- **The usual line:** on the by-fighter bars, your row has a line where your usual damage over this much time would
  reach, coloured like the percentage. The bars' scale stretches to hold it when you're behind.
- **Crawl / Zone:** the fights since you entered the zone. Leaving an instance and coming back within 10 minutes, with
  the crawl not done and no new instance made, continues the same stay (as the crawl tracker has it). Outside an
  instance the tab reads "Zone".
- **Session:** starts with the first log line after 30 or more minutes without any (a login after time away). Quick
  relogs and loadout swaps stay in the session.
- **Time in combat:** fights that overlap count their time once, so DPS for a crawl or session is damage over the time
  you were actually fighting.
- **Usual:** your damage per second (you and your pet) in the same zone and difficulty with the same trio (the app's:
  your /who, or the trio checked against the spells you cast; see docs/overlays.md), from up to 600 past fights. Recent ones weigh more (half as much per 30 days). It needs 5 fights. Runs
  before the 11-10 patch that were mostly Denon's Desperate Dirge are left out. The same per spell, and a per-fight
  figure for the mob and fight lists (where each fight's own time is the yardstick).

## Slices

Scope: **Fight · Crawl (or Zone) · Session**. By: **fighter, spell, mob, fight**. Mode: **Damage, Healing, Damage taken**.
Clicking a fighter opens their spells (hits, crit rate, biggest hit); a mob opens its fights; a fight opens that fight.
`MeterViews.Build(frame, query)` makes every view and is covered by `FightMeterTests`.

## Lines it reads beyond the crawl tracker's

| What | Example |
|---|---|
| Someone else's melee | `Goneker cleaves a gloomstalker mermaid for 137 points of damage.` |
| Someone else's spell | `Hellsfury hit a Tesch Mas Gnoll for 82 points of magic damage by Enveloping Roots.` |
| A mob's spell on you | `a Tesch Mal Gnoll hit you for 252 points of magic damage by Shock of Spikes.` |
| Heals | `You healed Venun for 871 hit points by Drain Soul.` · `Hellsfury healed you for 12 (255) hit points by Talisman of Altuna.` |

Players and their pets have one-word capitalized names; anything else hitting a mob is taken for another mob and left out
(so other players' charmed pets aren't counted). Your own pet is whoever last told you "Attacking … Master."

## Is it accurate?

`pathfinder fights --db <file> --check` compares the meter with the crawl tracker, which totals your damage per visit
by a separate path. On 3 months of logs (1,120 visits, 446 million damage) the meter's total is 100.5% of the
tracker's. The meter is higher where a new pet's hits before it first says "Attacking … Master." are counted as yours.
The check also showed the tracker counting 6.8 million that isn't damage to a mob: hits on yourself, and mobs you once
charmed (their name stays a pet name) hitting your group. `--visit "<start>" [--mob "<name>"]` shows one visit by target.

## Checking it without the game

- `pathfinder fights --db <file> [--at "2026-11-11 23:03"] [--scope place] [--by mob] [--mode healing] [--fighter Venun] [--mob "a rat"]`
  reads the logs into `<file>` the first time (about 13 seconds for 3 months of play) and prints a view as text.
- `EQLPathfinder.exe --meter-snapshot <outDir> <fights.db> "<date time>"` draws the overlay's views for that session as PNGs.

## Pets

Your pets count as you: by fighter shows one row for you and your pets ("Venun · incl. pet 39%"), and opening it lists
your spells and a line per pet. A new pet looks like any player until it first says "Attacking … Master.", so its earlier
hits in that fight, and the same name earlier in the slice, are yours too. Another player's pet can't be tied to them
(only its owner sees that line), so it stays its own row.

## Settings

The meter is one of the overlays (docs/overlays.md): Settings → Overlays, the tray's Overlays menu, and its own
right-click menu switch it, lock it, set its background and arrange it with the others.

## Not built yet

A fight history page in the main window, and pruning of old fights (the database is about 440 bytes a fight).

## Charmed pets

A summoned pet has its own name, so it's remembered. A charmed mob keeps its mob name, and the zone is full of others
like it, so `LineParser` counts it as your pet only while the charm holds (both the crawl tracker and the meter read
through it):

- **Starts** when it says "Attacking … Master." The spell you cast or sang in the 20 seconds before is learned as a charm.
- **Ends** when that charm spell wears off of it ("Your Cajole Undead spell has worn off of a revultant rat."), when one
  of that name swings at you or dies, when you die or zone, when a summoned pet speaks, on "You do not have a pet.", or
  after 2 minutes without a line from it.
- **Comes back** when it says "Attacking … Master." again, or when the charm lands on the same name within 2 minutes
  ("… has been charmed." / "…'s eyes glaze over.", a bard's song re-applying).
- **Only its own targets count:** the ones it announced ("Attacking a turmoil toad Master.") and the mobs you fight
  yourself (you hit them or they hit you), each for 3 minutes. Another of its name fighting something else, or hitting
  your group, is a wild one (or someone else's charm). On 3 months of logs this keeps 6.26 million and drops 0.2 million.
- **One-word names:** a pet with a player-style name is a charm when the charm visibly landed on that name in the 30
  seconds before ("Dread has been charmed."), or one of that name hit you earlier in the zone. A remembered pet name
  that hits you is forgotten: a pet never hits its master.
- **Restarts:** the charm state (`CharmState`: the pet, the learned charm spells, its targets) is saved with the
  checkpoint in `EngineState.Charm` and the meter's `FightContext.Charm`, so reopening the app mid-charm keeps the pet.

What the log still can't tell: a wild mob of the same name attacking one of your pet's own targets reads as your pet,
and an add your pet takes on by itself, that you never touch and it never announces, isn't counted. Engine version 5 and meter version 3 re-read history
with these rules (before them, a charmed name stayed a pet name forever: about 11 million phantom damage in 3 months
of one player's logs).
