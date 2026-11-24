# Account IDs, logins and names load from a private file

**Why:** Players see no change. The code goes public as a one-commit snapshot under the name Venun, and today its
tracked files carry the owner's Cloudflare and Azure IDs, the NAS login with its home-network address, the owner's
name and town, and home-folder paths. After this they live in one file outside the repository, each tool reads its
value from the environment, and the gate refuses a tracked file that holds any of them. It matters to players
because the hourly release reads the same values: with one missing it stops whole and says so, never half-publishes.
**Players:** none directly. Worst for the owner: an unattended release that publishes half, or a name that gets out.

## The private file
`%APPDATA%\EQLPathfinder-dev\private.env`, beside `r2.xml`: `NAME=value` lines and `#` comments.
`tools\private.example.env` is the same names with nothing after the `=`. The names (the architect may rename them):
`EQLP_CF_ACCOUNT_ID`, `EQLP_CF_ZONE_ID`, `EQLP_CF_CACHE_RULESET_ID`, `EQLP_CF_REDIRECTS_RULESET_ID`,
`EQLP_D1_PRODUCTION`, `EQLP_D1_STAGING`, `EQLP_AZ_SUBSCRIPTION_ID`, `EQLP_AZ_OWNER_OBJECT_ID`,
`EQLP_AZ_IDENTITY_VALIDATION_ID`, `EQLP_NAS_LOGIN` (user@host) and `EQLP_DENY`: comma-separated words no tracked
file may hold (the owner's first name short and long, the surname, the Windows and NAS account names, the town).

## Done when
- [x] The loader puts the file's values into the process's environment, so a program it starts sees them (no `setx`:
  the hourly task's child process loads them itself). It reads a file as Notepad saves it, and reports each name of
  the example with a value neither in the file nor already in the environment, so a CI's secret variables count -
  `check.ps1` sample "private settings", the loader's four lines
- [x] `tools\private.example.env` is there: names, no values - the same sample
- [x] The guard finds a home folder however it's written (`\`, `\\`, `/`, `/c/`, `C--Users-`), a 192.168.x.x address,
  and a denied word, whole and in any case. It passes Public, Bob, someone, `<name>`, `%APPDATA%`, other addresses
  and a denied word inside a longer one - the same sample, the guard's three lines (`Find-PrivateText`)
- [x] It reads what git tracks and nothing else - the same sample, its last line (`Find-PrivateFiles`)
- [x] `check.ps1` runs the guard over the repository, denying `EQLP_DENY`'s words and every other value in the
  private file, and fails with each file and line. Passing on this PC is the proof that no ID, login, name or home
  folder is left in a tracked file, this spec and the guard's own samples included - `.\tools\check.ps1 -SamplesOnly`
  prints `== nothing private` and how many words it denied; seen to fail once with a line added by hand
- [x] The hourly ship with no private file starts no release, leaves main and `releases\` as they were, alerts naming
  the file, keeps its log and ends with an error - `shipcheck.ps1` case `private-missing`
- [x] The same with one value blank, and the alert names that setting - `shipcheck.ps1` case `private-blank`
- [x] Every other ship case passes with the NAS login and the R2 endpoint read from the environment - `.\tools\shipcheck.ps1`
- [x] The community job is told its database and stops without one - `TheCommunityJobIsToldItsDatabaseAndHasNoneOfItsOwn`
- [x] A deploy or a migration reads its database's variable and stops, naming it, when it's unset or blank; tests
  need none - `server/test/database-ids.test.ts`, and `npm run typecheck` and `npm test` with no variable set
- [x] Terraform (its import blocks too) and Bicep read their IDs from the environment and change nothing live:
  `.\infra\cloudflare\tf.ps1 plan` says "No changes" and `az deployment group what-if` shows none (both only read),
  and each stops naming the variable when one is unset - the outputs' last lines, quoted under Reviews
- [x] This PC's real `private.env` has every name filled with today's values, written before the tracked copies go -
  `.\tools\check.ps1 -SamplesOnly` names no missing setting
- [x] The NAS's `eqlp.env` has `EQLP_D1` before this branch finishes, and the release note says `job: true` - one
  run of the job as the `community-pool` skill runs it, ending 0 (a change on the NAS: the user's go-ahead first)

## Edge cases
- **The alert travels over the NAS login, which is one of the settings.** When that's the one missing, ntfy can't
  be reached: the kept log and the task's error are the notice (a question for the user).
- **A clean copy, a stranger's clone, a CI:** no private file. The build, every test, the server's typecheck and the
  site's build pass with no variable set; the guard runs its two built-in patterns and says no words were denied.
- **PowerShell 5.1:** a byte-order mark before the first name, CRLF, an `=` inside a value, `-match` in a loop.
- **Half a release:** ship checks before the lock and anything written; `publish.ps1` by hand stops before the upload.
- **Same name, different thing:** a four-letter first name sits inside ordinary words, so words match whole. The
  NAS account name holds the surname with no break before it, so it's listed itself. A blank entry matches nothing.
- **Something leaving the PC:** the guard prints file, line and kind, never the denied word. Alerts and logs name
  settings, never values. The private file is never copied into a clean copy, a log or `artifacts\`.
- **Old versions in the field:** installed apps read none of these. Today's NAS job reads `EQLP_D1` as an
  override, so the line goes in first and the build with no default lands after it.
- **Another branch:** `public-docs` takes the home folders out of prose. Until it's on main the guard fails here on
  those lines; `finish` merges main first.

## Not doing
- The snapshot script, the host, and git history (the snapshot has none).
- Public addresses, the bucket's name, the old `r2.dev` address, Azure's built-in role IDs, the tests' made-up IDs,
  migration `0001_initial.sql`, the signing certificate's subject: decided with the user.
- Any deploy of the server or the infrastructure. Other private ranges (10.x, 172.16.x), the NAS's folder paths.
- The lines `public-docs` owns: CLAUDE.md, the agents, two skills, `docs/pipeline.md` line 17, three older specs.

## Where
- `tools/pipeline-common.ps1`: `Import-PrivateSettings`, `Find-PrivateText`, `Find-PrivateFiles`, `Test-NothingPrivate`,
  `Send-Ntfy`'s login. `tools/check.ps1`: the repository step. `tools/ship.ps1`: the check before step 1, two logins
- `tools/publish.ps1` with `tools/feed.json` (the endpoint); comments in `tools/feature.ps1`, `tools/analysis/session-cost.mjs`
- `infra/cloudflare/main.tf`, `imports.tf`, `tf.ps1`; `infra/azure/signing.bicepparam`, `README.md` (line 46 goes)
- `server/cloudflare.config.ts`, `server/database-ids.ts` (a stub today), `server/package.json` (the migrate
  scripts), `server/README.md`, `server/nas/eqlp-set-token.sh`
- `src/EqlPathfinder.Engine/Community/CommunityJob.cs` (`DefaultDatabase`), `src/EqlPathfinder.Cli/Program.cs`
- `docs/community-job.md`, `docs/pipeline.md` line 45, `docs/quality.md` (a case), the `community-pool` and `production-data` skills

## Decisions
The architect, 11-20. Three questions are with the user. These take the recommended answers, and say where another
answer would change one.

- **The environment is the only way in, and the file is how this PC fills it.** Terraform, Bicep, the Worker's
  config, `publish.ps1` and `ship.ps1` read `EQLP_*` from their own process's environment and never open the file.
  One loader opens it (`Import-PrivateSettings`). A CI service's secret variables are then the same eleven names with
  no file, and no tool changes. No `setx`, no `.tfvars`, no second reader in JavaScript: two parsers of one file drift.
- **The format, the place and qa's eleven names stand.** `NAME=value`, the value being everything after the first
  `=`, trimmed: no quotes, no `export`, no `$` expansion, no comment after a value. `%APPDATA%\EQLPathfinder-dev\` is
  outside every worktree and clean copy, so no `git add` reaches it. `.gitignore` gets `private.env` all the same.
- **IDs, logins and names only: never a token or a key.** The file is plain text. What lets someone act as the owner
  stays where it is: the `cf` and `az` logins, `r2.xml` (encrypted for the Windows account), the NAS's `eqlp.env`.
- **The file wins. The environment fills what the file leaves out or blank.** On this PC there's then one place to
  look for what a deploy will use, a value left in a shell by an earlier load can't outlive an edit, and a throwaway's
  file beats whatever the caller's shell holds. A CI has no file, so its variables are all there is. Owed: one more
  loader line in the "private settings" sample, a name set both ways.
- **Ship and `tools\private.ps1` need every name; a tool needs only its own.** Ship stops on any (shipcheck blanks the
  example's first name, so `EQLP_NAS_LOGIN` isn't first). `tf.ps1` checks its four, `databaseId` its one, Bicep its
  two, so a CI job with only Cloudflare's secrets can still plan.
- **The gate reads the values for the guard, then puts its environment back as it was.** The build, the tests and
  the server's typecheck that follow run as a stranger's clone would, on every gate run, so "passes with no variable
  set" is proven here and not only claimed. The guard denies `EQLP_DENY`'s words and each other name's value, read
  from the environment after the load, so a CI's secrets are denied too.
- **A throwaway ship starts with every name of the example cleared** (`shipcheck.ps1`, `Invoke-Ship`, with the
  variables it already saves and restores). With no file the environment counts, so a shell that had loaded the real
  settings would otherwise hand `private-missing` this PC's login and let it pass or fail for the wrong reason.
- **No settings at all: the gate passes on the two patterns and says no words were denied (question 3). Some but
  not all: it fails, naming them.** A half-filled file on the owner's PC is a half-armed guard. Owed: a sample line.
  If the user wants the gate to fail with none, a stranger's `check.ps1` needs a switch to pass, and that's all.
- **By hand, one command first: `tools\private.ps1`** (new). It loads into the PowerShell that ran it and throws,
  naming them, when a name is missing, so nothing after it on the line runs:
  `..\tools\private.ps1; npm run deploy`. Without it a tool stops naming its variable, and its message says this
  command. `npm run deploy` and `migrate` keep their names and work as they are with a CI's variables.
- **`databaseId` stands at `server/database-ids.ts`:** beside the config and outside `src/`, so it's never in the
  Worker. Modes keep today's meaning (test: none, read before anything else; staging; anything else production).
  Migrations go through it too, or its test proves nothing about them: `npm run migrate` becomes
  `node migrate.mjs production`, which asks `databaseId` and starts `cf d1 migrations apply <id>` (the CLI takes only
  an ID there). `cf workers types` must not need a database: give it `--mode test` if it evaluates production.
- **Terraform:** four plain string variables with no default (not `sensitive`: they're IDs, and an import's `id`
  won't take one). `imports.tf` builds its ids from them, which Terraform 1.6 allows and `versions.tf` already
  asks for. `tf.ps1` loads the settings itself, stops naming a missing `EQLP_CF_*`, and sets `TF_VAR_account_id`,
  `TF_VAR_zone_id`, `TF_VAR_cache_ruleset_id` and `TF_VAR_redirects_ruleset_id` for the run, removed after like the token.
- **Bicep:** `signing.bicepparam` takes both IDs with `readEnvironmentVariable('EQLP_AZ_...')` and no default, so
  Bicep itself stops naming an unset one. The README's commands start with `.\tools\private.ps1;` and pass
  `--subscription $env:EQLP_AZ_SUBSCRIPTION_ID`, so a deploy lands where the file says whatever the login's default is.
- **The R2 endpoint is worked out, not a twelfth name:** `publish.ps1` builds it from `EQLP_CF_ACCOUNT_ID`, and
  `feed.json` loses `endpoint`.
- **Ship checks before step 1,** since step 1 uses the login. It always raises the alert (kind `private`, so the
  20-hour quiet applies), which puts the names in the kept log, and exits non-zero. `Send-Ntfy` with no login returns
  false at once: no ssh, no default (question 1). A second channel, if the user wants one, changes `Send-Ntfy` only.
- **The NAS job keeps the name `EQLP_D1`** (question 2): the job running today already reads it, so the line goes in
  first and the build with no default after, `job: true`. `eqlp-set-token.sh` writes `eqlp.env` whole today, which
  would drop the line at the next token change: it keeps every line but its own two, and the NAS's copy of it is
  replaced under the same go-ahead. If the user says no to the NAS change, this branch can't finish: the guard
  refuses `CommunityJob.cs` while the ID is in it.
- **What's sent, and taking it back:** nothing new leaves the PC and nothing about players. Alerts and logs carry
  names of settings. A revert of the branch undoes all of it; the file and the NAS's line are harmless left behind.

## Reviews
- architect: 11-20: no must fix. Checked: finish's gate result is unaffected by the stray $LASTEXITCODE (powershell -File returns 0; failures throw); this PC's private.env equals main's ten values; a substring search of HEAD finds no name, ID or address; EQLP_D1 reaches the NAS job; the config sees its database in production mode. Should fix: Find-PrivateFiles skips a tracked path it can't find (git quotes non-ASCII names) and never reads file names. Logged to docs/debt.md: the queue-board samples after all green, shipcheck's wait, stale NAS schedules. Both fixed the same day: it lists with core.quotepath off and reports a tracked file it can't open as "not read", and it searches the names as well as the text; a sample for each in tools\check.ps1.
- proofs (the engineer's runs of 11-20, not a review; IDs left out):
  - `.\infra\cloudflare\tf.ps1 plan`, with the state copied beside the branch's files: "No changes. Your infrastructure
    matches the configuration." With no private file and no variable: "Missing private setting(s): EQLP_CF_ACCOUNT_ID,
    EQLP_CF_ZONE_ID, EQLP_CF_CACHE_RULESET_ID, EQLP_CF_REDIRECTS_RULESET_ID. Terraform wasn't started."
  - `.\tools\private.ps1; az deployment group what-if ... --subscription $env:EQLP_AZ_SUBSCRIPTION_ID`: "Resource
    changes: 4 no change." With no variable, `az bicep build-params`: "Error BCP427: Environment variable
    "EQLP_AZ_OWNER_OBJECT_ID" does not exist and there's no default value set", and the same for
    `EQLP_AZ_IDENTITY_VALIDATION_ID` with only the first set.
  - `.\tools\check.ps1 -SamplesOnly`: "no home folder, home-network address or any of this PC's 16 private words in a
    tracked file". With a line holding a database's ID added to docs/debt.md by hand: "check failed: private text in
    tracked files ... docs/debt.md:97 private". The line was taken out again.
  - The NAS, 11-20 18:49: `EQLP_D1` added to `eqlp.env` (three names now) and `eqlp-set-token.sh` replaced with the
    repository's copy. Its own next run, read from the log and not started by hand: "19:10:07 [eqlp-community.sh]
    community: 5 players, 2583 crawls used ... uploaded to community/v1.json in 6.4 s". That run is still the build
    with a database of its own; the build without one arrives with the release (`job: true`), which runs it once.
  - `node migrate.mjs production` with no variable: "migrate: EQLP_D1_PRODUCTION isn't set", exit 1. With a made-up
    ID and `--local`, the six migrations applied to a local database. `npm run typecheck` and `npm test` (41 tests)
    pass with no variable set: the types step reads the configuration in mode `test`, since it stopped on production.
