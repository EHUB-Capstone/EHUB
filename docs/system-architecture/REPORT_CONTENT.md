# 1.1 System Architecture

EHub is designed as a role-based web platform using a React single-page application and an ASP.NET Core modular monolith. The backend follows Clean Architecture principles and organizes business capabilities into cohesive modules while sharing a single PostgreSQL database. The target production topology uses containerized services on an Ubuntu VPS, integrates with managed identity, media, AI and email providers, and separates request processing from long-running background work.

## A. Development View Architecture

### Figure 1. EHub Development and Delivery Architecture

The Development View describes the target path from a reviewed source change to a verified and recoverable production release. A developer works on a feature branch and opens a pull request in the GitHub repository. GitHub Actions then runs three independent check streams: frontend quality checks, backend build and automated tests, and security and delivery checks covering migration validation, dependency or image scanning, and container construction. These streams enter the CI quality gate through separate paths; the change may be merged into the protected `develop` branch only when every required check and review succeeds. A non-blocking GitHub integration also publishes selected push, pull-request and workflow-status events to the team Discord channel so that failures and delivery progress are visible without making Discord part of the quality gate.

Merging into `develop` automatically deploys the integrated application to a shared staging environment. The team and mentor perform staging acceptance before the same reviewed commit is promoted to `main` and assigned a version tag such as `vX.Y.Z`. The release workflow builds immutable versioned images and publishes them to GitHub Container Registry. Deployment to the protected production environment requires explicit approval. Before the production VPS is updated, the workflow creates a recoverable database backup and applies the approved migration. Post-deployment health and smoke tests verify the release; if verification fails, the operator can roll back to the previous immutable image and restore data according to the migration rollback plan.

Main components:

- **GitHub Repository and Pull Request:** retain version history, isolate feature work, provide peer review and enforce protected-branch rules.
- **GitHub Actions:** orchestrates reproducible CI and release workflows without storing deployment secrets in source control.
- **Frontend Checks:** validate code quality, types, automated tests and the production web build.
- **Backend Checks:** compile the .NET solution and run unit and integration tests.
- **Security and Delivery Checks:** validate database migrations, inspect dependencies and images, and prove that deployable containers can be built.
- **CI Quality Gate:** blocks merging until every required review and automated check succeeds.
- **Staging Environment and Verification:** deploy the integrated `develop` branch and support team and mentor acceptance before release promotion.
- **Versioned Release Build and Registry:** create immutable images from the approved `main` commit and version tag and store them in GitHub Container Registry.
- **Production Approval:** protects the production environment with an explicit authorization gate.
- **Backup and Migration:** creates a recovery point and applies the reviewed database change before application rollout.
- **Production VPS:** pulls the approved image set and runs the EHub services with Docker Compose.
- **Release Verification and Rollback:** execute health and smoke tests and restore the previous known-good release when necessary.
- **Discord Team Notifications:** receives selected repository, pull-request and CI/CD status events as an external collaboration channel. Notification delivery is non-blocking, and a Discord outage must never prevent review, merge, deployment or rollback.

The Discord path is intentionally dashed and remains outside the critical delivery path. Any webhook or integration credential must be stored in GitHub configuration or encrypted secrets rather than source control, and notifications must exclude secrets, raw environment values and sensitive error payloads.

## B. Physical View Architecture

### Figure 2. EHub Target Production Deployment Architecture

The Physical View describes the target single-VPS production topology behind Cloudflare. EHub web traffic resolves through a proxied Cloudflare hostname and reaches Cloudflare Edge Protection before the origin VPS. Cloudflare provides the public DNS proxy, CDN, DDoS mitigation, WAF and rate-limiting layer. Accepted HTTPS traffic is forwarded to the Nginx gateway on port 443. Nginx serves the static React single-page application and routes `/api` and `/hubs` traffic to the ASP.NET Core API container. Cloudflare protects the public web entry point only; backend calls to managed providers remain direct outbound connections.

The React frontend, API, background worker and PostgreSQL are isolated within the VPS deployment. PostgreSQL exposes no public port and persists its data on durable VPS storage even though the physical storage device is intentionally not shown as a separate diagram component. A scheduled backup job creates an encrypted logical export and transfers it to off-site storage so that loss of the VPS does not also destroy every recovery copy. The worker processes outbox events, AI and email jobs, and expired import sessions independently from interactive API requests. API and worker telemetry is collected by the observability service. Provider-neutral external connections supply Google authentication, protected media and document storage, AI inference and transactional email delivery.

Main components:

- **Cloudflare Edge Protection:** provides proxied DNS, CDN delivery, DDoS mitigation, WAF and rate limiting before requests reach the origin VPS.
- **React Frontend:** provides the role-based single-page web interface as a production static build served through Nginx.
- **Nginx Web Gateway:** receives protected HTTPS traffic, serves the React SPA and proxies `/api` and `/hubs` traffic.
- **EHub API Container:** hosts REST endpoints, JWT authorization, SignalR and health checks.
- **EHub Worker Container:** executes reliable background and long-running work.
- **PostgreSQL:** stores business data, audit data, chat messages, jobs and outbox events.
- **Observability:** collects API and worker telemetry and monitors health, uptime and host resources.
- **Backup Job and Off-site Storage:** create scheduled encrypted exports and retain a recovery copy outside the production VPS.
- **Managed External Services:** provide identity, media, AI and email capabilities.

Reading conventions: solid arrows represent request, runtime or primary data flows; dashed arrows represent operational, scheduled or asynchronous flows. An arrow points toward the invoked destination or toward the receiver of the dominant data flow. Dashed boundaries distinguish the public Internet, Cloudflare Edge Protection, the production VPS, its private Docker network and managed systems outside the VPS.

Origin hardening remains mandatory even though a separate host-firewall icon is intentionally omitted from the figure. The VPS must accept web traffic only through the approved Cloudflare path, keep PostgreSQL and container ports private, restrict administrative access separately, and use strict TLS between Cloudflare and Nginx. Cloudflare protects inbound web traffic; it does not replace operating-system firewall policy or container-network isolation.

## C. Logical View Architecture

### Figure 3. EHub Overall Logical Architecture

The Overall Logical View presents EHUB's target software organization: principal actors, a React presentation layer, API and background entry points, nine business capabilities, a shared Domain model and outbound infrastructure adapters. Admin, Lecturer, Mentor and Student users access role-specific views within the same React application. `Role-based UI`, `React Web App` and `API Client` describe responsibilities inside that frontend, not three separate applications. The API client sends HTTPS REST requests to the backend; Figure 3 does not require SignalR or an in-platform realtime chat feature.

The API layer dispatches authorized requests to application use cases and handlers. `Use Cases / Handlers` describes application requests, not a requirement for MediatR, a CQRS framework or separate read/write databases. Background Processing represents asynchronous and scheduled responsibilities such as outbox delivery and cleanup. Its placement does not require a separate worker executable or container. Business processing should reuse application policies and domain rules; technical job claiming, leases and dispatch remain infrastructure responsibilities.

The Application layer groups related use cases into business capabilities and uses the Domain model's entities, enums and business concepts. Infrastructure implements ports declared by Application; its source-code dependency points inward even when runtime data access continues outward to PostgreSQL and providers. PostgreSQL is shown separately because it is EHUB's authoritative application store. Google Identity, Cloudinary, the unspecified AI provider and the email provider are external integrations. The backend is organized as a modular monolith, not nine microservices or nine databases. Capability names are logical ownership groups, not a claim that every group already has an independently enforced module or matching source directory.

Main logical components:

- **System Actors:** Admin, Lecturer, Mentor and Student operate within their authorized scope. Role-based screens improve usability; backend authorization remains mandatory.
- **Presentation Layer:** one React application provides role-specific routes, forms, server-state management and an API client for REST/JSON communication.
- **API Layer:** exposes controllers and transport contracts, performs authentication/authorization and invokes the relevant application handler.
- **Background Processing:** handles scheduled work and durable events without tying the logical view to a particular process topology. It is distinct from Notifications: processing is an execution mechanism, whereas Notifications is a business capability.
- **Application Layer — Business Capabilities:** groups related use cases, coordinates business workflows and transactions, and declares required persistence/service ports.
- **Domain Model:** represents core entities, enums and business rules/concepts; it is not an ERD, EF Core mapping layer or database service. Business rules may be coordinated by application handlers rather than all residing inside entity methods.
- **Infrastructure Adapters:** implement Application ports for persistence and external services; the API composition root wires interfaces to implementations.
- **Application Data:** one PostgreSQL store persists business data and operational records. The Data Bank capability uses this store; it is not a second database engine.
- **External Services:** Google Identity supports externally verified login, Cloudinary supplies media storage, the AI provider supplies model inference, and the email provider supplies transactional delivery. Provider-specific calls and secrets remain behind backend adapters where required.

Business capabilities, arranged in a 3 × 3 grid for readability rather than execution order:

- **Identity & Access:** authentication, user accounts, roles, permissions and account approval.
- **Academic & Class:** semesters, courses/subjects, classes, student records, enrollment, roster import and lecturer assignment.
- **Team Management:** team formation proposals, teams, membership and team leadership. A team formation proposal is distinct from the project proposal analyzed by AI.
- **Project Workspace & Submission:** project information, project directions/proposals, checkpoints and their requirements, submission versions, files and links.
- **Evaluation & Analytics:** rubric criteria/weights, evaluation, feedback, score statistics and academic progress/completion dashboards. Checkpoint/submission ownership remains in the workspace capability; analytics reads their authorized data.
- **Mentoring & Support:** mentor profiles/expertise, team assignments, mentoring sessions and feedback. Mentor assignment belongs here even if its current handler is located under the `Features/Teams` directory.
- **Data Bank:** scoped academic/project datasets, configurable columns, import/export, snapshots and history. It manages reusable data records, not every piece of application data and not notification delivery.
- **Notifications:** in-app notifications, read/history views and transactional email coordination. Notification generation/delivery can use Background Processing and Infrastructure adapters; it does not imply chat, realtime presence or a SignalR dependency.
- **AI Proposal Analysis:** target advisory analysis of submitted project proposals. Figure 4 further decomposes this capability into version-bound jobs, scoped context, provider execution, guardrails and lecturer review. This label does not claim official AI rubric grading has been implemented.

Typical reading flow:

1. A user opens the relevant role-based view in the React application.
2. The API client sends an HTTPS REST request to the API layer.
3. The backend checks authentication and operation-specific scope, then dispatches to an application handler. Hiding a button in the UI is not an authorization check.
4. The selected capability coordinates the use case, applying domain concepts/rules and invoking persistence or service ports where needed. A request uses the capabilities relevant to that operation, not all nine sequentially.
5. Infrastructure implementations access PostgreSQL or the required external provider. For example, a proposal submission persists application data; its later background analysis calls the AI provider through a backend adapter.
6. The response returns through the API to the React screen. Response arrows are omitted to avoid duplicating every connector. Background work can continue after a request completes; Figure 4 explains the proposal-analysis status/result path.

Reading conventions: solid arrows show primary invocation, use of domain concepts or outbound data access. The dashed `Implements Ports` arrow is a **source-code dependency**, not an asynchronous event or a response: Infrastructure implements Application interfaces. At runtime, an application handler invokes those interfaces and receives the registered implementation through dependency injection. The diagram is not a literal sequential chain from Domain to Infrastructure, and Domain does not call the database. A connector ending at a boundary summarizes access to the relevant components inside it. Labels sit off their connectors and SVG/PNG and Draw.io use the same explicit routes.

Architectural constraints: presentation code must not access PostgreSQL or hold database/provider secrets. The browser's Google sign-in handshake is permitted; the backend verifies the Google credential and applies EHUB account/role rules before creating an EHUB session. The single Google Identity integration in the figure summarizes that capability rather than enumerating every OAuth/identity handshake. Controllers and background orchestration should not duplicate business policies; capability collaboration should use explicit application contracts, and provider-specific credentials/SDKs belong behind backend adapters.

Implementation alignment: the repository contains `EHub.Api`, `EHub.Application`, `EHub.Domain` and `EHub.Infrastructure`, plus shared/contracts projects omitted from this overview. Application references Domain; Infrastructure references Application and Domain; the API composition root references Application and Infrastructure. Handlers are registered directly in Application dependency injection. `IApplicationDbContext` and repository/service interfaces are implemented by Infrastructure; the current context abstraction exposes EF Core types, so the diagram does not claim a completely framework-independent Application layer. Outbox processing and class-import cleanup are registered with `AddHostedService` and currently run within the API process. Some older chat-related code remains, but no `AddSignalR`/`MapHub` registration was found in the inspected backend. Excluding SignalR here does not delete that code or change Figure 2/5.

Scope and status: this is a target logical view aligned with the code structure, not certification that all nine capabilities are complete or deployed. Data Bank entities/mappings and frontend calls establish its distinct scope but do not alone prove an end-to-end implementation. The AI workflow, final provider choices and complete use-case coverage still require implementation verification. A separately hosted worker belongs to deployment planning in Figure 2; optional Discord integration requires its own scope decision. Neither is a prerequisite of this Figure 3. Infrastructure deployment belongs to Figure 2, delivery tooling to Figure 1 and detailed AI processing to Figure 4.

### Figure 4. Logical View – AI-assisted Project Proposal Analysis Architecture

This target logical view describes asynchronous, provider-neutral **proposal analysis**. A Student with the required team-scoped submission permission submits a proposal through EHub Web. The actor caption, `Authorized team member`, does not grant submission rights to every Student; the backend enforces the applicable team-role policy. Team Leader is a responsibility represented by `TeamMember.RoleInTeam`, not an additional global role alongside Student. The backend checks class/team membership, proposal ownership, lifecycle state and request limits, then prepares a versioned proposal snapshot. The submitted proposal state and a pending analysis job bound to that immutable submitted version are committed atomically in PostgreSQL. A successful submission automatically queues analysis inside the backend; an internal system trigger does not pass through the React application. The UI receives the submission outcome and an analysis identifier only after this durable commit. A dedicated asynchronous analysis-request endpoint may use `202 Accepted`; it must not report the analysis as completed at acceptance time.

The background processor claims a pending job and records its processing lease, attempt count and retry metadata. It executes the job through AI Orchestrator, which reads the submitted proposal version and a permitted comparison context from PostgreSQL. Comparison scope is derived from the proposal's class and semester, rather than whichever semester happens to be current when the worker runs. The orchestrator reapplies Context and Prompt Controls to all retrieved data before calling the external provider through the target `IAiProvider` abstraction. It coordinates proposal analysis, domain/tag classification, comparison with relevant projects and report generation. These are responsibilities of the orchestration workflow, not four separately deployed services.

Provider output is untrusted. Output Guardrails validate its structure, required fields, lengths, allowed tags/domains and referenced projects against the actual permitted comparison set. A valid report, its version/model metadata and the job's `Completed` state are stored in one transaction. The Analysis Status & Results component represents authorized REST queries over durable job state and reports. EHub Web polls this interface while the relevant screen is open, so users can see pending, processing, completed and failed states without a SignalR dependency. An authorized Lecturer reads the report and separately approves the proposal or requests revision through the normal proposal-review API. AI completion never changes proposal approval automatically.

This figure supports proposal review, not official rubric grading. Future AI-assisted grading needs explicit submission/rubric versions, criterion-level suggestions and a lecturer-confirmed grading workflow; those capabilities are not implied by this proposal-analysis figure.

Main logical components:

- **Student and EHub Web:** identify an authorized team member submitting the proposal; membership and submission rights remain backend decisions. The web interface also presents authorized status and report data. The actor notation does not introduce a separate Team Leader global role.
- **EHUB Proposal & Analysis API:** an API capability within the EHUB modular monolith that accepts the submission, checks authorization and eligibility, and coordinates durable analysis acceptance through application use cases. It is not a separate microservice.
- **Context & Prompt Controls:** minimize personal data, normalize content and select prompt/schema versions. The same controls apply when the orchestrator later reads comparison data; the new database-to-orchestrator edge does not bypass them.
- **Persist Proposal & Analysis Job:** stores the submitted proposal version/snapshot and its bound pending analysis job atomically. Draft edits do not change the input of an existing job. The durable job itself supplies the work queue; a separate outbox event is needed only for additional consumers or notifications, not as a second mandatory queue for the same work.
- **PostgreSQL:** stores proposal versions, scoped project data, job status/leases/attempts, comparison-context metadata and validated reports. It is one shared store, not an active worker that independently calls the AI provider.
- **Job Claim & Retry:** atomically claims available work, records processing state and schedules bounded retries or terminal failure. Processing leases and idempotency protect against duplicate effects; they do not guarantee that an external provider is invoked exactly once.
- **AI Orchestrator:** obtains the proposal's permitted comparison set, reapplies context controls, selects the configured provider/model and coordinates analysis, classification, similarity checks and reporting with timeout/cost limits. Classification includes domain classification and tag extraction; similarity identifies relevant projects in the permitted semester scope.
- **AI Model Provider:** supplies advisory structured output for the context EHUB sends; it has no direct database access and no approval authority.
- **Output Guardrails:** validate report structure and scoped references before the processing workflow commits the result and completed state. A similarity finding is evidence for human review, not an automatic plagiarism finding.
- **Analysis Status & Results:** exposes job status and validated reports through access-controlled REST queries; it can return state before any report exists. Optional in-app/email notifications may supplement this path but are not required for it.
- **Lecturer Review:** reads AI recommendations and makes a separate authorized, auditable proposal decision. The figure ends at human review; the decision is persisted through the existing application workflow rather than directly from the AI provider.

Numbered flow:

| Step | Meaning |
|---|---|
| 1 — Submit Proposal | The Student with submission permission submits the team's proposal through EHub Web. |
| 2 — Request | EHub Web sends the proposal submission request to the backend API. |
| 3 — Authorize | The API checks the caller's scope and submission eligibility. |
| 4 — Prepare | EHub prepares the immutable submitted content and versioned analysis settings. |
| 5 — Persist | The proposal submission and pending job commit together; analysis is queued automatically. |
| 6 — Claim Job | The worker obtains a lease on available durable work. |
| 7 — Execute | The leased job enters the orchestration workflow. |
| Scoped Proposal & Project Context | Supporting data flow: the orchestrator loads the submitted proposal version and only relevant, permitted project data for its semester scope, applying prompt/data controls before use. It does not expose the entire EHUB database to the AI provider. |
| 8 — Invoke | EHUB calls the configured external provider with controlled context. |
| 9 — Result | Untrusted structured output enters validation. |
| 10 — Store / Complete | The processing workflow commits the validated report, metadata and completed job state together. |
| 11 — Status / Result | The result interface reads durable state and, when available, the report. This read is possible while work is pending or processing; it is not limited to completion. |
| 12 — Review | Lecturer views the status/report in EHub and makes the subsequent proposal-review decision. |
| Job State / Retry | Supporting control flow: the processor writes lease, attempts, retry timing and terminal failure metadata back to PostgreSQL. |

Job lifecycle: `Pending -> Processing -> Completed`. A transient failure returns a job to `Pending` with a future availability time; exhaustion of the bounded retry policy produces `Failed`. A separate `RetryScheduled` state is optional, not necessary when the state and next-attempt timestamp already express the lifecycle. Only the validated-result transaction marks a successful job completed. A failed analysis does not undo the accepted proposal or prevent authorized manual review.

Failure handling applies to both provider invocation and output validation. Recoverable failures may be retried according to the bounded retry policy; non-recoverable failures or exhausted attempts mark the job `Failed`. Invalid output never follows `10 Store / Complete` as a successful report. This failure path uses the existing job-state/retry responsibility and is documented here rather than adding crossing connectors to the figure.

Version example: submitting proposal V1 creates job A001 bound to V1. If the Student edits a draft or later submits V2, A001 continues to analyze V1; the V2 submission creates its own job A002. Each report identifies the analyzed version. A001 finishing after A002 must not replace V2's report or appear as an analysis of V2. Duplicate delivery of the same submission must not create unintended duplicate jobs.

Quality and safety controls:

- Provider API keys remain in backend secrets.
- Personal data is minimized for both submitted and retrieved content before transmission; proposal text is data and cannot override system instructions or permission checks.
- Prompt/schema versions, provider/model metadata and the proposal version are recorded for traceability.
- Comparison uses the proposal's semester and explicitly permitted candidate states/fields. It excludes the project itself and does not expose other teams' private data merely because their project IDs exist.
- Freeze a bounded comparison snapshot, or immutable candidate-version references, before provider execution and reuse it for retries. Record the comparison scope so a later semester change does not silently change an existing analysis.
- Jobs use timeouts, bounded retries, leases and idempotency controls. Duplicate submissions, retries and stale lease owners must not overwrite the accepted result of another attempt; safe completion checks the job's current ownership/version.
- Structured output is validated before persistence.
- Proposal snapshots make the analyzed input traceable even if the editable proposal changes later; identical inputs do not guarantee identical output from a nondeterministic model. A new proposal version does not silently overwrite the report for an older version.
- Rate, token and cost limits prevent unbounded provider usage.
- Sensitive prompt contents and provider credentials must not be written to application logs.
- Failed jobs remain observable and may be retried explicitly rather than disappearing silently.
- Invalid project references are rejected or explicitly excluded according to the output policy; the system must not silently invent a valid replacement. Tags/domain classifications remain suggestions unless a separate validated workflow applies them.
- AI does not automatically approve, reject or grade a project. Lecturer decisions require their own authorization and audit trail; report access rechecks the viewer's current scope.

Reading conventions: solid arrows show primary invocation or the dominant data flow, including reads from PostgreSQL; they do not mean the database initiates calls. The separate dashed `Job State / Retry` connector writes operational state back to the same database. Numbering explains the main scenario, while context retrieval, lifecycle writes and status polling can occur repeatedly. The claim and state-update connectors use distinct ports/routes, all edge labels sit off their connector, and the Draw.io routes match the SVG/PNG layout.

Cross-view responsibility: secret injection, encrypted transport, container isolation, database backup and platform observability belong to the Physical View. Figure 4 retains the job lifecycle and failure/retry responsibilities needed to understand AI processing independently; it does not depend on inclusion of Figure 5 or deployment of realtime chat. Background components denote logical responsibilities and do not by themselves require separate executables or containers.

Implementation alignment note: the current domain provides `ProjectProposalVersion`, `ProjectAnalysis`, PostgreSQL mappings and an outbox/notification foundation. Existing outbox processing runs as a hosted service in the API process. Automatic proposal-analysis job creation, the dedicated analysis lifecycle, scoped comparison retrieval, provider abstraction, prompt/schema/context metadata, output validation and authorized status/report endpoints remain target capabilities to implement and verify. The selected AI provider/model is still unspecified. This documentation update does not implement these backend features or certify an operational deployment.

### Figure 5. Realtime Communication and Asynchronous Processing Architecture

The final logical view separates low-latency communication from reliable business-event delivery. A React SignalR client establishes an authenticated connection to the EHub hub. The backend verifies active class or team membership for group joins and commands, validates chat content and persists the message before performing a best-effort broadcast to authorized clients. SignalR is not treated as durable storage or guaranteed delivery; reconnecting clients retrieve missed state through the normal API and PostgreSQL source of truth.

In parallel, class, team and mentor operations commit business state and a pending outbox row in one PostgreSQL transaction. The worker leases pending rows with `FOR UPDATE SKIP LOCKED`, applies bounded retry and dispatches each versioned event idempotently. Consumers update chat-membership and notification projections, optionally request email delivery and publish access or unread-count changes through a single SignalR push adapter. Events that exhaust the retry limit remain in a terminal failed state and trigger operational investigation rather than disappearing silently.

At the single-VPS baseline, one API instance handles SignalR connections and does not require a Redis backplane. Redis should be introduced only if the SignalR/API tier is scaled horizontally across multiple instances.

Main logical components:

- **React SignalR Client:** authenticates the connection, reconnects with backoff and reloads missed durable state after reconnect.
- **SignalR Hub:** terminates realtime connections and accepts only authenticated group joins and commands.
- **Membership Authorization:** checks active class, team, lecturer and mentor scope on every protected operation; client-supplied group identifiers are never trusted by themselves.
- **Chat Message Service:** validates content, applies a server timestamp and persists the message before broadcasting.
- **PostgreSQL:** remains the source of truth for chat messages, memberships, notifications and outbox state.
- **Group Broadcast:** performs a low-latency, best-effort push after the database commit; it does not replace durable message history.
- **Atomic State and Outbox:** writes business changes and the pending event within one PostgreSQL transaction, preventing committed state without a corresponding event.
- **Outbox Worker:** claims rows with a lease, recovers stale work and applies exponential backoff and a maximum-attempt policy.
- **Event Dispatcher:** routes versioned events to idempotent consumers and records success or failure.
- **Chat Membership Sync:** projects class, team, lecturer and mentor changes into active chat-group access.
- **Notification Projection:** creates at most one in-app notification for each source event and recipient.
- **SignalR Push:** publishes access changes and unread-count updates without coupling domain handlers directly to live connections.
- **Email Channel:** provides optional external delivery; an email failure must not roll back the original business transaction.
- **Failure Monitoring:** exposes terminal failed events, the last sanitized error and retry metadata for operational repair.
- **Optional Redis Backplane:** is introduced only when multiple SignalR/API instances must share group broadcasts.

Reliability and security rules:

- Chat authorization is enforced by the backend for history reads, joins, sends and membership changes, not by hidden frontend controls.
- A message is broadcast only after its database transaction commits successfully.
- Realtime push is best effort; durable state is recovered through API queries after reconnect.
- Outbox delivery is at least once, so every consumer must be idempotent by event and recipient or aggregate key.
- Worker leases recover events left in `Processing` when a process terminates unexpectedly.
- Retryable errors use bounded exponential backoff; terminal failures remain queryable and alertable.
- Sensitive payloads and raw exception details are not exposed to clients or external notifications.
- Membership revocation must affect both durable membership and active realtime access.

Reading conventions: solid arrows denote synchronous commands, durable writes or primary event dispatch. Dashed arrows denote best-effort push, optional scale-out infrastructure and failure/recovery paths. The two PostgreSQL representations in this logical view refer to the same production database: the upper node emphasizes chat persistence, while `Atomic State and Outbox` emphasizes transaction semantics rather than a second datastore.

Implementation alignment note: the current source already contains PostgreSQL chat entities, class/team membership synchronization, `OutboxMessage`, lease-based claiming, exponential retry, terminal `Failed` status and idempotent notification projection. SignalR Hub/client integration, active-connection revocation, Redis scale-out, external email dispatch from outbox events and operational alerting remain target-production work. The current chat endpoints must also enforce active membership consistently for group lists, member lists and message history before the final as-built submission.

