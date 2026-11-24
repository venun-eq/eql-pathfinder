# Roles: who looks at a change before players get it

A change is sized first, and its size says which of four roles work on it. Each role is a subagent
(`.claude/agents/`) with a context of its own and one page of standing knowledge. The session that talks to the user
is the lead: it sizes the work, decides, and does the small things itself. CLAUDE.md has the rule, the `feature`
skill has the steps, and this page says how it works, why, and what it costs.

| Tier | When | Who | Budget |
|---|---|---|---|
| Trivial | Docs, skills, tests, release notes, the rules as prose | The lead | |
| Small | One cause in a few files, nothing risky | The lead writes the spec, the failing test and the fix. The architect reviews the diff, on Sonnet | 60k |
| Standard | Anything bigger that isn't risky | One engineer writes the spec, the failing tests and the code. The architect reviews | 300k |
| Risky | What's stored, what's sent, the server, the infrastructure, how updates arrive, the release scripts, the privacy page | qa writes the spec and failing tests, the architect decides, the engineer builds, the architect reviews | 600k |

When players will see or read something new, in any tier, the designer goes first and reviews the screenshots after
(100k more). A budget is the sum of what each agent reports when it hands back, which is its context's size at the
end. The lead adds them up, and stops to ask when a branch passes its budget.

| Role | Agent | Runs on | When | Its page | What it leaves |
|---|---|---|---|---|---|
| QA | `qa` | Opus, high effort | First, on a risky change | docs/quality.md | The spec, `docs/specs/<name>.md`, and tests that fail until it's true |
| Design and product | `designer` | Opus, high effort | Before code and after it, when players see or read something new | docs/design.md | "What the player sees" in the spec, then findings on the screenshots. At triage, the ranked docs/queue.md |
| Architect | `architect` | Opus, high effort. Sonnet for a small change's review, Fable for a decision that can't be taken back | Before code on a risky change. After code, always | docs/engineering.md, docs/debt.md | "Decisions" in the spec, then findings on the diff, and debt lines |
| Engineer | `engineer` | Sonnet, medium effort | Standard and risky changes | docs/engineering.md, docs/quality.md, docs/code-map.md | The change, green, with its release note. On a standard change, its spec and tests too |

```
request → lead: size it. More than one branch: an estimate, and one ask
  small     lead: spec, failing test, fix                         → architect (Sonnet): review the diff
  standard  engineer: spec, failing tests, code                   → architect: review
  risky     qa: spec and failing tests → architect: decisions → engineer: build → architect: review
  any tier, when players see something new: the designer first, and its review of the screenshots after
→ findings in one batch: the lead for a few lines, a fresh engineer for more → lead: record the reviews → finish
```

## What makes it hold

None of this depends on a session remembering it.

- **`tools/feature.ps1 finish` refuses a branch that skipped a step:** no spec, a "Done when" box unticked, code
  with no test, a change to what's stored or sent with no decisions, or no review recorded from a role its paths
  need. docs/specs/README.md lists exactly what it reads. `tools/feature.ps1 ready <name>` shows the same without
  merging, and names the reviews the branch needs.
- **The paths decide who reviews.** The designer: the app's pages, windows and overlays, the website's pages, and
  any change whose spec has "What the player sees". The architect: anything that runs or decides how things run,
  which is `src/` and the solution, `server/` but for its tests, `infra/`, the site's build and settings, the
  scripts in `tools/`, and the hooks with the settings that call them. Tests, docs, skills and release notes alone
  need nobody. The rules are `Get-RequiredRoles`, `Test-NeedsDecision` and `Get-RoleProblems` in
  `tools/pipeline-common.ps1`.
- **The paths set the tier's floor too.** A change to what's stored or sent is risky whatever the lead called it:
  `finish` refuses it without the architect's decisions.
- **Where the paths ask for the designer and nothing new is drawn, the spec says so:**
  `- designer: not needed, <why>`. It's refused without the reason, and when the spec itself says what the player
  sees. The architect's review can't be set aside this way.
- **The rules as prose need no agent** (`CLAUDE.md`, the agents, the `feature` skill): the user reads that diff, and
  their go-ahead is the review. `tools/check.ps1` still reads every agent's and skill's front matter.
- **A review is of this change.** A review line that main already has doesn't count, and `start` refuses a name
  whose spec is on main. `finish` merges exactly the commit it checked.
- **Two hooks** (`.claude/settings.json`, `.claude/hooks/`): one refuses an edit inside the main checkout and says
  where to make it; the other says once when a conversation passes 200k, 400k and 700k tokens.
- **`tools/check.ps1` tests all of it on samples:** the gate's rules, both hooks through the commands in the
  settings file, the agents' and skills' front matter (Claude Code skips or half-reads a bad one without a word,
  and drops a model, effort or turn limit it doesn't know), and docs/code-map.md against the code.
  `tools\check.ps1 -SamplesOnly` runs just these, in a few seconds.
- **A branch is held to the roles when it left main after they came in:** the commit it left from has this page
  (`Test-HeldToRoles`). Nothing is written to mark it, so a branch made by hand is held too, and one already in
  flight when the roles landed finishes as it always did.

## Why these four

Each answers something that went wrong in the first days (11-14 to 11-17), when a change went from a request
straight to code, and the session that wrote it was the only one that looked at it.

| What happened | What stops it now |
|---|---|
| Every page blank for every player (0.9.37). Help's answers taken for the page data, so opening Help could leave other pages without theirs (0.9.9 to 0.9.37) | The architect reads every diff; the spec has the case; `tools/webcheck.mjs` gates it |
| "0 of 16 classes at your goal", "Run 1 of 42" for a player six runs in, "2 Greater → 1 Superior", a guessed trio time: each built and shipped, then corrected by the user | The designer decides what players see before code, from docs/design.md, which holds every correction. The user sees a design, not a release |
| "Your log is off" fixed twice; a rate from a two-minute crawl; kills written twice by a second copy | The spec walks the cases in docs/quality.md before anyone builds |
| Worked-out numbers shared in place of facts; a math change that re-sent every history into the daily limit | The architect decides before code whenever what's stored or sent changes |

## Why sessions stay small

Every step a session takes re-reads its whole conversation. Measured on 11-17 over six hours of work (16
transcripts, 2,038 steps, 64 requests from the user):

- **633M tokens of context were processed, and 99% of that was re-reading.** 2.1M tokens were written.
- **83% went to steps that already had over 200k tokens behind them,** and 54% to steps with over 400k. The mean
  step re-read 311k. One conversation of 557 steps was 46% of everything.
- **Three steps in four made a single tool call.**
- **The same files were read whole, again and again:** `campaign.js` 22 times, `ask.js` 15, `Campaign.cs` 16.

So the saving is in the shape of the work, not in doing less of it:

- **Past a small change the lead doesn't read source or build.** A role agent starts at 42k tokens (measured
  11-18), does its part, and its context is gone when it reports back. A step there costs a fraction of the same
  step in a long conversation, and the lead stays small however many changes pass through it.
- **`/clear` before something new.** The hook says when a conversation has grown past 200k. The worktree, the
  spec and the commits carry a feature, so the conversation doesn't have to.
- **A lower compaction window is the backstop.** Without one, a conversation is compacted only near the model's
  limit of about 1M tokens. `"autoCompactWindow": 250000` in `.claude/settings.json` makes that 250k for every
  session here (set on 11-24; on 11-17 a window of 300k showed as an effective 280k). Settings load live, so it
  reaches conversations already running too. `/autocompact` changes it in a running session.
- **Agents are told to batch** independent reads, searches and edits into one step, and to read ranges.
- **docs/code-map.md** says where things are, by name, so a file is opened at the right place.

## What it costs

Small sessions weren't enough. Measured on 11-18, the first day with the roles, on one request: "look into the top
item in the debt log", then "do the rest". Every change went through all four roles, and every agent ran on
Fable 5.2 at high effort.

- **One debt row became five branches, 17 agents and 102.6M tokens processed:** 97.8M re-read from cache, 4.8M
  new, 1.1M written. At API prices that is about $111 (re-reads $20, new context $48, output $43), and it used
  the week's budget. The figure the harness reports for an agent is its context's size when it finished: those
  came to about 3M.
- **By role:** qa 40%, engineer 33%, architect 16% for six runs, lead 7%, designer 3%.
- **By size of change:** a 15-line script change cost $8, and a mechanical rewrite of one test log $6, of which
  qa's spec was 88%. The three larger changes cost $23 to $37 each.
- **Long agents:** four of the 17 were half the cost, and 75% of the agents' context was processed on steps
  already past 150k.
- **Waiting:** ten times a conversation was picked up after six minutes or more. The cache had gone, so each
  re-wrote its whole context at full price: 1.8M tokens, $18.
- **Not where the cost was:** all the file reads together were 430k tokens, the lead with every report was 7%, and
  no command waited more than three minutes for its result.

What the reviews caught that day is why they stay: a render that started the whole app on the real data, a false
privacy statement in a screenshot, a test whose two numbers were equal, and a harness blind to a script that
throws while loading.

| What it cost | What changed |
|---|---|
| The whole pipeline on a 15-line change | Tiers. The lead does a small change itself, and the paths set the floor |
| qa at 40%, then an engineer starting cold on the same ground | One engineer writes the spec, the tests and the code. A separate qa only on a risky change |
| Everything on the most expensive model | The engineer on Sonnet at medium effort, a quarter of the price. The reviews, where the catches came from, stayed on the session's model (Opus that week; pinned to it on 11-24, below) |
| Agents picked up again at 300k, after a wait | An agent ends with its question, and a fresh one carries on from the spec. Turn limits of 30 to 60, where they were 80 to 300 |
| One row becoming five branches, unasked | An estimate and one ask before more than one branch, a budget for each, and findings outside a branch's purpose go to docs/debt.md |
| Reviews that ran the suite in loops, and grew the branch | A review reads the diff and the spec, runs one thing to settle a doubt, and reports every must fix and at most three should fix |
| The designer asked for by path, on a change that drew nothing | `designer: not needed`, with its reason, in the spec |

**Targets:** a small fix at about 60k reported and $1, where it was 246k to 350k and $6 to $8. A standard feature
at 250k to 300k and $6 to $12, where it was 800k to 1.1M and $27 to $37.
`node tools/analysis/session-cost.mjs --session <id>` (the `session-cost` skill) measures a session against them.

## The week of 11-17

Measured on 11-24, after the week's allowance ran out on its fifth day (`session-cost --since 2026-11-16`: 62
sessions, 348 agents, 2,050M tokens processed, about $905 at API prices).

- **The lead was 45% of it** ($405), on Opus. With the tiers in (11-19 and 11-20: 58 features, about $8 each all
  told) it was still 36%: 69 steps a session, 1,811 of 2,619 holding a single call, and 1,079 reads and searches of
  its own. Of 51 Opus sessions, 23 passed 200k.
- **About a third of everything processed was the same opening context, re-read on every step:** 54k to 59k for a
  session and 42k to 47k for a role, before any work. Agents with a short tool list (`Explore`, the Claude Code
  guide) begin at 24k to 27k.
- **By role:** engineers $156 (133 runs, $0.80 each on Sonnet), the architect $98 (99 runs), the designer $88 (68),
  qa $86 (24), and general-purpose agents $71 (12 runs that ended near 250k).
- **The reviews earned it.** Counted by keyword from the specs' review lines, the architect's records something
  fixed on 65 of 73 specs, and the designer's on 28 of the 32 where it ran.
- **The session's model had since become Fable at xhigh effort.** The roles that followed the session's model
  would have cost about twice as much for the same week.

| What it cost | What changed |
|---|---|
| Reviews and qa following the session's model | `model: opus` in their files. Fable is named for one call: a decision that can't be taken back |
| 42k to 47k before a role reads anything | `tools:` in each role's file, the seven it uses, and a skill read as a file. The `begins` column of `session-cost --session` says whether it worked |
| General-purpose agents running to 250k | `Explore` for a search. A general-purpose agent only on Sonnet, with a step limit in its brief |
| The lead's bookkeeping, one call a step | A branch's review lines, queue row, commit and `finish` in one message. Screenshots stay with the designer |
| Red checks over whitespace, since the gate checks formatting (11-23) | The engineer formats before the check |

On 11-24 `.claude/settings.json` gave the session its own model, effort and compaction window: Opus at medium effort,
compacted at 250k, whatever this PC's default is. `tools\check.ps1` fails when one of the three goes missing or holds
a value Claude Code would ignore.

Still open, each a branch of its own (docs/queue.md's Inbox): `finish` taking the review lines and the commit in one
call, and a line at the start of a session saying what the week has cost so far. Not yet known: whether an effort
that `/effort` saved to a user's own settings outranks the project's `effortLevel`.

## Changing it

| To change | Edit |
|---|---|
| What a role knows | Its page |
| The tiers, the budgets, when the lead asks first | `.claude/skills/feature/SKILL.md`, and the table in CLAUDE.md |
| What a role does, how hard it thinks (`effort:`), which model it runs on (`model:`), its turn limit (`maxTurns:`) | `.claude/agents/<role>.md`. A role runs on the session's model unless `model:` says `fable`, `opus`, `sonnet` or `haiku`, and the lead can name a model for one call, as it does for a small change's review. The engineer is on Sonnet: when two branches in a row need a second round of review, try `opus` (half of Fable's price) before going back |
| Which paths need which review | `Get-RequiredRoles` and `Test-NeedsDecision` in `tools/pipeline-common.ps1`, with their samples in `tools/check.ps1` |
| The session's model, effort, and when a conversation is compacted | `model`, `effortLevel` and `autoCompactWindow` in `.claude/settings.json` (Opus, medium, 250000). `/model`, `/effort` and `/autocompact` change them in a running session |
| Letting one branch through without the roles | `git config branch.feature/<name>.roles false`. The user's call, not a session's |

**How they're written** (the user, 11-19): the rules, the roles and the skills say what to do. A limit is written as
the state it keeps: "Commits stay on this PC".
