# Specs: what done means, written before the code

Every change a role reviews (docs/roles.md) starts with one file here, named after the feature:
`docs/specs/<name>.md`. Whoever builds first writes it, with tests that fail until it's true: the lead on a small
change, the `engineer` on a standard one, `qa` on a risky one. `tools/feature.ps1 finish`
reads it and won't merge a branch whose spec is missing or unfinished. Specs stay after the merge: they're the record
of why something is the way it is.

A **draft** is a spec written before anyone builds it, from an audit or a review. It says "Draft" at the top, then the
Why and a picture as the player sees it (below), then the cause in the code, a proposal, its tier and what it waits
for. Whoever builds it starts under its name
(`feature.ps1 start` takes a draft's name, and only a draft's: a finished change's spec keeps its name), keeps those,
adds the rest, and replaces the "Draft" line with what was built and when. An index such as
[calc-audit.md](calc-audit.md) lists a set of drafts and the order they go in.

A small fix needs only **Why**, **Done when** and **Reviews**. This is the early-session-rate fix of 11-17 as its
spec could have read (the screenshot's name and the two review lines are made up, to show the form):

```markdown
# Home waits for a fair session rate

**Why:** A two-minute in-and-out for a task's boss read as 124.6 T5-eq an hour on Home, against a usual of 8
(a report from 0.10.3).
**Players:** everyone. Worst for a new player, whose first crawl is the whole session.

## What the player sees
Until a session has 6 minutes in crawls, Home's farming card shows "T5-eq so far this session" and "The rate starts
at 6 min." where the rate was. The overlay says the same. Times under an hour read in minutes, not "0.0 h".

## Done when
- [x] A session with under 6 minutes in crawls has no rate, and says when one starts - `ASessionHasNoRateUntilSixMinutesInCrawls`
- [x] Home's card shows the T5-eq so far in place of the rate - seen in `main-home-early.png`

## Edge cases
- No crawl yet this session: the card is as it was, with nothing to wait for.
- Six minutes spread over several short crawls: the rate starts, since it's time in crawls that counts.

## Not doing
- Changing the 6 minutes. It's the pace check's own number, so the two agree.

## Where
- `src/EqlPathfinder.Engine/Tracking/VisitStats.cs`: `Rates.MinKills`, `Early`
- `src/EqlPathfinder.App/web/js/home.js`: the farming card in `renderHome`

## Reviews
- architect: the page worked out "early" for itself; it now reads the engine's flag. Fixed.
- designer: "so far" had no unit beside the number. Fixed.
```

| Part | Who writes it | What goes in it |
|---|---|---|
| **Why** | its writer | The player's view: what they see today and what they'll see after, in a sentence or two, in their words where there are any. Code names, formulas and line numbers go under the cause and **Where**, not here. A change players can't see says "Players see no change" and why it matters to them anyway |
| **Players** | its writer | Which of docs/design.md's players it touches, and who it's worst for |
| **Picture** | its writer | When anything on screen changes: before and after (Pictures, below). Under the Why |
| **What the player sees** | `designer` | Only when players see or read something new: each state, and the exact words |
| **Done when** | its writer | One box per thing a player or a test can observe, each ending with its proof: a test's name, the screenshot that shows it (its file's name, as in `main-home-early.png`: a full path into a scratchpad holds a home folder, and the gate refuses one in a tracked file), or the command whose output does (a self-test, a sample in `tools/check.ps1`). The `engineer` ticks a box when its proof passes |
| **Edge cases** | its writer | The cases from docs/quality.md that apply, and what happens in each, in a line or two. A case that doesn't apply is left out |
| **Decisions** | `architect` | Only when the change touches what's stored, what's sent, the server or how updates arrive. Leave the heading out otherwise |
| **Not doing** | its writer | What was left out on purpose |
| **Where** | its writer | The files and functions the change lands in, so the engineer doesn't have to search |
| **Reviews** | the lead | One line per role after its review, `- architect: ...`, saying what it found and what was done about it. Where the paths ask for the designer and the change draws nothing new: `- designer: not needed, <why>` |

## Pictures

So a reader sees the change rather than working it out from the text. The lightest one that shows it:

| The change | The picture |
|---|---|
| Anything on screen: words, a number's value, a row | A text wireframe in the spec (a ` ```text ` block): before and after, the parts that change marked, made-up numbers called made up |
| A number's value on a screen that exists, when where it sits matters | Today's screen rendered (the `app-screenshots` skill), the parts that change marked |
| A new layout | The `designer`'s mock-up in the real window |

PNGs live beside the spec, in `docs/specs/<name>/`, linked from it. Scripts and review screenshots stay in the
scratchpad. A change nobody can see on screen needs no picture, though one that marks which numbers it touches
helps.

**A picture is a foundation, not a contract** (the user, 11-19). It gives the designer and the builder the same
place to start. Whoever finds a better way takes it, and says in a line under the picture what changed and why, or
replaces the picture. A review checks what was built against "What the player sees", "Done when" and docs/design.md,
never against a draft's picture.

Code that no test can reach gets a line anywhere in the spec, `No test: <the reason>`, and the architect weighs
the reason. Every agent after its writer reads the spec, so it stays short: 40 lines for a standard change, 80 for
a risky one, not counting the picture.

## What finish reads

`tools/feature.ps1 ready <name>` shows these without merging anything.

- **A missing spec**, when the branch changes anything a role reviews.
- **Any unticked box** (`- [ ]`), anywhere in the file. Something that turned out not to be needed moves to "Not
  doing"; it isn't left unticked.
- **Code without a test:** `src/` or `server/src/` changed, and nothing under `tests/` or `server/test/` did, nor
  `tools/webcheck.mjs`, and there's no `No test:` line with a reason of at least a few words.
- **No "## Decisions"** with something under it, when the change touches what's stored, what's sent, the server,
  the infrastructure or how updates arrive.
- **A role with no line under "## Reviews"**, when the changed paths need that role, or when the spec has "What
  the player sees" (then the designer, whatever the paths). A role's name with nothing after the colon doesn't
  count, and neither does a line main already has: a follow-up after `finish -Keep` needs its own reviews.
- **"not needed" where it can't stand:** for the architect, for the designer without a reason of a few words, or
  for the designer when the spec has "What the player sees".

A branch that changes only tests, docs, skills, release notes or the rules as prose (CLAUDE.md, the agents, the
`feature` skill) needs no spec and no review.

The rules are `Get-RequiredRoles`, `Test-NeedsDecision`, `Read-Spec` and `Get-RoleProblems` in `tools/pipeline-common.ps1`, and
`tools/check.ps1` tests them on samples.
