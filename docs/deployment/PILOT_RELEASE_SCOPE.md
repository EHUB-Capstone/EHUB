# EHUB Pilot Release Scope

This document defines the controlled first release for six classes on
`https://e-hub.com.vn`. It is a release boundary, not a statement that future
modules have been cancelled.

## Release policy

- Production frontend mocks must remain disabled.
- Realtime integration must remain disabled until its production transport and
  reverse-proxy path are verified.
- A feature is included only when its frontend, backend contract, authorization
  checks and production smoke tests are complete.
- Optional pages remain visible in navigation without a development label, as
  required for the pilot presentation. Their visibility is not evidence that
  unfinished actions are included in the accepted release scope.
- The release candidate must come from a reviewed Pull Request and an immutable
  commit SHA. Do not deploy a developer working tree or an unreviewed feature
  branch.

## Candidate pilot capabilities

The following capabilities form the candidate release scope. Each item must
still pass the role-based smoke checklist before the release is approved:

- System and Google authentication, registration OTP, password reset, logout
  and profile update.
- Admin user management, account approval and lecturer account import.
- Semester, subject, teaching staff and class management.
- Class roster import/export, enrolment, major verification and lifecycle.
- Team formation, membership, leader and mentor assignment.
- Student class and team self-service.
- Project direction and the implemented project workspace operations.
- Weekly execution-board tasks.
- In-app notification history and read state.
- Admin dashboard and authentication tracking.

## Visible pages outside the accepted first-pilot scope

Static inspection found frontend clients for these modules without a complete
matching production API surface. The pages remain visible, but their workflows
must not be represented as production-ready until their backend contracts and
authorization tests are complete. Each build-time flag remains available as an
emergency switch if a page must be hidden after verification:

| Module | Build-time flag |
| --- | --- |
| AI analysis | `VITE_FEATURE_AI` |
| In-platform chat | `VITE_FEATURE_CHAT` |
| Data Bank | `VITE_FEATURE_DATA_BANK` |
| Evaluation reports | `VITE_FEATURE_EVALUATIONS` |
| Mentoring sessions | `VITE_FEATURE_MENTORING` |
| Rankings | `VITE_FEATURE_RANKINGS` |
| Lecturer, mentor and student dashboards | `VITE_FEATURE_ROLE_DASHBOARDS` |
| Legacy startup-idea screens | `VITE_FEATURE_STARTUP_IDEAS` |
| Workshops | `VITE_FEATURE_WORKSHOPS` |

Class chat membership repair and unavailable development controls also remain
disabled in the pilot image. Core class features keep their explicit Docker
build arguments so that a future release can change one capability without
silently changing the others.

## Required release gates

Before tagging the pilot release:

1. Backend build and unit/application tests pass in GitHub Actions. Integration
   tests remain visible but temporarily non-blocking for the first pilot; every
   failure must be recorded and resolved before final handover or before the
   integration suite is restored as a required release gate.
2. Frontend uses the lock file (`npm ci`) and passes lint, type checking, all
   tests and the production build.
3. The production frontend is built with
   `VITE_ENABLE_API_MOCKS=false` and `VITE_ENABLE_REALTIME=false`.
4. Every included capability passes success, validation, unauthenticated,
   forbidden and resource-ownership checks appropriate to that flow.
5. Gmail and an activated `fpt.edu.vn` Google account both pass the production
   login smoke test.
6. Lecturer and student Excel imports are tested through the production-style
   Nginx path, including a file larger than 1 MB.
7. No release artifact contains local `.env`, user secrets, credentials or
   personal data.

## Release identification

Use an annotated pilot tag only after all gates are green, for example:

```text
v0.1.0-pilot
```

Docker images and deployment records must also retain the exact Git commit SHA
so the running version can be identified and rolled back.
