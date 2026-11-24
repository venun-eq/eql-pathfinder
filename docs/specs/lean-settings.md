# Sessions here run on Opus at medium effort, compacted at 250k

**Why:** a session here runs on whatever this PC's default is, and that had become Fable at xhigh effort, with a
conversation compacted only near the model's 1M limit. docs/roles.md, "The week of 11-17", has what that cost. The
project's `.claude/settings.json` can set all three for every session here. Checked against the Claude Code docs on
11-24: project settings may set `model`; a top-level `effortLevel` applies to every model and takes low, medium, high
or xhigh; `autoCompactWindow` takes a token count of 100000 or more.
**Players:** nobody sees it. It's the cost of building the app.

## Done when
- [x] `.claude/settings.json` has `"model": "opus"`, `"effortLevel": "medium"` and `"autoCompactWindow": 250000`
  beside the two hooks, which are unchanged. Shown by the "session settings" step of `.\tools\check.ps1 -SamplesOnly`,
  and the "hooks" step still passes
- [x] The check fails when one of the three is gone, or spelt another way (`effortlevel`, `effort`, `Model`), or
  holds a value Claude Code would ignore (`opus-5.5`, `max`, `50000`, `"250000"` in quotes, `250000.5`), or the file
  isn't JSON. Shown by the same step's twelve samples; on main's settings file it failed with "has no 'model'"
- [x] docs/roles.md no longer says the compaction window isn't set, its "Still open" list no longer has the session's
  settings, and its "Changing it" table names the three keys. Shown by `git grep -n "isn't set" -- docs/roles.md`,
  which prints nothing

## Edge cases
- **The pipeline itself (a clean copy, PowerShell 5.1):** `ConvertFrom-Json` finds a property by any case, so the
  keys are matched by exact name from the object's property list. A number comes back as Int32 or Int64, and a
  string or a decimal fails. The check reads the file and writes nothing.
- **Same name, different thing:** `effortLevel` and `effortlevel` are one key to PowerShell and two to Claude Code.
  The samples spell each key another way and expect a failure.
- **A second copy:** settings load live, so the window reaches conversations already running in other worktrees.
  The user asked for it. A user's own settings may also hold an effort: whether `/effort`'s `modelSettings` entry
  outranks the project's `effortLevel` is left open (docs/roles.md says so), with a check to run in a fresh session.

## Reviews
- architect: on Sonnet, no must or should fix, no debt. It checked that the hooks are unchanged, that the step reads the real file and runs in `finish`'s clean copy, that keys are matched by exact name under PowerShell 5.1, and that each sample reaches its own branch. It noted that the model check takes only the four aliases, which is deliberate; a full ID or `opusplan` would need the regex widened. Nothing to do.
