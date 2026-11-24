---
name: session-cost
description: Measure where Claude Code sessions on this repository spend their tokens. Totals by session, by agent and role with a price, by how big the context was on each step, by tool and by file read, and what the user asked. Use for "why does this cost so much", "what did that branch cost", "did the tiers cut tokens", "which files get read most".
---

# What sessions cost

```sh
node tools/analysis/session-cost.mjs                      # everything on record
node tools/analysis/session-cost.mjs --since 2026-11-18   # sessions that began on or after that day (UTC)
node tools/analysis/session-cost.mjs --session e22fbcdc   # one session and its agents, each with role, model, size, price
node tools/analysis/session-cost.mjs --prompts            # also what the user asked, in order
```

It reads the transcripts Claude Code keeps for this repository (the folders in `~/.claude/projects/` named after the main checkout's path, with a `-` for each `:` and `\`)
and changes nothing. Run it from any checkout. A session's id is the first column of the sessions table.

**Transcripts hold everything said and read in a session.** They, and what `--prompts` prints, stay out of the
repository and its commits.

## Reading it

| Line | What it tells you | 11-17, before the roles |
|---|---|---|
| context processed | Every step re-reads the whole conversation. This is the sum over all steps | 633M over six hours |
| at API prices | The same in dollars: re-reads are cheap, new context and output aren't. A yardstick for comparing, since the subscription's limits weigh tokens their own way | |
| mean context per turn | How long conversations were when the work happened | 311k |
| the context-size table | Where the tokens went. The rows from 200k up are the ones to shrink | 83% at 200k or more, 54% at 400k or more |
| turns that made one tool call | Steps that could have been batched | 1,500 of 1,981 |
| sessions | A session marked `agent` is a subagent's own transcript: a role doing its part | The largest conversation was 46% of everything |
| files read most | Files opened whole and often. docs/code-map.md should cover each | `campaign.js` 22 times |
| largest shell outputs | Commands that printed whole files. Search or read a range instead | |

## One session, agent by agent (`--session`)

| Column | What it tells you | 11-18, every change through all four roles |
|---|---|---|
| model | What the agent really ran on, whatever its definition says | Fable for all 17 |
| begins | Its context before it had read anything | 42k |
| size | Its context at the end. This is the figure the harness reports for an agent, and what a branch's budget adds up (docs/roles.md) | 64k to 379k; about 3M in all |
| context | Everything it processed: the sum of its context over every step | 0.3M to 22.9M; 102.6M with the lead |
| re-wrote | Context paid for again at full price, because the conversation was picked up after the cache's five minutes | 1.8M, $18 |
| price | At API prices, on the model it ran on | $111: qa 40%, engineer 33%, architect 16%, lead 7%, designer 3% |

The tiers are working when a small fix comes to about 60k in sizes and $1, a standard feature to 250k to 300k and
$6 to $12, the engineer's rows say sonnet, and "re-wrote" is close to nothing. docs/roles.md ("What it costs") has
what was changed to get there, and "Why sessions stay small" the day before.

To compare days, run it with `--since`, and divide the price by the features finished in that time
(`git log --oneline --since=<day> main`, less the `v0.x` release commits).
