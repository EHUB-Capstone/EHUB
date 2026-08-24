# 1.1 System Architecture

EHub is designed as a role-based web platform using a React single-page application and an ASP.NET Core modular monolith. The backend follows Clean Architecture principles and organizes business capabilities into cohesive modules while sharing a single PostgreSQL database. The target production topology uses containerized services on an Ubuntu VPS, integrates with managed identity, media, AI and email providers, and separates request processing from long-running background work.

## A. Development View Architecture

### Figure 1. EHub Development and Delivery Architecture

The Development View describes the target path from a reviewed source change to a verified and recoverable production release. A developer works on a feature branch and opens a pull request in the GitHub repository. GitHub Actions then runs three independent check streams: frontend quality checks, backend build and automated tests, and security and delivery checks covering migration validation, dependency or image scanning, and container construction. These streams enter the CI quality gate through separate paths; the change may be merged into the protected `develop` branch only when every required check and review succeeds.

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

## B. Physical View Architecture

### Figure 2. EHub Target Production Deployment Architecture

The Physical View describes the target single-VPS production topology. Users first resolve the public EHub domain through DNS and then initiate an HTTPS connection to the VPS on port 443. The VPS host firewall admits the request to the Nginx gateway, which terminates TLS, serves the React single-page application and forwards `/api` and `/hubs` traffic to the ASP.NET Core API container. The API, background worker and PostgreSQL communicate on a private Docker network; PostgreSQL has no public port. A host-level observability service receives aggregated logs, health data and metrics from all containers in that network. The worker processes outbox events, AI and email jobs, and expired import sessions independently from interactive API requests.

PostgreSQL data is mounted on a durable host volume that remains outside the lifecycle of an individual database container. A scheduled backup job creates an encrypted logical export and transfers it to off-site storage so that loss of the VPS does not also destroy every recovery copy. API and worker telemetry is collected by the observability service. Provider-neutral external connections supply Google authentication, protected media and document storage, AI inference and transactional email delivery.

Main components:

- **Domain and DNS:** resolves the public EHub address without exposing internal container addresses.
- **Host Firewall:** restricts the public application surface to HTTPS port 443. Any administrative or deployment channel must be separately restricted by an IP allow-list, VPN or equivalent control and is intentionally omitted from this runtime view.
- **Nginx Web Gateway:** terminates TLS, serves the SPA and proxies `/api` and `/hubs` traffic.
- **EHub API Container:** hosts REST endpoints, JWT authorization, SignalR and health checks.
- **EHub Worker Container:** executes reliable background and long-running work.
- **PostgreSQL:** stores business data, audit data, chat messages, jobs and outbox events.
- **PostgreSQL Data Volume:** retains database files when the PostgreSQL container is replaced or restarted.
- **Observability:** collects API and worker telemetry and monitors health, uptime and host resources.
- **Backup Job and Off-site Storage:** create scheduled encrypted exports and retain a recovery copy outside the production VPS.
- **Managed External Services:** provide identity, media, AI and email capabilities.

Reading conventions: solid arrows represent request, runtime or primary data flows; dashed arrows represent operational, scheduled or asynchronous flows. An arrow points toward the invoked destination or toward the receiver of the dominant data flow. Dashed boundaries distinguish the public Internet, the production VPS, its private Docker network and managed systems outside the VPS.

## C. Logical View Architecture

### Figure 3. EHub Overall Logical Architecture

The Overall Logical View shows the principal actors, presentation components, inbound adapters, modular business capabilities, Clean Architecture dependency direction and outbound integrations. Admin, Lecturer, Mentor and Student users access role-specific portals in the React application. Its REST and SignalR clients communicate with the API layer, while the background worker provides a second internal entry point for scheduled work, outbox events and long-running jobs. Both entry points invoke application use cases rather than addressing individual modules directly.

The Application layer organizes EHub into cohesive business capabilities and depends on the Domain model for entities, invariants and domain events. Infrastructure adapters implement ports declared by the Application layer; this dependency points inward even though runtime data flows continue outward to PostgreSQL and managed providers. PostgreSQL is separated from external services because it is EHub's authoritative application data store, whereas Google Identity, Cloudinary, the AI provider and the email provider remain replaceable integrations. EHub remains a modular monolith: the modules are logical ownership boundaries inside one backend, not independent microservices, deployment units or databases.

Main logical components:

- **System Actors:** Admin, Lecturer, Mentor and Student users interact only through authorized role-based portals.
- **Presentation Layer:** the React application provides routing, server-state management, forms, REST/JSON calls and the authenticated SignalR client.
- **API Layer:** exposes transport contracts, performs authentication and authorization, and translates synchronous requests into application commands and queries.
- **Background Worker:** invokes the same application use cases for scheduled jobs, outbox events and long-running work without duplicating business rules.
- **Application Layer:** coordinates use cases, transactions and module boundaries and declares the ports required from infrastructure.
- **Domain Model:** owns entities, invariants and domain events independently of transport, database and provider technology.
- **Infrastructure Adapters:** implement persistence, identity, media, AI and email ports and are composed at the application boundary.
- **Application Data:** PostgreSQL remains the authoritative store shared by the modular monolith while module ownership is enforced logically.
- **External Services:** Google Identity, Cloudinary, the AI provider and the email provider are accessed only through replaceable adapters.

Business modules:

- **Identity and Access:** authentication, roles, permissions and account approval.
- **Academic and Class:** semesters, subjects, classes, schedules, assignments and enrollment.
- **Team and Mentor:** teams, members, proposals, project directions and mentor assignments.
- **Project Workspace:** projects, milestones, tasks, submissions and collaboration data.
- **Evaluation and Tracking:** rubrics, checkpoints, evaluations and progress information.
- **Mentoring and Data:** mentoring sessions, workshops, academic datasets and the data bank.
- **Communication:** chat, realtime presence and notifications.
- **AI Assistance:** human-reviewed project proposal analysis and recommendations.

Reading conventions: solid arrows represent primary invocation or runtime data access. The dashed `Implements Ports` arrow represents a source-code dependency required by Dependency Inversion: Infrastructure depends on Application abstractions, not the reverse. A connector ending at a boundary summarizes access to the components inside that boundary and intentionally avoids repetitive crossing lines.

Architectural constraints: actors and presentation code must not access PostgreSQL or providers directly; API controllers and worker processes must not duplicate domain decisions; application modules collaborate through explicit use cases, contracts or domain events; and provider-specific SDK types must remain behind Infrastructure adapters. In the current implementation, outbox processing and import-session cleanup still run as hosted services in the API process, while the separate Worker host and SignalR route shown here remain target-production items to verify before the final as-built submission.

### Figure 4. AI-assisted Project Proposal Analysis Architecture

The AI logical view describes an asynchronous, provider-neutral and human-governed proposal-analysis workflow. A Team or Lecturer submits proposal data through the EHub web application. The API authorizes class, team and proposal ownership, validates request limits and sends only normalized, necessary context into a versioned prompt. EHub atomically stores the immutable proposal snapshot, analysis request, pending job and outbox state in PostgreSQL before returning an accepted response with a status identifier. The external model provider and the internal human-governance role are shown as separate trust boundaries: the provider generates advisory content, while Lecturer or Admin users retain academic and operational decision authority.

The worker leases a pending job with idempotency and retry controls, invokes the configured model through an `IAiProvider` port and treats the provider response as untrusted input. Output guardrails validate the JSON schema, allowed fields, lengths, score ranges and business consistency before any `ProjectAnalysis` is stored. The validated result, prompt version, provider/model metadata and completion event are committed before result delivery creates an in-app notification, SignalR update and optional email. Lecturer or Admin users review the result inside EHub and retain all approval, rejection and grading authority.

Main logical components:

- **Proposal Analysis API:** enforces authentication, ownership, request validation, rate limits and analysis eligibility.
- **Context and Prompt Controls:** minimize personal data, normalize proposal sections and bind the request to a versioned prompt and output schema.
- **Persist Request and Job:** commits the proposal snapshot, analysis request, pending job and outbox data atomically and returns only after durable acceptance.
- **PostgreSQL Analysis State:** stores request status, attempts, leases, prompt/model metadata, validated results and completion events.
- **Job Claim and Retry:** prevents duplicate concurrent execution and applies bounded retry with a terminal failed state.
- **AI Orchestrator:** selects the configured provider/model and applies timeout, cancellation and token/cost limits behind the `IAiProvider` abstraction.
- **Output Guardrails:** reject malformed, oversized or inconsistent provider output and prevent raw AI content from driving privileged state changes.
- **Result Delivery:** publishes a durable result notification through in-app, SignalR and optional email channels.
- **Human Review:** presents AI recommendations to Lecturer or Admin users without delegating academic or governance decisions to the model.

Quality and safety controls:

- Provider API keys remain in backend secrets.
- Personal data is minimized before transmission.
- Prompt and model versions are recorded for traceability.
- Jobs use timeouts, retries and idempotency controls.
- Structured output is validated before persistence.
- Proposal snapshots make an analysis reproducible even if the editable proposal changes later.
- Rate, token and cost limits prevent unbounded provider usage.
- Sensitive prompt contents and provider credentials must not be written to application logs.
- Failed jobs remain observable and may be retried explicitly rather than disappearing silently.
- AI does not automatically approve, reject or grade a project.

Reading conventions: the numbered solid arrows show the accepted request and successful processing path; the dashed completion-event arrow denotes durable asynchronous delivery. PostgreSQL is a shared durable state boundary rather than a processing step. Provider responses are deliberately routed through Output Guardrails before they can be persisted or shown to a reviewer.

Cross-view responsibility: secret injection, encrypted transport, container isolation, database backup and platform observability are defined by the Physical View; dead-letter handling, dispatcher retries and realtime delivery mechanics are expanded in the Realtime and Asynchronous Processing View. They are intentionally not duplicated here so that Figure 4 remains focused on AI safety, durable analysis state and human decision governance.

Implementation alignment note: the current domain already provides proposal versions, `ProjectAnalysis`, PostgreSQL persistence and the outbox/notification foundation. Before the final as-built submission, the dedicated analysis-request/job lifecycle, provider abstraction, prompt/schema version metadata, output-validation pipeline and result-delivery integration must be implemented and verified against this target view.

### Figure 5. Realtime Communication and Asynchronous Processing Architecture

The final logical view separates low-latency communication from reliable business-event delivery. A React SignalR client establishes an authenticated connection to the EHub hub. The backend verifies active class or team membership before allowing a connection to join a group or send a command. Chat content is validated and persisted before it is broadcast to authorized group members. In parallel, class, team and mentor transactions save both business state and an `OutboxMessage` in the same PostgreSQL transaction. The worker claims pending events and dispatches them idempotently to chat-membership synchronization, in-app notification, SignalR and email channels. Repeated failures are recorded for operational investigation rather than silently discarded.

At the single-VPS baseline, one API instance handles SignalR connections and does not require a Redis backplane. Redis should be introduced only if the SignalR/API tier is scaled horizontally across multiple instances.

