# The source on GitHub, one commit per release

**Why:** Players can read the code of the app they install, on GitHub under the account `venun-eq`, and what's there
is the source of the version they have, tagged with its number. No commit of this PC's history goes with it: earlier
commits hold account IDs, a home-network address, home folders and the owner's name. So GitHub gets a snapshot of
main's tree without `leave_out`, one commit per release, made by the pseudonym Venun.
**Players:** no change in the app. A regular checking what the app sends reads code that matches their installer.
Worst for the owner if it goes wrong: a push of local history can't be taken back.

## Done when
`tools/publish-github.ps1`, run by hand. Each proof is a case of the `github snapshot` sample in `tools/check.ps1`,
run on throwaway repositories with a bare repository standing in for GitHub: no network.
- [x] Without this PC's private settings all filled it refuses, naming the missing names, and makes nothing - `no settings`
- [x] It refuses, naming file and line, when main's tree fails `Test-NothingPrivate` with the settings' deny list - `private word`
- [x] It refuses, naming the note, while a note waits in main's `docs/releases/next/` - `notes waiting`
- [x] It refuses when its settings leave the account, repository, name or email empty, naming which - `no email`
- [x] It refuses when git is set to sign commits or tags, naming the setting - `signing on`
- [x] When `gitleaks` is on the PATH and reports a finding, it refuses; when it isn't, the output says it wasn't run - `gitleaks finds`
- [x] Without `-Push` it shows the version, the remote and the commit it would push, and changes nothing: no ref, no tag, nothing on the remote - `dry run`
- [x] The first `-Push` leaves one commit on the remote's main: no parent, main's tree (not the checked-out branch's), author and committer `Venun <email>` with +0000 dates, message `EQL Pathfinder v<version>`, tag `v<version>` on it, no other ref, and no object of main's history - `first push`
- [x] The local `refs/public/main` moves to the pushed commit only once the push succeeds, and shares no history with main - `first push`
- [x] The same version with the same tree again says "already published" and pushes nothing - `again`
- [x] The next release adds one commit whose only parent is the last public one, tagged with its version - `next release`
- [x] The same version with a different tree (docs changed after the release) refuses: the next release carries it - `same version`
- [x] A public ref that shares history with main (a slip, a ref set by hand) refuses before any push - `shares history`
- [x] A remote whose main holds history the public ref doesn't is refused by git and left as it was: never a forced push - `other history`
- [x] The public commit holds main's tree without the `leave_out` paths, and the dry run names them with their file counts; a literal entry that matches no file refuses - `left out`

## Edge cases
- **Nothing yet:** no `refs/public/main` and an empty remote: the root commit. A repository created on GitHub with a
  README has a main of its own: the push is refused (`other history`). Create it empty.
- **A second copy, starting and stopping:** main is read once, as a commit, so a ship committing mid-run can't mix two
  trees. A push that fails leaves the local ref where it was; the commit it made is left loose and harmless.
- **Re-sends:** running again for a version already published does nothing (`again`).
- **Something private:** the guard reads text, so images and fonts go unread (`Find-PrivateFiles` skips a file with a
  NUL). `gitleaks`, when installed, is the second look: keys and tokens the deny list can't name, in every file.
  The commit carries nothing of this PC: no local commit's message, no `user.name`, `user.email` or `.mailmap`, and
  dates in UTC so it holds no time zone. A signature would carry the owner's key, so signing turned on refuses.
- **Same name, different thing:** run from a worktree on another branch, the snapshot is still main. A tag `v<version>`
  already on another commit refuses.
- **Old versions in the field:** each version keeps its tag, so a player on an older one reads its source.
- **Uncommitted changes** in the main checkout aren't in the snapshot: main's commit is.
- **The pipeline itself:** Windows PowerShell 5.1, and GitHub's CI, which runs these samples with no private settings
  and no git identity (the sample sets its own).

## First push, by hand
1. Waits for `upload-budget` to be live in production: the server's code shows which limits it has.
2. On GitHub, create `venun-eq/eql-pathfinder` **private and empty** (no README, licence or .gitignore).
3. Install gitleaks. Run the script without `-Push` and read what it would push; then with `-Push`.
4. Actions: CI (`.github/workflows/ci.yml`) is green on the pushed commit.
5. Settings, Security: private vulnerability reporting on (`SECURITY.md` points to it), secret scanning and push
   protection on.
6. Read the repository as a stranger would, then Settings, change visibility to public.

## Not doing
- Pushing from `ship.ps1` or on a schedule: a session's commits stay on this PC, and the push is the user's.
- Rewriting local history: main keeps every commit here.
- Handoff steps 1 and 2 (`upload-budget`, the stash): other branches.
- Leaving more out than `leave_out` (the specs' images, `docs/debt.md`, `docs/queue.md`): the rest of `docs/specs` and `.claude` stay, as the user decided.
- Taking contributions from GitHub back into main: CONTRIBUTING.md's policy, later.

## Where
- `tools/publish-github.ps1` (new; the stub has only its parameters): `-Repo` (the main checkout), `-PrivateSettings`,
  `-ExamplePath`, `-Config`, `-Remote` (by default from the config's account and repository), `-Push`.
- `tools/github.json` (new): account `venun-eq`, repository `eql-pathfinder`, name `Venun`, email empty until decided.
  Public values, so not in `private.env`: there the gate would deny them as words.
- `tools/pipeline-common.ps1`: `Import-PrivateSettings`, `Test-NothingPrivate` (on a detached checkout of main's
  commit), `Get-PendingNotes`. The version is `<Version>` in main's `src/EqlPathfinder.App/EqlPathfinder.App.csproj`.
  `Test-NeedsDecision` should list the new script beside the release scripts.
- `tools/check.ps1`: the `github snapshot` sample. `docs/pipeline.md`: a short section linking this spec.

## Decisions
The architect, 11-24. Touches what's sent (the source, to a public host) and can't be taken back. Nothing is stored,
the server, the updater and players' data are unaffected.
- **Settings in a tracked `tools/github.json`,** as qa proposed: account, repository, name and email are public, and in
  `private.env` they'd become deny words the gate fails on. Tracked, it's checked by the gate like any file. By default
  the script reads it from main's commit (`git show <main>:tools/github.json`), not the folder it runs from, so a
  worktree's edit can't redirect a push. `-Config` is for the sample.
- **One read of main, everything from it:** `refs/heads/main` resolved once to a commit (never `HEAD`), checked out
  detached in a temporary worktree (removed in `finally`, then `git worktree prune`). `Test-NothingPrivate`,
  `Get-PendingNotes`, the version, gitleaks and the tree that's committed all come from that one commit.
- **The local ref is `refs/public/main`:** outside `refs/heads` and `refs/tags`, so it's no branch (`feature.ps1 list`,
  a checkout, `git push --all` and the editor's Sync never see it), and in the store every worktree shares, so `gc`
  keeps it. No local tags: `v<version>` exists only on the remote (this repository has none, and they'd sit beside
  main's history). No named remote either: the URL comes from github.json on each run, so a by-hand `git push <name>`
  with git's default refspec can't send main. Default URL `https://venun-eq@github.com/venun-eq/<repository>.git`: the
  user name makes the credential manager pick that account, not the owner's own.
- **The remote is the truth, read before anything is made:** `git ls-remote <url> refs/heads/main refs/tags/v<version>`.
  It must equal `refs/public/main` (both absent the first time); anything else refuses before a commit is made, and
  with no local ref but a remote main the message gives the fetch that recovers it
  (`git fetch <url> main:refs/public/main`). This refuses `other history` early, and the push being refused stays the
  second wall. A remote tag `v<version>` on another commit refuses.
- **The commit:** `git commit-tree --no-gpg-sign <main>^{tree} [-p <public>]`, with `GIT_AUTHOR_*` and `GIT_COMMITTER_*`
  set from github.json and both dates main's committer time as `<unix seconds> +0000`. The same main then makes the same
  commit, so the hash the dry run shows is the hash `-Push` pushes. Before pushing, `git merge-base <new> <main>` must
  find nothing and `rev-list --count <new>` must be the public count plus one. The tag is lightweight: an annotated one
  carries a tagger, a time zone and could carry a signature.
- **The push:** `git -c push.followTags=false push --atomic --porcelain <url> <new>:refs/heads/main
  <new>:refs/tags/v<version>`. No `+`, `--force`, `--all`, `--mirror` or `--tags`, ever. On success
  `git update-ref refs/public/main <new> <old or zero>`, so a second run racing it can't move it twice.
- **gitleaks is required for `-Push`** (yes to the lead's question). The deny list knows only the words it's given;
  gitleaks finds keys and tokens of any shape, and before the repository goes public GitHub's own secret scanning and
  push protection may not run on it (for a personal private repository they're a paid feature, to be confirmed in
  Settings in step 5), so on the first push gitleaks is the only look for secrets. A new `-Gitleaks <path>` parameter
  (by default `gitleaks` found on the PATH) runs `<path> dir --redact --no-banner <checkout>`: any exit but 0 refuses,
  and so does a missing gitleaks with `-Push`; the dry run says when it wasn't run. The sample passes its own stubs
  (one that finds, one that passes, a path that isn't there) instead of changing PATH, so a PC with gitleaks installed
  runs the sample the same. That changes the `gitleaks finds` line: missing with `-Push` refuses.
- **Files the guard can't read need the user's eyes, once each.** `Find-PrivateFiles` skips a file with a NUL:
  images, fonts, and any UTF-16 text. 34 tracked mock-ups in `docs/specs/*/` are renders of the app, possibly from real
  logs with a character's name in them. github.json gets `looked`, each unread file's path with its blob hash (git's
  own), and the script refuses naming every unread file in main's tree that isn't there with that hash, so a new or
  changed image waits for a look too. The dry run lists them. New sample case `unread file`: a file with a NUL refuses
  until its hash is listed.
- **Only the user's terminal pushes.** With `-Push` the script refuses when `CLAUDECODE` is set in its environment, so
  no session can push however it's asked to. The engineer checks that Claude Code sets that variable in its
  commands' shells (claude-code-guide, or `Get-ChildItem env:` in a session) before relying on it; the sample clears it
  for its own runs and has one case (`from a session`) that sets it and expects a refusal and nothing pushed.
- **Private settings must all be filled** (`Missing` empty and at least one deny word), stricter than the gate, which
  passes with no file: a push is the one place a stranger's empty settings must not pass.
- **`Test-NeedsDecision` lists `tools/publish-github.ps1` and `tools/github.json`** beside the release scripts: they
  decide what becomes public, which can't be taken back. A role-gate sample case for each.
- **Fails closed:** `$ErrorActionPreference = "Stop"` and the `git` wrapper; every check runs before `commit-tree`, the
  push is the last thing that sends, and any exception exits non-zero with nothing pushed.
- **Who else runs this:** nobody automatically. `ship.ps1` doesn't call it, CI runs the sample against a local bare
  repository, and the one change to shared code (`Test-NeedsDecision`'s list) is harmless to the hourly ship and to
  worktrees in flight.
- **Built to the recommendations, one line each:** the repository name (github.json), the email (github.json, empty
  refuses), refusing when signing is on, and refusing the same version with another tree.
- **First push, step 3 gains:** check `EQLP_DENY` holds the owner's character and guild names as well as their own,
  and look at each file the dry run lists as unread before adding it to `looked`.

## Engineer's notes
- Decision (user, 11-24): `leave_out` holds every image type under `docs/specs` (png, jpg, jpeg, gif, webp), `docs/debt.md` and `docs/queue.md`. The specs' text stays public: it backs "built spec first, judge the source". The images are 4.3 MB the deny check can't read; debt.md lists known weaknesses; queue.md is daily working notes.
- 11-24, from the user: the commit email is `360353646+venun-eq@users.noreply.github.com`; the repository name, refusing when signing is on and refusing the same version with another tree are confirmed. New `leave_out` in github.json (git pathspecs with glob magic: a folder or a pattern like `docs/specs/**/*.png`; empty for now, the user hasn't chosen): the public tree is main's without them, made in the detached checkout (`git rm`, then `write-tree`), so the deny, unread-file and gitleaks checks read what is published and a left-out image needs no look. The dry run prints `Left out:`. Sample case `left out`. For the architect: what is pushed changes with this list, and an entry with `..` or a leading `/` or `:` is refused.
- Checked 11-24: Claude Code sets `CLAUDECODE=1` in both the Bash and the PowerShell shells it starts (seen with `env` and `Get-ChildItem env:`), so the `-Push` refusal holds.
- The dry run on main lists 48 unread files (34 .webp, 9 .png, 3 .ttf, 2 .ico) for the user to look at and put in `looked`; a dry run reports every problem at once and exits 1 while any stands.
- The sample's gitleaks case now passes stubs with `-Gitleaks` (finds, passes, missing) instead of editing PATH.
- 11-24, from the user: looked at the 14 unread files the dry run lists (six `site/img` screenshots, `site/public/og.png` and `favicon.ico`, the app's `pathfinder-256.png`, `pathfinder.ico` and `splash.png`, and the three copies of `Cinzel-Bold.ttf`): "the 14 files are fine". Each is in `looked` with its blob hash, from a dry run after `git merge main`; the dry run has no unread file left. The user accepts the links that break in the public copy (seven campaign specs embed left-out mock-ups; `docs/README.md` links to `debt.md` and `queue.md`) "with a note": a line in `docs/README.md` says so.

## Reviews
- architect: (11-24, review of 34d00f3) one must fix, the `shares history` case passes with the merge-base checks deleted (the remote check refuses first), so it must name "shares history". Should fix: a literal `leave_out` entry that matches nothing (git's pathspec is case-sensitive here, `docs/Debt.md` leaves out nothing) passes silently; the first push isn't compared with the dry run's hash; Done when and Not doing still describe main's whole tree. Sound: no named remote, explicit refspecs, never forced, both history walls, `CLAUDECODE` and gitleaks refusals, `looked` by blob hash, `..`, `/` and `:` refused; a dry run on main left out the 34 webp mock-ups and listed the 14 unread files left.
  - Fixed (11-24): the `shares history` case now needs "shares history" in the output (and fails with the line-130 merge-base check removed); a literal `leave_out` entry matching no file refuses (case `left out`); the first push is compared with the dry run's commit hash; the spec says "main's tree without `leave_out`" and has a Done-when line for `left out`.
