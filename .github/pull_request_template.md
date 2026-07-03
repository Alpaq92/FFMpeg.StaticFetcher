<!-- Thanks for contributing! Keep PRs focused and small where possible. -->

## What & why

<!-- What does this change do, and why? Link any related issue: Closes #123 -->

## Type of change

- [ ] Bug fix (non-breaking)
- [ ] New feature (non-breaking)
- [ ] Breaking change (API or behavior) — note it in `CHANGELOG.md` under **Breaking**
- [ ] Docs / tests / CI only

## Checklist

- [ ] `dotnet build -c Release` succeeds with no new warnings
- [ ] `dotnet test --filter "Category!=Integration"` passes locally
- [ ] Added/updated tests for the change
- [ ] Updated `README.md` / `CHANGELOG.md` if behavior or the public API changed
- [ ] The PR is scoped to one logical change (squash-merge produces one clean commit)

<!--
Merge flow (see CONTRIBUTING.md):
- CI (Build & Test) + CodeQL must pass, and all review threads resolved.
- Needs one approval — a maintainer or CodeRabbit.
- After approval, it auto-merges once the 7-day cool-off elapses (a maintainer can merge sooner).
-->
