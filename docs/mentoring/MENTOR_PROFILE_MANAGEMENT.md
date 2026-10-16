# Mentor profile management

Admins create and edit mentor accounts and profiles from `/mentors`. Creating an account requires a temporary password, following the existing admin account workflow. Email addresses are unique across accounts. Changing mentor type requires reassigning active teams first.

Profiles store expertise, startup domains, technology skills and mentor tags as separate arrays. Structured experience entries store kind (`Startup` or `Technology`), domain/skill, optional years (0–80 with one decimal), level and notes. Duplicate tags and duplicate kind/area entries are rejected after trimming and case-insensitive comparison. Free-text experience is retained for existing profiles.

Lecturers can search and filter only profiles available in semesters where they are assigned as the primary lecturer, preserving existing directory scope. Structured metadata is included in both keyword matching and the embedding text; the embedding cache detects changed text.

## Semester availability

The supplied EHUB-475 screenshot includes only a title. This implementation uses the existing semester staff availability model: an Active Mentor entry means available; Inactive or no entry means unavailable. Admins use the Availability button to manage each semester independently. Closing/completed/archived semesters are read-only. Existing teaching-staff APIs preserve rowVersion checks, audit/outbox behavior and checks that prevent deactivation while assignments are in use. This does not define weekly meeting hours or a maximum team capacity.

## Migration and rollback

After merging `origin/develop` on 2026-10-09, `20261009080700_AddStructuredMentorExperience` adds only the `mentor_experiences` table. The shared startup domain, technology skill and mentor tag arrays come from develop's `AddMentorProfileTags` migration. Both mentor APIs use the same `mentor_tags` column. The experience table has a mentor foreign key with cascade deletion, a unique mentor/kind/area index, kind validation and a years range check. Existing free-text experience is not automatically converted because its domain and years cannot be inferred reliably. The original uncommitted 20261006091158 migration was regenerated to avoid duplicate columns.

Apply with the normal reviewed EF migration process. Rollback targets the preceding migration, `20261009043959_AddSemesterTemporaryMentors`, using `dotnet ef database update` with the Infrastructure project and Api startup project. Back up structured mentor experience before rollback: `Down` drops only the experience table, while shared metadata and existing profile/account data remain.

## Verification (2026-10-06)

| Command | Result |
| --- | --- |
| `dotnet build EHub.slnx --no-restore` | Passed; one existing nullable warning in RegisterCommandHandlerTests. |
| `dotnet test EHub.slnx --no-build --filter "FullyQualifiedName!~IntegrationTests"` | 137 unit + 368 application tests passed. |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-restore` | 301/302 passed using Docker/PostgreSQL and the complete migration chain. |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~MentorProfileManagementIntegrationTests"` | 5/5 passed using normal migrations: creation/update, invalid/duplicate data, 401, 403 and 404. |
| `npm run lint` | Passed with existing warnings outside the new mentor components. |
| `npm run type-check` | Passed. |
| `npm test` | 311/312 passed; all added mentor tests passed. |
| `npm run build` | Passed. |

The remaining integration failure is `NotificationEmailOutboxIntegrationTests.AppDbContext_WakesOutboxOnlyAfterTheContainingCommit`: its standalone Npgsql options omit `UseVector()` and cannot map the existing MentorEmbedding vector property. The remaining frontend failure is dark-theme coverage for `bg-indigo-50/50` in AssignMentorsModal. Neither failing file was changed for this task.

Browser checks with fake local data verified duplicate-tag validation without clearing the form, creation with structured experience, successful semester availability, confirmation and refusal to deactivate an assigned mentor, and layout at 390px width. No staging/production database was modified.

## Verification after pulling develop (2026-10-09)

Merged `origin/develop` at `010726a` into `AI` with merge commit `9b1b6ad`. Resolved four merge conflicts and five conflicts when restoring the uncommitted mentor work. Recommendations were moved into the new per-team MentorSlotDialog. The old carryover API and test were removed in accordance with develop's removal of that feature. The uncommitted mentor work remains unstaged; a backup is retained in the stash named `codex: preserve mentor work before pulling develop 2026-10-09`. Nothing was pushed.

| Command/check | Result |
| --- | --- |
| `dotnet build src/EHub.Api/EHub.Api.csproj --no-restore -m:1` | Passed, 0 warnings/errors. |
| `dotnet test tests/EHub.ApplicationTests/EHub.ApplicationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~Mentoring"` | 21/21 passed outside the sandbox. The initial sandbox testhost connection timed out. |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~MentorProfileManagementIntegrationTests"` | 5/5 passed with Docker/PostgreSQL and the complete merged migration chain. Docker was started after the first attempt could not connect. |
| EF `migrations has-pending-model-changes` with the Infrastructure and Api projects | No pending model changes. |
| `npm run type-check` / `npm run build` | Both passed. |
| Node test runner with `--test-name-pattern="mentor"` over mockApi, mentorProfiles, mentorProfileForm, mentorTags, mentorSlotBoard and mentorBatchAssignment tests | 17/17 passed. The initial wider run exposed an obsolete carryover test, corrected in the merge commit. |
| Oxlint on the changed mentor UI, APIs and mock source files | Passed. The repository-wide `npm run lint` did not succeed and scanned dependency files under node_modules. |
| `git diff --check` and unresolved paths check | Passed; no unresolved conflicts. |

The full backend/frontend test suites were not rerun for this pull. No real database migration was applied. The earlier 2026-10-06 results describe the state before this merge.
