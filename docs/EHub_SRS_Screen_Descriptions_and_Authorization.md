# 3.1 Screen Specifications

This section is aligned to the existing E-HUB routes and UI. `Not implemented` means there is no dedicated frontend screen in the current project; it must not be presented as a completed screen in the SRS.

## 3.1.1 Screen Flows

The accompanying draw.io file contains four actor-specific tabs: Administrator, Lecturer, Mentor, and Student. Solid coloured boxes are current screens. Grey boxes identify functions that have no dedicated screen in the current frontend.

Figure 3.1 E-HUB Screen Flows by Actor

## 3.1.2 Screen Descriptions

| Screen ID | Screen name and route | Actor | Description and main actions | Related requirements | Implementation |
|---|---|---|---|---|---|
| SCR-01 | Register account `/register` | All users | Creates a system account, verifies the registration OTP, then continues to sign in. | 3.1 | Available |
| SCR-02 | Authentication `/login` | All users | Signs in with a system account or Google OAuth; logout is available from the application shell. | 3.2 | Available |
| SCR-03 | Password management `/forgot-password`, `/reset-password`, `/settings` | All users | Requests password reset, resets a password using the reset flow, and changes password from profile settings. | 3.3 | Available |
| SCR-04 | Profile settings `/settings` | All users | Views and updates the signed-in user's common profile, avatar, and password. It is not a dedicated mentor-profile screen. | 3.4, 3.34 | Available |
| SCR-05 | User management `/admin/users` | Administrator | Paged user table with name/email/student-ID search, role and status filters, and account-status controls. | 3.5 | Available |
| SCR-06 | User access logs | Administrator | Intended access-log/audit screen. No route or frontend page currently exposes user access logs. | 3.6 | Not implemented |
| SCR-07 | Account approvals `/admin/account-approvals` | Administrator | Lists pending Lecturer and Mentor registration requests; administrator approves or rejects each request. | 3.7 | Available |
| SCR-08 | Subject and semester management `/admin/subjects` | Administrator | Plans, edits, activates, completes, or reopens semesters; lists and manages subjects/courses and teaching staff by semester. | 3.8, 3.9 | Available |
| SCR-09 | Subject configuration `/admin/subjects/:subjectCode` | Administrator | Configures subject roadmap, checkpoints, requirements, criteria, and rubric status. Current checkpoint editor has no deadline field. | 3.9.3, 3.26, 3.27 | Partially available |
| SCR-10 | Class management `/admin/classes` | Administrator, Lecturer where assigned | Lists, filters, creates, edits, assigns lecturers, imports students, and applies class lifecycle actions. | 3.10, 3.11.3 | Available |
| SCR-11 | Class detail `/classes/:slug` | Administrator, assigned Lecturer | Shows class roster and team tabs. Adds/removes/imports students, exports roster data, creates/edits teams, and assigns mentors. | 3.11, 3.12, 3.13, 3.14, 3.36 | Available |
| SCR-12 | Student classes `/student/classes`, `/student/classes/:slug` | Student | Shows enrolled classes, classmates/team state, and allows an eligible student to begin a team proposal. | 3.13, 3.14, 3.40 | Available |
| SCR-13 | My Team `/student/team` | Student | Displays the student's team and links to its workspace. It may display assignment context, but is not a separate mentor-detail screen. | 3.13, 3.40 | Partially available |
| SCR-14 | Workspace hub `/workspace` | Administrator, Lecturer, Mentor | Lists only teams/projects accessible to the current user; supports search and opens selected team workspaces. It is not an unrestricted platform-project catalogue. | 3.18, 3.19, 3.22, 3.37 | Partially available |
| SCR-15 | Team workspace `/workspace/teams/:teamId`, `/student/workspace` | Student, Administrator, Lecturer, Mentor | Shows project profile, roadmap, shortcuts, proposal, checkpoint overview, files, submission state, feedback, and linked tools for an authorized team. | 3.15, 3.17, 3.19, 3.23, 3.24 | Available |
| SCR-16 | Project profile `/workspace/teams/:teamId/project-profile`, `/student/workspace/project-profile/:teamId` | Student team member; authorized staff view | Creates or updates team project profile information. Editing is disabled for archived workspaces. | 3.16 | Available |
| SCR-17 | Proposal editor `/workspace/teams/:teamId/proposal`, `/student/workspace/proposal` | Student team member; authorized staff view | Drafts and submits a proposal. Proposal version history allows viewing and restoring prior versions when editing is allowed. | 3.17 | Available |
| SCR-18 | Checkpoint detail panel within Team Workspace | Student team member; authorized Lecturer/Mentor/Admin | Opens a checkpoint to view requirements, rubric, uploaded files, feedback thread, and submission metadata. Students can complete requirements and upload deliverables. | 3.17, 3.23, 3.24, 3.28 | Available |
| SCR-19 | Execution board `/executionboard` | Authorized signed-in user | Displays the project-progress board. The exact data shown is constrained by the user's backend access rights. | 3.25 | Available |
| SCR-20 | Evaluation panel within checkpoint | Authorized evaluator | Presents rubric/evaluation data at checkpoint level. The separate finalize, revision, release, history and report-export screens are not implemented. | 3.28-3.32 | Partially available |
| SCR-21 | Dashboard `/admin`, `/mentor`, `/lecturer`, `/student`, `/rankings` | Role-specific | Provides the corresponding dashboard. Rankings is feature-flagged; it is not a replacement for an evaluation-report export. | 3.31 | Available |
| SCR-22 | Mentoring sessions `/sessions` | Student, Mentor, Lecturer as permitted | Creates, edits, cancels, and views mentoring sessions. Mentors/lecturers select an accessible class and team; notes and action items record feedback. | 3.38, 3.39, 3.41 | Available when mentoring flag is enabled |
| SCR-23 | Startup Data Bank `/lecturer/data-bank` | Administrator, Lecturer | Provides datasets/grid, search/filter, imports, history, and data export. The route is feature-flagged and currently hidden from the sidebar. | 3.42, 3.47 | Available when Data Bank flag is enabled |
| SCR-24 | AI analysis `/student/ai-analysis` | Student | Provides AI suggestions and Similar Ideas. It is based on startup-idea data and does not provide a general project-tag catalogue. | 3.43 | Partially available when AI flag is enabled |
| SCR-25 | Future project portfolio screens | Administrator, Mentor, Student | Dedicated screens for project archive, unrestricted platform projects, evaluation release/history/export, mentor directory/recommendations, tag catalogue, high-potential projects, and incubation do not exist. | 3.20-3.22, 3.29-3.35, 3.44-3.46, 3.48 | Not implemented |

## 3.1.3 Screen Authorization

Legend: **Full** = can use the screen actions within ownership and lifecycle checks; **View** = can open only data they are authorized to see; **None** = no screen permission. Route access never replaces backend resource checks.

| Function / screen | Administrator | Lecturer | Mentor | Student | Current authorization rule |
|---|---|---|---|---|---|
| Register, authentication, password reset, common profile | Full | Full | Full | Full | Public registration/auth routes; profile applies to the current user only. |
| User management | Full | None | None | None | `/admin/users` requires Administrator. |
| Access logs | No screen | No screen | No screen | No screen | No current UI. |
| Lecturer/Mentor registration approval | Full | None | None | None | `/admin/account-approvals` requires Administrator. |
| Semester and course management | Full | None | None | None | `/admin/subjects` requires Administrator. |
| Create/edit/assign lecturer to academic class | Full | Assigned class view only | None | None | `/admin/classes` is Administrator-only; lecturer scope is limited to assigned classes. |
| Add/remove/import students and export class data | Full | Full for assigned manageable class | None | View own enrollment only | `/classes/:slug` checks class role and class state; student never manages roster. |
| Create/manage team members | Full | Full for assigned manageable class | View assigned teams | Create proposal/manage own team only | Class/team management remains subject to membership and class lifecycle. |
| Workspace hub and team project detail | View accessible teams | View accessible teams | View accessible teams | Own team only | Workspace APIs must enforce team/class authorization. |
| Create/update project profile and proposal | View / permitted edit | View / permitted edit | View / permitted edit | Full for own active team | Archived workspace disables editing. |
| Checkpoint submission | View / feedback | View / evaluate | View / feedback | Full for own active team | Upload and requirement editing are student team actions. |
| Progress board | View authorized scope | View authorized scope | View authorized scope | View own scope | Board content must remain scoped by backend data access. |
| Checkpoint and rubric configuration | Full | Only if explicitly assigned/authorized | None | None | Current routes are under the Administrator subject-management area. |
| Checkpoint evaluation | Full | Authorized evaluator | Authorized evaluator where allowed | View feedback/results only | Current frontend has checkpoint-level evaluation only. |
| Dashboard and rankings | Full | Role dashboard / View | Role dashboard / View | Role dashboard / View | Optional pages depend on release feature flags. |
| Mentor assignment | Full | Manage for assigned class where allowed | View own assignments | View assigned mentor only | Assignment must validate class/team relationships. |
| Mentoring sessions and feedback | View/manage where authorized | Full for accessible teams | Full for assigned teams | Create/view own-team sessions as permitted | `/sessions` is feature-flagged; team choice is filtered by authorization. |
| Startup Data Bank and export | Full | Full | None | None | `/lecturer/data-bank` currently permits Administrator and Lecturer only. |
| AI analysis | None | None | None | Full when feature enabled | Current routes are Student-only and feature-flagged. |
| Project archive, tag catalogue, high-potential, incubation, evaluation release/export | No screen | No screen | No screen | No screen | These capabilities require new screens and backend authorization policies before they can be granted. |
