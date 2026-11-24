# Docs: how EQL Pathfinder works

How-to procedures (querying data, rendering the app, checking releases) are skills in `.claude/skills/`. These pages
explain how things work. Add a line here when you add a page.

| Page | What it explains |
|---|---|
| [ask-backtest.md](ask-backtest.md) | Why Ask reads level by level, not in 10-level bands (the backtest) |
| [calculations.md](calculations.md) | Every number a player sees: what it is, how it is worked out, stored or shown, its function, and the test that holds it to a hand-checked value (or a plain "none") |
| [code-map.md](code-map.md) | Where things are, by name: how data moves, every folder and script, the page's messages, recipes for common changes |
| [community-job.md](community-job.md) | The NAS job that builds the shared numbers: schedule, token, publishing rules, fake-player brakes |
| [debt.md](debt.md) | What hides bugs or slows work down, the costliest first (the architect's ledger) |
| [design.md](design.md) | Who plays, the design rules and the cases that taught them, the states every screen has (the designer's page) |
| [engineering.md](engineering.md) | How the code is modeled, written and reviewed; the decisions taken before code (the engineer's and architect's page) |
| [fight-meter.md](fight-meter.md) | The fight meter: what a fight is, pets and charm, usual |
| [game-memory.md](game-memory.md) | Why the app doesn't read the game's memory: the rules, Loadout Legends, what would change it |
| [log-formats.md](log-formats.md) | Every log line the app reads, with examples |
| [motes-and-fragments.md](motes-and-fragments.md) | Mote and fragment tiers, values, T5-eq |
| [overlays.md](overlays.md) | The overlays and their shared plumbing |
| [pace.md](pace.md) | "Am I going at a proper speed?": kill speed, XP per kill, comparing like with like |
| [pipeline.md](pipeline.md) | Worktrees, the gate, hourly releases, logins, alerts |
| [page-tests.md](page-tests.md) | What page tests share when runs overlap, and the cap on Edges at once |
| [developing-while-playing.md](developing-while-playing.md) | Keeping builds, tests and Edge off the game: the pin, the launcher, what backs off when the game runs, how to undo |
| [queue.md](queue.md) | The order of work: what to pick up next, how each row is ranked, and where new reports and ideas land |
| [quality.md](quality.md) | Where each kind of thing is tested, tests first, the recipes, and the cases that have bitten (the page of whoever writes the tests) |
| [roadmap.md](roadmap.md) | What's coming, in step with the app's What's coming (D1 `roadmap`) |
| [roles.md](roles.md) | The four tiers a change is sized into, the roles each one uses, what holds them in place, and what it costs |
| [usage.md](usage.md) | Usage counts: what's sent, how, and ready-made queries |
| [releases/](releases/) | Every released version's notes; `next/` holds the waiting ones |
| [specs/](specs/) | Every change's spec: what done meant, its edge cases, decisions and reviews. Its README has the format |

In the public copy on GitHub the work queue, the debt list and the specs' mock-up pictures are left out: they stay in the working copy, so links to them from here break there.

Elsewhere:
- `server/README.md`: the Worker.
- `site/README.md`: the website and its screenshots.
- `infra/azure/README.md`, `infra/cloudflare/README.md`: signing, the domain and caching.
