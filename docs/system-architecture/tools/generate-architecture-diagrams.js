/* eslint-disable no-console */
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const SVG_DIR = path.join(ROOT, 'svg');
const PNG_DIR = path.join(ROOT, 'png');
const DRAWIO_PATH = path.join(ROOT, 'EHub-System-Architecture.drawio');
const BRAND_ICONS = JSON.parse(fs.readFileSync(path.join(ROOT, 'assets', 'brand-icons.json'), 'utf8'));

const WIDTH = 1680;
const HEIGHT = 1050;

const palette = {
  canvas: '#F8FAFC',
  ink: '#0F172A',
  muted: '#475569',
  line: '#64748B',
  lightLine: '#CBD5E1',
  white: '#FFFFFF',
  actor: { fill: '#0F172A', stroke: '#020617', accent: '#38BDF8', text: '#FFFFFF' },
  delivery: { fill: '#ECFEFF', stroke: '#0891B2', accent: '#06B6D4', text: '#0F172A' },
  frontend: { fill: '#EFF6FF', stroke: '#2563EB', accent: '#3B82F6', text: '#0F172A' },
  backend: { fill: '#FFF7ED', stroke: '#EA580C', accent: '#F97316', text: '#0F172A' },
  domain: { fill: '#FFFBEB', stroke: '#D97706', accent: '#F59E0B', text: '#0F172A' },
  data: { fill: '#F0FDF4', stroke: '#16A34A', accent: '#22C55E', text: '#0F172A' },
  external: { fill: '#FAF5FF', stroke: '#9333EA', accent: '#A855F7', text: '#0F172A' },
  infra: { fill: '#F1F5F9', stroke: '#64748B', accent: '#94A3B8', text: '#0F172A' },
  security: { fill: '#FEFCE8', stroke: '#CA8A04', accent: '#EAB308', text: '#0F172A' },
  success: { fill: '#ECFDF5', stroke: '#059669', accent: '#10B981', text: '#0F172A' },
  danger: { fill: '#FEF2F2', stroke: '#DC2626', accent: '#EF4444', text: '#0F172A' },
};

const BRAND_COLORS = {
  postgresql: '#4169E1',
  react: '#149ECA',
  docker: '#2496ED',
  dotnet: '#512BD4',
  githubactions: '#2088FF',
  github: '#181717',
  nginx: '#009639',
  google: '#4285F4',
  cloudinary: '#3448C5',
};

const GENERIC_ICONS = {
  user: '<circle cx="12" cy="7" r="4"/><path d="M4 21v-2a8 8 0 0 1 16 0v2"/>',
  users: '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/>',
  code: '<path d="m8 9-4 3 4 3M16 9l4 3-4 3M14 5l-4 14"/>',
  branch: '<circle cx="6" cy="5" r="2"/><circle cx="18" cy="7" r="2"/><circle cx="6" cy="19" r="2"/><path d="M6 7v10M8 7h6a4 4 0 0 1 4 4v0"/>',
  review: '<path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>',
  shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z"/><path d="m9 12 2 2 4-4"/>',
  package: '<path d="m21 8-9-5-9 5 9 5 9-5Z"/><path d="m3 8 9 5v9l9-5V8M12 13v9"/>',
  server: '<rect x="3" y="4" width="18" height="6" rx="2"/><rect x="3" y="14" width="18" height="6" rx="2"/><path d="M7 7h.01M7 17h.01"/>',
  monitor: '<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/>',
  globe: '<circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15.3 15.3 0 0 1 0 20M12 2a15.3 15.3 0 0 0 0 20"/>',
  drive: '<path d="M22 12H2l3-8h14l3 8Z"/><path d="M2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6M6 16h.01M10 16h.01"/>',
  activity: '<path d="M3 12h4l3-9 4 18 3-9h4"/>',
  database: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5v14c0 1.7 4 3 9 3s9-1.3 9-3V5M3 12c0 1.7 4 3 9 3s9-1.3 9-3"/>',
  cloud: '<path d="M17.5 19H6a4 4 0 0 1-.6-7.95A7 7 0 0 1 19 9.5 4.5 4.5 0 0 1 17.5 19Z"/>',
  brain: '<path d="M9.5 4a3 3 0 0 0-5 2.2A3.5 3.5 0 0 0 5 13a3 3 0 0 0 4.5 3M14.5 4a3 3 0 0 1 5 2.2A3.5 3.5 0 0 1 19 13a3 3 0 0 1-4.5 3M9.5 4v16M14.5 4v16M7 8h2.5M14.5 8H17M7 15h2.5M14.5 15H17"/>',
  mail: '<rect x="2" y="4" width="20" height="16" rx="2"/><path d="m22 7-10 6L2 7"/>',
  archive: '<rect x="3" y="4" width="18" height="5" rx="1"/><path d="M5 9v11h14V9M9 13h6"/>',
  book: '<path d="M2 4h6a4 4 0 0 1 4 4v12a4 4 0 0 0-4-4H2Z"/><path d="M22 4h-6a4 4 0 0 0-4 4v12a4 4 0 0 1 4-4h6Z"/>',
  graduation: '<path d="m2 10 10-5 10 5-10 5L2 10Z"/><path d="M6 12v5c3 2 9 2 12 0v-5M22 10v6"/>',
  calendar: '<rect x="3" y="4" width="18" height="17" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/>',
  folder: '<path d="M3 5h6l2 2h10v12H3Z"/><path d="M8 12h8M8 16h5"/>',
  chart: '<path d="M3 3v18h18M7 16l4-5 4 3 5-7"/>',
  message: '<path d="M21 15a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4Z"/><path d="M8 9h8M8 13h5"/>',
  sparkles: '<path d="m12 3-1.3 3.7L7 8l3.7 1.3L12 13l1.3-3.7L17 8l-3.7-1.3ZM5 14l-.8 2.2L2 17l2.2.8L5 20l.8-2.2L8 17l-2.2-.8ZM19 14l-.8 2.2L16 17l2.2.8L19 20l.8-2.2L22 17l-2.2-.8Z"/>',
  layers: '<path d="m12 2 9 5-9 5-9-5 9-5Z"/><path d="m3 12 9 5 9-5M3 17l9 5 9-5"/>',
  key: '<circle cx="8" cy="15" r="4"/><path d="m11 12 9-9M15 7l2 2M18 4l2 2"/>',
  list: '<path d="m3 6 1 1 2-2M3 12l1 1 2-2M3 18l1 1 2-2M9 6h12M9 12h12M9 18h12"/>',
  radio: '<path d="M4.9 19.1a10 10 0 0 1 0-14.2M19.1 4.9a10 10 0 0 1 0 14.2M8.5 15.5a5 5 0 0 1 0-7M15.5 8.5a5 5 0 0 1 0 7"/><circle cx="12" cy="12" r="1.5"/>',
  lock: '<rect x="4" y="10" width="16" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/>',
  workflow: '<rect x="3" y="3" width="6" height="6" rx="1"/><rect x="15" y="15" width="6" height="6" rx="1"/><path d="M9 6h4a4 4 0 0 1 4 4v5M15 18h-4a4 4 0 0 1-4-4V9"/>',
  bell: '<path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4"/>',
  refresh: '<path d="M20 7h-5V2M4 17h5v5M5 9a8 8 0 0 1 13-3l2 1M19 15a8 8 0 0 1-13 3l-2-1"/>',
  alert: '<path d="M10.3 2.9 1.8 17a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 2.9a2 2 0 0 0-3.4 0Z"/><path d="M12 9v4M12 17h.01"/>',
  file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6M8 13h8M8 17h6"/>',
  link: '<path d="M10 13a5 5 0 0 0 7.5.5l2-2a5 5 0 0 0-7-7l-1.1 1.1M14 11a5 5 0 0 0-7.5-.5l-2 2a5 5 0 0 0 7 7l1.1-1.1"/>',
  settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1-2.8 2.8-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.6v.2h-4V21a1.7 1.7 0 0 0-1-1.6 1.7 1.7 0 0 0-1.9.3l-.1.1L4.2 17l.1-.1a1.7 1.7 0 0 0 .3-1.9A1.7 1.7 0 0 0 3 14H2.8v-4H3a1.7 1.7 0 0 0 1.6-1 1.7 1.7 0 0 0-.3-1.9L4.2 7 7 4.2l.1.1A1.7 1.7 0 0 0 9 4.6 1.7 1.7 0 0 0 10 3V2.8h4V3a1.7 1.7 0 0 0 1 1.6 1.7 1.7 0 0 0 1.9-.3l.1-.1L19.8 7l-.1.1a1.7 1.7 0 0 0-.3 1.9 1.7 1.7 0 0 0 1.6 1h.2v4H21a1.7 1.7 0 0 0-1.6 1Z"/>',
};

const ICON_ASSIGNMENTS = {
  developer: 'code', repository: 'github', 'pull-request': 'branch', 'ci-orchestrator': 'githubactions',
  'frontend-checks': 'react', 'backend-checks': 'dotnet', 'security-checks': 'shield', 'quality-gate': 'review',
  'merge-develop': 'branch', staging: 'monitor', 'staging-verification': 'review', 'release-build': 'docker',
  registry: 'package', 'release-gate': 'shield', 'database-gate': 'database', production: 'docker', verification: 'review',
  'end-users': 'users', dns: 'globe', 'web-gateway': 'nginx', 'api-container': 'dotnet', 'worker-container': 'settings',
  postgres: 'postgresql', monitoring: 'activity', 'persistent-volume': 'drive', 'backup-agent': 'archive', google: 'google', cloudinary: 'cloudinary',
  'ai-provider': 'brain', 'email-provider': 'mail', backup: 'archive', firewall: 'shield',
  admin: 'shield', lecturer: 'graduation', mentor: 'user', student: 'user', 'role-portals': 'monitor', 'web-state': 'react',
  'web-clients': 'link', 'api-layer': 'dotnet', 'iam-module': 'key', 'academic-module': 'book', 'team-module': 'users',
  'workspace-module': 'folder', 'evaluation-module': 'chart', 'learning-module': 'calendar', 'communication-module': 'message',
  'ai-module': 'sparkles', 'domain-layer': 'layers', 'infrastructure-layer': 'server', 'logical-db': 'postgresql',
  'logical-worker': 'settings', 'logical-google': 'google', 'logical-cloud': 'cloudinary', 'logical-ai': 'brain',
  'logical-email': 'mail',
  'ai-user': 'users', 'ai-web': 'react', 'ai-api': 'dotnet', 'context-minimizer': 'shield', 'ai-transaction': 'workflow',
  'ai-db': 'postgresql', 'job-claimer': 'list', 'ai-orchestrator': 'brain', 'result-validator': 'shield',
  'external-ai': 'brain', 'notification-delivery': 'bell', 'human-governance': 'user',
  'rt-user': 'user', 'signalr-client': 'react', 'signalr-hub': 'radio', 'membership-auth': 'lock',
  'message-service': 'message', 'rt-database': 'postgresql', broadcast: 'radio', 'business-action': 'workflow',
  'outbox-store': 'file', 'outbox-worker': 'settings', 'event-dispatcher': 'workflow', 'membership-sync': 'users',
  'notification-channel': 'bell', 'email-channel': 'mail', 'failure-monitor': 'alert', 'scale-note': 'server',
};

const DEFAULT_ICONS = {
  actor: 'user', delivery: 'package', frontend: 'monitor', backend: 'server', domain: 'layers',
  data: 'database', external: 'cloud', infra: 'server', security: 'shield', success: 'review', danger: 'alert',
};

function esc(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&apos;');
}

function node(id, x, y, w, h, title, body = [], type = 'backend', extra = {}) {
  return { kind: 'node', id, x, y, w, h, title, body, type, ...extra };
}

function group(id, x, y, w, h, title, type = 'infra', extra = {}) {
  return { kind: 'group', id, x, y, w, h, title, type, ...extra };
}

function edge(id, source, target, label = '', extra = {}) {
  return { kind: 'edge', id, source, target, label, ...extra };
}

function note(id, x, y, w, h, title, body = []) {
  return node(id, x, y, w, h, title, body, 'security', { note: true });
}

const diagrams = [
  {
    id: 'development-view',
    page: '01 - Development View',
    file: '01-development-view-architecture',
    title: 'EHub Development and Delivery Architecture',
    subtitle: 'Target workflow from reviewed source change to verified and recoverable production release',
    groups: [
      group('source-boundary', 25, 160, 400, 330, 'Source and Review', 'infra', { compactTitleGap: true }),
      group('ci-boundary', 450, 120, 650, 590, 'Continuous Integration', 'delivery', { compactTitleGap: true }),
      group('staging-boundary', 1125, 120, 525, 360, 'Staging Verification', 'frontend', { compactTitleGap: true }),
      group('release-boundary', 1125, 520, 525, 440, 'Controlled Production Release', 'backend', { compactTitleGap: true, titleOffsetX: 250 }),
    ],
    nodes: [
      node('developer', 45, 270, 105, 100, 'Developer', ['Feature branch'], 'actor'),
      node('repository', 175, 270, 120, 100, 'GitHub Repository', ['Protected source'], 'delivery'),
      node('pull-request', 320, 270, 90, 100, 'Pull Request', ['Peer review'], 'delivery'),

      node('ci-orchestrator', 710, 170, 170, 110, 'GitHub Actions', ['Reproducible pipeline'], 'delivery'),
      node('frontend-checks', 505, 345, 170, 115, 'Frontend Checks', ['Lint, test and build'], 'frontend'),
      node('backend-checks', 710, 345, 170, 115, 'Backend Checks', ['Build and tests'], 'backend'),
      node('security-checks', 925, 345, 150, 115, 'Security and Delivery Checks', ['Migrations, scans and containers'], 'security'),
      node('quality-gate', 715, 555, 160, 110, 'CI Quality Gate', ['All required checks pass'], 'success'),

      node('merge-develop', 1150, 175, 135, 110, 'Merge to develop', ['Protected branch'], 'success'),
      node('staging', 1330, 175, 160, 110, 'Staging Environment', ['Automatic deployment'], 'frontend'),
      node('staging-verification', 1325, 335, 170, 110, 'Staging Verification', ['Mentor and team acceptance'], 'success'),

      node('release-build', 1150, 580, 140, 110, 'Versioned Release Build', ['main and vX.Y.Z'], 'delivery'),
      node('registry', 1330, 580, 150, 110, 'GitHub Container Registry', ['Immutable image tags'], 'delivery'),
      node('release-gate', 1500, 580, 140, 110, 'Production Approval', ['Protected environment'], 'security'),
      node('database-gate', 1500, 745, 140, 110, 'Backup and Migration', ['Pre-deployment gate'], 'security'),
      node('production', 1320, 745, 140, 110, 'Production VPS', ['Docker Compose'], 'backend'),
      node('verification', 1140, 745, 140, 110, 'Release Verification', ['Health and smoke tests'], 'success'),
    ],
    edges: [
      edge('d1', 'developer', 'repository', 'push', { sourceSide: 'right', targetSide: 'left' }),
      edge('d2', 'repository', 'pull-request', 'open PR', { sourceSide: 'right', targetSide: 'left' }),
      edge('d3', 'pull-request', 'ci-orchestrator', 'trigger', {
        sourceSide: 'right', targetSide: 'top', labelX: 455, labelY: 296,
        points: [{ x: 430, y: 302 }, { x: 430, y: 145 }, { x: 795, y: 145 }],
      }),
      edge('d4', 'ci-orchestrator', 'frontend-checks', '', {
        sourceSide: 'left', targetSide: 'top',
        points: [{ x: 590, y: 207 }],
      }),
      edge('d5', 'ci-orchestrator', 'backend-checks', '', {
        sourceSide: 'bottom', targetSide: 'top',
      }),
      edge('d6', 'ci-orchestrator', 'security-checks', '', {
        sourceSide: 'right', targetSide: 'top',
        points: [{ x: 1000, y: 207 }],
      }),
      edge('d7', 'frontend-checks', 'quality-gate', '', {
        sourceSide: 'bottom', targetSide: 'left',
        points: [{ x: 590, y: 592 }],
      }),
      edge('d8', 'backend-checks', 'quality-gate', '', {
        sourceSide: 'bottom', targetSide: 'top',
      }),
      edge('d9', 'security-checks', 'quality-gate', '', {
        sourceSide: 'bottom', targetSide: 'right', targetRatio: 0.28,
        points: [{ x: 1000, y: 579.24 }],
      }),
      edge('d10', 'quality-gate', 'merge-develop', 'quality gate passed', {
        sourceSide: 'right', sourceRatio: 0.72, targetSide: 'left', labelX: 1085, labelY: 470,
        points: [{ x: 1085, y: 604.76 }, { x: 1085, y: 212 }],
      }),
      edge('d11', 'merge-develop', 'staging', 'deploy develop', { sourceSide: 'right', targetSide: 'left' }),
      edge('d12', 'staging', 'staging-verification', 'verify', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('d13', 'staging-verification', 'release-build', 'promote release', {
        sourceSide: 'bottom', targetSide: 'top', labelX: 1315, labelY: 494,
        points: [{ x: 1410, y: 485 }, { x: 1220, y: 485 }, { x: 1220, y: 555 }],
      }),
      edge('d14', 'release-build', 'registry', 'publish images', { sourceSide: 'right', targetSide: 'left' }),
      edge('d15', 'registry', 'release-gate', 'request deployment', { sourceSide: 'right', targetSide: 'left' }),
      edge('d16', 'release-gate', 'database-gate', 'approved', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('d17', 'database-gate', 'production', 'backup and migrate', { sourceSide: 'left', targetSide: 'right' }),
      edge('d18', 'production', 'verification', 'deploy and verify', { sourceSide: 'left', targetSide: 'right' }),
      edge('d19', 'verification', 'production', 'rollback image', {
        sourceSide: 'bottom', targetSide: 'bottom', dashed: true, labelX: 1300, labelY: 914,
        points: [{ x: 1210, y: 910 }, { x: 1390, y: 910 }],
      }),
    ],
  },
  {
    id: 'physical-view',
    page: '02 - Physical View',
    file: '02-physical-view-architecture',
    title: 'EHub Target Production Deployment Architecture',
    subtitle: 'Single-VPS production topology with a protected entry point, isolated containers, durable storage and managed integrations',
    groups: [
      group('internet-boundary', 25, 145, 245, 760, 'Public Internet', 'infra', { compactTitleGap: true }),
      group('vps-boundary', 290, 110, 1000, 840, 'Production VPS — Ubuntu LTS / Docker Compose', 'backend', { compactTitleGap: true }),
      group('private-network', 470, 170, 790, 500, 'Private Docker Network', 'infra', { compactTitleGap: true }),
      group('external-boundary', 1320, 110, 335, 840, 'Managed External Services', 'external', { compactTitleGap: true }),
    ],
    nodes: [
      node('end-users', 60, 190, 175, 110, 'EHub Users', ['Admin, Lecturer, Mentor and Student'], 'actor'),
      node('dns', 60, 340, 175, 110, 'Domain and DNS', ['Public resolution'], 'delivery'),

      node('firewall', 310, 340, 140, 110, 'Host Firewall', ['HTTPS 443 only'], 'security'),
      node('web-gateway', 500, 340, 170, 120, 'Nginx Web Gateway', ['TLS, React SPA and reverse proxy'], 'frontend'),
      node('api-container', 750, 340, 180, 120, 'EHub API Container', ['REST, JWT, SignalR and health'], 'backend'),
      node('worker-container', 950, 500, 180, 120, 'EHub Worker Container', ['Outbox, AI, email and cleanup jobs'], 'backend'),
      node('postgres', 755, 500, 170, 120, 'PostgreSQL', ['Private application database'], 'data'),

      node('monitoring', 1040, 720, 180, 105, 'Observability', ['Logs, health and resource alerts'], 'infra'),
      node('persistent-volume', 600, 800, 170, 105, 'PostgreSQL Data Volume', ['Durable local data'], 'data'),
      node('backup-agent', 850, 800, 180, 105, 'Backup Job', ['Scheduled encrypted export'], 'infra'),

      node('google', 1370, 175, 220, 100, 'Google Identity Platform', ['Google OAuth 2.0'], 'external'),
      node('cloudinary', 1370, 340, 220, 110, 'Cloudinary', ['Media and protected documents'], 'external'),
      node('ai-provider', 1370, 500, 220, 110, 'External AI Provider', ['Provider-neutral model API'], 'external'),
      node('email-provider', 1370, 650, 220, 110, 'Email Provider', ['Transactional email delivery'], 'external'),
      node('backup', 1370, 800, 220, 105, 'Off-site Backup Storage', ['Encrypted database backups'], 'external'),
    ],
    edges: [
      edge('p1', 'end-users', 'dns', 'resolve domain', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('p2', 'end-users', 'firewall', 'HTTPS :443', {
        sourceSide: 'right', targetSide: 'left', labelX: 260, labelY: 300,
        points: [{ x: 260, y: 227 }, { x: 260, y: 377 }],
      }),
      edge('p3', 'firewall', 'web-gateway', 'allow :443', { sourceSide: 'right', targetSide: 'left' }),
      edge('p4', 'web-gateway', 'api-container', '/api and /hubs', { sourceSide: 'right', targetSide: 'left' }),
      edge('p5', 'api-container', 'postgres', 'EF Core / PostgreSQL', {
        sourceSide: 'bottom', targetSide: 'top',
        labelX: 875, labelY: 485,
      }),
      edge('p6', 'worker-container', 'postgres', 'job and outbox access', {
        sourceSide: 'left', sourceRatio: 0.65, targetSide: 'right', targetRatio: 0.65,
        labelX: 940, labelY: 562,
      }),
      edge('p7', 'postgres', 'persistent-volume', 'durable storage', {
        sourceSide: 'left', sourceRatio: 1, targetSide: 'top', labelX: 650, labelY: 710,
        points: [{ x: 685, y: 566 }],
      }),
      edge('p8', 'api-container', 'google', 'token validation', {
        sourceSide: 'top', targetSide: 'left', labelX: 1160, labelY: 217,
        points: [{ x: 840, y: 212 }, { x: 1451, y: 212 }],
      }),
      edge('p9', 'api-container', 'cloudinary', 'signed asset operations', {
        sourceSide: 'right', sourceRatio: 0.5, targetSide: 'left', targetRatio: 0.5,
        labelX: 1160, labelY: 370,
      }),
      edge('p10', 'worker-container', 'ai-provider', 'AI analysis requests', {
        sourceSide: 'right', sourceRatio: 0.35, targetSide: 'left', targetRatio: 0.35,
        labelX: 1240, labelY: 520,
      }),
      edge('p11', 'worker-container', 'email-provider', 'SMTP / HTTPS', {
        sourceSide: 'right', sourceRatio: 0.8, targetSide: 'left', targetRatio: 0.5,
        labelX: 1360, labelY: 679,
        points: [{ x: 1280, y: 554.4 }, { x: 1280, y: 687 }, { x: 1451, y: 687 }],
      }),
      edge('p12', 'private-network', 'monitoring', 'all-container runtime telemetry', {
        sourceSide: 'bottom', sourceRatio: 0.8354, targetSide: 'top', dashed: true,
        labelX: 1045, labelY: 690,
      }),
      edge('p14', 'postgres', 'backup-agent', 'logical database export', {
        sourceSide: 'bottom', targetSide: 'left', dashed: true,
        labelX: 875, labelY: 825,
        points: [{ x: 840, y: 837 }],
      }),
      edge('p15', 'backup-agent', 'backup', 'encrypted copy', {
        sourceSide: 'right', targetSide: 'left', dashed: true,
        labelX: 1210, labelY: 829,
      }),
    ],
  },
  {
    id: 'logical-overall',
    page: '03 - Overall Logical View',
    file: '03-overall-logical-view-architecture',
    title: 'EHub Overall Logical Architecture',
    subtitle: 'Role-based presentation, modular business capabilities and dependency-inverted adapters',
    groups: [
      group('actors-boundary', 25, 145, 170, 775, 'System Actors', 'infra', { compactTitleGap: true }),
      group('client-boundary', 220, 145, 260, 775, 'Presentation Layer', 'frontend', { compactTitleGap: true }),
      group('backend-boundary', 505, 105, 790, 835, 'EHub Backend — Modular Monolith', 'backend', { compactTitleGap: true }),
      group('application-boundary', 570, 335, 655, 365, 'Application Layer — Business Use Cases', 'domain', { compactTitleGap: true }),
      group('data-boundary', 1320, 145, 335, 220, 'Application Data', 'data', { compactTitleGap: true }),
      group('external-logical', 1320, 395, 335, 525, 'External Services', 'external', { compactTitleGap: true }),
    ],
    nodes: [
      node('admin', 50, 205, 120, 80, 'Admin', ['System governance'], 'actor'),
      node('lecturer', 50, 350, 120, 80, 'Lecturer', ['Assigned classes'], 'actor'),
      node('mentor', 50, 495, 120, 80, 'Mentor', ['Assigned teams'], 'actor'),
      node('student', 50, 640, 120, 80, 'Student', ['Own class and team'], 'actor'),

      node('role-portals', 260, 220, 180, 115, 'Role-based Portals', ['Admin, Lecturer, Mentor and Student'], 'frontend'),
      node('web-state', 260, 410, 180, 115, 'React Web Application', ['Routing, server state and forms'], 'frontend'),
      node('web-clients', 260, 600, 180, 115, 'REST and SignalR Clients', ['HTTPS/JSON and realtime connection'], 'frontend'),

      node('api-layer', 645, 160, 200, 110, 'API Layer', ['Controllers, contracts, auth and SignalR'], 'backend'),
      node('logical-worker', 955, 160, 200, 110, 'Background Worker', ['Outbox and scheduled jobs'], 'backend'),

      node('iam-module', 590, 385, 150, 115, 'Identity and Access', ['Auth, roles and account approval'], 'domain'),
      node('academic-module', 745, 385, 150, 115, 'Academic and Class', ['Terms, subjects, classes and enrollment'], 'domain'),
      node('team-module', 900, 385, 150, 115, 'Team and Mentor', ['Teams, proposals and assignments'], 'domain'),
      node('workspace-module', 1055, 385, 150, 115, 'Project Workspace', ['Projects, milestones, tasks and submissions'], 'domain'),
      node('evaluation-module', 590, 535, 150, 115, 'Evaluation and Tracking', ['Rubrics, checkpoints and progress'], 'domain'),
      node('learning-module', 745, 535, 150, 115, 'Mentoring and Data', ['Sessions, workshops and data bank'], 'domain'),
      node('communication-module', 900, 535, 150, 115, 'Communication', ['Chat, presence and notifications'], 'domain'),
      node('ai-module', 1055, 535, 150, 115, 'AI Assistance', ['Human-reviewed proposal analysis'], 'domain'),

      node('domain-layer', 650, 770, 230, 105, 'Domain Model', ['Entities, invariants and domain events'], 'domain'),
      node('infrastructure-layer', 970, 770, 230, 105, 'Infrastructure Adapters', ['Persistence and external-service adapters'], 'infra'),

      node('logical-db', 1385, 205, 205, 105, 'PostgreSQL', ['Single source of truth'], 'data'),
      node('logical-google', 1385, 430, 205, 100, 'Google Identity', ['External authentication'], 'external'),
      node('logical-cloud', 1385, 550, 205, 100, 'Cloudinary', ['Media and documents'], 'external'),
      node('logical-ai', 1385, 670, 205, 100, 'AI Provider', ['Provider-neutral model API'], 'external'),
      node('logical-email', 1385, 790, 205, 100, 'Email Provider', ['Transactional delivery'], 'external'),
    ],
    edges: [
      edge('l1', 'actors-boundary', 'role-portals', 'role-based access', {
        sourceSide: 'right', sourceRatio: 0.1445, targetSide: 'left', targetRatio: 0.5,
        labelX: 260, labelY: 245,
      }),
      edge('l2', 'role-portals', 'web-state', '', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('l3', 'web-state', 'web-clients', '', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('l4', 'web-clients', 'api-layer', 'HTTPS REST / SignalR', {
        sourceSide: 'right', targetSide: 'left', labelX: 525, labelY: 195,
        points: [{ x: 525, y: 637 }, { x: 525, y: 197 }],
      }),
      edge('l5', 'api-layer', 'application-boundary', 'commands and queries', {
        sourceSide: 'bottom', targetSide: 'top', targetRatio: 0.2672,
      }),
      edge('l6', 'logical-worker', 'application-boundary', 'jobs and events', {
        sourceSide: 'bottom', targetSide: 'top', targetRatio: 0.7405,
      }),
      edge('l7', 'application-boundary', 'domain-layer', 'uses domain rules', {
        sourceSide: 'bottom', sourceRatio: 0.2977, targetSide: 'top',
      }),
      edge('l8', 'infrastructure-layer', 'application-boundary', 'implements application ports', {
        sourceSide: 'top', targetSide: 'bottom', targetRatio: 0.7863, dashed: true,
      }),
      edge('l9', 'infrastructure-layer', 'logical-db', 'persistence adapter', {
        sourceSide: 'right', sourceRatio: 0.25, targetSide: 'left', labelX: 1300, labelY: 235,
        points: [{ x: 1270, y: 792.5 }, { x: 1270, y: 242 }],
      }),
      edge('l10', 'infrastructure-layer', 'external-logical', 'external adapters', {
        sourceSide: 'right', sourceRatio: 0.75, targetSide: 'left', targetRatio: 0.8124,
        labelX: 1217, labelY: 810,
      }),
    ],
  },
  {
    id: 'ai-logical',
    page: '04 - AI Logical View',
    file: '04-ai-proposal-analysis-architecture',
    title: 'AI-assisted Project Proposal Analysis Architecture',
    subtitle: 'Authorized, privacy-aware, durable and human-governed AI analysis workflow',
    groups: [
      group('request-lane', 25, 145, 700, 760, 'Request and Safety Controls', 'frontend', { compactTitleGap: true }),
      group('ai-state-boundary', 750, 145, 255, 760, 'Durable Analysis State', 'data', { compactTitleGap: true }),
      group('worker-lane', 1030, 145, 350, 760, 'Asynchronous AI Processing', 'backend', { compactTitleGap: true }),
      group('ai-external-boundary', 1405, 145, 250, 500, 'External AI Provider', 'external', { compactTitleGap: true }),
      group('ai-governance-boundary', 1405, 670, 250, 235, 'Human Governance', 'security', { compactTitleGap: true }),
    ],
    nodes: [
      node('ai-user', 55, 230, 145, 110, 'Team / Lecturer', ['Submit proposal or request analysis'], 'actor'),
      node('ai-web', 245, 230, 150, 110, 'EHub Web', ['Proposal form and analysis status'], 'frontend'),
      node('ai-api', 475, 230, 200, 110, 'Proposal Analysis API', ['Authorization, validation and rate limit'], 'backend'),
      node('context-minimizer', 475, 410, 200, 115, 'Context and Prompt Controls', ['Minimize personal data and select prompt version'], 'security'),
      node('ai-transaction', 475, 620, 200, 110, 'Persist Request and Job', ['Atomic request, job and outbox write'], 'data'),

      node('ai-db', 785, 620, 185, 110, 'PostgreSQL', ['Proposal snapshot, job, result and metadata'], 'data'),

      node('job-claimer', 1100, 230, 210, 110, 'Job Claim and Retry', ['Lease, idempotency and retry policy'], 'backend'),
      node('ai-orchestrator', 1100, 410, 210, 110, 'AI Orchestrator', ['IAiProvider, timeout and model configuration'], 'backend'),
      node('result-validator', 1100, 620, 210, 110, 'Output Guardrails', ['Schema validation and business sanity checks'], 'security'),
      node('notification-delivery', 1100, 790, 210, 95, 'Result Delivery', ['In-app, SignalR and optional email'], 'backend'),

      node('external-ai', 1440, 410, 180, 110, 'AI Model Provider', ['Structured response API'], 'external'),
      node('human-governance', 1440, 790, 180, 95, 'Human Review', ['Lecturer or Admin retains decision authority'], 'security'),
    ],
    edges: [
      edge('a1', 'ai-user', 'ai-web', 'submit proposal', { sourceSide: 'right', targetSide: 'left' }),
      edge('a2', 'ai-web', 'ai-api', 'request analysis', { sourceSide: 'right', targetSide: 'left' }),
      edge('a3', 'ai-api', 'context-minimizer', 'authorized input', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('a4', 'context-minimizer', 'ai-transaction', 'safe versioned context', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('a5', 'ai-transaction', 'ai-db', 'atomic write and accept', { sourceSide: 'right', targetSide: 'left' }),
      edge('a6', 'ai-db', 'job-claimer', 'claim pending job', {
        sourceSide: 'right', targetSide: 'left', labelX: 1017, labelY: 430,
        points: [{ x: 1017, y: 657 }, { x: 1017, y: 267 }],
      }),
      edge('a7', 'job-claimer', 'ai-orchestrator', 'execute once', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('a8', 'ai-orchestrator', 'external-ai', 'provider request', { sourceSide: 'right', targetSide: 'left' }),
      edge('a9', 'external-ai', 'result-validator', 'untrusted structured result', {
        sourceSide: 'bottom', targetSide: 'right', labelX: 1392, labelY: 585,
        points: [{ x: 1392, y: 520 }, { x: 1392, y: 657 }],
      }),
      edge('a10', 'result-validator', 'ai-db', 'validated result and completion event', {
        sourceSide: 'left', targetSide: 'right', labelX: 1035, labelY: 645,
      }),
      edge('a11', 'ai-db', 'notification-delivery', 'completion event', {
        sourceSide: 'bottom', targetSide: 'left', dashed: true, labelX: 1017, labelY: 765,
        points: [{ x: 1017, y: 730 }, { x: 1017, y: 827 }],
      }),
      edge('a12', 'notification-delivery', 'human-governance', 'notify and review', {
        sourceSide: 'right', targetSide: 'left',
      }),
    ],
  },
  {
    id: 'realtime-async',
    page: '05 - Realtime and Async View',
    file: '05-realtime-and-asynchronous-processing-architecture',
    title: 'Realtime Communication and Asynchronous Processing Architecture',
    subtitle: 'Authorized SignalR delivery combined with a transactional PostgreSQL outbox',
    groups: [
      group('realtime-lane', 25, 140, 1630, 380, 'Realtime Communication Path', 'frontend'),
      group('async-lane', 25, 560, 1630, 380, 'Reliable Asynchronous Event Path', 'backend'),
    ],
    nodes: [
      node('rt-user', 60, 250, 150, 110, 'EHub User', ['Authorized role'], 'actor'),
      node('signalr-client', 255, 250, 180, 110, 'React SignalR Client', ['JWT connection', 'Reconnect policy'], 'frontend'),
      node('signalr-hub', 485, 235, 190, 140, 'SignalR Hub', ['Authenticate connection', 'Join authorized groups', 'Receive commands'], 'backend'),
      node('membership-auth', 725, 235, 210, 140, 'Membership Authorization', ['Class/team scope', 'Active assignment', 'Least privilege'], 'security'),
      node('message-service', 985, 235, 210, 140, 'Chat Message Service', ['Validate content', 'Persist before delivery', 'Server timestamp'], 'domain'),
      node('rt-database', 1245, 235, 180, 140, 'PostgreSQL', ['Chat messages', 'Group membership'], 'data'),
      node('broadcast', 1460, 235, 170, 140, 'Group Broadcast', ['Message and presence', 'Delivery acknowledgement'], 'success'),
      node('business-action', 65, 665, 210, 145, 'Business Transaction', ['Class, team or mentor change', 'Save state and event together'], 'domain'),
      node('outbox-store', 330, 665, 200, 145, 'OutboxMessage', ['Pending event', 'Attempt and status'], 'data'),
      node('outbox-worker', 590, 665, 200, 145, 'Outbox Worker', ['Claim batch', 'Retry with backoff', 'Idempotent dispatch'], 'backend'),
      node('event-dispatcher', 850, 665, 205, 145, 'Event Dispatcher', ['Route event by type', 'Record delivery result'], 'backend'),
      node('membership-sync', 1115, 630, 190, 105, 'Chat Membership Sync', ['Add/revoke access'], 'security'),
      node('notification-channel', 1115, 775, 190, 105, 'Notification Channel', ['Persist and push'], 'backend'),
      node('email-channel', 1365, 630, 210, 105, 'Email Channel', ['External email provider'], 'external'),
      node('failure-monitor', 1365, 775, 210, 105, 'Failure Monitoring', ['Dead-letter status', 'Operational alert'], 'danger'),
      note('scale-note', 40, 415, 410, 95, 'Scale-out note', ['A single API instance needs no Redis backplane.', 'Add Redis only when SignalR scales horizontally.']),
    ],
    edges: [
      edge('r1', 'rt-user', 'signalr-client', 'open application'),
      edge('r2', 'signalr-client', 'signalr-hub', 'WebSocket / SignalR'),
      edge('r3', 'signalr-hub', 'membership-auth', 'authorize'),
      edge('r4', 'membership-auth', 'message-service', 'allowed command'),
      edge('r5', 'message-service', 'rt-database', 'persist'),
      edge('r6', 'rt-database', 'broadcast', 'committed message'),
      edge('r7', 'broadcast', 'signalr-client', 'push to group', {
        sourceSide: 'bottom', targetSide: 'right', dashed: true,
        points: [{ x: 1545, y: 405 }, { x: 455, y: 405 }, { x: 455, y: 305 }], labelX: 1010, labelY: 398,
      }),
      edge('r8', 'business-action', 'outbox-store', 'same DB transaction'),
      edge('r9', 'outbox-store', 'outbox-worker', 'claim pending'),
      edge('r10', 'outbox-worker', 'event-dispatcher', 'dispatch'),
      edge('r11', 'event-dispatcher', 'membership-sync', 'membership event'),
      edge('r12', 'event-dispatcher', 'notification-channel', 'notification event'),
      edge('r13', 'event-dispatcher', 'email-channel', 'email event'),
      edge('r14', 'event-dispatcher', 'failure-monitor', 'exhausted retry', { dashed: true }),
      edge('r15', 'membership-sync', 'signalr-hub', 'revoke/join groups', { targetSide: 'bottom', dashed: true }),
      edge('r16', 'notification-channel', 'signalr-hub', 'push notification', { targetSide: 'bottom', dashed: true }),
    ],
  },
];

// Report figures intentionally keep only the information required to read the
// architecture at a glance. Detailed responsibilities remain in REPORT_CONTENT.md.
const COMPACT_BODIES = {
  developer: '',
  repository: 'Feature branch',
  'pull-request': 'Review gate',
  'ci-orchestrator': '',
  'frontend-checks': 'Lint • Test • Build',
  'backend-checks': 'Build • Unit • Integration',
  'security-checks': 'Migrate • Scan • Container',
  'quality-gate': 'Required checks pass',
  'merge-develop': 'Protected develop',
  staging: 'Automatic deployment',
  'staging-verification': 'Mentor • Team acceptance',
  'release-build': 'main • vX.Y.Z',
  registry: 'GHCR',
  'release-gate': 'Protected environment',
  'database-gate': 'Backup • Apply migration',
  production: 'Docker Compose',
  verification: 'Health • Smoke • Rollback',

  'end-users': 'Admin • Lecturer • Mentor • Student',
  dns: 'Public domain',
  'web-gateway': 'Nginx • TLS • React SPA',
  'api-container': '.NET API • JWT • SignalR',
  'worker-container': 'Outbox • Jobs • Cleanup',
  postgres: 'Business data • Audit • Outbox',
  monitoring: 'Logs • Health • Alerts',
  'persistent-volume': 'PostgreSQL data',
  'backup-agent': 'Scheduled encrypted export',
  google: 'OAuth 2.0',
  cloudinary: 'Media • Documents',
  'ai-provider': 'Model API',
  'email-provider': 'Transactional email',
  backup: 'Encrypted backups',
  firewall: 'Public HTTPS only',

  admin: '',
  lecturer: '',
  mentor: '',
  student: '',
  'role-portals': 'Role-based UI',
  'web-state': 'React SPA',
  'web-clients': 'REST • SignalR',
  'api-layer': 'Auth • REST • Hubs',
  'iam-module': 'Auth • Roles',
  'academic-module': 'Classes • Roster',
  'team-module': 'Teams • Mentors',
  'workspace-module': 'Projects • Tasks',
  'evaluation-module': 'Rubrics • Progress',
  'learning-module': 'Sessions • Data bank',
  'communication-module': 'Chat • Notifications',
  'ai-module': 'Proposal analysis',
  'domain-layer': 'Rules • Events',
  'infrastructure-layer': 'EF Core • Adapters',
  'logical-db': 'Source of truth',
  'logical-worker': 'Outbox • Jobs',
  'logical-google': 'OAuth',
  'logical-cloud': 'Media • Documents',
  'logical-ai': 'Model API',
  'logical-email': 'Transactional Email',

  'ai-user': '',
  'ai-web': 'Submit • Track status',
  'ai-api': 'Authorize • Validate',
  'context-minimizer': 'Minimize • Normalize • Version',
  'ai-transaction': 'Request + Job + Outbox',
  'ai-db': 'Request • Job • Result',
  'job-claimer': 'Lease • Idempotency • Retry',
  'ai-orchestrator': 'Provider-neutral execution',
  'result-validator': 'Schema + business checks',
  'external-ai': 'Structured API',
  'notification-delivery': 'In-app • SignalR • Email',
  'human-governance': 'AI recommends; human decides',

  'rt-user': '',
  'signalr-client': 'JWT • Reconnect',
  'signalr-hub': 'Authenticate • Join groups',
  'membership-auth': 'Class/team scope',
  'message-service': 'Validate • Persist',
  'rt-database': 'Messages • Membership',
  broadcast: 'Message • Presence',
  'business-action': 'State + Event',
  'outbox-store': 'Pending event',
  'outbox-worker': 'Retry • Idempotency',
  'event-dispatcher': 'Route by event type',
  'membership-sync': 'Grant • Revoke',
  'notification-channel': 'Persist • Push',
  'email-channel': 'External provider',
  'failure-monitor': 'Dead-letter • Alert',
  'scale-note': 'Redis only for horizontal scale-out',
};

const COMPACT_EDGE_LABELS = {
  d1: 'Push', d2: 'PR', d3: 'CI', d10: 'Passed', d11: 'Deploy', d12: 'Verify',
  d13: 'Promote', d14: 'Publish', d15: 'Deploy', d16: 'Approve',
  d17: 'DB ready', d18: 'Verify', d19: 'Rollback',
  p1: 'Resolve', p2: 'HTTPS :443', p3: 'Allow :443', p4: 'API / SignalR',
  p5: 'EF Core', p6: 'Jobs / Outbox', p7: 'Volume', p8: 'OAuth',
  p9: 'Storage', p10: 'AI', p11: 'Email', p12: 'All-container telemetry', p13: 'Telemetry',
  p14: 'DB export', p15: 'Encrypted',
  l1: 'Role Access', l4: 'REST / SignalR', l5: 'Commands / Queries', l6: 'Jobs / Events',
  l7: 'Domain Rules', l8: 'Implements Ports', l9: 'Persistence', l10: 'External Adapters',
  a1: '1 Submit', a2: '2 Request', a3: '3 Authorize', a4: '4 Prepare', a5: '5 Persist', a6: '6 Claimed Job',
  a7: '7 Execute', a8: '8 Invoke', a9: '9 Result', a10: '10 Store', a11: '11 Completion', a12: '12 Review',
  r1: '', r2: 'SignalR', r3: 'Auth', r4: '', r5: 'Persist', r6: '',
  r7: 'Broadcast', r8: 'Atomic', r9: 'Claim', r10: 'Dispatch', r11: '',
  r12: '', r13: '', r14: 'Failed', r15: '', r16: '',
};

const SHORT_TITLES = {
  repository: 'GitHub Repository',
  'ci-orchestrator': 'GitHub Actions',
  'security-checks': 'Security & Delivery',
  'quality-gate': 'CI Quality Gate',
  'merge-develop': 'Merge to develop',
  'staging-verification': 'Staging Verification',
  'release-build': 'Versioned Release',
  registry: 'GitHub Container Registry',
  'release-gate': 'Production Approval',
  'database-gate': 'Backup & Migration',
  verification: 'Release Verification',
  'end-users': 'EHub Users',
  dns: 'Domain / DNS',
  'web-gateway': 'Nginx Gateway',
  'api-container': 'EHub API',
  'worker-container': 'Background Worker',
  'persistent-volume': 'PostgreSQL Volume',
  'backup-agent': 'Backup Job',
  google: 'Google OAuth',
  'ai-provider': 'AI Provider',
  'email-provider': 'Email Provider',
  backup: 'Off-site Backup',
  firewall: 'Host Firewall',
  'role-portals': 'Role Portals',
  'web-state': 'React Web App',
  'web-clients': 'REST / SignalR Client',
  'api-layer': 'API Layer',
  'iam-module': 'Identity & Access',
  'academic-module': 'Academic & Class',
  'team-module': 'Team & Mentor',
  'evaluation-module': 'Evaluation & Tracking',
  'learning-module': 'Mentoring & Data',
  'communication-module': 'Communication',
  'domain-layer': 'Domain Model',
  'infrastructure-layer': 'Infrastructure Adapters',
  'logical-worker': 'Background Worker',
  'logical-google': 'Google OAuth',
  'logical-ai': 'AI Provider',
  'logical-email': 'Email Provider',
  'ai-user': 'Team / Lecturer',
  'ai-web': 'EHub Web',
  'ai-api': 'Proposal Analysis API',
  'context-minimizer': 'Context & Prompt Controls',
  'ai-transaction': 'Persist Request & Job',
  'job-claimer': 'Job Claim & Retry',
  'ai-orchestrator': 'AI Orchestrator',
  'result-validator': 'Output Guardrails',
  'external-ai': 'AI Model Provider',
  'notification-delivery': 'Result Delivery',
  'human-governance': 'Human Review',
  'signalr-client': 'React SignalR Client',
  'membership-auth': 'Membership Authorization',
  'message-service': 'Chat Message Service',
  broadcast: 'Group Broadcast',
  'business-action': 'Business Transaction',
  'outbox-store': 'Outbox Message',
  'outbox-worker': 'Outbox Worker',
  'event-dispatcher': 'Event Dispatcher',
  'membership-sync': 'Membership Sync',
  'notification-channel': 'Notification Channel',
  'email-channel': 'Email Channel',
  'failure-monitor': 'Failure Monitoring',
  'scale-note': 'Scale-out Note',
};

function displayBody(n) {
  const value = COMPACT_BODIES[n.id];
  return value ? [value] : [];
}

function displayEdgeLabel(e) {
  return Object.hasOwn(COMPACT_EDGE_LABELS, e.id) ? COMPACT_EDGE_LABELS[e.id] : '';
}

function displayTitle(n) {
  return SHORT_TITLES[n.id] || n.title;
}

function nodeVisualBox(n) {
  const iconSize = n.w <= 130 ? 48 : 58;
  const iconX = n.x + (n.w - iconSize) / 2;
  const iconY = n.y + 8;
  return { iconSize, iconX, iconY, cx: n.x + n.w / 2 };
}

function sidePoint(item, side = 'auto', toward = null, ratio = 0.5) {
  const bounds = item.kind === 'node'
    ? (() => {
      const visual = nodeVisualBox(item);
      return {
        x: visual.iconX,
        y: visual.iconY,
        w: visual.iconSize,
        h: visual.iconSize,
      };
    })()
    : item;
  const selected = side === 'auto' && toward
    ? (Math.abs((toward.x + toward.w / 2) - (item.x + item.w / 2)) >= Math.abs((toward.y + toward.h / 2) - (item.y + item.h / 2))
      ? ((toward.x + toward.w / 2) >= (item.x + item.w / 2) ? 'right' : 'left')
      : ((toward.y + toward.h / 2) >= (item.y + item.h / 2) ? 'bottom' : 'top'))
    : side;
  if (selected === 'left') return { x: bounds.x, y: bounds.y + bounds.h * ratio };
  if (selected === 'right') return { x: bounds.x + bounds.w, y: bounds.y + bounds.h * ratio };
  if (selected === 'top') return { x: bounds.x + bounds.w * ratio, y: bounds.y };
  return item.kind === 'node'
    ? { x: item.x + item.w * ratio, y: item.y + item.h }
    : { x: bounds.x + bounds.w * ratio, y: bounds.y + bounds.h };
}

function pathForEdge(e, lookup) {
  const s = lookup.get(e.source);
  const t = lookup.get(e.target);
  if (!s || !t) return null;
  const start = sidePoint(s, e.sourceSide || 'auto', t, e.sourceRatio ?? 0.5);
  const end = sidePoint(t, e.targetSide || 'auto', s, e.targetRatio ?? 0.5);
  const offset = e.offset || 0;
  let points;
  if (e.points) {
    points = [start, ...e.points, end];
  } else if (Math.abs(start.x - end.x) >= Math.abs(start.y - end.y)) {
    const mx = (start.x + end.x) / 2 + offset;
    points = [start, { x: mx, y: start.y }, { x: mx, y: end.y }, end];
  } else {
    const my = (start.y + end.y) / 2 + offset;
    points = [start, { x: start.x, y: my }, { x: end.x, y: my }, end];
  }
  const compact = points.filter((p, i) => i === 0 || p.x !== points[i - 1].x || p.y !== points[i - 1].y);
  return { points: compact, start, end };
}

function edgeLabelPosition(route) {
  const points = route.points;
  let best = null;
  for (let i = 0; i < points.length - 1; i += 1) {
    const a = points[i];
    const b = points[i + 1];
    const length = Math.hypot(b.x - a.x, b.y - a.y);
    if (!best || length > best.length) best = { a, b, length };
  }
  return {
    x: (best.a.x + best.b.x) / 2,
    y: (best.a.y + best.b.y) / 2 - 7,
  };
}

function wrapWords(value, maxChars) {
  const words = String(value).split(/\s+/).filter(Boolean);
  if (!words.length) return [''];
  const lines = [];
  let current = words[0];
  for (let index = 1; index < words.length; index += 1) {
    const candidate = `${current} ${words[index]}`;
    if (candidate.length <= maxChars) current = candidate;
    else {
      lines.push(current);
      current = words[index];
    }
  }
  lines.push(current);
  return lines;
}

function iconKeyForNode(n) {
  return ICON_ASSIGNMENTS[n.id] || DEFAULT_ICONS[n.type] || 'layers';
}

function iconArtwork(key, color) {
  if (BRAND_ICONS[key]) {
    return `<path d="${esc(BRAND_ICONS[key].path)}" fill="${BRAND_COLORS[key] || color}"/>`;
  }
  const artwork = GENERIC_ICONS[key] || GENERIC_ICONS.layers;
  return `<g fill="none" stroke="${color}" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${artwork}</g>`;
}

function standaloneIconDocument(n) {
  const p = palette[n.type] || palette.backend;
  const key = iconKeyForNode(n);
  return `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><g transform="translate(2 2) scale(2.5)">${iconArtwork(key, p.accent)}</g></svg>`;
}

function svgNode(n) {
  const p = palette[n.type] || palette.backend;
  const key = iconKeyForNode(n);
  const visual = nodeVisualBox(n);
  const maxTitleChars = Math.max(12, Math.floor(n.w / 7.4));
  const titleLines = wrapWords(displayTitle(n), maxTitleChars).slice(0, 3);
  const titleY = visual.iconY + visual.iconSize + 22;
  const titleMarkup = titleLines.map((line, index) =>
    `<text x="${visual.cx}" y="${titleY + index * 17}" class="node-title" fill="${palette.ink}">${esc(line)}</text>`).join('');
  return `<g id="${esc(n.id)}" class="diagram-node">
    <g transform="translate(${visual.iconX} ${visual.iconY}) scale(${visual.iconSize / 24})" aria-hidden="true">${iconArtwork(key, p.accent)}</g>
    ${titleMarkup}
  </g>`;
}

function svgGroup(g) {
  const p = palette[g.type] || palette.infra;
  const titleOffsetX = g.titleOffsetX ?? 18;
  const titleGapWidth = g.compactTitleGap
    ? Math.max(100, Math.min(g.w - titleOffsetX - 12, g.title.length * 7.2 + 28))
    : Math.max(170, Math.min(g.w - 36, g.title.length * 9.2 + 34));
  return `<g id="${esc(g.id)}" class="diagram-group">
    <rect x="${g.x}" y="${g.y}" width="${g.w}" height="${g.h}" rx="4" fill="none" stroke="${p.stroke}" stroke-width="1.25" stroke-dasharray="6 5"/>
    <rect x="${g.x + titleOffsetX}" y="${g.y - 13}" width="${titleGapWidth}" height="28" rx="8" fill="${palette.canvas}"/>
    <text x="${g.x + titleOffsetX + 11}" y="${g.y + 7}" class="group-title">${esc(g.title)}</text>
  </g>`;
}

function svgEdge(e, lookup) {
  const route = pathForEdge(e, lookup);
  if (!route) return '';
  const d = route.points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');
  const labelPosition = e.labelX !== undefined && e.labelY !== undefined
    ? { x: e.labelX, y: e.labelY }
    : edgeLabelPosition(route);
  const displayLabel = displayEdgeLabel(e);
  const labelWidth = Math.max(42, displayLabel.length * 6.6 + 16);
  const label = displayLabel ? `<g class="edge-label">
    <rect x="${labelPosition.x - labelWidth / 2}" y="${labelPosition.y - 13}" width="${labelWidth}" height="20" rx="6"/>
    <text x="${labelPosition.x}" y="${labelPosition.y + 2}">${esc(displayLabel)}</text>
  </g>` : '';
  return `<g id="${esc(e.id)}" class="diagram-edge">
    <path d="${d}" fill="none" stroke="${e.danger ? palette.danger.stroke : palette.line}" stroke-width="1.8" ${e.dashed ? 'stroke-dasharray="7 6"' : ''} marker-end="url(#arrow)"/>
    ${label}
  </g>`;
}

function legendSvg() {
  const items = [
    ['User / actor', 'actor'],
    ['Frontend / client', 'frontend'],
    ['Backend / process', 'backend'],
    ['Domain logic', 'domain'],
    ['Data store', 'data'],
    ['External service', 'external'],
  ];
  return `<g transform="translate(42 985)">
    ${items.map(([label, type], i) => {
      const x = i * 205;
      const p = palette[type];
      return `<rect x="${x}" y="0" width="18" height="18" rx="4" fill="${p.fill}" stroke="${p.stroke}"/><text x="${x + 28}" y="14" class="legend-text">${esc(label)}</text>`;
    }).join('')}
    <text x="1295" y="14" class="legend-text" text-anchor="middle">Solid: runtime/data flow</text>
    <text x="1515" y="14" class="legend-text" text-anchor="middle">Dashed: control/async</text>
  </g>`;
}

function renderSvg(diagram) {
  const all = [...diagram.groups, ...diagram.nodes];
  const lookup = new Map(all.map((item) => [item.id, item]));
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH}" height="${HEIGHT}" viewBox="0 0 ${WIDTH} ${HEIGHT}" role="img" aria-labelledby="title desc">
  <title id="title">${esc(diagram.title)}</title>
  <desc id="desc">${esc(diagram.subtitle)}</desc>
  <defs>
    <marker id="arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto" markerUnits="strokeWidth"><path d="M 0 0 L 8 4 L 0 8 z" fill="${palette.line}"/></marker>
    <filter id="softShadow" x="-20%" y="-20%" width="140%" height="140%"><feDropShadow dx="0" dy="2" stdDeviation="3" flood-color="#0F172A" flood-opacity="0.08"/></filter>
  </defs>
  <style>
    text { font-family: Inter, Segoe UI, Arial, sans-serif; }
    .page-title { font-size: 24px; font-weight: 600; fill: ${palette.ink}; }
    .node-title { font-size: 13px; font-weight: 600; text-anchor: middle; }
    .group-title { font-size: 14px; font-weight: 600; fill: ${palette.ink}; }
    .edge-label rect { fill: ${palette.canvas}; fill-opacity: 0.96; }
    .edge-label text { font-size: 11px; fill: ${palette.muted}; text-anchor: middle; }
  </style>
  <rect width="${WIDTH}" height="${HEIGHT}" fill="${palette.canvas}"/>
  <text x="42" y="52" class="page-title">${esc(diagram.title)}</text>
  ${diagram.groups.map(svgGroup).join('\n')}
  ${diagram.edges.map((e) => svgEdge(e, lookup)).join('\n')}
  ${diagram.nodes.map(svgNode).join('\n')}
</svg>`;
}

function drawioNode(n) {
  const encoded = Buffer.from(standaloneIconDocument(n), 'utf8').toString('base64');
  const visual = nodeVisualBox(n);
  const style = [
    'shape=image', 'verticalLabelPosition=bottom', 'verticalAlign=top', 'imageAspect=0', 'aspect=fixed',
    `image=data:image/svg+xml;base64,${encoded}`,
  ].join(';');
  return `<mxCell id="${esc(n.id)}" value="" style="${style}" vertex="1" parent="1"><mxGeometry x="${visual.iconX}" y="${visual.iconY}" width="${visual.iconSize}" height="${visual.iconSize}" as="geometry"/></mxCell>`;
}

function drawioLabel(n) {
  const visual = nodeVisualBox(n);
  const labelY = visual.iconY + visual.iconSize + 7;
  const labelHeight = Math.max(34, n.y + n.h - labelY);
  const style = [
    'text', 'html=1', 'align=center', 'verticalAlign=top', 'whiteSpace=wrap', 'rounded=0',
    'fillColor=none', 'strokeColor=none', `fontColor=${palette.ink}`, 'fontSize=12', 'fontStyle=1',
  ].join(';');
  return `<mxCell id="label-${esc(n.id)}" value="${esc(displayTitle(n))}" style="${style}" vertex="1" parent="1"><mxGeometry x="${n.x}" y="${labelY}" width="${n.w}" height="${labelHeight}" as="geometry"/></mxCell>`;
}

function drawioGroup(g) {
  const p = palette[g.type] || palette.infra;
  const titleOffsetX = g.titleOffsetX ?? 0;
  const style = [
    'rounded=0', 'whiteSpace=wrap', 'html=1', 'dashed=1', 'dashPattern=6 5',
    'fillColor=none', `strokeColor=${p.stroke}`, 'strokeWidth=1.25',
    `fontColor=${palette.ink}`, 'fontSize=14', 'fontStyle=1', 'align=left', 'verticalAlign=top',
    'spacingTop=8', `spacingLeft=${12 + titleOffsetX}`,
  ].join(';');
  return `<mxCell id="${esc(g.id)}" value="${esc(g.title)}" style="${style}" vertex="1" parent="1"><mxGeometry x="${g.x}" y="${g.y}" width="${g.w}" height="${g.h}" as="geometry"/></mxCell>`;
}

function drawioPortStyle(prefix, side, ratio = 0.5) {
  if (!side) return '';
  const x = side === 'left' ? 0 : side === 'right' ? 1 : ratio;
  const y = side === 'top' ? 0 : side === 'bottom' ? 1 : ratio;
  return `${prefix}X=${x};${prefix}Y=${y}`;
}

function drawioEdge(e) {
  const style = [
    'edgeStyle=orthogonalEdgeStyle', 'rounded=1', 'orthogonalLoop=1', 'jettySize=auto',
    'html=1', 'endArrow=block', 'endFill=1', `strokeColor=${palette.line}`, 'strokeWidth=1.5',
    `fontColor=${palette.muted}`, 'fontSize=12', `labelBackgroundColor=${palette.canvas}`,
    e.dashed ? 'dashed=1;dashPattern=7 6' : '',
    drawioPortStyle('exit', e.sourceSide, e.sourceRatio ?? 0.5),
    drawioPortStyle('entry', e.targetSide, e.targetRatio ?? 0.5),
  ].filter(Boolean).join(';');
  const waypoints = e.points?.length
    ? `<Array as="points">${e.points.map((point) => `<mxPoint x="${point.x}" y="${point.y}"/>`).join('')}</Array>`
    : '';
  return `<mxCell id="${esc(e.id)}" value="${esc(displayEdgeLabel(e))}" style="${style}" edge="1" parent="1" source="${esc(e.source)}" target="${esc(e.target)}"><mxGeometry relative="1" as="geometry">${waypoints}</mxGeometry></mxCell>`;
}

function drawioPage(diagram) {
  const titleStyle = `text;html=1;align=left;verticalAlign=middle;whiteSpace=wrap;rounded=0;fontSize=22;fontStyle=1;fontColor=${palette.ink};`;
  return `<diagram id="${esc(diagram.id)}" name="${esc(diagram.page)}">
    <mxGraphModel dx="1680" dy="1050" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1680" pageHeight="1050" math="0" shadow="0">
      <root>
        <mxCell id="0"/>
        <mxCell id="1" parent="0"/>
        <mxCell id="title-${esc(diagram.id)}" value="${esc(diagram.title)}" style="${titleStyle}" vertex="1" parent="1"><mxGeometry x="40" y="25" width="1200" height="40" as="geometry"/></mxCell>
        ${diagram.groups.map(drawioGroup).join('\n')}
        ${diagram.edges.map(drawioEdge).join('\n')}
        ${diagram.nodes.map(drawioNode).join('\n')}
        ${diagram.nodes.map(drawioLabel).join('\n')}
      </root>
    </mxGraphModel>
  </diagram>`;
}

async function main() {
  fs.mkdirSync(SVG_DIR, { recursive: true });
  fs.mkdirSync(PNG_DIR, { recursive: true });

  for (const diagram of diagrams) {
    const svg = renderSvg(diagram);
    fs.writeFileSync(path.join(SVG_DIR, `${diagram.file}.svg`), svg, 'utf8');
  }

  const drawio = `<mxfile host="app.diagrams.net" agent="EHub Architecture Generator" version="26.0.9" type="device" compressed="false">${diagrams.map(drawioPage).join('\n')}</mxfile>`;
  fs.writeFileSync(DRAWIO_PATH, drawio, 'utf8');

  let sharp;
  try {
    sharp = require('sharp');
  } catch {
    console.warn('sharp is unavailable; SVG and Draw.io files were generated without PNG exports.');
    return;
  }

  for (const diagram of diagrams) {
    const svgPath = path.join(SVG_DIR, `${diagram.file}.svg`);
    const pngPath = path.join(PNG_DIR, `${diagram.file}.png`);
    await sharp(svgPath, { density: 180 }).png({ compressionLevel: 9 }).toFile(pngPath);
  }

  console.log(`Generated ${diagrams.length} architecture diagrams in ${ROOT}`);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
