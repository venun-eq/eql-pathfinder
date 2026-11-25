# CI/CD on GitHub: releases that don't need this PC or the NAS

**Plan, 11-24 (v0.10.34). Not a change of its own:** this page is the index of the drafts it leads to. Nothing is built
until the user has answered the decisions below. Each phase then becomes a draft of its own, solved in its own branch
and session, in the order below.

**Why:** the user, 11-24: "Draft a plan for how we can move CI/CD to github, and get the process off of just my local
machine + NAS." Releases today need this PC switched on and signed in (the hourly task runs only while you're signed
in), four personal logins kept alive, and the NAS for the community numbers and the notifications. The same day,
GitHub's CI went red for v0.10.34 on a file the public snapshot leaves out, and nobody saw it until after the push
(fixed in `ci-public-queue`).

**Players:** nothing changes for them. They update from the same feed (`updates.eqlpathfinder.com`), get the same
signed installer, and read the same What's new. What changes is where it's built.

## Where everything runs today

| What | Where it runs | What it needs |
|---|---|---|
| The gate (`check.ps1`) | this PC, in `finish` | Edge, .NET, Node |
| Release (`ship.ps1` hourly → `release.ps1` → `publish.ps1`) | this PC's scheduled task, while signed in | `az login` (Artifact Signing), the R2 key (DPAPI, this account only), the private settings |
| The website (What's new) | `ship.ps1` on this PC | the `cf` login |
| The version commit `v<version>` | this PC's main | the main checkout |
| The public source | `publish-github.ps1 -Push`, by hand | gitleaks, your terminal |
| The community job (every 30 min) | the NAS's `scheduler` container | a Cloudflare token limited to the home IP address (D1 read, R2 edit) |
| Notifications (ntfy) | the NAS, over ssh | the NAS login |
| GitHub CI | GitHub, on the public snapshot only | nothing private |
| Server deploy, migrations, Terraform, Bicep | this PC, by hand | your logins |

What's already in our favor:
- **Signing is Azure Artifact Signing,** and `infra/azure/signing.bicep` already grants the signer role to a list of
  identities (`releaseSigners`). GitHub can sign in to Azure with OIDC (a federated credential), so no Azure secret is
  stored anywhere.
- **The private settings were built for this:** every tool reads `EQLP_*` from the environment, and docs/pipeline.md
  says "A CI service sets the same names as secret variables and needs no file."
- **The Windows runner already builds and runs all 999 tests,** page tests in Edge included (the fixed CI run).

## The decisions (the user's)

**1. Where the real main lives.** This is the decision everything else waits for, and the one that's hard to take
back. It goes to the architect, and on to Fable for the final call (CLAUDE.md: "a decision that can't be taken back").

| | A. A private GitHub repository holds main, with full history (recommended) | B. The public repository becomes main, with new history from now on |
|---|---|---|
| History | every commit, private, on GitHub's servers | the old history stays on this PC; every new commit is public as soon as it's pushed |
| The public source | a step of the release pushes the snapshot, as `publish-github.ps1` does now | is main itself |
| Queue, debt, spec images | stay in the repository | move to a private repository or stay on this PC |
| Risk | GitHub holds the old account IDs and home details (private, but off this PC) | a private word in any branch commit is public for good: the gate has to run before every push, not only on main |
| Outside PRs | still copied in by hand | merge directly |
| Actions minutes | private repos have a monthly allowance, and Windows minutes count double: check current pricing (a release is about 10 Windows minutes) | free on public repositories |

Recommended: **A.** It keeps every promise in docs/specs/github-launch.md, it changes the least about how sessions work,
and B can still follow later. Going from B back to A isn't possible.

**2. What starts a release.** (a) Every merge to main that brings a note in `docs/releases/next` (fastest; releases
queue one at a time). (b) An hourly schedule, as now, which groups what was merged that hour. (c) A tag pushed by you,
as everquest-companion does. Recommended: **(b) at first** so behavior matches today, then (a) once
`quiet-releases` is in (it keeps invisible changes from making releases of their own).

**3. Who pushes main.** Today it's "a session's commits stay on this PC: pushing main is the user's, by hand". With
CI/CD, `finish` would push the branch, open a pull request, and merge it when GitHub's checks pass, with branch
protection on main. That changes the rules in CLAUDE.md and the `feature` skill, so you review that change.
Recommended: sessions push feature branches and open PRs, and a PR merges itself when it's green. Pushes straight to
main are refused for everyone, sessions included.

**4. Where notifications go.** The ntfy topics are reached through the NAS. Options: make the homelab ntfy reachable
from the internet (for example through a Cloudflare Tunnel), use ntfy.sh with a secret topic name, or rely on GitHub's
own failure emails and the Actions page. Recommended: **a Cloudflare Tunnel to the homelab ntfy** if it's simple on
your network, otherwise ntfy.sh. The topics stay the same either way.

**5. The community job.** (a) A scheduled GitHub workflow on Linux (GitHub can start scheduled runs late under load,
and turns schedules off in a public repository after 60 days without activity: neither applies to A's private repo
with daily commits); (b) stay on the NAS. Moving it needs a new Cloudflare token without the home IP limit, scoped to D1
read and R2 write on the one bucket. Recommended: **(a), run alongside the NAS for a week,** comparing the files they
make, then the NAS job is switched off.

## The phases, and what each waits for

Every phase except 0 is **risky** (the release scripts, how updates arrive, the infrastructure): `qa` writes the spec
and failing tests, the architect decides before code, the engineer builds, the architect reviews. Budgets are first
estimates. Each phase ends with something working, and `ship.ps1` stays usable by hand until phase 3 has run for two
weeks, as the way back.

| Order | Draft | What's true when it's done | Tier, budget | Waits for |
|---|---|---|---|---|
| 0 | decisions | the five answers above, recorded here | the user + architect + Fable for 1 | nothing |
| 1 | gh-remote | main lives where decision 1 says, CI runs `check.ps1 -All` on every PR, main is protected | risky, 250k | 0 |
| 2 | gh-finish | `finish` pushes the branch and opens a PR that merges itself when it's green; CLAUDE.md and the `feature` skill say so | risky, 300k (rules: your review) | 1 |
| 3 | gh-release | a workflow does what `ship.ps1` does: version, notes, build, test, sign (OIDC), publish to R2, the version commit, What's new, the post-release checks, the notification. First on the beta channel, then the hourly task on this PC is removed | risky, 600k | 2 |
| 4 | gh-snapshot | the public snapshot is a step of the release (decision 1A), with the same refusals as `publish-github.ps1`, gitleaks included. It runs CI's checks on the published tree first, which closes the gap that hit v0.10.34 | risky, 200k | 3 |
| 5 | gh-community | the community job runs on GitHub on its schedule, beside the NAS for a week, then alone | risky, 250k | 3 |
| 6 | gh-deploys | the server (migrations, then deploy, staging then production) and Terraform/Bicep run from workflows you start by hand, each behind a GitHub environment that waits for your approval | risky, 400k | 3; can wait |
| 7 | docs | docs/pipeline.md, docs/community-job.md, the `release-status` and `community-pool` skills rewritten for the new places | trivial, 60k | 3, 4, 5 |

Phases 4, 5 and 6 don't depend on each other. Phase 6 can wait: those steps stay by hand anyway (CLAUDE.md), and
moving them only removes the need for this PC's logins.

## What moves where (for the drafts)

- **Signing:** an Entra app registration with a federated credential for the repository's release workflow (its
  `environment`, so only that job can sign), added to `signerObjectIds` in `infra/azure/signing.bicepparam`. Remove
  `WorkloadIdentityCredential` from `ExcludeCredentials` in `tools/signing.json` for CI. `az login` on this PC becomes
  optional.
- **R2:** an R2 API token scoped to the update bucket, as GitHub secrets, in place of `r2.xml`.
- **The website:** a Cloudflare API token scoped to deploying the site, in place of the `cf` login. This is the
  "scoped Cloudflare API token" already listed under hardening in docs/pipeline.md.
- **The private settings:** the eleven `EQLP_*` names become secrets of the release environment. `EQLP_DENY` stays a
  secret, so the gate's "nothing private" step still has words to deny.
- **Least privilege, as everquest-companion does it:** the PR checks run with a read-only token and no secrets, and only
  the release job gets write access, the signing identity and the R2 key. Actions are pinned to exact commit SHAs, and
  Node is moved off 20 (the runs warn that it's deprecated).
- **The lock:** `finish` and `ship` share a lock on this PC. On GitHub, a `concurrency` group on the release workflow does
  the same, so two releases never run at once.

## Not doing

- Changing what players get, the feed, the installer, or how updates arrive in the app.
- Moving development off this PC: sessions still work in worktrees here. Only the gate's second run, the release and
  the jobs move.
- Taking outside contributions straight into main: CONTRIBUTING.md's policy stays as it is until decision 1 says otherwise.
