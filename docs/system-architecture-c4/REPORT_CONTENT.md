# EHUB System Architecture

## Architecture scope and modeling approach

The EHUB System Context Diagram is presented in Report 3 and serves as Level 1
of the C4 model. This section continues the architectural description with the
Container, Backend Component and Production Deployment diagrams; therefore,
the System Context Diagram is referenced rather than duplicated. These figures
describe the implemented modular monolith and its production topology.

## Figure 1. EHUB Container Diagram

The container diagram presents the major executable and data elements of EHUB.
Student, Lecturer, Mentor and Admin users access the Frontend, a React
single-page application, which calls the ASP.NET Core backend through HTTPS
using JSON. The backend performs authentication, authorization and application
use cases, runs its registered hosted background services, and persists
authoritative application data in PostgreSQL with the pgvector extension.

The AI capability is self-hosted. The Ollama AI Service container runs the BGE-M3
embedding model; the backend sends it text over HTTP and stores the returned
vectors in PostgreSQL. No application data is sent to an external AI provider.

For Google sign-in, the backend verifies the Google ID token with Google
Identity. Student submission files are stored in Cloudflare R2: the backend
issues short-lived presigned URLs, the browser uploads and downloads files
directly through R2, and the backend verifies, previews and deletes the stored
objects through the S3 API. Cloudinary stores profile avatar images uploaded by
the backend, and Gmail SMTP delivers transactional email, with Brevo as an SMTP
fallback. These external systems remain outside the EHUB software system
boundary.

## Figure 2. EHUB Backend Component Diagram

The component diagram expands the backend container. `EHub.Api` provides the
HTTP interface, middleware, security policies and health endpoints, and delegates
work to use-case handlers in `EHub.Application`. Application handlers coordinate
business rules in `EHub.Domain` and use request/response models and shared
results from `EHub.Contracts` and `EHub.Shared`, which are used by every layer.
`EHub.Infrastructure` implements the application's persistence, identity,
storage, email and AI ports. Application handlers query the database through the
application-owned `IApplicationDbContext` interface, which the Infrastructure
`AppDbContext` implements. Managed providers and the Ollama AI Service are
reached only through Infrastructure adapters. Hosted cleanup and outbox services
are implemented in Infrastructure but execute in the same backend process.

The diagram expresses the Clean Architecture boundary: Infrastructure depends
on Application and implements its ports, which is why the arrow points from
Infrastructure to Application. Application does not depend on Infrastructure,
and Domain does not depend on API, EF Core or external providers.

## Figure 3. EHUB Production Deployment Diagram

EHUB runs on a single Ubuntu 24.04 VPS. The applications are packaged as Docker
containers and managed with Docker Compose. The system does not use Docker
Swarm, Kubernetes or a managed cloud platform such as Azure, because the pilot
deployment runs on one host and Docker Compose keeps operations simple.

A browser reaches `e-hub.com.vn` through Cloudflare. Cloudflare provides
authoritative DNS, proxied web traffic, edge TLS and DDoS protection, then sends
HTTPS traffic to the host Nginx gateway. Host Nginx uses a Let's Encrypt
certificate, redirects the `www` hostname to the canonical domain, and proxies
requests to the frontend container through the loopback-only address
`127.0.0.1:3000`.

Inside Docker, the frontend Nginx serves the React build and forwards API
requests to the backend on private port 8080. The backend connects to PostgreSQL
on private port 5432 and to the Ollama container on private port 11434. None of
these services publishes a host port. PostgreSQL and Ollama model data persist
in named Docker volumes, although the volumes are omitted from the figure to keep
the deployment view focused.

Each container has a health check: PostgreSQL uses `pg_isready`, the backend
exposes `/health/ready` (which also verifies the database connection) and the
frontend exposes `/healthz`. Docker Compose uses these checks to start the
containers in the correct order. Docker also restarts a container automatically
if it stops unexpectedly (`restart: unless-stopped`).

A scheduled host-side backup job (systemd timer, daily at 02:00 Vietnam time)
creates and validates a PostgreSQL export with a SHA-256 checksum, keeps 14 days
of copies on the VPS, uploads each export to a dedicated private Cloudflare R2
bucket (30-day lifecycle rule) and reports success or failure to a Discord
channel. A second daily check alerts when no fresh backup exists or disk usage is
high. The same script also runs before each release with the application
stopped. Restoring the R2 copy into a disposable database has been verified: the
restore took about five seconds and the table counts matched production.

The backend makes outbound connections to Google Identity, Cloudinary (profile
avatars), Cloudflare R2 (submission files) and Gmail SMTP, with Brevo as an SMTP
fallback when the Gmail daily sending limit is reached. Submission files are
uploaded and downloaded by the browser directly through R2 presigned URLs, so
file content does not pass through the VPS; the backend only verifies and manages
the stored objects. Bounded local-driver logs remain a deployment safeguard.

## AI capability (supporting notes)

- **Purpose:** mentor recommendation. For a team, the backend compares the
  project description and tags with active mentor profiles and returns a ranked
  list with reasons.
- **Technology:** semantic search with text embeddings (NLP), using the
  multilingual BGE-M3 model served by Ollama.
- **Data flow:** Frontend, then Backend API, then the Ollama adapter in
  Infrastructure, then the Ollama container. Returned 1024-dimension vectors are
  cached in the PostgreSQL `mentor_embeddings` table (pgvector) and reused while
  the profile text is unchanged.
- **Scoring:** 70% cosine similarity between embeddings and 30% keyword match of
  mentor expertise; the score is a ranking aid, not a probability.
- **Hosting:** the Ollama container runs on the same VPS and private Docker
  network as the backend, with its own resource limits.
- **Reliability:** the model only produces vectors and no generated text, so it
  cannot fabricate content; the risk is imperfect ranking, reduced by combining
  the keyword score and showing reasons. If the service is unavailable or exceeds
  45 seconds, the backend returns a clear "temporarily unavailable" message and
  other features continue to work.
- **Role:** assistant. Staff members review the ranking and make the final mentor
  assignment.
