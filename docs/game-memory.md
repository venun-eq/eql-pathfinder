# Reading the game's memory (looked at 11-19)

The user asked whether the app should read EverQuest Legends' memory, as Loadout Legends does, to know the loadout
without a /who. Not decided; the lead advised against it. A sanctioned way to see swaps was found instead: the
per-loadout files ([log-formats.md](log-formats.md), "Which loadout is on").

## What the rules say

- **Daybreak's Terms of Service** (updated July 13, 2026), §10 Cheating: no software "that modifies the game play in
  any way" or "that gives a user any kind of advantage over other end users, except as expressly authorized by
  Daybreak", and "You may not disassemble, reverse engineer, or modify any Daybreak Game(s) software" (again in §12 and
  §13(c)). §11: Daybreak "may also monitor the processes on your computer" to detect cheating software. Suspension or
  termination is at its sole discretion (§3, §26.2) and Virtual Currency is forfeited (§5.1).
- **"What constitutes Cheating?"**, EverQuest forums, Senior Community Manager, Sep 5, 2024 (before Legends): "We do
  not allow the use of any third party programs that modify the game client or its memory data." Approved programs are
  never listed publicly.
- **Reading without writing isn't named anywhere.** Finding where things sit in memory after each patch means
  disassembling the client, which §10 bans outright.
- **Loadout Legends** (its FAQ): reads memory read-only, no injection, no input. It asked the EverQuest community
  manager and "was directed to Section 10"; it calls that "guidance, not a formal Daybreak whitelist". Tolerated, not
  approved, and the risk sits with its players' accounts.

## What would change the answer

- Daybreak's permission in writing (§10's "except as expressly authorized").
- Game Jawn logging the loadout in the eqlog, which would make all of this unnecessary.

## If it is ever built

- A separate, opt-in download. A setting inside the app doesn't protect anyone if detection looks at the program.
- The client's build is `dbg.txt`'s first line (`Starting EverQuest (Built Nov 17 2026 09:39:03)`), so a patch is
  known at launch; no need to watch patch notes.
- Check every read and fall back to the logs when a check fails; never store or share a fact read from memory
  unchecked. It is the risky tier (what's stored, what's sent, how updates arrive).
