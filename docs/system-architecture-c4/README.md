# EHUB C4 System Architecture

This directory contains the current architecture diagrams for the EHUB
graduation report, with one clearly marked planned extension. It intentionally
does not replace or duplicate the System
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
- Google Identity, Gmail SMTP and Cloudinary are shown because the deployed
  backend is configured to use them.
- A provider-neutral AI system is included only as a planned external
  integration. Its dashed relationship and `Planned` label must remain until a
  concrete AI feature and provider are selected and implemented.
- Hosted cleanup and outbox processing are shown inside the backend process,
  not as a separate worker container.
- SignalR, Discord, staging and automated CD are not shown because they are not
  part of the verified production architecture represented here.
- The deployment diagram shows the existing host-side PostgreSQL export script
  and the required off-site copy as an operational backup flow. The off-site
  storage provider is intentionally unspecified.
- The System Context Diagram remains in Report 3 and should be referenced from
  the opening of Report 4 as C4 Level 1.

## Regeneration

From the repository root:

```powershell
node docs/system-architecture-c4/tools/generate-c4-diagrams.js
```

The Draw.io and SVG files are always generated. PNG export is also generated
when the optional `sharp` Node package is available.
