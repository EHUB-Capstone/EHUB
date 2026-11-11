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

EHUB has two AI capabilities. Mentor recommendation for a team is produced by
the external Google Gemini API, which the backend calls over HTTPS. AI checkpoint
summaries are self-hosted: the Ollama AI Service container runs the Qwen 3.5 2B
model, and the backend sends it submission text over HTTP on the private
network, so submission content used for summaries does not leave the server.

For Google sign-in, the backend verifies the Google ID token with Google
Identity. Student submission files are stored in Cloudflare R2: the backend
issues short-lived presigned URLs, the browser uploads and downloads files
directly through R2, and the backend verifies, previews and deletes the stored
objects through the S3 API. Cloudinary stores profile avatars, mentor CVs and
product feedback attachments uploaded by the backend, and Gmail SMTP delivers
transactional email, with Brevo as an SMTP fallback. These external systems
remain outside the EHUB software system boundary.

## Figure 2. EHUB Backend Component Diagram

The component diagram expands the backend container. `EHub.Api` provides the
HTTP interface, middleware, security policies, rate limiting and health
endpoints, and delegates work to use-case handlers in `EHub.Application`. The
application layer is organised by feature module: classes, teams, workspaces and
checkpoints, mentoring, dashboards and rankings, and the project showcase.
Application handlers coordinate business rules in `EHub.Domain` and use
request/response models and shared results from `EHub.Contracts` and
`EHub.Shared`. `EHub.Infrastructure` implements the application's persistence,
identity, storage, email, document preview and AI ports. Application handlers
query the database through the application-owned `IApplicationDbContext`
interface, which the Infrastructure `AppDbContext` implements. Managed providers
(including Gemini) and the Ollama AI Service are reached only through
Infrastructure adapters. Hosted services for cleanup, document preview, checkpoint
summaries and the outbox are implemented in Infrastructure but execute in the
same backend process.

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
requests to the backend on private port 8080. The backend image also contains
LibreOffice, which converts DOCX and PPTX submissions into PDF previews. The
backend connects to PostgreSQL on private port 5432 and to the Ollama container
(Qwen 3.5 2B, limited to 2 vCPU and 3 GB of memory) on private port 11434. None
of these services publishes a host port. PostgreSQL and Ollama model data persist
in named Docker volumes, although the volumes are omitted from the figure to keep
the deployment view focused.

Each container has a health check: PostgreSQL uses `pg_isready`, the backend
exposes `/health/ready` (which also verifies the database connection) and the
frontend exposes `/healthz`. Docker Compose uses these checks to start the
containers in the correct order. Docker also restarts a container automatically
if it stops unexpectedly (`restart: unless-stopped`).

Production releases are started manually from GitHub Actions. The workflow
connects to the VPS over SSH with a restricted deployment key, and a deploy
script builds the images on the host, takes a fresh backup and restarts the
application. A release must come from `main` and must match a version already
tested on staging. The result is posted to a Discord channel.

A scheduled host-side backup job (systemd timer, daily at 02:00 Vietnam time)
creates and validates a PostgreSQL export with a SHA-256 checksum, keeps 14 days
of copies on the VPS, uploads each export to a dedicated private Cloudflare R2
bucket (30-day lifecycle rule) and reports success or failure to a Discord
channel. Further timers check every morning that a fresh backup exists and check
disk usage every hour. The same backup script also runs before each release. Restoring the R2 copy into a disposable database has been verified: the
restore took about five seconds and the table counts matched production.

The backend makes outbound connections to Google Identity, the Google Gemini API
(mentor recommendation), Cloudinary (avatars and documents), Cloudflare R2
(submission files) and Gmail SMTP, with Brevo as an SMTP fallback when the Gmail daily sending limit is reached. Submission files are
uploaded and downloaded by the browser directly through R2 presigned URLs, so
file content does not pass through the VPS; the backend only verifies and manages
the stored objects. Bounded local-driver logs remain a deployment safeguard.

## AI capabilities (supporting notes)

- **Mentor recommendation:** for a team, the backend sends the project
  information and the candidate mentor profiles to the Google Gemini API over
  HTTPS and returns a ranked list of mentors. The API key is kept in server
  secrets, never in the frontend. Staff review the ranking and make the final
  mentor assignment.
- **AI checkpoint summaries:** lecturers, mentors and admins can request a
  summary of a team's latest checkpoint submission. The backend queues the job,
  extracts the text and sends it to the self-hosted Ollama container running
  Qwen 3.5 2B on the private Docker network, so this content does not leave the
  server. The feature has a per-environment switch; when the model is
  unavailable the rest of the system keeps working.
- **Role:** both features assist people and do not make decisions on their own.
