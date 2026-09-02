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

