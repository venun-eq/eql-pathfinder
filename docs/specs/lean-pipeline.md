# A pipeline sized to the change

**Why:** One debt row ("look into it", then "do the rest") ran five branches through all four roles on the most
expensive model: 102.6M tokens processed, about $111 at API prices, and the user's budget for the week. A 15-line
script change cost what a feature should (measured 11-18; docs/roles.md, "What it costs").
**Players:** none directly. What protects them at `finish` is unchanged.

## Done when
- [x] A change to the prose rules alone (CLAUDE.md, an agent, the feature skill) needs no review at finish, and a hook, the settings or a script still needs the architect - the role gate's samples, `tools\check.ps1 -SamplesOnly`
- [x] "designer: not needed, <why>" stands in for the designer's review of a change that draws nothing new, and is refused without a reason, for the architect, and when the spec says what the player sees - the same samples
- [x] An agent's `model:`, `effort:` or `maxTurns:` that Claude Code wouldn't take stops the check - the front matter samples, `tools\check.ps1 -SamplesOnly`
- [x] The engineer is set to Sonnet at medium effort, and every role's turn limit is a fifth to a half of what it was - `.claude/agents/*.md`, which the front matter check reads
- [x] The feature skill sizes a change into four tiers, asks with an estimate before more than one branch, holds a branch to a budget, and ends agents where it used to resume them - `.claude/skills/feature/SKILL.md`
- [x] One command shows each agent of a session with its role, model, size and price - `node tools/analysis/session-cost.mjs --session e22fbcdc` prints 11-18's session agent by agent (17 agents and $111 when it was measured; that session was still running)

## Edge cases
- A branch already in flight (page-tests-other-pages): the gate only asks for less than before, so it finishes as
  it would have. An agent started after this lands gets the new definitions: the engineer still builds to a spec
  and tests that are already there.
- An agent cut off at its turn limit: its commits and the spec's unticked boxes say where it stopped, and a fresh
  one picks up from them.

## Not doing
- An allowlist for routine commands. No agent command on 11-18 waited over three minutes for its result, so
  approvals weren't what the long waits were: they were agents idle between hand-offs.
- A `tools:` list per agent, to shrink the 42k an agent starts with. It's about 4% of the cost.
- Any change to what `finish` asks of code: a spec with its boxes ticked, a test, the decisions, the architect's
  review, the release note, the check in a clean copy.

## Where
- `tools/pipeline-common.ps1`: `Get-RequiredRoles`, `Get-RoleProblems`
- `tools/check.ps1`: the role gate's samples, `Get-FrontMatterProblem`
- `.claude/agents/*.md`, `.claude/skills/feature/SKILL.md`, `CLAUDE.md`, `docs/roles.md`, `docs/specs/README.md`
- `docs/quality.md` ("Recipes"), `docs/engineering.md`
- `tools/analysis/session-cost.mjs` and `.claude/skills/session-cost/SKILL.md`

## Reviews
- architect: one must fix. A standard change's engineer no longer walked docs/quality.md's "Cases that have bitten", and nothing else did: the engineer reads them again, and so does the lead on a small change. Three should fix, all done: a misspelt `maxTurns` or `model` key stops the check, a fresh engineer looks for uncommitted work left behind, and `--session` matches only the session it names and says so when given none. One debt line (`ship.ps1` isn't on the risky list), which is the user's call. The scripts were found sound, and the rules agree with the gate.
