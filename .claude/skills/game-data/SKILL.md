---
name: game-data
description: Where EverQuest Legends' logs and game files are, where the app keeps its data and error.log, and the command-line tool that answers questions from a log. Use when a task needs a real log line, the app's state on this PC, a reproduction of a player's bug, or a game fact such as the level a class gets a spell (from spells_us.txt, not the web).
---

# Logs, game files, the app's data

**The game:** `C:\Users\Public\Daybreak Game Company\Installed Games\EverQuest Legends\`
- **Logs:** `Logs\eqlog_<Name>_<server>.txt`, plus dated splits (`.2026-10-14.txt`). Hundreds of MB each, so search
  them with grep, or Read a range. Line formats are in docs/log-formats.md.
- **Game files the app reads:** `spells_us.txt` (which class casts what), `dbstr_us.txt` (reincarnation passives),
  the `/outputfile inventory` export `<Name>_<server>-Inventory.txt` (Upgrades), and `Logs\dbg.txt`, the client's own
  log, rewritten at each start: when a character entered the world (docs/log-formats.md, last section).
- **Not in any game file:** the passives' amounts (`#1` in dbstr ids 26000–26299, "by #1%"). The server sends them;
  `spells_us.txt` has only the rank-3 activated abilities (Thrash, Divine Wrath … ids 74128+). So the Roster says what a
  passive does without the number (`GameData.PassiveText`).
- **The app only reads here, and so do you.**

**At what level a class gets a spell:** read it from `spells_us.txt`, the one source for it, whatever a guide or a
wiki class page's summary says (docs/quality.md, "A game fact from a summary"). It's the game's own data, and one command reads it for free;
a web lookup of four classes' slows cost 181k on 11-19 and found the same levels. Fields are `^`-separated: the name
is field 2, and the 16 classes' levels are fields 37–52 (awk's 1-based count) in `ClassNames.All`'s order; 0 or 255
means the class doesn't get it (`GameData.LoadSpellClasses` reads the same columns).

```sh
cd "/c/Users/Public/Daybreak Game Company/Installed Games/EverQuest Legends"
C="WAR CLR PAL RNG SHD DRU MNK BRD ROG SHM NEC WIZ MAG ENC BST BER"
# Named spells: who gets each, at what level
awk -F'^' -v C="$C" 'BEGIN{split(C,c," ")} $2=="Togor'"'"'s Insects"||$2=="Languid Pace"{s=$2":"; for(i=1;i<=16;i++){v=$(36+i)+0; if(v>0&&v<255) s=s" "c[i]" "v} print s}' spells_us.txt
# One class's spells up to a level, lowest first (then grep the names you want)
awk -F'^' -v C="$C" -v cls=SHM -v max=50 'BEGIN{n=split(C,c," "); for(i=1;i<=n;i++) if(c[i]==cls) k=i} {v=$(36+k)+0; if(v>0&&v<=max) print v"\t"$2}' spells_us.txt | sort -n | uniq
```

The file gives levels, not a spell's strength in words: for "how much does it slow", read the spell's own eqlwiki page
(`https://eqlwiki.com/<Spell_Name>`) rather than the class page's summary, and name the spell and its level beside the
number. Slows checked this way on 11-19: SHM Drowsy 5, Walking Sleep 13, Tagar's Insects 27, Togor's Insects 38; ENC
Languid Pace 9, Tepid Deeds 23, Shiftless Deeds 41; BRD Largo's Melodic Binding 20, Selo's Consonant Chain 23; BST
Drowsy 20, Languid Pace 35, Sha's Lethargy 50.

**The app's data:** `%LocalAppData%\EQL Pathfinder\`

| File | What it is |
|---|---|
| `pathfinder.db` | Everything read from the logs (SQLite) |
| `fights.db` | The fight meter's fights |
| `settings.json` | Settings, entered reincarnation counts, campaigns, the sharing key (DPAPI) |
| `error.log` | The app's errors: check it first for a bug |
| `usage.json` | Usage counts not yet sent |
| `share-ledger.json` | What sharing has already sent |
| `community-v1.json` | The last community file |

The installed app (Velopack) is in `%LocalAppData%\EQLPathfinder\current`. To test and leave all of this as it is, use
an isolated copy (`EQLPATHFINDER_DATA`, see the [app-screenshots](../app-screenshots/SKILL.md) skill).

**The command-line tool** (`src/EqlPathfinder.Cli`, `dotnet run --project src/EqlPathfinder.Cli -- <command>`) reads
logs the app's way:

| Command | What it does |
|---|---|
| `status` (or `import`) | The tracker's view: relevels, the forecast, the campaign presets. `--hunt-check` adds the hunts' trios |
| `fights` | Fight meter checks (`--fighter`, `--mob`, `--spell`, `--visit`, `--at`) |
| `upgrade` | The Upgrades math (`--tier`, `--goal`, `--motes`, `--frags`, `--vtps`) |
| `community` | The NAS job's build of the shared numbers (`--upload` only on the NAS) |
| `share-smoke` | Sharing end to end against `--api` (staging) |
| `bench` | Timing |
| `unknown` | Lines nothing parsed |
| `ledger` | Loot by tier |
| `reinc` | Reincarnations as read |
| `mobs` | Kills by mob |

Read `src/EqlPathfinder.Cli/Program.cs` for each command's options.

**Reproducing a player's bug:** their error log is in feedback (the [app-feedback](../app-feedback/SKILL.md) skill).
Your log is the closest real data; a test in `tests/EqlPathfinder.Engine.Tests` with the few log lines that matter is
the fix's proof.
