# Working on EQL Pathfinder

Several sessions work on this repository at once, and releases happen without anyone in the loop. These rules keep
them out of each other's way. docs/pipeline.md has the details.

## Never in the main checkout

The main checkout (the `EQLPathfinder` folder itself) is the release lane: `tools\ship.ps1` releases from it every hour.
- Edit, build and commit in a worktree (below).
- `ship.ps1` alone runs `tools\release.ps1` and `tools\publish.ps1` and deploys the website.
- `ship.ps1` does not push to a git remote; pushing `main` to GitHub is done by hand.

## One feature, one worktree

```powershell
.\tools\feature.ps1 start <name>     # ..\EQLPathfinder-wt\<name>, branch feature/<name>, from main
.\tools\feature.ps1 list             # every feature branch: ahead/behind main, release note and spec or not
.\tools\feature.ps1 ready <name>     # the reviews it needs, and what finish would refuse
```

Run these from any checkout. Work and commit in your worktree. Run `.\tools\check.ps1` there whenever you like: it's
the same gate `finish` uses.

## What to work on next

docs/queue.md is the order of work. "Pick up the next task" takes its first ready row by the rule at the top of that
page, then follows the `feature` skill with the row's tier and budget. Product (the `designer`, `Mode: triage`)
ranks the queue, and the user's **You** column wins.

## Size the change first

This is a side project with a weekly budget. The session talking to the user is the lead: it sizes each change,
and the size says who works on it. The `feature` skill has every step, and docs/roles.md says how it works, why,
and what it costs.

| Tier | What | Who |
|---|---|---|
| Trivial | Docs, skills, tests, release notes, these rules | The lead alone |
| Small | One cause in a few files | The lead writes the spec, the failing test and the fix. The `architect` reviews the diff |
| Standard | Anything bigger that isn't risky | One `engineer` writes the spec, the failing tests and the code. The `architect` reviews |
| Risky | What's stored, what's sent, the server, the infrastructure, how updates arrive, the release scripts | `qa` writes the spec and failing tests, the `architect` decides before code, the `engineer` builds, the `architect` reviews |

- **The `designer`** goes first and reviews after, in any tier, when players will see or read something new.
- **Each role has its model,** whatever the session runs on: the reviewers and `qa` on Opus, the `engineer` on Sonnet.
  Fable is named for one call, a decision that can't be taken back.
- **The paths set the floor.** The lead may go up a tier, and stays at or above the floor. `finish` refuses code without a spec, a test
  and the architect's review, and a risky change without its decisions.
- **Estimate before more than one branch.** The lead lists the branches, each one's tier and budget, and every
  question it has, and asks once. A branch that passes its budget stops and asks.
- **Agents end, and a fresh one picks up.** One that needs an answer ends with the question, and a fresh one carries on from
  the spec and the commits.
- **Only this change.** What a review finds outside the branch's purpose goes to docs/debt.md, and the branch keeps to its purpose.
- **Past a small change the lead leaves reading source and building to the agents,** because every step of a long conversation
  re-reads all of it.
- **A change to these rules** (this file, the agents, the `feature` skill) is the user's to review: show them
  the diff, and their go-ahead is the review. Scripts, hooks and settings get the architect's.

When the user corrects a decision, add the rule to that role's page (docs/design.md, docs/quality.md or
docs/engineering.md) in the same branch.

## A release note for every app change

A branch that changes anything under `src/` must add `docs/releases/next/<name>.md`. The front matter is `title`,
`reread`, `job` and `bump`, then text for players. `docs/releases/next/README.md` has the format and when each flag
is true. `docs/releases/<version>.md` stay as they were released: they are history.

## Finish

```powershell
.\tools\feature.ps1 finish <name>
```

1. Checks that every role's step is there (above), then merges main into the branch.
2. Builds the solution and runs every test in a clean copy. The Worker's tests and the website's build run too when
   the branch touches them.
3. Fast-forwards main only if all of that is green.
4. Removes the worktree and branch (`-Keep` keeps them).

If anything fails, nothing changes on main. On a conflict or a red check, fix it in your worktree, commit, and finish
again. Finish a feature only when it's ready for players: whatever is on main ships within the hour.

## What ships by itself, and what ships by hand

- **By itself (`ship.ps1`):**
  - the app: version, notes, build, test, sign and publish;
  - the website's What's new;
  - the NAS community job, when a note says `job: true`.
  
  You get a notification on ntfy (homelab-updates, or homelab-alerts on a problem).
- **By hand, after the user says so:**
  - the server: `server/`, with `npm run migrate` then `npm run deploy`, staging first;
  - the infrastructure: `infra/cloudflare` with Terraform, `infra/azure` with Bicep;
  - website pages other than What's new: `npm run deploy` in `site/`;
  - the community launch switches in the server's config table.
- **Pausing releases:** `.\tools\ship.ps1 -Pause` stops them; `-Resume` starts them again.

## Before you research, and after

- **Look first:** `.claude/skills/` has how-tos, such as querying live data, reading feedback, rendering the app,
  where logs live, release status. `docs/README.md` lists the explanations.
- **When you had to work something out that the next session would need again, save it before you finish:**
  - a task someone will repeat (a query, a check, a way to test or render) becomes a skill in
    `.claude/skills/<name>/SKILL.md`: a `description` that says when to use it, and short steps that link into
    `docs/` rather than copying it;
  - how something works goes on the `docs/` page it belongs to, with a line in `docs/README.md` for a new page.
- **Update an existing skill** rather than adding a near-duplicate. Skills and `docs/` hold what you saved: this
  file holds rules only.

## Conventions

- **Tests:** engine tests in `tests/EqlPathfinder.Engine.Tests`, Worker tests in `server/test`. A fix comes with a
  test that would have caught it.
- **Shared data:** facts are stored and shared raw; comparisons adjust at view time (docs/pace.md).
- **Players' privacy:** names, chat and clock times stay on this PC. `site/privacy.html` must match what the app
  sends.
- **Commit messages:** say what changed for players and why. A session's commits stay on this PC: pushing `main`
  is the user's, by hand.
