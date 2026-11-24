# Release notes waiting for the next release

Every finished feature that changes the app (anything under `src/`) leaves one file here, named after the feature:
`docs/releases/next/<name>.md`. `tools/feature.ps1 finish` won't merge without it. `tools/ship.ps1` turns the waiting
files into the next version's notes (`docs/releases/<version>.md`, What's new on the website, the in-app update notes)
and removes them. One file per feature, so two features never edit the same file.

```markdown
---
title: Plan → Campaign
reread: false
job: false
bump: patch
---
Plan → Campaign plans every reincarnation you mean to do.

- Pick the passive rank you want for each class and race, and it lays out the loadouts that get there.
- Reincarnations your log sees tick its runs off.
```

| Key | Meaning |
|---|---|
| `title` | A few words. With several features in one release, their titles make the headline. Required |
| `reread` | `true` when this version re-reads players' logs once (a raised `EngineState.CurrentVersion`, or the fight meter's version). The notes then say so; otherwise they say "No re-read." |
| `job` | `true` when the NAS community job needs the new command-line build (a change to what the shared numbers are made from: `Engine/Community`, `AskData`, `ShareUnits`, `CommunityJob`). `ship.ps1` publishes it to the NAS and runs it once |
| `bump` | `minor` for a big feature (0.9.x → 0.10.0); otherwise leave it out |

The text is for players, in the voice of `docs/releases/*.md`: the first line is one sentence (it's the update
balloon's headline), then a short list. Say what changes for them, not how the code changed.
