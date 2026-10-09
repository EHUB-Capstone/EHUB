# Phase 0.5 — Class Management Domain Decisions

Status: Approved and implemented locally

This record freezes the class-management rules that later phases must preserve.
It does not authorize deployment and does not enable unfinished Mentor, Team,
Major verification, Archive, Restore, Delete, or Project Direction screens.

## Class identity and lifecycle

- `ClassCode` is unique inside one semester, not globally.
- `(SemesterId, CourseId, ClassIndex)` is also unique.
- A new class starts as `Draft`.
- `Draft` may temporarily have no lecturer or no schedule.
- `Active` must have exactly one lecturer and at least one schedule slot.
- Adding the missing schedule to a Draft class that already has a lecturer activates it.
- An Active class cannot be unassigned. Admin must reassign from the old lecturer
  to the new lecturer atomically.
- Archived classes are read-only and do not participate in schedule or enrollment
  conflict checks. Archive/restore endpoints remain a later phase.

## Lecturer ownership

- One class has at most one `ClassLecturer` row, and it is always primary.
- `Class.PrimaryLecturerId` is the fast ownership reference used for authorization.
- Reassignment revokes the previous row, creates/updates the new row, updates the
  class reference, and writes an audit record in one save transaction.
- Lecturer can create a class for themselves, manually add a student, and run the
  two-step student import for their assigned class.

## Schedule

- A class may have multiple meetings per week.
- Schedule and teaching assignment remain separate API contracts.
- Draft and Active classes participate in conflict detection; Archived classes do not.
- Reassigning a lecturer rechecks the existing schedule against that lecturer's other
  classes without mutating the schedule.

## Enrollment uniqueness

- A student cannot have two counted enrollments for the same `CourseId` in the same
  `SemesterId`, even when the class codes are different.
- Different courses in the same semester are allowed.
- The same course in a later semester is allowed.
- A dropped enrollment no longer counts toward this limit. Completed enrollment still counts.
- `ClassStudent` stores the immutable semester/course scope and PostgreSQL enforces
  the rule with a partial unique index.

## Major ownership

- `Student.MajorCode` is the current global profile value.
- `ClassStudent.MajorCodeAtEnrollment` is the value used by that class enrollment.
- Adding or importing an existing student never overwrites the global profile.
- Changing an enrollment major resets its verification state.
- The assigned lecturer may later edit, verify, and lock enrollment-major data only.
- Admin may later unlock/override with an audit reason. These workflow endpoints stay
  behind their feature flag until the dedicated phase is implemented.

## Student import

- Required columns are `StudentCode`, `FullName`, `Email`, and `MajorCode`.
- Structural file errors block the file. Data errors are attached to their row.
- Preview writes no student/enrollment data.
- Preview creates a PostgreSQL-backed session bound to `UserId` and `ClassId` for 30 minutes.
- Commit reacquires the session with optimistic concurrency, rechecks ownership,
  archive state, identity, and enrollment conflicts, then commits valid rows and session
  consumption in one database transaction.
- A processing lease allows recovery after a crashed request; a consumed session cannot
  be replayed.
- Rows that became invalid after preview are skipped and returned with row-level errors.

## Deferred decisions already fixed for later phases

- Archive is the normal removal mechanism. Restore is Admin-only with revalidation and audit.
- Permanent deletion is not a normal UI action; it is limited to privileged cleanup of an
  empty, never-used class.
- Mentor follows least privilege and can see only explicitly assigned teams/classes.
- Mentor never inherits Lecturer or Admin class-management routes.
