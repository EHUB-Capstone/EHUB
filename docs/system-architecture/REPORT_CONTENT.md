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

The Physical View describes the target production topology. Users access a single public domain through HTTPS. Nginx terminates TLS, serves the React static application and forwards `/api` and `/hubs` traffic to the ASP.NET Core API container. The API exposes REST endpoints, authorization, SignalR hubs and health endpoints. A separate worker container processes outbox events, AI analyses, email jobs and expired import sessions. The API and worker use PostgreSQL over a private Docker network; the database port is not exposed publicly. Persistent storage protects database data from container replacement, while encrypted backups are copied to off-site storage. Managed external services provide Google authentication, protected media/document storage, AI inference and transactional email delivery.

Main components:

- **Domain and DNS:** resolves the public EHub address.
- **Nginx Web Gateway:** terminates TLS, serves the SPA and proxies API and SignalR traffic.
- **EHub API Container:** hosts REST endpoints, JWT authorization, SignalR and health checks.
- **EHub Worker Container:** executes reliable background and long-running work.
- **PostgreSQL:** stores business data, audit data, chat messages, jobs and outbox events.
- **Persistent Volume:** retains database and operational data across container replacement.
- **Observability:** collects structured logs and monitors health, uptime and resources.
- **Off-site Backup Storage:** stores encrypted backups outside the production VPS.
- **Managed External Services:** provide identity, media, AI and email capabilities.

## C. Logical View Architecture

### Figure 3. EHub Overall Logical Architecture

The Overall Logical View shows the principal actors, presentation components, business modules, Clean Architecture layers and external dependencies. Admin, Lecturer, Mentor and Student users access role-specific portals in the React application. The web client communicates with the backend through REST/JSON and SignalR. The API layer handles transport concerns and delegates commands and queries to application use cases. Business rules remain in the Domain layer, while the Infrastructure layer implements database and external-service adapters. EHub remains a modular monolith: modules are cohesive logical boundaries inside one backend deployment and are not represented as independent microservices.

Business modules:

- **Identity and Access:** authentication, roles, permissions and account approval.
- **Academic and Class:** semesters, subjects, classes, schedules, assignments and enrollment.
- **Team and Mentor:** teams, members, proposals, project directions and mentor assignments.
- **Project Workspace:** projects, milestones, tasks, submissions and collaboration data.
- **Evaluation and Tracking:** rubrics, checkpoints, evaluations and progress information.
- **Mentoring and Data:** mentoring sessions, workshops, academic datasets and the data bank.
- **Communication:** chat, realtime presence and notifications.
- **AI Assistance:** human-reviewed project proposal analysis and recommendations.

### Figure 4. AI-assisted Project Proposal Analysis Architecture

The AI logical view describes an asynchronous, provider-neutral and human-governed analysis workflow. A team or lecturer submits proposal data through the EHub web application. The API authorizes the caller, validates the request, minimizes personal data and selects a versioned prompt. The request and job record are stored atomically before an accepted response is returned. The worker claims the job idempotently, invokes the configured AI provider through the `IAiProvider` abstraction and validates the structured result against both a JSON schema and business constraints. The validated analysis, model metadata and prompt version are stored in PostgreSQL, after which EHub sends an in-app notification and a SignalR update. AI output is advisory; Lecturer or Admin users retain decision authority.

Quality and safety controls:

- Provider API keys remain in backend secrets.
- Personal data is minimized before transmission.
- Prompt and model versions are recorded for traceability.
- Jobs use timeouts, retries and idempotency controls.
- Structured output is validated before persistence.
- AI does not automatically approve, reject or grade a project.

### Figure 5. Realtime Communication and Asynchronous Processing Architecture

The final logical view separates low-latency communication from reliable business-event delivery. A React SignalR client establishes an authenticated connection to the EHub hub. The backend verifies active class or team membership before allowing a connection to join a group or send a command. Chat content is validated and persisted before it is broadcast to authorized group members. In parallel, class, team and mentor transactions save both business state and an `OutboxMessage` in the same PostgreSQL transaction. The worker claims pending events and dispatches them idempotently to chat-membership synchronization, in-app notification, SignalR and email channels. Repeated failures are recorded for operational investigation rather than silently discarded.

At the single-VPS baseline, one API instance handles SignalR connections and does not require a Redis backplane. Redis should be introduced only if the SignalR/API tier is scaled horizontally across multiple instances.

