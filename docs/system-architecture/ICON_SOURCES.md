# EHub Architecture Icon Sources

The architecture figures combine brand marks for recognizable technologies with project-owned generic outline icons for architectural concepts.

## Brand marks

The embedded vector paths in `assets/brand-icons.json` were obtained from the open-source [Simple Icons](https://github.com/simple-icons/simple-icons) collection on 20 August 2026.

Included brands:

- PostgreSQL
- React
- Docker
- .NET
- GitHub Actions
- GitHub
- Nginx
- Google
- Cloudinary

The marks are used only to identify technologies in technical architecture documentation. All product names, logos and trademarks remain the property of their respective owners. Brand marks should not be altered in a way that suggests endorsement or partnership.

## Generic architecture icons

Generic icons such as actor, authorization, database, AI, messaging, notification, audit, monitoring and workflow symbols are local outline drawings defined in `tools/generate-architecture-diagrams.js`. They do not require external downloads when the figures are regenerated.

## Offline behavior

All icon artwork is embedded directly into the exported SVG files and encoded into the multi-page Draw.io file. Opening, editing or exporting the diagrams therefore does not contact an external image host.
