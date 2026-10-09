# EHub System Architecture Diagram Set

This directory contains the target architecture baseline for the final EHub graduation report. The set uses three architectural viewpoints and five figures.

## Diagram inventory

1. **Development and Delivery View Architecture** — reviewed source change, parallel CI checks, staging acceptance, versioned releases and controlled production deployment.
2. **Physical View Architecture** — target production deployment behind Cloudflare on a single VPS with an explicit React frontend and external managed services.
3. **Overall Logical View Architecture** — role-based React UI, REST API, hosting-neutral background processing, nine business capabilities, inward infrastructure dependencies and external systems.
4. **AI Proposal Analysis Architecture** — analysis bound to a submitted proposal version, scoped project similarity checks, durable job state, validated reports, REST status polling and lecturer review.
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

- Recognizable technology components use their official-style brand marks, including PostgreSQL, React, .NET, Docker, GitHub, Nginx, Cloudflare, Google and Cloudinary.
- Business and architectural components use a consistent outline icon set embedded by the generator.
- Every icon is stored inside the generated SVG/Draw.io content, so the diagrams remain fully visible offline and do not depend on external image URLs.
- Each component is represented by a standalone large icon or logo with its name directly underneath; rectangular component cards are intentionally omitted.
- In the Draw.io source, each icon and caption remains a separate editable object so the layout can be refined without rebuilding the diagram.
- Report figures primarily show component names. Figure 4 adds short captions to clarify the authorized Student actor, version-bound automatic job creation, orchestration, REST polling and lecturer decisions; detailed responsibilities remain in `REPORT_CONTENT.md`.

## Recommended report order

```text
1.1 System Architecture

A. Development View Architecture
   Figure 1. EHub Development and Delivery Architecture

B. Physical View Architecture
   Figure 2. EHub Target Production Deployment Architecture

C. Logical View Architecture
   Figure 3. EHub Overall Logical Architecture
   Figure 4. Logical View – AI-assisted Project Proposal Analysis Architecture
   Figure 5. Realtime Communication and Asynchronous Processing Architecture
```

## Editing and export

1. Open `EHub-System-Architecture.drawio` in diagrams.net or the Draw.io desktop application.
2. Keep the page size in landscape orientation.
3. Preserve the shared icon, color and connector language across all pages.
4. Export as SVG for the final report. Use PNG only when the report editor cannot preserve SVG quality.
5. Keep captions outside the image in the report document so figure numbering remains controlled by the document editor.

To regenerate only Figure 4's SVG/PNG exports while retaining the complete multi-page Draw.io document, run `node docs/system-architecture/tools/generate-architecture-diagrams.js --only=04-ai-proposal-analysis-architecture` from the repository root. PNG generation requires `sharp` on Node's module search path.

For Figure 3, use `--only=03-overall-logical-view-architecture`. Its nine capabilities use a 3 × 3 layout; Team Management, Mentoring & Support, Data Bank and Notifications have distinct responsibilities. The `AI Proposal Analysis` capability is detailed in Figure 4. Figure 3's REST path does not require SignalR, and `Background Processing` does not mandate a separate worker process. Capability groups express the target organization rather than nine already-complete code modules. The detailed implementation qualifications are in `REPORT_CONTENT.md`.

Figure 4's target scope is **proposal analysis**, automatically queued after an authorized submission. It supports lecturer approval or revision decisions; it is not a rubric-grading workflow. Its REST status/result path does not require the SignalR implementation shown in the older realtime view. Changes to rubric-based AI grading or the other figures require their own scope review.

## Architecture status

These diagrams describe the **approved target architecture**, not the temporary Vercel–Render–Neon mentor staging environment. Before final submission, perform an as-built review and remove or update any component that was not implemented.

Items requiring final verification include:

- AI provider and deployed model.
- Separate `EHub.Worker` executable/container.
- SignalR implementation and production route.
- Cloudinary access mode for protected documents.
- Transactional email provider.
- Production monitoring and off-site backup implementation.
- Cloudflare proxied DNS, WAF, rate limiting, strict origin TLS and origin access restrictions.
- GitHub Actions production deployment workflow.
