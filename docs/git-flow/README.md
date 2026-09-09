# EHub Git Branching and Release Flow

This directory contains a simplified report figure and a detailed technical Git Flow diagram for the target EHub delivery process.

## Files

- `EHub-Git-Flow.drawio`: editable source for the simplified report figure.
- `svg/ehub-git-branching-and-release-flow.svg`: simplified vector report export.
- `png/ehub-git-branching-and-release-flow.png`: simplified high-resolution report preview.
- `EHub-Git-Flow-Detailed.drawio`: editable source for the detailed technical figure.
- `svg/ehub-git-branching-and-release-flow-detailed.svg`: detailed vector export.
- `png/ehub-git-branching-and-release-flow-detailed.png`: detailed high-resolution reference.
- `REPORT_CONTENT.md`: concise report text, branch policies and flow explanation.
- `tools/generate-git-flow-diagram.js`: deterministic diagram generator.

## Regenerate

Run from the repository root:

```powershell
node docs/git-flow/tools/generate-git-flow-diagram.js
```

The generator requires the `sharp` Node.js package for PNG rendering. It produces both diagram variants and reuses the locally stored GitHub, GitHub Actions and Discord assets from `docs/system-architecture/assets`; the generated SVG and Draw.io files embed those assets and remain viewable offline.

## Report placement

Recommended section:

```text
Software Development Process
  Git Branching and Release Strategy
    Figure X. EHub Git Branching and Release Flow
```

Use the simplified figure in the main report and the detailed figure in an appendix or technical delivery guide. Both describe the **approved target workflow** for the completed project. Before final submission, verify that GitHub branch protection, required checks, release tagging and production deployment triggers match the implemented repository settings.
