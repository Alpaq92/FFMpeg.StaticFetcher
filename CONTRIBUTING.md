# Contributing to FFMpeg.StaticFetcher

Thanks for your interest in improving **FFMpeg.StaticFetcher** — a small, dependency-light
.NET 8 library that downloads static `ffmpeg`/`ffprobe` binaries. This document explains how
to build the project, how the automated review-and-merge pipeline works, and the one-time
setup a maintainer needs to enable it.

> **TL;DR merge flow:** open a PR → CI (`Build & Test`) + CodeQL must pass → CodeRabbit
> reviews it → one approval (a maintainer **or** CodeRabbit) → resolve all threads → it
> **auto-merges one week after approval**. Maintainers can merge sooner. Dependabot PRs are
> handled automatically.

---

## 1. Prerequisites

- **.NET SDK 8.0** or newer — <https://dotnet.microsoft.com/download>
- Git, and (optional) the [GitHub CLI](https://cli.github.com/) for the maintainer scripts

Clone and restore:

```bash
git clone https://github.com/Alpaq92/FFMpeg.StaticFetcher.git
cd FFMpeg.StaticFetcher
dotnet restore
```

## 2. Build & test

```bash
# Build everything
dotnet build -c Release

# Fast, offline test suite — this is the CI gate. Run this before pushing.
dotnet test --filter "Category!=Integration"

# Full suite including live-network integration tests (hits the GitHub API + downloads).
# Optional locally; set GITHUB_TOKEN to avoid the 60 req/hr anonymous rate limit.
dotnet test
```

### Project layout

| Path | What it is |
|---|---|
| `FFMpeg.StaticFetcher/` | The library (the published NuGet package) |
| `FFMpeg.StaticFetcher.Tests/` | xUnit tests — offline mock tests + `[Trait("Category","Integration")]` live tests |
| `FakeFFmpeg/` | A tiny stub exe used by the tests to exercise the real version-probe process path |

The offline tests must stay green and fast; the integration lane is informational (it runs
in CI as a separate non-blocking job).

## 3. Making a change

1. **Branch** off `main`: `git switch -c fix/short-description`.
2. Keep the change **focused** — one logical change per PR (we squash-merge, so a PR becomes
   one commit on `main`).
3. **Add or update tests** for anything you change.
4. Update **`README.md`** and **`CHANGELOG.md`** when behavior or the public API changes.
   The changelog follows [Keep a Changelog](https://keepachangelog.com/); put entries under
   `## [Unreleased]`, and call out breaking changes under **Breaking**.
5. Code style is guided by [`.editorconfig`](.editorconfig) — most rules are suggestions, so
   they won't fail the build, but please follow the prevailing style (file-scoped namespaces,
   `_camelCase` private fields, `var` when the type is apparent).
6. Push and **open a pull request against `main`**.

### Commit / PR titles

Because PRs are squash-merged, the **PR title** becomes the commit subject on `main`. Prefer a
short imperative summary, optionally [Conventional Commits](https://www.conventionalcommits.org/)
style (`fix:`, `feat:`, `docs:`, `chore:`). The release automation reads these:

- A PR title/body containing `[major]` or `BREAKING CHANGE` triggers a **major** version bump.
- A merged Dependabot **NuGet** PR (one that touches a `.csproj` / `packages.lock.json`)
  triggers a **minor** bump. Dependabot **GitHub-Actions** updates only change workflow files,
  so they fall through to a **patch** bump.
- Everything else is a **patch** bump.

## 4. How review & merge works (the pipeline)

Every PR to `main` goes through the same gates, enforced by a
[branch ruleset](.github/rulesets/README.md):

1. **Continuous integration** — [`ci.yml`](.github/workflows/ci.yml) runs `Build & Test`
   (the offline suite) on Ubuntu. A separate, non-blocking job runs the live integration tests.
2. **Security scanning** — [`codeql.yml`](.github/workflows/codeql.yml) runs CodeQL over the C#
   code. It must pass at "high or higher".
3. **Automated review** — [**CodeRabbit**](https://coderabbit.ai) reviews every PR, posts a
   summary and line comments, and submits a real **Approve** or **Request changes** review.
4. **Approval** — the ruleset requires **one approving review**. That can come from a
   **collaborator** *or* from **CodeRabbit** (its approval counts). New pushes dismiss stale
   approvals, and **all review conversations must be resolved**.
5. **Cool-off + auto-merge** — once a PR is approved,
   [`auto-merge-approved.yml`](.github/workflows/auto-merge-approved.yml) merges it
   automatically **7 days after the approval** (the clock starts at approval, not at PR
   creation), provided it's still mergeable and checks pass. The workflow runs every 6 hours.

Merges are **squash-only** and history stays **linear**; force-pushes and branch deletion on
`main` are blocked.

### Shortcuts and exemptions

- **Maintainers can bypass.** Anyone with the **Admin** or **Maintain** role can merge a PR
  immediately — skipping the approval and/or the 7-day cool-off — via the ruleset's bypass. Use
  this for urgent fixes or trusted changes.
- **The owner's own PRs are fast-tracked.**
  [`auto-merge-trusted.yml`](.github/workflows/auto-merge-trusted.yml) approves (as
  `github-actions[bot]`, since an author can't approve their own PR) and enables auto-merge on
  the owner's PRs — no external review, no cool-off. CI + CodeQL still gate the merge.
- **Dependabot is automated.** [`dependabot.yml`](.github/dependabot.yml) opens monthly NuGet
  and GitHub-Actions update PRs;
  [`dependabot-auto-merge.yml`](.github/workflows/dependabot-auto-merge.yml) auto-approves and
  auto-merges **minor/patch** bumps once CI passes (no human approval needed), and flags
  **major** bumps for manual review.
- **GitHub Actions are exempt too.** Automated workflows (the auto-merge flows, the release
  job) act without a human approval — the release job pushes tags directly, and the auto-merge
  jobs only act on PRs that already satisfy the rules.

### Manually merging faster

To ship an approved PR before the cool-off, a maintainer can either merge it in the GitHub UI
(the bypass allows it) or run the cool-off workflow with a smaller window:

**Actions → Auto-merge approved PRs after cool-off → Run workflow → cool-off-days: `0`**.

## 5. Releases

[`release.yml`](.github/workflows/release.yml) publishes to NuGet.org. On a push to `main` (and
on a monthly schedule, and via manual dispatch) it computes the next version from git tags and
the commit message (see §3), builds, tests, packs, `dotnet nuget push`es the package, tags the
commit `vX.Y.Z`, and creates a GitHub Release. It needs the `NUGET_API_KEY` secret.

## 6. Security

- **CodeQL** scans every PR and push, plus a weekly scheduled run.
- **Dependabot alerts** and **automated security fixes** are enabled.
- **Private vulnerability reporting** is enabled — please report security issues privately via
  the repository's **Security → Report a vulnerability** rather than a public issue.

---

## 7. Maintainer setup (one-time)

Most of the pipeline is code in this repo, but a few things must be enabled once on GitHub:

### a. Install the CodeRabbit GitHub App  *(required for automated review/approval)*

CodeRabbit is a GitHub App and **cannot be installed from a config file** — a maintainer must
authorize it:

1. Go to <https://coderabbit.ai>, sign in with GitHub, and install the app on
   `Alpaq92/FFMpeg.StaticFetcher` with **read & write** access (write is needed so its approval
   counts toward the required review).
2. That's it — [`.coderabbit.yaml`](.coderabbit.yaml) in this repo configures its behavior.

> Until the app is installed, PRs still work — but the only way to get the required approval is a
> human maintainer review (or the maintainer bypass).

### b. Repository secrets

| Secret | Required? | Purpose |
|---|---|---|
| `NUGET_API_KEY` | for releases | `dotnet nuget push` in `release.yml` |
| `ADMIN_PAT` | optional | A fine-grained PAT (repo-scoped, **Administration: read/write**) used by [`repo-settings.yml`](.github/workflows/repo-settings.yml) to keep repo settings in sync, and preferred by `auto-merge-approved.yml` so a merge can re-trigger the release workflow. Without it, `repo-settings.yml` fails and `auto-merge-approved.yml` falls back to `GITHUB_TOKEN`. The Dependabot and owner fast-track flows do **not** need it — they approve as `github-actions[bot]` via `GITHUB_TOKEN`. |

Add them under **Settings → Secrets and variables → Actions**.

The bot approvals above rely on **Settings → Actions → General → "Allow GitHub Actions to
create and approve pull requests"** being enabled — `repo-settings.yml` (and the one-time
`gh` setup) turns this on for you. Without it, Dependabot and owner PRs can't self-approve and
won't auto-merge.

### c. Branch ruleset & repo settings

The branch protection lives in [`.github/rulesets/main-branch-protection.json`](.github/rulesets/main-branch-protection.json)
and was applied at setup. To re-apply after editing it, or to review it, see
[`.github/rulesets/README.md`](.github/rulesets/README.md). Repository-level toggles
(auto-merge enabled, squash-only, delete-branch-on-merge, security features) are applied by
[`repo-settings.yml`](.github/workflows/repo-settings.yml) — or once via `gh` at setup.

### d. Note on required check names

The ruleset requires the status checks **`Build & Test`** and **`CodeQL`** — these are the *job
names* in `ci.yml` and `codeql.yml`. If you rename those jobs, update the ruleset to match, or
PRs will block on a check that never reports.

---

Questions? Open a [discussion or issue](https://github.com/Alpaq92/FFMpeg.StaticFetcher/issues).
