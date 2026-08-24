# EHub System Architecture Diagram Set

This directory contains the target architecture baseline for the final EHub graduation report. The set uses three architectural viewpoints and five figures.

## Diagram inventory

1. **Development and Delivery View Architecture** — reviewed source change, parallel CI checks, staging acceptance, versioned releases and controlled production deployment.
2. **Physical View Architecture** — target production deployment on a single VPS with external managed services.
3. **Overall Logical View Architecture** — actors, presentation layer, modular-monolith business modules, Clean Architecture layers and external systems.
4. **AI Proposal Analysis Architecture** — provider-neutral asynchronous AI workflow with structured validation and human governance.
5. **Realtime and Asynchronous Processing Architecture** — authorized SignalR communication and transactional PostgreSQL outbox processing.

## Files

- `EHub-System-Architecture.drawio`: editable multi-page Draw.io source.
- `svg/`: vector exports recommended for Word, Google Docs and PDF reports.
- `png/`: high-resolution fallback exports.
- `assets/brand-icons.json`: locally embedded brand-vector paths used by the generator.
- `ICON_SOURCES.md`: icon provenance and report-use guidance.
- `REPORT_CONTENT.md`: report-ready English captions and component explanations.
- `tools/generate-architecture-diagrams.js`: deterministic generator for the Draw.io, SVG and PNG files.

## Icon system

- Recognizable technology components use their official-style brand marks, including PostgreSQL, React, .NET, Docker, GitHub, Nginx, Google and Cloudinary.
- Business and architectural components use a consistent outline icon set embedded by the generator.
- Every icon is stored inside the generated SVG/Draw.io content, so the diagrams remain fully visible offline and do not depend on external image URLs.
- Each component is represented by a standalone large icon or logo with its name directly underneath; rectangular component cards are intentionally omitted.
- In the Draw.io source, each icon and caption remains a separate editable object so the layout can be refined without rebuilding the diagram.
- Report figures show component names only. Detailed responsibilities and architectural rationale belong in `REPORT_CONTENT.md`, not inside the figures.

## Recommended report order

```text
1.1 System Architecture

A. Development View Architecture
   Figure 1. EHub Development and Delivery Architecture

B. Physical View Architecture
   Figure 2. EHub Target Production Deployment Architecture

C. Logical View Architecture
   Figure 3. EHub Overall Logical Architecture
   Figure 4. AI-assisted Project Proposal Analysis Architecture
   Figure 5. Realtime Communication and Asynchronous Processing Architecture
```

## Editing and export

1. Open `EHub-System-Architecture.drawio` in diagrams.net or the Draw.io desktop application.
2. Keep the page size in landscape orientation.
3. Preserve the shared icon, color and connector language across all pages.
4. Export as SVG for the final report. Use PNG only when the report editor cannot preserve SVG quality.
5. Keep captions outside the image in the report document so figure numbering remains controlled by the document editor.

## Architecture status

These diagrams describe the **approved target architecture**, not the temporary Vercel–Render–Neon mentor staging environment. Before final submission, perform an as-built review and remove or update any component that was not implemented.

Items requiring final verification include:

- AI provider and deployed model.
- Separate `EHub.Worker` executable/container.
- SignalR implementation and production route.
- Cloudinary access mode for protected documents.
- Transactional email provider.
- Production monitoring and off-site backup implementation.
- GitHub Actions production deployment workflow.
