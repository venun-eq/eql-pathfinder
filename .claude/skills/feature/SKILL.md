---
name: feature
description: The steps for any change to the app, the server, the site or the pipeline's scripts, from request to main. Size it first (trivial, small, standard or risky), give an estimate before more than one branch, then the worktree, the failing test, the build, the review, finish. Use whenever the user asks for a feature, a fix or a UI change, or says "build it", "fix this" or "finish it".
---

# A change, from request to main

You are the lead. You talk to the user, size the work, decide, and do the small things yourself. This is a side
project with a weekly budget: docs/roles.md ("What it costs") has what one debt row cost on 11-18 and why each rule
below is here.

## Picking up the next task

"Pick up the next task" starts from docs/queue.md and takes the row its rule at the top of that page names. The row's
tier and budget are its estimate: a trivial, small or standard row starts, and a risky one still tells the user first,
as step 2 says.

## 1. Size it

| Tier | When | Who does what | Budget |
|---|---|---|---|
| **Trivial** | Docs, skills, tests, release notes, the rules as prose (CLAUDE.md, the agents, this skill) | You alone, in a worktree: just the change. For the rules, show the user the diff before `finish`: their go-ahead is the review | |
| **Small** | One cause, up to three files, about 50 lines, nothing on the risky list | You write the spec (Why, Done when, each of docs/quality.md's "Cases that have bitten" that applies, Reviews), the failing test and the fix. Then one `architect` review, called with `model: sonnet` | 60k |
| **Standard** | Anything bigger that isn't risky | One `engineer` writes the spec, the failing tests and the code. Then one `architect` review | 300k |
| **Risky** | What's stored, what's sent, the server, the infrastructure, how updates arrive, the release scripts, the privacy page, and anything that has to run the real app | `qa` (spec and failing tests), then `architect` `Mode: decide`, then `engineer`, then the `architect` review | 600k |

- **The paths set the floor.** The risky list is `Test-NeedsDecision` in `tools/pipeline-common.ps1`, and `finish`
  refuses such a change without the architect's decisions. Between two tiers, take the higher, and always at least
  what the paths say.
- **Each role has its model,** whatever this session runs on: the `architect`, the `designer` and `qa` on Opus, the
  `engineer` on Sonnet (their files say so). Name a model for one call in two cases: `model: sonnet` for a small
  change's review, and `model: fable` for the architect's `Mode: decide` on a change to what's stored or sent, where
  a wrong call can't be taken back. docs/roles.md ("The week of 11-17") has what each costs.
- **When players will see or read something new** (a page, a number, wording, a setting), in any tier above
  trivial: the `designer` goes first, `Mode: design`, and reviews the screenshots after. Add 100k to the budget.
- **Planner logic and a new page are two branches.** When a change adds engine rules and a new page, tab or card,
  the engine and its tests go first, then the page in a branch of its own. campaign-class-roles (11-19) did both in
  one risky branch: eight parts, two engineers cut off at their step limit, 1.5M against a 700k estimate.
- **A budget** is the sum of what each agent reports when it hands back (its size at the end). Add them up as you go.
  Sizes count a restarted agent again and weigh a cheap model like any other, so when a branch passes its budget,
  give its price too: the `session-cost` skill, `--session <id>` (11-19: 1.5M in sizes was $12).
- **An engineer cut off at its step limit:** commit what it left (`git add -A`, "Work in progress: …") and start a
  fresh one with the spec. A fresh one costs about half of carrying on past 200k (4.9M processed against about 9M on
  11-19), so the limit stays at 60.
- **You build a small change yourself only while this conversation is small** (the context hook hasn't spoken).
  Past that, hand it to an engineer as a standard change.

## 2. Estimate, and ask once

One small or one standard branch: start. For anything more (several branches, a risky one, "do the rest", a debt
row that turns out to be several changes), first tell the user in one message: the branches, each one's tier and
budget, the total, what you'd leave out, and every question you have, each with your recommendation. Then wait.

**Several problems from an audit or a review** become one draft each (docs/specs/README.md), with an index of the
order they go in, and each is built in its own session. Each draft gets a row in docs/queue.md's Inbox, where
product places it at triage. Write a draft from the player's side: what they see today and after, with a picture
when anything on screen changes. When you tell the user about open work, say it the same
way.

Stop and ask again when a branch passes its budget, when a review asks for more than the branch set out to do, or
when the work turns out to need another branch. Say what's done, what it cost, and what the rest would.

## 3. Steps

1. **Start:** `.\tools\feature.ps1 start <name>`, in lowercase words with dashes. One change per worktree.
2. **Build it, by its tier** (the table). An agent's brief is below. `qa`, the `designer`'s design, the
   `architect`'s decisions and the `engineer` all write in the worktree, so they run one after another.
   - **A build too big for one engineer** is split from the start. That's when the estimate leaves the engineer more
     than about 200k, or the work spans the engine and several pages. Brief one engineer per part, one after
     another: first the engine with its store and tests, then the pages, the shots and the self-test. Each commits
     after every piece. An engineer stops at 60 turns, and one stopped partway leaves its code uncommitted
     (current-loadout, 11-19: three stopped).
3. **`.\tools\feature.ps1 ready <name>`** names the reviews the changed paths need. Run them in one message:
   `architect`, `Mode: review`, and the `designer`, `Mode: review`, with the screenshots' paths.
   - The designer is asked for by path, and the change draws nothing new (a page script moved about, with the
     same renders before and after): write `- designer: not needed, <what shows nothing drawn changed>` under
     `## Reviews` and skip that review. The architect always runs, and so does the designer whenever the spec says
     what the player sees.
4. **Findings.** Every "must fix" is fixed. A "should fix" about this change is fixed; one about anything else is
   a line in docs/debt.md, or in docs/queue.md's Inbox when it's an idea players would see. A finding that's really
   the user's decision goes to the user.
   - A few lines' worth (a wrong assertion, a one-line slip): fix it yourself, whoever wrote the line.
   - More: all the findings at once, in one message to a fresh `engineer`, with the spec.
   - A second review only when a must fix changed how the thing works. Otherwise read the fix yourself.
5. **Record and finish:** under `## Reviews` in the spec, one line per role, saying what it found and what was
   done. Remove the branch's row from docs/queue.md. Commit, then `.\tools\feature.ps1 finish <name>`. After a finish
   that removed a queue row, republish the board (the `queue-board` skill). A red
   check goes to whoever built it, or a fresh engineer.
6. **Tell the user once per branch,** in a few lines: what players get, what the review caught, what it cost
   against its budget, and what's left for them, such as a server deploy. The hand-backs go into that one message. Finish only
   what's ready for players: main ships within the hour.

## Agents end, and a fresh one picks up

- **An agent that needs the user's answer ends with its question.** The spec and the commits carry the work. When
  the answer comes, start a fresh agent with it under `Known:`.
- **SendMessage to a finished agent only when both are true:** it handed back under five minutes ago, and it
  reported under 120k. Otherwise a fresh one with the spec is cheaper: picking a conversation up after five
  minutes re-reads all of it at full price (that was $18 of 11-18's $111).
- **Questions for the user go in one ask,** yours and the agents' together.

## Together, and one at a time

- **Together:** the two reviews of one branch, and branches the user approved together, each in its own worktree.
- **One at a time:** agents that write in one worktree, and anything that renders or runs the app. There is one
  desktop, and the user may be playing on it.

## Your own steps

Every step you take re-reads this whole conversation, so your steps are the dearest in the branch. On 11-19 and 11-20
the leads took 2,619 steps for 58 features: 1,811 held a single call, and 1,079 were reads and searches of their own.

- **A branch's bookkeeping goes in one message:** the review lines in the spec, the queue row, the commit and
  `finish` together. So do `start` and the first brief, and `ready` and the reviews.
- **Read the `--stat`, and leave the diff to the architect.** What you need to decide is in the agents' reports.
- **Screenshots stay with the designer.** Open one when the user asks about it: an image stays in your context for
  every later step.
- **A search across many files goes to `Explore`.** Use a `general-purpose` agent only with `model: sonnet` and a
  step limit in its brief: twelve of them ran to 250k that week, at $6 each.

## Briefs

The agents know the project and their role. A brief holds only what they can't know:

```
Feature: <name>
Worktree: <its full path, as `feature.ps1 start` printed it>
Tier: <standard | risky>                          (engineer)
Mode: <design | options | decide | review>        (designer and architect)
Request, in the user's words: "<...>"             (whoever writes the spec)
Known: <what the user said or showed that matters: answers, screenshots' paths, a report's id, the findings>
```

## When the user corrects a decision

Add the rule to that role's page in the same branch: docs/design.md for what players see, docs/quality.md for a
case that got through, docs/engineering.md for modeling or code. The next change then gets it right untold.

## Outside these steps

- **A question, research, reading data:** you alone, right here in the conversation.
- **The server, the infrastructure, website pages:** built this way, deployed by hand after the user says so
  (CLAUDE.md).
