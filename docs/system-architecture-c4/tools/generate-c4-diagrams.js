/* eslint-disable no-console */
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const SVG_DIR = path.join(ROOT, 'svg');
const PNG_DIR = path.join(ROOT, 'png');
const DRAWIO_PATH = path.join(ROOT, 'EHub-C4-System-Architecture.drawio');
const BRAND_ICONS = JSON.parse(fs.readFileSync(
  path.resolve(ROOT, '..', 'system-architecture', 'assets', 'brand-icons.json'),
  'utf8',
));

const WIDTH = 1600;
const HEIGHT = 1000;

const palette = {
  canvas: '#F8FAFC', ink: '#0F172A', muted: '#475569', line: '#64748B', boundary: '#94A3B8',
  actor: '#0F172A', frontend: '#2563EB', backend: '#EA580C', application: '#D97706',
  domain: '#CA8A04', data: '#16A34A', external: '#9333EA', infrastructure: '#64748B', security: '#0891B2',
};

const brandColors = {
  cloudflare: '#F38020', dotnet: '#512BD4', postgresql: '#4169E1', react: '#149ECA',
  nginx: '#009639', docker: '#2496ED', cloudinary: '#3448C5', google: '#4285F4',
};

const genericIcons = {
  user: '<circle cx="12" cy="7" r="4"/><path d="M4 21v-2a8 8 0 0 1 16 0v2"/>',
  shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z"/><path d="m9 12 2 2 4-4"/>',
  workflow: '<rect x="3" y="3" width="6" height="6" rx="1"/><rect x="15" y="15" width="6" height="6" rx="1"/><path d="M9 6h4a4 4 0 0 1 4 4v5M15 18h-4a4 4 0 0 1-4-4V9"/>',
  layers: '<path d="m12 2 9 5-9 5-9-5 9-5Z"/><path d="m3 12 9 5 9-5M3 17l9 5 9-5"/>',
  file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6M8 13h8M8 17h6"/>',
  server: '<rect x="3" y="4" width="18" height="6" rx="2"/><rect x="3" y="14" width="18" height="6" rx="2"/><path d="M7 7h.01M7 17h.01"/>',
  settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1-2.8 2.8-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.6v.2h-4V21a1.7 1.7 0 0 0-1-1.6 1.7 1.7 0 0 0-1.9.3l-.1.1L4.2 17l.1-.1a1.7 1.7 0 0 0 .3-1.9A1.7 1.7 0 0 0 3 14H2.8v-4H3a1.7 1.7 0 0 0 1.6-1 1.7 1.7 0 0 0-.3-1.9L4.2 7 7 4.2l.1.1A1.7 1.7 0 0 0 9 4.6 1.7 1.7 0 0 0 10 3V2.8h4V3a1.7 1.7 0 0 0 1 1.6 1.7 1.7 0 0 0 1.9-.3l.1-.1L19.8 7l-.1.1a1.7 1.7 0 0 0-.3 1.9 1.7 1.7 0 0 0 1.6 1h.2v4H21a1.7 1.7 0 0 0-1.6 1Z"/>',
  mail: '<rect x="2" y="4" width="20" height="16" rx="2"/><path d="m22 7-10 6L2 7"/>',
  drive: '<path d="M22 12H2l3-8h14l3 8Z"/><path d="M2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6M6 16h.01M10 16h.01"/>',
  cloud: '<path d="M17.5 19H6a4 4 0 0 1-.6-7.95A7 7 0 0 1 19 9.5 4.5 4.5 0 0 1 17.5 19Z"/>',
  brain: '<path d="M9.5 4a3 3 0 0 0-5 2.2A3.5 3.5 0 0 0 5 13a3 3 0 0 0 4.5 3M14.5 4a3 3 0 0 1 5 2.2A3.5 3.5 0 0 1 19 13a3 3 0 0 1-4.5 3M9.5 4v16M14.5 4v16M7 8h2.5M14.5 8H17M7 15h2.5M14.5 15H17"/>',
  archive: '<rect x="3" y="4" width="18" height="5" rx="1"/><path d="M5 9v11h14V9M9 13h6"/>',
};

function esc(value) {
  return String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;').replaceAll("'", '&apos;');
}

function node(id, x, y, w, h, title, meta, technology, caption, type, icon) {
  return { kind: 'node', id, x, y, w, h, title, meta, technology, caption, type, icon };
}

function group(id, x, y, w, h, title, subtitle = '') {
  return { kind: 'group', id, x, y, w, h, title, subtitle };
}

function edge(id, source, target, label, options = {}) {
  return { id, source, target, label, ...options };
}

const diagrams = [
  {
    id: 'ehub-container', page: '01 - Container Diagram', file: '01-ehub-container-diagram',
    title: 'EHUB Container Diagram',
    subtitle: 'C4 Level 2 — applications, data store and external systems',
    groups: [group('ehub-system', 300, 120, 930, 760, 'Software System: EHUB', 'Modular monolith')],
    nodes: [
      node('users', 20, 340, 220, 155, 'EHUB Users', '«Person»', '',
        ['4 application roles'], 'actor', 'user'),
      node('web-app', 380, 340, 220, 165, 'EHUB Web Application', '«Container»', 'React · TypeScript',
        [], 'frontend', 'react'),
      node('backend-api', 760, 340, 230, 170, 'EHUB Backend API', '«Container»', 'ASP.NET Core · .NET 10',
        [], 'backend', 'dotnet'),
      node('database', 765, 620, 220, 160, 'EHUB Database', '«Database»', 'PostgreSQL 18',
        [], 'data', 'postgresql'),
      node('google', 1320, 130, 220, 155, 'Google Identity', '«External System»', 'OAuth 2.0',
        [], 'external', 'google'),
      node('cloudinary', 1320, 340, 220, 155, 'Cloudinary', '«External System»', 'Media storage',
        [], 'external', 'cloudinary'),
      node('email', 1320, 720, 220, 155, 'Gmail SMTP', '«External System»', 'Transactional email',
        [], 'external', 'mail'),
      node('ai-provider', 1320, 520, 220, 155, 'AI Provider', '«External System — Planned»', 'Provider TBD',
        [], 'external', 'brain'),
    ],
    edges: [
      edge('c1', 'users', 'web-app', 'HTTPS', { sourceSide: 'right', targetSide: 'left', labelX: 310, labelY: 362 }),
      edge('c2', 'web-app', 'backend-api', 'HTTPS API', { sourceSide: 'right', targetSide: 'left', labelX: 685, labelY: 362 }),
      edge('c3', 'backend-api', 'database', 'Database', { sourceSide: 'full-bottom', targetSide: 'top', labelX: 940, labelY: 565 }),
      edge('c4', 'backend-api', 'google', 'Login token', { sourceSide: 'top', targetSide: 'left', via: [[875, 170]], labelX: 1125, labelY: 149 }),
      edge('c5', 'backend-api', 'cloudinary', 'Media API', { sourceSide: 'right', targetSide: 'left', labelX: 1195, labelY: 362 }),
      edge('c6', 'backend-api', 'email', 'SMTP', { sourceSide: 'full-right', sourceRatio: 1, targetSide: 'left', via: [[990, 760]], labelX: 1190, labelY: 739 }),
      edge('c7', 'backend-api', 'ai-provider', 'Future API', { sourceSide: 'full-right', sourceRatio: 160 / 170, targetSide: 'top', via: [[1430, 500]], labelX: 1190, labelY: 480, dashed: true }),
    ],
    footer: 'Solid lines show current integrations; the dashed AI relationship is planned and provider-neutral.',
  },
  {
    id: 'ehub-backend-component', page: '02 - Backend Component Diagram', file: '02-ehub-backend-component-diagram',
    title: 'EHUB Backend Component Diagram',
    subtitle: 'C4 Level 3 — internal structure of the Backend API container',
    groups: [group('backend-container', 280, 120, 960, 760, 'Container: EHUB Backend API', 'ASP.NET Core · .NET 10')],
    nodes: [
      node('web-client', 20, 340, 220, 160, 'EHUB Web Application', '«Container»', 'React SPA', [], 'frontend', 'react'),
      node('api-entry', 320, 340, 220, 165, 'API Entry and Security', '«EHub.Api»', 'HTTP · authentication · authorization',
        [], 'backend', 'shield'),
      node('application-use-cases', 620, 340, 240, 170, 'Application Use Cases', '«EHub.Application»', 'Use cases · validation',
        [], 'application', 'workflow'),
      node('infrastructure-adapters', 950, 340, 220, 170, 'Infrastructure Adapters', '«EHub.Infrastructure»', 'Database · identity · integrations',
        [], 'infrastructure', 'server'),
      node('contracts-shared', 320, 650, 220, 165, 'Contracts and Shared', '«Contracts / Shared»', 'DTOs · Result · constants',
        [], 'security', 'file'),
      node('domain-model', 620, 650, 240, 165, 'Domain Model', '«EHub.Domain»', 'Entities · business rules',
        [], 'domain', 'layers'),
      node('background-jobs', 950, 650, 220, 165, 'Hosted Background Jobs', '«Infrastructure»', 'Outbox · cleanup in API',
        [], 'infrastructure', 'settings'),
      node('postgres', 1360, 340, 190, 160, 'PostgreSQL', '«Database»', 'PostgreSQL 18', [], 'data', 'postgresql'),
      node('providers', 1360, 650, 190, 175, 'External Providers', '«External Systems»', 'Google · Cloudinary · Gmail',
        ['AI Provider · planned'], 'external', 'cloud'),
    ],
    edges: [
      edge('b1', 'web-client', 'api-entry', 'HTTPS', { sourceSide: 'right', targetSide: 'left', labelX: 280, labelY: 362 }),
      edge('b2', 'api-entry', 'application-use-cases', 'Invoke', { sourceSide: 'right', targetSide: 'left', labelX: 580, labelY: 362 }),
      edge('b3', 'api-entry', 'contracts-shared', 'Contracts', { sourceSide: 'full-bottom', targetSide: 'top', labelX: 485, labelY: 575 }),
      edge('b4', 'application-use-cases', 'domain-model', 'Domain', { sourceSide: 'full-bottom', targetSide: 'top', labelX: 795, labelY: 575 }),
      edge('b5', 'application-use-cases', 'infrastructure-adapters', 'Ports', { sourceSide: 'right', targetSide: 'left', labelX: 905, labelY: 362 }),
      edge('b6', 'background-jobs', 'infrastructure-adapters', 'Hosted in API', { sourceSide: 'full-top', targetSide: 'full-bottom', labelX: 1115, labelY: 575 }),
      edge('b7', 'infrastructure-adapters', 'postgres', 'Database', { sourceSide: 'right', targetSide: 'left', labelX: 1270, labelY: 362 }),
      edge('b8', 'infrastructure-adapters', 'providers', 'Providers', { sourceSide: 'full-bottom', targetSide: 'top', via: [[1455, 510]], labelX: 1260, labelY: 489 }),
    ],
    footer: 'Runtime calls use interfaces owned by Application; Infrastructure supplies their implementations at composition time.',
  },
  {
    id: 'ehub-production-deployment', page: '03 - Production Deployment', file: '03-ehub-production-deployment-diagram',
    title: 'EHUB Production Deployment Diagram', subtitle: 'Production topology for e-hub.com.vn',
    groups: [
      group('vps', 540, 110, 770, 770, 'Deployment Node: Production Server', 'Ubuntu 24.04 · UFW'),
      group('docker', 790, 180, 500, 510, 'Docker Engine / Compose', 'Private network: ehub-production-private'),
      group('managed', 1330, 130, 240, 750, 'Managed External Systems', 'Providers and off-site backup'),
    ],
    nodes: [
      node('browser', 20, 300, 220, 155, 'User Browser', '«Deployment Node»', '', [], 'actor', 'user'),
      node('cloudflare', 270, 300, 220, 165, 'Cloudflare Edge', '«Edge»', 'DNS · proxy · TLS', [], 'security', 'cloudflare'),
      node('host-nginx', 570, 300, 220, 170, 'Host Nginx Gateway', '«Host Gateway»', 'TLS · reverse proxy', [], 'infrastructure', 'nginx'),
      node('frontend-container', 820, 300, 220, 165, 'Frontend Container', '«Container»', 'React · Nginx · :3000', [], 'frontend', 'react'),
      node('backend-container', 1060, 300, 220, 170, 'Backend Container', '«Container»', '.NET 10 · :8080', [], 'backend', 'dotnet'),
      node('postgres-container', 1060, 510, 220, 150, 'PostgreSQL Container', '«Container»', 'PostgreSQL 18 · :5432', [], 'data', 'postgresql'),
      node('backup-job', 820, 740, 220, 135, 'Backup Job', '«Host Operation»', 'Validated database export', [], 'infrastructure', 'archive'),
      node('google-managed', 1360, 150, 180, 125, 'Google Identity', '«External System»', 'OAuth 2.0', [], 'external', 'google'),
      node('cloudinary-managed', 1360, 290, 180, 125, 'Cloudinary', '«External System»', 'Media API', [], 'external', 'cloudinary'),
      node('ai-managed', 1360, 450, 180, 125, 'AI Provider', '«Planned»', 'Provider TBD', [], 'external', 'brain'),
      node('smtp-managed', 1360, 580, 180, 125, 'Gmail SMTP', '«External System»', 'SMTP', [], 'external', 'mail'),
      node('offsite-backup', 1360, 740, 180, 125, 'Off-site Backup', '«External Storage»', 'Encrypted copy', [], 'external', 'archive'),
    ],
    edges: [
      edge('d1', 'browser', 'cloudflare', 'HTTPS :443', { sourceSide: 'right', targetSide: 'left', labelX: 255, labelY: 322 }),
      edge('d2', 'cloudflare', 'host-nginx', 'HTTPS :443', { sourceSide: 'right', targetSide: 'left', labelX: 530, labelY: 322 }),
      edge('d3', 'host-nginx', 'frontend-container', 'HTTP :3000', { sourceSide: 'right', targetSide: 'left', labelX: 805, labelY: 322 }),
      edge('d4', 'frontend-container', 'backend-container', 'HTTP :8080', { sourceSide: 'right', targetSide: 'left', labelX: 1050, labelY: 322 }),
      edge('d5', 'backend-container', 'postgres-container', 'Npgsql :5432', { sourceSide: 'full-bottom', targetSide: 'top', labelX: 1230, labelY: 500 }),
      edge('d6', 'postgres-container', 'backup-job', 'DB export', { sourceSide: 'left', targetSide: 'top', via: [[930, 550]], labelX: 1020, labelY: 530, dashed: true }),
      edge('d7', 'backup-job', 'offsite-backup', 'Encrypted copy', { sourceSide: 'right', targetSide: 'left', labelX: 1210, labelY: 762, dashed: true }),
      edge('d8', 'backend-container', 'managed', 'Providers', { sourceSide: 'right', targetSide: 'left', targetRatio: 210 / 750, labelX: 1305, labelY: 322 }),
    ],
    footer: 'Ports 8080 and 5432 stay private. Dashed lines show backup operations; AI Provider is marked as planned.',
  },
];

function wrapWords(value, maxChars) {
  const words = String(value).split(/\s+/).filter(Boolean);
  const lines = [];
  let current = '';
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (current && next.length > maxChars) { lines.push(current); current = word; } else current = next;
  }
  if (current) lines.push(current);
  return lines;
}

function visualBox(item) {
  const size = 64;
  return { size, x: item.x + (item.w - size) / 2, y: item.y + 8, cx: item.x + item.w / 2 };
}

function sidePoint(item, side = 'auto', toward = null, ratio = 0.5) {
  if (side === 'full-left') return { x: item.x, y: item.y + item.h * ratio };
  if (side === 'full-right') return { x: item.x + item.w, y: item.y + item.h * ratio };
  if (side === 'full-top') return { x: item.x + item.w * ratio, y: item.y };
  if (side === 'full-bottom') return { x: item.x + item.w * ratio, y: item.y + item.h };
  const visual = item.kind === 'node' ? visualBox(item) : null;
  const box = item.kind === 'node' ? { x: visual.x, y: visual.y, w: visual.size, h: visual.size } : item;
  let selected = side;
  if (selected === 'auto' && toward) {
    const dx = (toward.x + toward.w / 2) - (item.x + item.w / 2);
    const dy = (toward.y + toward.h / 2) - (item.y + item.h / 2);
    selected = Math.abs(dx) >= Math.abs(dy) ? (dx >= 0 ? 'right' : 'left') : (dy >= 0 ? 'bottom' : 'top');
  }
  if (selected === 'left') return { x: box.x, y: box.y + box.h * ratio };
  if (selected === 'right') return { x: box.x + box.w, y: box.y + box.h * ratio };
  if (selected === 'top') return { x: box.x + box.w * ratio, y: box.y };
  return { x: box.x + box.w * ratio, y: box.y + box.h };
}

function routeForEdge(item, lookup) {
  const source = lookup.get(item.source);
  const target = lookup.get(item.target);
  const start = sidePoint(source, item.sourceSide || 'auto', target, item.sourceRatio ?? 0.5);
  const end = sidePoint(target, item.targetSide || 'auto', source, item.targetRatio ?? 0.5);
  return [start, ...(item.via || []).map(([x, y]) => ({ x, y })), end]
    .filter((point, index, points) => index === 0 || point.x !== points[index - 1].x || point.y !== points[index - 1].y);
}

function iconArtwork(key, color) {
  if (BRAND_ICONS[key]) return `<path d="${esc(BRAND_ICONS[key].path)}" fill="${brandColors[key] || color}"/>`;
  const artwork = genericIcons[key] || genericIcons.layers;
  return `<g fill="none" stroke="${color}" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${artwork}</g>`;
}

function standaloneIconDocument(item) {
  const color = palette[item.type] || palette.infrastructure;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><g transform="translate(2 2) scale(2.5)">${iconArtwork(item.icon, color)}</g></svg>`;
}

function renderSvgNode(item) {
  const visual = visualBox(item);
  const color = palette[item.type] || palette.infrastructure;
  const titleLines = wrapWords(item.title, Math.max(15, Math.floor(item.w / 8))).slice(0, 2);
  const titleY = visual.y + visual.size + 24;
  const metaY = titleY + titleLines.length * 18 + 2;
  const technologyY = metaY + 17;
  const captionY = technologyY + (item.technology ? 17 : 0);
  return `<g id="${esc(item.id)}" class="diagram-node">
    <g transform="translate(${visual.x} ${visual.y}) scale(${visual.size / 24})" aria-hidden="true">${iconArtwork(item.icon, color)}</g>
    ${titleLines.map((line, index) => `<text x="${visual.cx}" y="${titleY + index * 18}" class="node-title">${esc(line)}</text>`).join('')}
    <text x="${visual.cx}" y="${metaY}" class="node-meta" fill="${color}">${esc(item.meta)}</text>
    ${item.technology ? `<text x="${visual.cx}" y="${technologyY}" class="node-tech">${esc(item.technology)}</text>` : ''}
    ${item.caption.map((line, index) => `<text x="${visual.cx}" y="${captionY + index * 15}" class="node-caption">${esc(line)}</text>`).join('')}
  </g>`;
}

function renderSvgGroup(item) {
  const titleWidth = Math.min(item.w - 35, Math.max(190, item.title.length * 9 + 30));
  return `<g id="${esc(item.id)}" class="diagram-group">
    <rect x="${item.x}" y="${item.y}" width="${item.w}" height="${item.h}" rx="5" fill="none" stroke="${palette.boundary}" stroke-width="1.5" stroke-dasharray="7 6"/>
    <rect x="${item.x + 18}" y="${item.y - 14}" width="${titleWidth}" height="47" rx="7" fill="${palette.canvas}"/>
    <text x="${item.x + 30}" y="${item.y + 5}" class="group-title">${esc(item.title)}</text>
    ${item.subtitle ? `<text x="${item.x + 30}" y="${item.y + 24}" class="group-subtitle">${esc(item.subtitle)}</text>` : ''}
  </g>`;
}

function renderSvgEdge(item, lookup) {
  const points = routeForEdge(item, lookup);
  const d = points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`).join(' ');
  const labelWidth = Math.max(52, item.label.length * 6.5 + 18);
  return `<g id="${esc(item.id)}" class="diagram-edge">
    <path d="${d}" fill="none" stroke="${palette.line}" stroke-width="1.7" ${item.dashed ? 'stroke-dasharray="7 6"' : ''} marker-end="url(#arrow)"/>
    ${item.label ? `<rect x="${item.labelX - labelWidth / 2}" y="${item.labelY - 14}" width="${labelWidth}" height="22" rx="5" fill="${palette.canvas}" fill-opacity="0.97"/><text x="${item.labelX}" y="${item.labelY + 1}" class="edge-label">${esc(item.label)}</text>` : ''}
  </g>`;
}

function renderSvg(diagram) {
  const lookup = new Map([...diagram.groups, ...diagram.nodes].map((item) => [item.id, item]));
  return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH}" height="${HEIGHT}" viewBox="0 0 ${WIDTH} ${HEIGHT}" role="img" aria-labelledby="title desc">
  <title id="title">${esc(diagram.title)}</title><desc id="desc">${esc(diagram.subtitle)}</desc>
  <defs><marker id="arrow" markerWidth="9" markerHeight="8" refX="8" refY="4" orient="auto" markerUnits="strokeWidth"><path d="M0 0 L9 4 L0 8 Z" fill="${palette.line}"/></marker></defs>
  <style>
    text { font-family: Inter, Segoe UI, Arial, sans-serif; }
    .page-title { font-size: 27px; font-weight: 600; fill: ${palette.ink}; }
    .page-subtitle { font-size: 15px; fill: ${palette.muted}; }
    .group-title { font-size: 14px; font-weight: 600; fill: ${palette.ink}; }
    .group-subtitle { font-size: 10.5px; fill: ${palette.muted}; }
    .node-title { font-size: 14px; font-weight: 600; fill: ${palette.ink}; text-anchor: middle; }
    .node-meta { font-size: 10px; font-weight: 600; text-anchor: middle; }
    .node-tech { font-size: 10px; font-style: italic; fill: ${palette.muted}; text-anchor: middle; }
    .node-caption { font-size: 10px; fill: ${palette.muted}; text-anchor: middle; }
    .edge-label { font-size: 10.5px; font-weight: 600; fill: ${palette.muted}; text-anchor: middle; }
    .footer { font-size: 10.5px; fill: ${palette.muted}; }
  </style>
  <rect width="${WIDTH}" height="${HEIGHT}" fill="${palette.canvas}"/>
  <text x="40" y="50" class="page-title">${esc(diagram.title)}</text>
  <text x="40" y="78" class="page-subtitle">${esc(diagram.subtitle)}</text>
  ${diagram.groups.map(renderSvgGroup).join('\n')}
  ${diagram.edges.map((item) => renderSvgEdge(item, lookup)).join('\n')}
  ${diagram.nodes.map(renderSvgNode).join('\n')}
  <line x1="40" y1="935" x2="1560" y2="935" stroke="#CBD5E1"/>
  <text x="40" y="962" class="footer">${esc(diagram.footer)}</text>
</svg>`;
}

function drawioNode(item) {
  const visual = visualBox(item);
  const encoded = Buffer.from(standaloneIconDocument(item), 'utf8').toString('base64');
  const style = `shape=image;imageAspect=0;aspect=fixed;image=data:image/svg+xml;base64,${encoded};`;
  return `<mxCell id="${esc(item.id)}" value="" style="${style}" vertex="1" parent="1"><mxGeometry x="${visual.x}" y="${visual.y}" width="${visual.size}" height="${visual.size}" as="geometry"/></mxCell>`;
}

function drawioLabel(item) {
  const visual = visualBox(item);
  const color = palette[item.type] || palette.infrastructure;
  const value = `<b>${esc(item.title)}</b><br><font color="${color}"><b>${esc(item.meta)}</b></font>${item.technology ? `<br><i>${esc(item.technology)}</i>` : ''}${item.caption.length ? `<br>${item.caption.map(esc).join('<br>')}` : ''}`;
  const y = visual.y + visual.size + 8;
  return `<mxCell id="label-${esc(item.id)}" value="${esc(value)}" style="text;html=1;align=center;verticalAlign=top;whiteSpace=wrap;rounded=0;fillColor=none;strokeColor=none;fontColor=${palette.ink};fontSize=11;" vertex="1" parent="1"><mxGeometry x="${item.x}" y="${y}" width="${item.w}" height="${Math.max(55, item.y + item.h - y)}" as="geometry"/></mxCell>`;
}

function drawioGroup(item) {
  const value = `<b>${esc(item.title)}</b>${item.subtitle ? `<br><i>${esc(item.subtitle)}</i>` : ''}`;
  return `<mxCell id="${esc(item.id)}" value="${esc(value)}" style="rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=${palette.boundary};dashed=1;dashPattern=7 6;strokeWidth=1.5;align=left;verticalAlign=top;spacingTop=9;spacingLeft=15;fontColor=${palette.ink};fontSize=12;" vertex="1" parent="1"><mxGeometry x="${item.x}" y="${item.y}" width="${item.w}" height="${item.h}" as="geometry"/></mxCell>`;
}

function drawioEdge(item, lookup) {
  const points = routeForEdge(item, lookup);
  const waypoints = points.slice(1, -1).map((point) => `<mxPoint x="${point.x}" y="${point.y}"/>`).join('');
  const style = `edgeStyle=none;rounded=0;html=1;endArrow=block;endFill=1;strokeColor=${palette.line};strokeWidth=1.7;${item.dashed ? 'dashed=1;dashPattern=7 6;' : ''}`;
  const edgeCell = `<mxCell id="${esc(item.id)}" value="" style="${style}" edge="1" parent="1" source="${esc(item.source)}" target="${esc(item.target)}"><mxGeometry relative="1" as="geometry"><Array as="points">${waypoints}</Array></mxGeometry></mxCell>`;
  if (!item.label) return edgeCell;
  const width = Math.max(52, item.label.length * 6.5 + 18);
  const labelCell = `<mxCell id="label-${esc(item.id)}" value="${esc(item.label)}" style="text;html=1;align=center;verticalAlign=middle;whiteSpace=wrap;rounded=0;fillColor=${palette.canvas};strokeColor=none;fontColor=${palette.muted};fontSize=10;" vertex="1" parent="1"><mxGeometry x="${item.labelX - width / 2}" y="${item.labelY - 14}" width="${width}" height="22" as="geometry"/></mxCell>`;
  return `${edgeCell}${labelCell}`;
}

function renderDrawioPage(diagram) {
  const lookup = new Map([...diagram.groups, ...diagram.nodes].map((item) => [item.id, item]));
  const title = `<mxCell id="page-title" value="${esc(`<b>${esc(diagram.title)}</b><br><font color=&quot;${palette.muted}&quot;>${esc(diagram.subtitle)}</font>`)}" style="text;html=1;align=left;verticalAlign=middle;whiteSpace=wrap;rounded=0;fontSize=20;" vertex="1" parent="1"><mxGeometry x="40" y="20" width="1300" height="65" as="geometry"/></mxCell>`;
  const footer = `<mxCell id="page-footer" value="${esc(diagram.footer)}" style="text;html=1;align=left;verticalAlign=middle;whiteSpace=wrap;rounded=0;fontSize=10;fontColor=${palette.muted};" vertex="1" parent="1"><mxGeometry x="40" y="940" width="1500" height="30" as="geometry"/></mxCell>`;
  return `<diagram id="${esc(diagram.id)}" name="${esc(diagram.page)}"><mxGraphModel dx="1600" dy="1000" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1600" pageHeight="1000" math="0" shadow="0"><root><mxCell id="0"/><mxCell id="1" parent="0"/>${title}${diagram.groups.map(drawioGroup).join('')}${diagram.nodes.map(drawioNode).join('')}${diagram.nodes.map(drawioLabel).join('')}${diagram.edges.map((item) => drawioEdge(item, lookup)).join('')}${footer}</root></mxGraphModel></diagram>`;
}

function writeBaseOutputs() {
  fs.mkdirSync(SVG_DIR, { recursive: true });
  fs.mkdirSync(PNG_DIR, { recursive: true });
  for (const diagram of diagrams) fs.writeFileSync(path.join(SVG_DIR, `${diagram.file}.svg`), renderSvg(diagram), 'utf8');
  const pages = diagrams.map(renderDrawioPage).join('');
  fs.writeFileSync(DRAWIO_PATH, `<?xml version="1.0" encoding="UTF-8"?><mxfile host="app.diagrams.net" modified="2026-09-13T00:00:00.000Z" agent="EHUB C4 generator" version="24.7.17" type="device">${pages}</mxfile>`, 'utf8');
}

async function writePngOutputs() {
  let sharp;
  try { sharp = require('sharp'); } catch { console.warn('sharp is not available; SVG and Draw.io files were generated without PNG exports.'); return; }
  for (const diagram of diagrams) {
    await sharp(path.join(SVG_DIR, `${diagram.file}.svg`), { density: 180 }).png()
      .toFile(path.join(PNG_DIR, `${diagram.file}.png`));
  }
}

writeBaseOutputs();
writePngOutputs().then(() => console.log(`Generated ${diagrams.length} EHUB C4 diagrams in ${ROOT}`))
  .catch((error) => { console.error(error); process.exitCode = 1; });
