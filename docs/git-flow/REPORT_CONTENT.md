# EHub Git Branching and Release Flow

## Figure caption

**Figure X. EHub Git Branching and Release Flow**

The simplified figure is intended for the main report. The detailed variant may be placed in the appendix when individual CI/CD, Release Candidate and branch-retention checkpoints need to be demonstrated.

## Report description

EHub adopts a controlled Git Flow variant with `develop` as the protected integration branch and `main` as the protected production branch. Task-scoped `feature/*`, `fix/*`, `docs/*` and `chore/*` branches are created from the latest `develop` and validated through a pull request, automated CI checks and peer review. After a successful merge, the branch reference is retained for traceability but becomes inactive and must not receive new work. Every accepted change on `develop` is automatically deployed to the staging environment for team and mentor verification.

When the integrated scope is ready for delivery, a `release/vX.Y.Z` branch is created from `develop`. Only release stabilization, migration review, documentation and blocking fixes are permitted on this branch. Every release candidate, including any release-only fix, is deployed to the release UAT environment and must pass final acceptance before the branch can be merged into `main`. The accepted commit is assigned an immutable Semantic Versioning tag and deployed to production. Release-only changes are merged back into `develop`; the release branch is then retained as an inactive reference and must not be reused.

Urgent production defects use a `hotfix/*` branch created from `main`. The hotfix must still pass pull-request review, CI and production approval. The resulting patch is tagged and deployed from `main`. The production fix is then synchronized from `main` back into `develop` and, when necessary, any active release branch to prevent regression.

## Branch policy

| Branch type | Created from | Merged into | Purpose |
|---|---|---|---|
| `feature/*` | `develop` | `develop` | Product feature development |
| `fix/*` | `develop` | `develop` | Defect not yet present in production |
| `docs/*` | `develop` | `develop` | Documentation and report artifacts |
| `chore/*` | `develop` | `develop` | CI/CD, dependency and infrastructure maintenance |
| `release/vX.Y.Z` | `develop` | `main`, then `develop` | Release stabilization and final acceptance |
| `hotfix/*` | `main` | `main`, then `develop` | Urgent production correction |
| `develop` | Long-lived | `release/*` | Integrated staging-ready source |
| `main` | Long-lived | Production | Auditable production history |

## Required controls

- Direct pushes to `develop` and `main` are prohibited.
- A pull request, successful required checks and at least one approval are required before merge.
- Merged task, release and hotfix branches are retained as inactive references and must not receive new commits or be reused for later work.
- Production releases are identified by immutable `vX.Y.Z` tags.
- Release and hotfix changes are synchronized back into `develop`.
- GitHub Actions reports CI status and Discord distributes non-blocking team notifications.
