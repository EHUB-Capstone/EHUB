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
  'frontend-checks': 'react', 'backend-checks': 'dotnet', 'supply-chain': 'docker', merge: 'review',
  staging: 'monitor', 'release-gate': 'shield', registry: 'package', production: 'docker', verification: 'review', secrets: 'key',
  'end-users': 'users', dns: 'globe', 'web-gateway': 'nginx', 'api-container': 'dotnet', 'worker-container': 'settings',
  postgres: 'postgresql', monitoring: 'activity', 'persistent-volume': 'drive', google: 'google', cloudinary: 'cloudinary',
  'ai-provider': 'brain', 'email-provider': 'mail', backup: 'archive', firewall: 'shield',
  admin: 'shield', lecturer: 'graduation', mentor: 'user', student: 'user', 'role-portals': 'monitor', 'web-state': 'react',
  'web-clients': 'link', 'api-layer': 'dotnet', 'iam-module': 'key', 'academic-module': 'book', 'team-module': 'users',
  'workspace-module': 'folder', 'evaluation-module': 'chart', 'learning-module': 'calendar', 'communication-module': 'message',
  'ai-module': 'sparkles', 'domain-layer': 'layers', 'infrastructure-layer': 'server', 'logical-db': 'postgresql',
  'logical-worker': 'settings', 'logical-google': 'google', 'logical-cloud': 'cloudinary', 'logical-external': 'cloud',
  'ai-user': 'users', 'ai-web': 'react', 'ai-api': 'dotnet', 'context-minimizer': 'shield', 'ai-transaction': 'database',
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
    title: 'EHub Development and CI/CD Architecture',
    subtitle: 'Target software delivery workflow from source change to verified deployment',
    groups: [
      group('ci-boundary', 640, 160, 430, 520, 'GitHub Actions — CI Quality Gate', 'delivery'),
      group('env-boundary', 1110, 145, 510, 780, 'Deployment Environments', 'infra'),
    ],
    nodes: [
      node('developer', 45, 365, 155, 110, 'Developer', ['Feature branch', 'Signed commit'], 'actor'),
      node('repository', 245, 365, 170, 110, 'GitHub Repository', ['Source code', 'Version history'], 'delivery'),
      node('pull-request', 460, 365, 145, 110, 'Pull Request', ['Peer review', 'Branch protection'], 'delivery'),
      node('ci-orchestrator', 745, 205, 220, 80, 'CI Orchestrator', ['Reproducible pipeline'], 'delivery'),
      node('frontend-checks', 675, 330, 170, 135, 'Frontend Checks', ['Lint and type-check', 'Component tests', 'Production build'], 'frontend'),
      node('backend-checks', 865, 330, 170, 135, 'Backend Checks', ['Build and test', 'Integration tests', 'Migration validation'], 'backend'),
      node('supply-chain', 770, 510, 170, 125, 'Delivery Checks', ['Docker build', 'Dependency scan', 'Versioned artifact'], 'security'),
      node('merge', 1125, 345, 165, 105, 'Review and Merge', ['Protected branch'], 'success'),
      node('staging', 1350, 215, 220, 120, 'Staging Environment', ['develop branch', 'Mentor verification'], 'frontend'),
      node('release-gate', 1125, 520, 165, 115, 'Release Approval', ['main / release tag', 'Manual production gate'], 'security'),
      node('registry', 1350, 470, 220, 115, 'Container Registry', ['GHCR', 'Immutable image tags'], 'delivery'),
      node('production', 1350, 660, 220, 125, 'Production VPS', ['Docker Compose', 'Controlled migration'], 'backend'),
      node('verification', 1125, 760, 165, 125, 'Release Verification', ['Health checks', 'Smoke tests', 'Rollback on failure'], 'success'),
      note('secrets', 680, 735, 360, 120, 'Security controls', ['Secrets remain in GitHub Environments and VPS configuration.', 'No production credential is stored in source control.']),
    ],
    edges: [
      edge('d1', 'developer', 'repository', 'push'),
      edge('d2', 'repository', 'pull-request', 'open PR'),
      edge('d3', 'pull-request', 'ci-orchestrator', 'trigger', { targetSide: 'left' }),
      edge('d4', 'ci-orchestrator', 'frontend-checks', '', {
        sourceSide: 'left', targetSide: 'top',
        points: [{ x: 790, y: 242 }, { x: 790, y: 310 }, { x: 760, y: 310 }],
      }),
      edge('d5', 'ci-orchestrator', 'backend-checks', '', {
        sourceSide: 'right', targetSide: 'top',
        points: [{ x: 920, y: 242 }, { x: 920, y: 310 }, { x: 950, y: 310 }],
      }),
      edge('d6', 'frontend-checks', 'supply-chain', '', {
        sourceSide: 'bottom', targetSide: 'left',
        points: [{ x: 760, y: 490 }, { x: 805, y: 490 }, { x: 805, y: 547 }],
      }),
      edge('d7', 'backend-checks', 'supply-chain', '', {
        sourceSide: 'bottom', targetSide: 'right',
        points: [{ x: 950, y: 490 }, { x: 905, y: 490 }, { x: 905, y: 547 }],
      }),
      edge('d8', 'supply-chain', 'merge', 'quality gate passed'),
      edge('d9', 'merge', 'staging', 'develop', { sourceSide: 'right', targetSide: 'left' }),
      edge('d10', 'merge', 'release-gate', 'release tag', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('d11', 'release-gate', 'registry', 'publish images'),
      edge('d12', 'registry', 'production', 'pull by version', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('d13', 'production', 'verification', 'verify', {
        sourceSide: 'left', targetSide: 'top', labelX: 1265, labelY: 724,
        points: [{ x: 1330, y: 697 }, { x: 1330, y: 735 }, { x: 1207.5, y: 735 }],
      }),
      edge('d14', 'verification', 'production', 'rollback tag', {
        sourceSide: 'right', targetSide: 'bottom', dashed: true, labelX: 1385, labelY: 821,
        points: [{ x: 1305, y: 797 }, { x: 1305, y: 830 }, { x: 1460, y: 830 }],
      }),
    ],
  },
  {
    id: 'physical-view',
    page: '02 - Physical View',
    file: '02-physical-view-architecture',
    title: 'EHub Target Production Deployment Architecture',
    subtitle: 'Single-VPS production baseline with isolated containers, durable data and external integrations',
    groups: [
      group('internet-boundary', 25, 145, 270, 760, 'Public Internet', 'infra'),
      group('vps-boundary', 325, 115, 990, 820, 'Production VPS — Ubuntu LTS / Docker Compose', 'backend'),
      group('private-network', 360, 205, 915, 665, 'Private Docker Network', 'infra'),
      group('external-boundary', 1340, 115, 315, 820, 'Managed External Services', 'external'),
    ],
    nodes: [
      node('end-users', 65, 330, 190, 145, 'EHub Users', ['Admin', 'Lecturer', 'Mentor and Student'], 'actor'),
      node('dns', 65, 555, 190, 120, 'Domain and DNS', ['ehub.example.com', 'Public resolution'], 'delivery'),
      node('web-gateway', 405, 290, 230, 160, 'Web Gateway', ['Nginx reverse proxy', 'TLS termination', 'React static assets'], 'frontend'),
      node('api-container', 700, 265, 250, 190, 'EHub API Container', ['ASP.NET Core REST API', 'JWT authorization', 'SignalR Hub', 'Health endpoints'], 'backend'),
      node('worker-container', 700, 585, 250, 160, 'EHub Worker Container', ['Outbox processor', 'AI and email jobs', 'Import-session cleanup'], 'backend'),
      node('postgres', 1030, 390, 200, 165, 'PostgreSQL', ['Application database', 'Outbox and audit data', 'Internal port only'], 'data'),
      node('monitoring', 1010, 650, 220, 135, 'Observability', ['Structured logs', 'Health and uptime', 'Resource alerts'], 'infra'),
      node('persistent-volume', 1010, 815, 220, 70, 'Persistent Volume', ['Database and operational data'], 'data'),
      node('google', 1380, 175, 235, 100, 'Google Identity Platform', ['Google OAuth 2.0'], 'external'),
      node('cloudinary', 1380, 315, 235, 110, 'Cloudinary', ['Media and protected documents'], 'external'),
      node('ai-provider', 1380, 475, 235, 110, 'External AI Provider', ['Provider-neutral model API'], 'external'),
      node('email-provider', 1380, 635, 235, 110, 'Email Provider', ['Transactional email delivery'], 'external'),
      node('backup', 1380, 790, 235, 105, 'Off-site Backup Storage', ['Encrypted database backups'], 'external'),
      note('firewall', 405, 505, 230, 105, 'Network boundary', ['Only HTTPS 443 is public.', 'Database ports stay private.']),
    ],
    edges: [
      edge('p1', 'end-users', 'dns', 'HTTPS request', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('p2', 'dns', 'web-gateway', 'HTTPS :443'),
      edge('p3', 'web-gateway', 'api-container', '/api and /hubs'),
      edge('p4', 'api-container', 'postgres', 'EF Core / PostgreSQL'),
      edge('p5', 'worker-container', 'postgres', 'job and outbox access'),
      edge('p6', 'postgres', 'persistent-volume', 'durable storage', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('p7', 'api-container', 'google', 'token validation', { targetSide: 'left' }),
      edge('p8', 'api-container', 'cloudinary', 'signed asset operations', { targetSide: 'left' }),
      edge('p9', 'worker-container', 'ai-provider', 'AI analysis requests', { targetSide: 'left' }),
      edge('p10', 'worker-container', 'email-provider', 'SMTP / HTTPS', { targetSide: 'left' }),
      edge('p11', 'postgres', 'backup', 'encrypted scheduled backup', { targetSide: 'left', dashed: true }),
      edge('p12', 'monitoring', 'api-container', 'health and logs', { targetSide: 'bottom', dashed: true }),
      edge('p13', 'monitoring', 'worker-container', 'job health and logs', { targetSide: 'right', dashed: true }),
    ],
  },
  {
    id: 'logical-overall',
    page: '03 - Overall Logical View',
    file: '03-overall-logical-view-architecture',
    title: 'EHub Overall Logical Architecture',
    subtitle: 'Role-based React SPA connected to a Clean Architecture modular monolith',
    groups: [
      group('actors-boundary', 25, 135, 170, 775, 'System Actors', 'infra'),
      group('client-boundary', 225, 135, 260, 775, 'Presentation Layer', 'frontend'),
      group('backend-boundary', 515, 105, 785, 835, 'EHub Backend — Modular Monolith', 'backend'),
      group('application-boundary', 555, 325, 700, 365, 'Application Layer — Business Use Cases', 'domain'),
      group('external-logical', 1330, 135, 320, 775, 'Data and External Systems', 'external'),
    ],
    nodes: [
      node('admin', 50, 205, 120, 80, 'Admin', ['Governance'], 'actor'),
      node('lecturer', 50, 340, 120, 80, 'Lecturer', ['Assigned classes'], 'actor'),
      node('mentor', 50, 475, 120, 80, 'Mentor', ['Assigned teams'], 'actor'),
      node('student', 50, 610, 120, 80, 'Student', ['Own class/team'], 'actor'),
      node('role-portals', 265, 210, 180, 130, 'Role-based Portals', ['Admin, Lecturer', 'Mentor, Student'], 'frontend'),
      node('web-state', 265, 395, 180, 125, 'Web Application', ['React Router', 'TanStack Query', 'Form validation'], 'frontend'),
      node('web-clients', 265, 580, 180, 125, 'Integration Clients', ['REST/JSON client', 'SignalR client'], 'frontend'),
      node('api-layer', 555, 160, 700, 120, 'API Layer', ['Controllers and contracts  •  Authentication and authorization  •  Middleware  •  SignalR hubs'], 'backend'),
      node('iam-module', 570, 370, 155, 125, 'Identity and Access', ['Auth and roles', 'Account approval'], 'domain'),
      node('academic-module', 735, 370, 155, 125, 'Academic and Class', ['Terms and subjects', 'Class roster'], 'domain'),
      node('team-module', 900, 370, 155, 125, 'Team and Mentor', ['Team proposals', 'Mentor assignments'], 'domain'),
      node('workspace-module', 1065, 370, 155, 125, 'Project Workspace', ['Milestones and tasks', 'Submissions'], 'domain'),
      node('evaluation-module', 570, 525, 155, 125, 'Evaluation and Tracking', ['Rubrics and checkpoints', 'Progress tracking'], 'domain'),
      node('learning-module', 735, 525, 155, 125, 'Mentoring and Data', ['Sessions and workshops', 'Data bank'], 'domain'),
      node('communication-module', 900, 525, 155, 125, 'Communication', ['Chat and presence', 'Notifications'], 'domain'),
      node('ai-module', 1065, 525, 155, 125, 'AI Assistance', ['Proposal analysis', 'Reviewed result'], 'domain'),
      node('domain-layer', 565, 720, 320, 125, 'Domain Layer', ['Entities and value rules', 'Domain events and invariants'], 'domain'),
      node('infrastructure-layer', 925, 720, 320, 125, 'Infrastructure Layer', ['EF Core and PostgreSQL', 'Identity, AI, email and storage adapters'], 'infra'),
      node('logical-db', 1375, 220, 230, 110, 'PostgreSQL', ['Single source of truth'], 'data'),
      node('logical-worker', 1375, 380, 230, 115, 'Background Worker', ['Outbox and long-running jobs'], 'backend'),
      node('logical-google', 1375, 545, 230, 90, 'Google Identity', ['External authentication'], 'external'),
      node('logical-cloud', 1375, 670, 230, 90, 'Cloudinary', ['Media and documents'], 'external'),
      node('logical-external', 1375, 795, 230, 90, 'AI and Email Providers', ['External service adapters'], 'external'),
    ],
    edges: [
      edge('l1', 'admin', 'role-portals', '', { targetSide: 'left' }),
      edge('l2', 'lecturer', 'role-portals', '', { targetSide: 'left' }),
      edge('l3', 'mentor', 'role-portals', '', { targetSide: 'left' }),
      edge('l4', 'student', 'role-portals', '', { targetSide: 'left' }),
      edge('l5', 'role-portals', 'web-state', '', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('l6', 'web-state', 'web-clients', '', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('l7', 'web-clients', 'api-layer', 'HTTPS REST / SignalR'),
      edge('l8', 'api-layer', 'academic-module', 'commands and queries', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('l9', 'application-boundary', 'domain-layer', 'domain rules', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('l10', 'infrastructure-layer', 'domain-layer', 'implements ports', { targetSide: 'right', dashed: true }),
      edge('l11', 'infrastructure-layer', 'logical-db', 'persistence'),
      edge('l12', 'logical-worker', 'logical-db', 'outbox and job state', { sourceSide: 'top', targetSide: 'bottom' }),
      edge('l13', 'infrastructure-layer', 'logical-google', 'OAuth adapter'),
      edge('l14', 'infrastructure-layer', 'logical-cloud', 'storage adapter'),
      edge('l15', 'logical-worker', 'logical-external', 'AI and email jobs'),
    ],
  },
  {
    id: 'ai-logical',
    page: '04 - AI Logical View',
    file: '04-ai-proposal-analysis-architecture',
    title: 'AI-assisted Project Proposal Analysis Architecture',
    subtitle: 'Provider-neutral, asynchronous and human-governed AI workflow',
    groups: [
      group('request-lane', 25, 145, 790, 740, 'Synchronous Request Path', 'frontend'),
      group('worker-lane', 845, 145, 520, 740, 'Asynchronous AI Processing', 'backend'),
      group('ai-external-boundary', 1395, 145, 260, 740, 'External and Delivery', 'external'),
    ],
    nodes: [
      node('ai-user', 65, 300, 150, 115, 'Team / Lecturer', ['Submit or request analysis'], 'actor'),
      node('ai-web', 265, 300, 170, 115, 'EHub Web', ['Proposal form', 'Analysis status'], 'frontend'),
      node('ai-api', 485, 250, 270, 120, 'Proposal Analysis API', ['Authorization', 'Input validation', 'Rate limit'], 'backend'),
      node('context-minimizer', 485, 430, 270, 120, 'Context Preparation', ['Minimize personal data', 'Normalize proposal data', 'Select prompt version'], 'security'),
      node('ai-transaction', 485, 615, 270, 135, 'Transactional Request', ['Save analysis request', 'Create job/outbox record', 'Return Accepted + status ID'], 'data'),
      node('ai-db', 880, 230, 210, 125, 'PostgreSQL', ['Proposal', 'AI job and result', 'Prompt/model metadata'], 'data'),
      node('job-claimer', 1120, 230, 205, 125, 'Job Claimer', ['Lease and idempotency', 'Retry policy'], 'backend'),
      node('ai-orchestrator', 915, 440, 380, 130, 'AI Orchestrator', ['IAiProvider abstraction', 'Timeout and cancellation', 'Provider/model configuration'], 'backend'),
      node('result-validator', 915, 650, 380, 135, 'Structured Result Validator', ['JSON schema validation', 'Business sanity checks', 'Persist ProjectAnalysis'], 'security'),
      node('external-ai', 1430, 260, 190, 120, 'AI Model Provider', ['Structured response API'], 'external'),
      node('notification-delivery', 1430, 520, 190, 125, 'Notification Delivery', ['In-app notification', 'SignalR update', 'Optional email'], 'external'),
      note('human-governance', 1415, 700, 220, 140, 'Human governance', ['AI provides recommendations only.', 'Lecturer/Admin retains decision authority.']),
    ],
    edges: [
      edge('a1', 'ai-user', 'ai-web', '1. submit'),
      edge('a2', 'ai-web', 'ai-api', '2. HTTPS request'),
      edge('a3', 'ai-api', 'context-minimizer', '3. validated input', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('a4', 'context-minimizer', 'ai-transaction', '4. safe context', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('a5', 'ai-transaction', 'ai-db', '5. atomic write'),
      edge('a6', 'ai-db', 'job-claimer', '6. claim pending job'),
      edge('a7', 'job-claimer', 'ai-orchestrator', '7. execute', { sourceSide: 'bottom', targetSide: 'top' }),
      edge('a8', 'ai-orchestrator', 'external-ai', '8. provider request', {
        sourceSide: 'right', targetSide: 'left', points: [{ x: 1380, y: 505 }, { x: 1380, y: 320 }], labelX: 1380, labelY: 410,
      }),
      edge('a9', 'external-ai', 'result-validator', '9. structured result', {
        sourceSide: 'left', targetSide: 'right', points: [{ x: 1335, y: 320 }, { x: 1335, y: 718 }], labelX: 1335, labelY: 615,
      }),
      edge('a10', 'result-validator', 'ai-db', '10. store result', {
        sourceSide: 'left', targetSide: 'bottom', points: [{ x: 850, y: 718 }, { x: 850, y: 385 }, { x: 985, y: 385 }], labelX: 850, labelY: 545,
      }),
      edge('a11', 'result-validator', 'notification-delivery', '11. publish completion', {
        sourceSide: 'right', targetSide: 'left', points: [{ x: 1370, y: 718 }, { x: 1370, y: 583 }], labelX: 1370, labelY: 665,
      }),
      edge('a12', 'notification-delivery', 'ai-web', '12. status update', {
        sourceSide: 'left', targetSide: 'right', dashed: true,
        points: [{ x: 1390, y: 583 }, { x: 1390, y: 855 }, { x: 455, y: 855 }, { x: 455, y: 358 }],
        labelX: 930, labelY: 848,
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
  repository: 'Source repository',
  'pull-request': 'Review gate',
  'ci-orchestrator': '',
  'frontend-checks': 'Lint • Test • Build',
  'backend-checks': 'Build • Test • Migrate',
  'supply-chain': 'Docker • Scan • Artifact',
  merge: '',
  staging: 'Mentor verification',
  'release-gate': '',
  registry: 'GHCR',
  production: 'Docker Compose',
  verification: 'Health • Smoke • Rollback',
  secrets: 'Secrets outside source control',

  'end-users': 'Admin • Lecturer • Mentor • Student',
  dns: 'Public domain',
  'web-gateway': 'Nginx • TLS • React SPA',
  'api-container': '.NET API • JWT • SignalR',
  'worker-container': 'Outbox • Jobs • Cleanup',
  postgres: 'Business data • Audit • Outbox',
  monitoring: 'Logs • Health • Alerts',
  'persistent-volume': 'Persistent data',
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
  'logical-external': 'AI • Email',

  'ai-user': '',
  'ai-web': 'Submit • Track status',
  'ai-api': 'Authorize • Validate',
  'context-minimizer': 'Minimize • Normalize',
  'ai-transaction': 'Request + Job + Outbox',
  'ai-db': 'Request • Job • Result',
  'job-claimer': 'Lease • Retry',
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
  d1: 'Push', d2: 'PR', d3: 'CI', d8: 'Passed', d9: 'Staging', d10: 'Release',
  d11: 'Publish', d12: 'Pull', d13: 'Verify', d14: 'Rollback',
  p1: '', p2: 'HTTPS', p3: 'API / SignalR', p4: 'EF Core', p5: 'Jobs', p6: 'Volume',
  p7: 'OAuth', p8: 'Storage', p9: 'AI', p10: 'Email', p11: 'Backup', p12: '', p13: '',
  l7: 'REST / SignalR', l8: 'Use cases', l9: '', l10: 'Ports', l11: 'Persistence',
  l12: 'Outbox', l13: 'OAuth', l14: 'Storage', l15: 'Jobs',
  a1: '1', a2: '2', a3: '3', a4: '4', a5: '5', a6: '6',
  a7: '7', a8: '8', a9: '9', a10: '10', a11: '11', a12: '12',
  r1: '', r2: 'SignalR', r3: 'Auth', r4: '', r5: 'Persist', r6: '',
  r7: 'Broadcast', r8: 'Atomic', r9: 'Claim', r10: 'Dispatch', r11: '',
  r12: '', r13: '', r14: 'Failed', r15: '', r16: '',
};

const SHORT_TITLES = {
  repository: 'GitHub Repository',
  'ci-orchestrator': 'GitHub Actions',
  'supply-chain': 'Delivery Checks',
  verification: 'Release Verification',
  secrets: 'Security Controls',
  'end-users': 'EHub Users',
  dns: 'Domain / DNS',
  'web-gateway': 'Nginx Gateway',
  'api-container': 'EHub API',
  'worker-container': 'Background Worker',
  google: 'Google OAuth',
  'ai-provider': 'AI Provider',
  'email-provider': 'Email Provider',
  backup: 'Off-site Backup',
  firewall: 'HTTPS Boundary',
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
  'domain-layer': 'Domain Layer',
  'infrastructure-layer': 'Infrastructure Layer',
  'logical-worker': 'Background Worker',
  'logical-google': 'Google OAuth',
  'logical-external': 'AI / Email Providers',
  'ai-user': 'Team / Lecturer',
  'ai-web': 'EHub Web',
  'ai-api': 'Proposal API',
  'context-minimizer': 'Context Preparation',
  'ai-transaction': 'Transactional Request',
  'job-claimer': 'Job Claimer',
  'ai-orchestrator': 'AI Orchestrator',
  'result-validator': 'Result Validator',
  'external-ai': 'AI Model Provider',
  'notification-delivery': 'Notification Delivery',
  'human-governance': 'Human Governance',
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

function sidePoint(item, side = 'auto', toward = null) {
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
  if (selected === 'left') return { x: bounds.x, y: bounds.y + bounds.h / 2 };
  if (selected === 'right') return { x: bounds.x + bounds.w, y: bounds.y + bounds.h / 2 };
  if (selected === 'top') return { x: bounds.x + bounds.w / 2, y: bounds.y };
  return item.kind === 'node'
    ? { x: item.x + item.w / 2, y: item.y + item.h }
    : { x: bounds.x + bounds.w / 2, y: bounds.y + bounds.h };
}

function pathForEdge(e, lookup) {
  const s = lookup.get(e.source);
  const t = lookup.get(e.target);
  if (!s || !t) return null;
  const start = sidePoint(s, e.sourceSide || 'auto', t);
  const end = sidePoint(t, e.targetSide || 'auto', s);
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
  return `<g id="${esc(g.id)}" class="diagram-group">
    <rect x="${g.x}" y="${g.y}" width="${g.w}" height="${g.h}" rx="4" fill="none" stroke="${p.stroke}" stroke-width="1.25" stroke-dasharray="6 5"/>
    <rect x="${g.x + 18}" y="${g.y - 13}" width="${Math.max(170, Math.min(g.w - 36, g.title.length * 9.2 + 34))}" height="28" rx="8" fill="${palette.canvas}"/>
    <text x="${g.x + 29}" y="${g.y + 7}" class="group-title">${esc(g.title)}</text>
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
  const style = [
    'rounded=0', 'whiteSpace=wrap', 'html=1', 'dashed=1', 'dashPattern=6 5',
    'fillColor=none', `strokeColor=${p.stroke}`, 'strokeWidth=1.25',
    `fontColor=${palette.ink}`, 'fontSize=14', 'fontStyle=1', 'align=left', 'verticalAlign=top',
    'spacingTop=8', 'spacingLeft=12',
  ].join(';');
  return `<mxCell id="${esc(g.id)}" value="${esc(g.title)}" style="${style}" vertex="1" parent="1"><mxGeometry x="${g.x}" y="${g.y}" width="${g.w}" height="${g.h}" as="geometry"/></mxCell>`;
}

function drawioEdge(e) {
  const style = [
    'edgeStyle=orthogonalEdgeStyle', 'rounded=1', 'orthogonalLoop=1', 'jettySize=auto',
    'html=1', 'endArrow=block', 'endFill=1', `strokeColor=${palette.line}`, 'strokeWidth=1.5',
    `fontColor=${palette.muted}`, 'fontSize=12', `labelBackgroundColor=${palette.canvas}`,
    e.dashed ? 'dashed=1;dashPattern=7 6' : '',
    e.sourceSide ? `exitX=${e.sourceSide === 'left' ? 0 : e.sourceSide === 'right' ? 1 : 0.5};exitY=${e.sourceSide === 'top' ? 0 : e.sourceSide === 'bottom' ? 1 : 0.5}` : '',
    e.targetSide ? `entryX=${e.targetSide === 'left' ? 0 : e.targetSide === 'right' ? 1 : 0.5};entryY=${e.targetSide === 'top' ? 0 : e.targetSide === 'bottom' ? 1 : 0.5}` : '',
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
