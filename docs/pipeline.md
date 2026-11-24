# Build, test and release pipeline

How features get from a session to players without anyone driving it. The rules every session follows are in
`CLAUDE.md`; this page is the machinery.

```
feature.ps1 start   →  worktree + branch feature/<name>
   (the roles, docs/roles.md: spec and failing tests → build → reviews; add docs/releases/next/<name>.md)
feature.ps1 finish  →  the roles' steps are all there → merge main in → check.ps1 in a clean copy → fast-forward main
ship.ps1 (hourly)   →  version + notes → release.ps1 (build, test, sign, publish) → commit → website → NAS job → checks → ntfy
```

## The scripts (tools/)

| Script | What it does |
|---|---|
| `feature.ps1` | `start <name>`, `list`, `ready <name>`, `finish <name> [-Keep]`. One feature per worktree in `EQLPathfinder-wt`, beside the main checkout. `finish` refuses: uncommitted work, an app change without a release note, a missing step of the roles (docs/specs/README.md lists what it reads), a conflict with main, a red check, or a dirty main checkout. `ready` shows what `finish` would refuse and which reviews the branch needs, and changes nothing |
| `check.ps1` | The gate. First the pipeline's own parts on samples (the release-note parser, the role gate, the hooks), the agents' and skills' front matter, docs/code-map.md against the code (`mapcheck.mjs`), and `webcheck.mjs` against copies of the page scripts with one slip in each (a name core.js uses that nothing declares, for each answer that reaches one and for a new answer; any answer taken for the page data; a script that doesn't parse). Then always the solution build (Release), the engine tests and the page scripts (`webcheck.mjs`). The engine tests include the page tests, which open the app's page in headless Microsoft Edge (docs/quality.md): Edge must be installed, and without it they fail and say where they looked. `tools\release.ps1` runs the same tests in every release, as the user in their own session, so no Edge or a failing page test stops that release and the alert says so. Runs the Worker's typecheck and tests when `server/` changed, and the website's typecheck and build when `site/` or `docs/releases/` changed. `-All` runs everything |
| `ship.ps1` | Releases main when notes wait in `docs/releases/next` (see its header). Flags: `-DryRun` (shows the version and notes), `-Pause`, `-Resume` |
| `install-ship-task.ps1` | Registers the scheduled task (hourly at :25, as you, only while signed in, no window). `-Remove` takes it away |
| `pipeline-common.ps1` | Shared helpers. One lock for finish and ship, which deletes itself if a process dies. The release-note parser. The role gate's rules: which paths need which review, and how a spec is read. ntfy. The private settings' loader and the guard that keeps them out of tracked files (below) |
| `private.ps1` | Loads this PC's private settings into the PowerShell that ran it, for a deploy or a plan by hand |
| `release.ps1`, `publish.ps1` | The steps `ship.ps1` runs; nothing else should call them |
| `publish-github.ps1` | The public repository: one snapshot commit of main per release, by hand (below) |

## Versions and notes

- **Version:** patch + 1 by default, or minor + 1 if any waiting note says `bump: minor`.
- **One note waiting:** its text is the release notes.
- **Several waiting:** their titles joined make the headline, and their texts follow.
- **Last line:** "No re-read." or the re-read warning, from the `reread` flags.
- **Afterwards:**
  - `docs/releases/<version>.md` is written (it feeds the website's What's new);
  - the waiting notes are removed;
  - `v<version>` is committed on main.

## Logins it relies on

`ship.ps1` checks every login on each hourly run. Using a login keeps it from lapsing. If one fails, you get a
homelab-alerts notification (at most once a day), and nothing ships until it's fixed.

| For | What | If it lapses |
|---|---|---|
| Code signing | Your Azure CLI login (`az login`) | `az login` |
| Website deploy | Your Cloudflare `cf` login | `npx cf auth login` in `site\` |
| Publishing | The R2 key, encrypted for your Windows account (`%APPDATA%\EQLPathfinder-dev\r2.xml`) | `.\tools\publish.ps1 -ResetKey` |
| NAS job and notifications | ssh key login to the NAS (the private setting `EQLP_NAS_LOGIN`, below) | Add `~/.ssh/id_ed25519.pub` to the NAS's `authorized_keys` |

Hardening for later: a service principal for signing, and a scoped Cloudflare API token, so neither depends on
personal logins.

## Private settings

The repository is public, so it holds no account ID, no login and none of the owner's names. They live in one file
outside it, `%APPDATA%\EQLPathfinder-dev\private.env` (beside `r2.xml`), as `NAME=value` lines: the value is everything
after the first `=`, with no quotes. `tools\private.example.env` is the same eleven names with no values: Cloudflare's
account, zone, two rulesets and two D1 databases, Azure's subscription and two IDs, the NAS's login, and `EQLP_DENY`,
the words no tracked file may hold. IDs, logins and names only: a token or a key never goes in it.

- **The environment is the only way in.** Terraform, Bicep, the Worker's configuration, `publish.ps1` and `ship.ps1`
  read `EQLP_*` from their own process's environment and never open the file. A CI service sets the same names as
  secret variables and needs no file.
- **One loader opens the file:** `Import-PrivateSettings` in `pipeline-common.ps1`. It puts each value into the
  process's environment, so a program started next sees it. The file wins over a variable already set, and the
  environment fills what the file leaves out or blank. It reports each name of the example that has no value either way.
- **The hourly ship loads them itself** before step 1 and needs every name. With one missing it starts no release,
  raises an alert that names the setting (never a value), keeps its log and ends with an error. When the missing one
  is the NAS's login, the alert can't be sent, and the kept log and the task's error are the notice.
- **By hand, one command first:** `.\tools\private.ps1; <the command>`. It loads the settings into the PowerShell that
  ran it and throws, naming them, when one is missing, so nothing after the `;` runs. `infra\cloudflare\tf.ps1` loads
  them itself. Each tool needs only its own: `tf.ps1` its four, a deploy or a migration its database
  (`server/database-ids.ts`), Bicep its two.
- **The gate keeps them out:** `check.ps1`'s "nothing private" step reads every file git tracks and fails, with the file
  and line but never the text, on a home folder, a 192.168 address, a word of `EQLP_DENY` or the value of any other
  setting (`Test-NothingPrivate`). It then puts the environment back, so the build and the tests after it run as a
  stranger's clone would, with no variable set. With no settings at all it passes on the two patterns and says no
  words were denied. With some but not all it fails, naming the missing ones.
- **The NAS has its own file,** `eqlp.env`, with the job's token, account and database (docs/community-job.md).

## The public repository

The source players can read is a snapshot of main's tracked files, one commit per release, made by `tools\publish-github.ps1`
by hand, never by `ship.ps1`. None of this PC's history goes with it (docs/specs/github-launch.md has the reasons).

- **Dry run first:** `.\tools\publish-github.ps1` checks and shows the version, the remote and the commit. `-Push` sends it,
  from your own terminal only (it refuses when `CLAUDECODE` is set). `-Gitleaks <path>` names the scanner; `-Push` needs it.
- **Settings:** `tools\github.json`, read from main's own commit: account, repository, name, email (it refuses while one
  is empty) and `looked`, each unread file (images, fonts: the guard reads text only) with its blob hash, once you have
  looked at it, and `leave_out`, git pathspecs with glob magic (a folder such as `docs/specs/`, or `docs/specs/**/*.png`) left out of the public tree (every check reads what is published). The private settings must all be filled.
- **What it refuses:** a private word in main's tree, a release note waiting, a file not looked at, a gitleaks finding,
  git set to sign, a public ref sharing history with main, a remote whose main isn't the public ref, a tag already
  on another commit, and the same version with a different tree. Every check runs before the commit is made.
- **State:** `refs/public/main` here (no branch, no local tags, no named remote); the remote's `main` and `v<version>`.
  It moves only after a push succeeds. Never forced.

## Watching it

- **Notifications:** ntfy homelab-updates for a release (the version and headline); homelab-alerts for a failure, a
  lapsed login, or a main checkout with uncommitted changes.
- **Logs:** `artifacts\ship-logs\ship-<time>.log`. Only runs that did something are kept, for 30 days.
- **By hand:** `.\tools\ship.ps1 -DryRun` shows what the next release would be. `.\tools\feature.ps1 list` shows
  what's in flight.
- **Stopping it:** `.\tools\ship.ps1 -Pause`, or disable the task in Task Scheduler.

## Not covered

- **The server (`server/`):** migrations, then deploy, both by hand. Migrations have to run before a Worker that
  needs them, and that order is easy to get wrong unattended.
- **The infrastructure:** Terraform in `infra/cloudflare`, Bicep in `infra/azure`.
- **Website pages other than What's new:** deploy them with `npm run deploy` in `site/`. Every release deploys the
  site too, so anything already on main goes out with the next release.
- **When the source goes public:** a GitHub Actions workflow can call these same scripts.
