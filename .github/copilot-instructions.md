# Commit Conventions (required)

This repository uses **semantic-release** with **Conventional Commits**. CI derives the next
version *entirely* from commit-message prefixes. A commit without a release-triggering prefix
produces **no new version**, and the `build-*` CI jobs are **skipped** (the run looks green but
no artifact is produced). Every commit — by a human or an AI agent — MUST use a Conventional
Commit message:

| Prefix | Effect | Use for |
|--------|--------|---------|
| `feat:` | minor bump | a new user-facing feature |
| `fix:` | patch bump | a bug fix |
| `feat!:` or a `BREAKING CHANGE:` footer | major bump | a breaking change |
| `perf:` | patch bump | a performance fix |
| `docs:` / `chore:` / `ci:` / `refactor:` / `style:` / `test:` / `build:` | **no release** | changes that intentionally shouldn't ship a new version |

**To force a release for a change that isn't a `feat`/`fix`** (e.g. a CI-only or workflow-only
change that still needs to be built and published), use the `force-patch` scope:

```
ci(force-patch): <description>
```

`.releaserc.json`'s `commit-analyzer` `releaseRules` maps the `force-patch` scope to a patch
release. There is also a `no-release` scope for the opposite intent (explicitly suppress a
release).

**Never** write a plain, prefix-less commit message on a branch that is expected to build.
