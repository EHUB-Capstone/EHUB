# EHUB System Architecture

## Architecture scope and modeling approach

The EHUB System Context Diagram is presented in Report 3 and serves as Level 1
of the C4 model. This section continues the architectural description with the
Container, Backend Component and Production Deployment diagrams; therefore,
the System Context Diagram is referenced rather than duplicated. These figures
describe the implemented modular monolith and the current production topology.
Because the project has committed to an AI capability but has not selected its
feature or provider, the diagrams include only a provider-neutral planned AI
integration. A dashed relationship distinguishes it from implemented services.

## Figure 1. EHUB Container Diagram

The container diagram presents the major executable and data elements of EHUB.
Users access a React single-page web application, which communicates with the
ASP.NET Core backend through HTTPS requests using JSON or multipart form data.
The backend performs authentication, authorization and application use cases,
runs its registered hosted background services, and persists authoritative
application data in PostgreSQL. Google Identity Services supports Google
sign-in, Cloudinary stores uploaded images, and Gmail SMTP delivers
transactional email. These external systems remain outside the EHUB software
system boundary. The AI Provider is deliberately marked as planned and
provider-neutral; the diagram does not claim an implemented AI use case.

## Figure 2. EHUB Backend Component Diagram

The component diagram expands the backend container. `EHub.Api` provides the
HTTP interface, middleware and security policies and delegates work to use-case
handlers in `EHub.Application`. Application handlers coordinate business rules
in `EHub.Domain` and use request/response models and shared results from
`EHub.Contracts` and `EHub.Shared`. `EHub.Infrastructure` implements the
application's persistence, identity, image-storage and email ports. Hosted
cleanup and outbox services are implemented in Infrastructure but execute in
the same backend process. PostgreSQL and managed providers are accessed only
through these infrastructure adapters. The consolidated External Providers
node also records the planned, provider-neutral AI integration without
claiming that an AI adapter or use case has already been implemented.

The diagram expresses both the runtime collaboration and the Clean Architecture
boundary: Application does not depend on Infrastructure, and Domain does not
depend on API, EF Core or external providers. The runtime arrow from Application
to Infrastructure means that a use case invokes an application-owned interface
whose implementation is supplied by Infrastructure at composition time.

## Figure 3. EHUB Production Deployment Diagram

The deployment diagram shows the verified single-VPS production topology. A
browser reaches `e-hub.com.vn` through Cloudflare. Cloudflare provides
authoritative DNS, proxied web traffic, edge TLS and DDoS protection, then sends
HTTPS traffic to the host Nginx gateway on the Ubuntu 24.04 VPS. Host Nginx uses
a Let's Encrypt certificate, redirects the `www` hostname to the canonical
domain, and proxies requests to the frontend container through the loopback-only
address `127.0.0.1:3000`.

Inside Docker, the frontend Nginx serves the React build and forwards API
requests to the backend on private port 8080. The backend connects to PostgreSQL
on private port 5432. Neither the backend nor PostgreSQL publishes a host port.
PostgreSQL data persists in the named Docker volume
`ehub-production-postgres-data`, although the volume is omitted from the figure
to keep the deployment view focused. The host-side backup job uses the existing
production script to create and validate a PostgreSQL export, which must then be
copied to encrypted off-site storage. The chosen off-site provider and transfer
automation remain operational decisions. The backend currently makes outbound
connections to Google Identity, Cloudinary and Gmail SMTP; the AI provider is a
clearly marked planned integration. Container health checks, restart policies
and bounded local-driver logs remain deployment safeguards.
