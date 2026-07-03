# Branch ruleset (as code)

`main-branch-protection.json` is the branch protection for `main`, kept in the repo so
it can be reviewed and re-applied deterministically. It enforces:

- **squash-only** merges, **linear history**, no force-push, no branch deletion
- **1 approving review** required — a collaborator's review **or** CodeRabbit's bot
  review satisfies it. An approval only counts if the reviewer has **write** access, so
  this requires the CodeRabbit GitHub App to be installed with write permission. Automated
  approvals from `github-actions[bot]` (the Dependabot and owner-fast-track flows) also
  count, because `repo-settings.yml` enables "Allow GitHub Actions to approve PRs".
- **stale reviews dismissed** on new pushes, and **all review threads resolved**
- required status checks (strict / up-to-date): **Build & Test** (CI) and **CodeQL**
- **CodeQL** code-scanning must pass at "high or higher"
- the **Admin** and **Maintain** repository roles can **bypass** — so trusted
  collaborators can force-merge or merge without waiting for the cool-off window

The "PR must be approved" gate here is what the automated flows build on:

- `../workflows/auto-merge-approved.yml` merges an approved PR only after a 7-day cool-off.
- `../workflows/auto-merge-trusted.yml` fast-tracks the owner's own PRs.
- `../workflows/dependabot-auto-merge.yml` auto-approves + merges Dependabot PRs (still
  gated on CI), so bot dependency bumps are free of the human-approval requirement.

## Apply / update it

GitHub does not auto-apply ruleset files — import once (the token must be a repo admin;
the `gh` CLI logged in as the owner works):

```bash
# create
gh api repos/Alpaq92/FFMpeg.StaticFetcher/rulesets \
  --method POST --input .github/rulesets/main-branch-protection.json

# update later: find the id, then PUT
gh api repos/Alpaq92/FFMpeg.StaticFetcher/rulesets --jq '.[] | "\(.id)\t\(.name)"'
gh api repos/Alpaq92/FFMpeg.StaticFetcher/rulesets/<id> \
  --method PUT --input .github/rulesets/main-branch-protection.json
```

Or via the UI: **Settings → Rules → Rulesets → New ruleset → Import**.

> **Note on required-check names.** The `required_status_checks` contexts must match the
> job **names** GitHub reports: `Build & Test` (the `build-test` job in `ci.yml`) and
> `CodeQL` (the job in `codeql.yml`). If you rename those jobs, update this ruleset to match,
> or merges will block on a check that never reports.
