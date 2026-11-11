# EHUB C4 System Architecture

This directory contains the current and target architecture diagrams for the
EHUB graduation report. It intentionally does not replace or duplicate the System
Context Diagram in Report 3. Report 4 continues the C4 description with exactly
three diagrams:

1. `01-ehub-container-diagram` - C4 Level 2 containers and external systems.
2. `02-ehub-backend-component-diagram` - C4 Level 3 view of the backend API.
3. `03-ehub-production-deployment-diagram` - the deployed single-VPS topology.

## Files

- `EHub-C4-System-Architecture.drawio`: editable three-page diagrams.net source.
- `svg/`: vector exports recommended for Word, Google Docs and PDF reports.
- `png/`: high-resolution fallback exports.
- `REPORT_CONTENT.md`: concise report-ready descriptions and captions.
- `tools/generate-c4-diagrams.js`: deterministic source for all diagram files.

The visual language follows the existing EHUB architecture set: technologies
use recognizable logos, architectural responsibilities use simple line icons,
and names appear directly below the icons instead of inside component cards.
Brand paths are reused from `docs/system-architecture/assets/brand-icons.json`.
Dashed rectangles are used only for C4 system, container and deployment
boundaries.

## Scope decisions

- The diagrams describe the current modular-monolith implementation and the
  production deployment represented by `docker-compose.production.yml`.
- Google Identity, Gmail SMTP (with Brevo fallback), Cloudinary (avatars,
  mentor CVs and feedback attachments) and Cloudflare R2 (submission files and
  preview PDFs) are shown because the deployed backend is configured to use them.
- There are two AI capabilities. Mentor recommendation calls the external
  Google Gemini API. AI checkpoint summaries run on a self-hosted Ollama
  container (Qwen 3.5 2B), drawn as an EHUB container. The summary feature has a
  per-environment switch; the diagrams show it enabled.
- The infrastructure layer is a single VPS running Docker Compose; Docker Swarm,
  Kubernetes and managed cloud platforms are not used.
- Hosted cleanup, document preview, summary and outbox processing are shown
  inside the backend process, not as a separate worker container. Document
  previews are produced by LibreOffice inside the backend container.
- Chat and the WebSocket realtime channel are not shown because they are not
  used. Only production is drawn; the staging stack on the same VPS is omitted.
- The deployment diagram shows the host-side backup job (systemd timers for the
  daily backup, freshness and disk checks) uploading to a private Cloudflare R2
  bucket and sending alerts to Discord. The GitHub Actions release pipeline is
  not part of the runtime topology, so it is described in text only.
- The System Context Diagram remains in Report 3 and should be referenced from
  the opening of Report 4 as C4 Level 1.

## Regeneration

From the repository root:

```powershell
node docs/system-architecture-c4/tools/generate-c4-diagrams.js
```

The Draw.io and SVG files are always generated. PNG export is also generated
when the optional `sharp` Node package is available (for example through
`NODE_PATH` pointing to a folder where `sharp` is installed).
