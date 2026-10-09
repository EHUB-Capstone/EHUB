/* eslint-disable no-console */
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const SVG_DIR = path.join(ROOT, 'svg');
const PNG_DIR = path.join(ROOT, 'png');
const DRAWIO_PATH = path.join(ROOT, 'EHub-Git-Flow.drawio');
const DETAILED_DRAWIO_PATH = path.join(ROOT, 'EHub-Git-Flow-Detailed.drawio');
const SVG_PATH = path.join(SVG_DIR, 'ehub-git-branching-and-release-flow.svg');
const DETAILED_SVG_PATH = path.join(SVG_DIR, 'ehub-git-branching-and-release-flow-detailed.svg');
const PNG_PATH = path.join(PNG_DIR, 'ehub-git-branching-and-release-flow.png');
const DETAILED_PNG_PATH = path.join(PNG_DIR, 'ehub-git-branching-and-release-flow-detailed.png');
const ASSET_DIR = path.resolve(ROOT, '..', 'system-architecture', 'assets');
const BRAND_ICONS = JSON.parse(fs.readFileSync(path.join(ASSET_DIR, 'brand-icons.json'), 'utf8'));
const DISCORD_LOGO = `data:image/png;base64,${fs.readFileSync(path.join(ASSET_DIR, 'logo_discord.png')).toString('base64')}`;

const WIDTH = 1900;
const HEIGHT = 1600;
const SIMPLE_WIDTH = 1900;
const SIMPLE_HEIGHT = 1050;

const color = {
  canvas: '#F8FAFC',
  ink: '#0F172A',
  muted: '#475569',
  line: '#64748B',
  light: '#CBD5E1',
  work: '#22C55E',
  develop: '#F97316',
  release: '#8B5CF6',
  main: '#2563EB',
  hotfix: '#EF4444',
  automation: '#0891B2',
  success: '#10B981',
  white: '#FFFFFF',
};

const brandColor = {
  github: '#181717',
  githubactions: '#2088FF',
};

const genericIcons = {
  monitor: '<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/>',
  review: '<path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>',
  server: '<rect x="3" y="4" width="18" height="6" rx="2"/><rect x="3" y="14" width="18" height="6" rx="2"/><path d="M7 7h.01M7 17h.01"/>',
  tag: '<path d="M20.6 13.6 11 23l-9-9 9.4-9.6H20v8.6Z"/><circle cx="16" cy="8" r="1.5"/>',
  branch: '<circle cx="6" cy="5" r="2"/><circle cx="18" cy="7" r="2"/><circle cx="6" cy="19" r="2"/><path d="M6 7v10M8 7h6a4 4 0 0 1 4 4"/>',
  shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z"/><path d="m9 12 2 2 4-4"/>',
};

const lanes = [
  { id: 'work', x: 250, color: color.work, title: 'Task Work Branches', subtitle: 'feature/* · fix/* · docs/* · chore/*' },
  { id: 'develop', x: 650, color: color.develop, title: 'develop', subtitle: 'Protected integration · Staging' },
  { id: 'release', x: 1040, color: color.release, title: 'release/vX.Y.Z', subtitle: 'Release candidate · UAT' },
  { id: 'main', x: 1410, color: color.main, title: 'main', subtitle: 'Protected production history' },
  { id: 'hotfix', x: 1710, color: color.hotfix, title: 'hotfix/*', subtitle: 'Emergency production fix' },
];

const phaseGroups = [
  { id: 'development', x: 25, y: 110, w: 765, h: 1385, title: 'DEVELOPMENT', stroke: '#0EA5E9' },
  { id: 'staging-release', x: 810, y: 110, w: 430, h: 1385, title: 'STAGING & RELEASE', stroke: color.release },
  { id: 'production', x: 1260, y: 110, w: 615, h: 1385, title: 'PRODUCTION', stroke: color.main },
];

const commitNodes = [
  { id: 'develop-baseline', lane: 'develop', y: 250, label: 'Development baseline', align: 'left', labelDy: 24 },
  { id: 'main-baseline', lane: 'main', y: 250, label: 'Stable baseline', align: 'right', labelDy: 24 },
  { id: 'work-commit-1', lane: 'work', y: 375, label: 'Feature commits', align: 'left' },
  { id: 'work-commit-2', lane: 'work', y: 430, label: 'Local validation', align: 'left' },
  { id: 'develop-integrated', lane: 'develop', y: 490, label: 'Integrated change', align: 'right', labelDy: 24 },
  { id: 'develop-release-candidate', lane: 'develop', y: 750, label: 'Release candidate', align: 'left', labelDy: 24 },
  { id: 'release-rc1', lane: 'release', y: 800, label: 'RC1', align: 'right' },
  { id: 'release-fixes', lane: 'release', y: 855, label: 'Release-only fixes', align: 'right' },
  { id: 'release-approved', lane: 'release', y: 1020, label: 'Final UAT passed', align: 'right', labelDy: 28 },
  { id: 'main-v1', lane: 'main', y: 1020, label: 'Tag v1.0.0', align: 'left', labelDy: 25 },
  { id: 'develop-release-sync', lane: 'develop', y: 1110, label: 'Release fixes synced', align: 'left', labelDy: 24 },
  { id: 'main-stable-v1', lane: 'main', y: 1160, label: 'Production v1.0.0', align: 'left', labelDy: 24 },
  { id: 'hotfix-commit', lane: 'hotfix', y: 1220, label: 'Critical fix', align: 'right' },
  { id: 'main-v101', lane: 'main', y: 1280, label: 'Tag v1.0.1', align: 'left', labelDy: 25 },
  { id: 'develop-hotfix-sync', lane: 'develop', y: 1460, label: 'Hotfix synced', align: 'left', labelDy: -17 },
];

const iconNodes = [
  { id: 'github-actions', x: 450, y: 490, icon: 'githubactions', color: '#2088FF', title: 'GitHub Actions', subtitle: 'Required CI checks' },
  { id: 'discord', x: 450, y: 610, icon: 'discord', color: '#5865F2', title: 'Discord', subtitle: 'Non-blocking status updates' },
  { id: 'staging', x: 835, y: 550, icon: 'monitor', color: color.main, title: 'Staging Deploy', subtitle: 'Automatic from develop', labelSide: 'right' },
  { id: 'acceptance', x: 835, y: 650, icon: 'review', color: color.success, title: 'Acceptance', subtitle: 'Team and mentor verification', labelSide: 'right' },
  { id: 'release-uat', x: 875, y: 900, icon: 'monitor', color: color.release, title: 'Release UAT', subtitle: 'Release candidate verification' },
  { id: 'production-deploy', x: 1555, y: 1020, icon: 'server', color: color.main, title: 'Production Deploy', subtitle: 'Versioned image v1.0.0' },
  { id: 'patch-deploy', x: 1555, y: 1360, icon: 'server', color: color.hotfix, title: 'Patch Deploy', subtitle: 'Versioned image v1.0.1' },
];

const edges = [
  { id: 'e1', points: [[1410, 250], [650, 250]], label: 'Initialize integration branch', labelX: 1030, labelY: 238, color: color.main },
  { id: 'e2', points: [[650, 330], [250, 330]], label: 'Branch from develop', labelX: 450, labelY: 318, color: color.work },
  { id: 'e3a', points: [[250, 490], [427, 490]], label: 'Pull Request + Review', labelX: 337, labelY: 478, color: color.develop },
  { id: 'e3b', points: [[473, 490], [650, 490]], label: 'Checks passed', labelX: 562, labelY: 478, color: color.develop },
  { id: 'e4', points: [[450, 513], [530, 513], [530, 610], [473, 610]], label: 'Status', labelX: 548, labelY: 565, dashed: true, color: color.automation },
  { id: 'e6', points: [[650, 550], [812, 550]], label: 'Auto deploy', labelX: 730, labelY: 538, dashed: true, color: color.main },
  { id: 'e7', points: [[835, 573], [835, 627]], label: 'Verify', labelX: 875, labelY: 606, color: color.success },
  { id: 'e8', points: [[650, 750], [1040, 750]], label: 'Cut release/v1.0.0', labelX: 845, labelY: 738, color: color.release },
  { id: 'e9a', points: [[1040, 855], [1040, 875], [875, 875], [875, 877]], label: 'Deploy RC', labelX: 955, labelY: 863, dashed: true, color: color.release },
  { id: 'e9b', points: [[875, 923], [875, 985], [1040, 985], [1040, 1020]], label: 'Passed', labelX: 955, labelY: 992, dashed: true, color: color.success },
  { id: 'e10', points: [[1040, 1020], [1410, 1020]], label: 'PR · CI · Production approval', labelX: 1205, labelY: 1008, color: color.main },
  { id: 'e11', points: [[1410, 1020], [1532, 1020]], label: 'Deploy tag', labelX: 1470, labelY: 1008, dashed: true, color: color.main },
  { id: 'e12', points: [[1040, 1110], [650, 1110]], label: 'Back-merge release fixes', labelX: 845, labelY: 1098, color: color.develop },
  { id: 'e13', points: [[1410, 1160], [1710, 1160]], label: 'Branch from main', labelX: 1560, labelY: 1148, color: color.hotfix },
  { id: 'e14', points: [[1710, 1280], [1410, 1280]], label: 'PR · CI · Approval', labelX: 1590, labelY: 1268, color: color.hotfix },
  { id: 'e15', points: [[1410, 1280], [1470, 1280], [1470, 1360], [1532, 1360]], label: 'Deploy patch', labelX: 1435, labelY: 1320, dashed: true, color: color.hotfix },
  { id: 'e16', points: [[1410, 1460], [650, 1460]], label: 'Sync production fix back to develop', labelX: 1030, labelY: 1448, color: color.develop },
];

function esc(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function laneById(id) {
  return lanes.find((lane) => lane.id === id);
}

function svgIcon(key, x, y, size, stroke) {
  if (key === 'discord') {
    return `<image href="${DISCORD_LOGO}" x="${x}" y="${y}" width="${size}" height="${size}" preserveAspectRatio="xMidYMid meet"/>`;
  }
  if (BRAND_ICONS[key]) {
    const fill = brandColor[key] || stroke;
    return `<g transform="translate(${x} ${y}) scale(${size / 24})"><path d="${esc(BRAND_ICONS[key].path)}" fill="${fill}"/></g>`;
  }
  return `<g transform="translate(${x} ${y}) scale(${size / 24})" fill="none" stroke="${stroke}" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${genericIcons[key] || genericIcons.branch}</g>`;
}

function edgeLabel(edge) {
  if (!edge.label) return '';
  const width = Math.max(70, edge.label.length * 6.7 + 18);
  return `<g><rect x="${edge.labelX - width / 2}" y="${edge.labelY - 13}" width="${width}" height="22" rx="5" fill="${color.canvas}"/><text x="${edge.labelX}" y="${edge.labelY + 2}" class="edge-label">${esc(edge.label)}</text></g>`;
}

function svgEdge(edge) {
  const pathData = edge.points.map((point, index) => `${index ? 'L' : 'M'} ${point[0]} ${point[1]}`).join(' ');
  return `<g id="${edge.id}"><path d="${pathData}" fill="none" stroke="${edge.color || color.line}" stroke-width="2" ${edge.dashed ? 'stroke-dasharray="8 7"' : ''} marker-end="url(#arrow)"/>${edgeLabel(edge)}</g>`;
}

function svgCommit(node) {
  const lane = laneById(node.lane);
  const labelX = node.align === 'right' ? lane.x + 20 : lane.x - 20;
  const labelY = node.y + (node.labelDy || 5);
  const anchor = node.align === 'right' ? 'start' : 'end';
  return `<g id="${node.id}">
    <circle cx="${lane.x}" cy="${node.y}" r="10" fill="${color.canvas}" stroke="${lane.color}" stroke-width="4"/>
    <circle cx="${lane.x}" cy="${node.y}" r="3.5" fill="${lane.color}"/>
    <text x="${labelX}" y="${labelY}" text-anchor="${anchor}" class="commit-label">${esc(node.label)}</text>
  </g>`;
}

function svgIconNode(node) {
  const size = 46;
  const sideLabel = node.labelSide === 'right';
  const labelX = sideLabel ? node.x + 36 : node.x;
  const titleY = sideLabel ? node.y - 2 : node.y + 42;
  const subtitleY = sideLabel ? node.y + 17 : node.y + 60;
  const anchor = sideLabel ? 'start' : 'middle';
  return `<g id="${node.id}">
    ${svgIcon(node.icon, node.x - size / 2, node.y - size / 2, size, node.color)}
    <text x="${labelX}" y="${titleY}" text-anchor="${anchor}" class="icon-title">${esc(node.title)}</text>
    <text x="${labelX}" y="${subtitleY}" text-anchor="${anchor}" class="icon-subtitle">${esc(node.subtitle)}</text>
  </g>`;
}

function svgBranchState(x, y, branchColor, label) {
  return `<g><circle cx="${x}" cy="${y}" r="9" fill="${branchColor}"/><path d="M ${x - 4} ${y} L ${x - 1} ${y + 3} L ${x + 5} ${y - 4}" fill="none" stroke="#FFFFFF" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/><text x="${x + 18}" y="${y + 5}" class="branch-end">${esc(label)}</text></g>`;
}

function buildDetailedSvg() {
  const groupMarkup = phaseGroups.map((group) => `<g id="group-${group.id}">
    <rect x="${group.x}" y="${group.y}" width="${group.w}" height="${group.h}" rx="4" fill="none" stroke="${group.stroke}" stroke-width="1.5" stroke-dasharray="8 7" opacity="0.78"/>
    <rect x="${group.x + 24}" y="${group.y - 12}" width="${group.title.length * 8 + 28}" height="25" fill="${color.canvas}"/>
    <text x="${group.x + 35}" y="${group.y + 5}" class="phase-title" fill="${group.stroke}">${esc(group.title)}</text>
  </g>`).join('');

  const laneMarkup = lanes.map((lane) => {
    let start = 220;
    let end = 1470;
    if (lane.id === 'work') { start = 330; end = 515; }
    if (lane.id === 'release') { start = 750; end = 1140; }
    if (lane.id === 'hotfix') { start = 1160; end = 1330; }
    return `<g id="lane-${lane.id}">
      <line x1="${lane.x}" y1="${start}" x2="${lane.x}" y2="${end}" stroke="${lane.color}" stroke-width="4" stroke-linecap="round"/>
      <line x1="${lane.x - 44}" y1="160" x2="${lane.x + 44}" y2="160" stroke="${lane.color}" stroke-width="5" stroke-linecap="round"/>
      <text x="${lane.x}" y="188" class="lane-title">${esc(lane.title)}</text>
      <text x="${lane.x}" y="209" class="lane-subtitle">${esc(lane.subtitle)}</text>
    </g>`;
  }).join('');

  const policyItems = [
    ['Protected branches', 'No direct push to develop or main'],
    ['Required gate', 'Pull request · CI · Review'],
    ['Release identity', 'Immutable SemVer tag vX.Y.Z'],
    ['Retention policy', 'Merged branches stay inactive and are never reused'],
  ];
  const policyMarkup = policyItems.map((item, index) => {
    const x = 55 + index * 455;
    return `<g><circle cx="${x}" cy="1545" r="5" fill="${[color.develop, color.automation, color.main, color.hotfix][index]}"/><text x="${x + 14}" y="1540" class="policy-title">${esc(item[0])}</text><text x="${x + 14}" y="1560" class="policy-text">${esc(item[1])}</text></g>`;
  }).join('');

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH}" height="${HEIGHT}" viewBox="0 0 ${WIDTH} ${HEIGHT}" role="img" aria-labelledby="title desc">
  <title id="title">EHub Git Branching and Release Flow</title>
  <desc id="desc">Target Git Flow for task-scoped work branches, protected develop and main branches, staging acceptance, versioned releases, production deployments, retained inactive branch references and emergency hotfixes.</desc>
  <defs>
    <marker id="arrow" markerWidth="10" markerHeight="10" refX="8" refY="3" orient="auto" markerUnits="strokeWidth"><path d="M0,0 L0,6 L9,3 z" fill="${color.line}"/></marker>
  </defs>
  <style>
    text { font-family: Inter, Segoe UI, Arial, sans-serif; fill: ${color.ink}; }
    .page-title { font-size: 30px; font-weight: 700; }
    .page-subtitle { font-size: 15px; fill: ${color.muted}; }
    .phase-title { font-size: 13px; font-weight: 700; letter-spacing: 1.1px; }
    .lane-title { font-size: 16px; font-weight: 700; text-anchor: middle; }
    .lane-subtitle { font-size: 12px; text-anchor: middle; fill: ${color.muted}; }
    .edge-label { font-size: 12px; text-anchor: middle; fill: ${color.muted}; }
    .commit-label { font-size: 12px; font-weight: 600; fill: ${color.ink}; }
    .icon-title { font-size: 12px; font-weight: 700; }
    .icon-subtitle { font-size: 11px; fill: ${color.muted}; }
    .branch-end { font-size: 11px; fill: ${color.muted}; }
    .time-label { font-size: 12px; font-weight: 700; letter-spacing: 1px; fill: ${color.muted}; }
    .policy-title { font-size: 12px; font-weight: 700; }
    .policy-text { font-size: 11px; fill: ${color.muted}; }
  </style>
  <rect width="${WIDTH}" height="${HEIGHT}" fill="${color.canvas}"/>
  <text x="45" y="55" class="page-title">EHub Git Branching and Release Flow</text>
  <text x="45" y="82" class="page-subtitle">Target workflow from task-scoped work branches to versioned, verified and recoverable production releases</text>
  ${groupMarkup}
  <g id="time-axis"><line x1="70" y1="205" x2="70" y2="1465" stroke="${color.line}" stroke-width="1.8" marker-end="url(#arrow)"/><text x="57" y="190" class="time-label" transform="rotate(-90 57 190)">TIME</text></g>
  ${laneMarkup}
  <g id="edges">${edges.map(svgEdge).join('')}</g>
  <g id="commits">${commitNodes.map(svgCommit).join('')}</g>
  <g id="automation-and-environments">${iconNodes.map(svgIconNode).join('')}</g>
  ${svgBranchState(250, 515, color.work, 'Merged · retained (inactive)')}
  ${svgBranchState(1040, 1140, color.release, 'Synchronized · retained (inactive)')}
  ${svgBranchState(1710, 1330, color.hotfix, 'Merged · retained (inactive)')}
  <line x1="35" y1="1515" x2="1865" y2="1515" stroke="${color.light}"/>
  ${policyMarkup}
  </svg>`;
}

function standaloneIconDocument(key, iconColor) {
  if (key === 'discord') {
    return `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><image href="${DISCORD_LOGO}" x="2" y="2" width="60" height="60" preserveAspectRatio="xMidYMid meet"/></svg>`;
  }
  if (BRAND_ICONS[key]) {
    return `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><g transform="translate(2 2) scale(2.5)"><path d="${esc(BRAND_ICONS[key].path)}" fill="${brandColor[key] || iconColor}"/></g></svg>`;
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><g transform="translate(2 2) scale(2.5)" fill="none" stroke="${iconColor}" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${genericIcons[key] || genericIcons.branch}</g></svg>`;
}

function drawioImageStyle(key, iconColor) {
  const encoded = Buffer.from(standaloneIconDocument(key, iconColor), 'utf8').toString('base64');
  return `shape=image;verticalLabelPosition=bottom;verticalAlign=top;imageAspect=0;aspect=fixed;image=data:image/svg+xml;base64,${encoded}`;
}

function drawioText(id, value, x, y, w, h, style = '') {
  return `<mxCell id="${id}" value="${esc(value)}" style="text;html=1;whiteSpace=wrap;rounded=0;fillColor=none;strokeColor=none;${style}" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="${w}" height="${h}" as="geometry"/></mxCell>`;
}

function drawioEdge(edge) {
  const source = edge.points[0];
  const target = edge.points[edge.points.length - 1];
  const waypoints = edge.points.slice(1, -1).map((point) => `<mxPoint x="${point[0]}" y="${point[1]}"/>`).join('');
  const dashed = edge.dashed ? 'dashed=1;dashPattern=8 7;' : '';
  const value = edge.label ? esc(edge.label) : '';
  return `<mxCell id="${edge.id}" value="${value}" style="edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=${edge.color || color.line};strokeWidth=2;fontColor=${color.muted};fontSize=12;labelBackgroundColor=${color.canvas};${dashed}" edge="1" parent="1"><mxGeometry relative="1" as="geometry"><mxPoint x="${source[0]}" y="${source[1]}" as="sourcePoint"/><mxPoint x="${target[0]}" y="${target[1]}" as="targetPoint"/>${waypoints ? `<Array as="points">${waypoints}</Array>` : ''}</mxGeometry></mxCell>`;
}

function buildDetailedDrawio() {
  const cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>'];
  cells.push(drawioText('title', 'EHub Git Branching and Release Flow', 45, 25, 900, 40, `fontSize=24;fontStyle=1;fontColor=${color.ink};align=left;verticalAlign=middle;`));
  cells.push(drawioText('subtitle', 'Target workflow from task-scoped work branches to versioned, verified and recoverable production releases', 45, 67, 1300, 25, `fontSize=13;fontColor=${color.muted};align=left;verticalAlign=middle;`));

  phaseGroups.forEach((group) => {
    cells.push(`<mxCell id="group-${group.id}" value="${esc(group.title)}" style="rounded=0;whiteSpace=wrap;html=1;dashed=1;dashPattern=8 7;fillColor=none;strokeColor=${group.stroke};strokeWidth=1.5;fontColor=${group.stroke};fontSize=13;fontStyle=1;align=left;verticalAlign=top;spacingTop=8;spacingLeft=22" vertex="1" parent="1"><mxGeometry x="${group.x}" y="${group.y}" width="${group.w}" height="${group.h}" as="geometry"/></mxCell>`);
  });

  cells.push(`<mxCell id="time-axis" value="TIME" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;endFill=1;strokeColor=${color.line};strokeWidth=1.8;fontColor=${color.muted};fontSize=12;fontStyle=1;verticalAlign=top;" edge="1" parent="1"><mxGeometry relative="1" as="geometry"><mxPoint x="70" y="205" as="sourcePoint"/><mxPoint x="70" y="1465" as="targetPoint"/></mxGeometry></mxCell>`);

  lanes.forEach((lane) => {
    let start = 220;
    let end = 1470;
    if (lane.id === 'work') { start = 330; end = 515; }
    if (lane.id === 'release') { start = 750; end = 1140; }
    if (lane.id === 'hotfix') { start = 1160; end = 1330; }
    cells.push(`<mxCell id="line-${lane.id}" value="" style="edgeStyle=none;html=1;endArrow=none;strokeColor=${lane.color};strokeWidth=4;" edge="1" parent="1"><mxGeometry relative="1" as="geometry"><mxPoint x="${lane.x}" y="${start}" as="sourcePoint"/><mxPoint x="${lane.x}" y="${end}" as="targetPoint"/></mxGeometry></mxCell>`);
    cells.push(drawioText(`lane-title-${lane.id}`, lane.title, lane.x - 150, 170, 300, 25, `fontSize=15;fontStyle=1;fontColor=${color.ink};align=center;verticalAlign=middle;`));
    cells.push(drawioText(`lane-subtitle-${lane.id}`, lane.subtitle, lane.x - 175, 197, 350, 20, `fontSize=11;fontColor=${color.muted};align=center;verticalAlign=middle;`));
    cells.push(`<mxCell id="lane-accent-${lane.id}" value="" style="rounded=1;fillColor=${lane.color};strokeColor=${lane.color};" vertex="1" parent="1"><mxGeometry x="${lane.x - 44}" y="157" width="88" height="6" as="geometry"/></mxCell>`);
  });

  edges.forEach((edge) => cells.push(drawioEdge(edge)));

  commitNodes.forEach((node) => {
    const lane = laneById(node.lane);
    cells.push(`<mxCell id="${node.id}" value="" style="ellipse;whiteSpace=wrap;html=1;aspect=fixed;fillColor=${color.canvas};strokeColor=${lane.color};strokeWidth=4;" vertex="1" parent="1"><mxGeometry x="${lane.x - 10}" y="${node.y - 10}" width="20" height="20" as="geometry"/></mxCell>`);
    const x = node.align === 'right' ? lane.x + 20 : lane.x - 220;
    const labelY = node.y + (node.labelDy || 0) - 12;
    cells.push(drawioText(`label-${node.id}`, node.label, x, labelY, 200, 24, `fontSize=12;fontStyle=1;fontColor=${color.ink};align=${node.align === 'right' ? 'left' : 'right'};verticalAlign=middle;`));
  });

  iconNodes.forEach((node) => {
    cells.push(`<mxCell id="${node.id}" value="" style="${drawioImageStyle(node.icon, node.color)}" vertex="1" parent="1"><mxGeometry x="${node.x - 23}" y="${node.y - 23}" width="46" height="46" as="geometry"/></mxCell>`);
    const sideLabel = node.labelSide === 'right';
    const labelX = sideLabel ? node.x + 34 : node.x - 105;
    const labelY = sideLabel ? node.y - 15 : node.y + 31;
    const labelWidth = sideLabel ? 250 : 210;
    cells.push(drawioText(`label-${node.id}`, `${node.title}\n${node.subtitle}`, labelX, labelY, labelWidth, 45, `fontSize=12;fontStyle=1;fontColor=${color.ink};align=${sideLabel ? 'left' : 'center'};verticalAlign=top;`));
  });

  const branchEnds = [
    ['work-end', 250, 515, color.work, 'Merged · retained (inactive)'],
    ['release-end', 1040, 1140, color.release, 'Synchronized · retained (inactive)'],
    ['hotfix-end', 1710, 1330, color.hotfix, 'Merged · retained (inactive)'],
  ];
  branchEnds.forEach(([id, x, y, branchColor, label]) => {
    cells.push(drawioText(id, '✓', x - 13, y - 15, 26, 26, `shape=ellipse;fillColor=${branchColor};strokeColor=${branchColor};fontSize=16;fontStyle=1;fontColor=#FFFFFF;align=center;verticalAlign=middle;`));
    cells.push(drawioText(`label-${id}`, label, x + 18, y - 9, 170, 20, `fontSize=11;fontColor=${color.muted};align=left;verticalAlign=middle;`));
  });

  const policyItems = [
    ['Protected branches', 'No direct push to develop or main'],
    ['Required gate', 'Pull request · CI · Review'],
    ['Release identity', 'Immutable SemVer tag vX.Y.Z'],
    ['Retention policy', 'Merged branches stay inactive and are never reused'],
  ];
  policyItems.forEach((item, index) => {
    const x = 55 + index * 455;
    cells.push(drawioText(`policy-${index}`, `${item[0]}\n${item[1]}`, x, 1523, 420, 45, `fontSize=11;fontColor=${color.muted};align=left;verticalAlign=top;`));
  });

  return `<mxfile host="app.diagrams.net" agent="EHub Git Flow Generator" version="26.0.9" type="device" compressed="false"><diagram id="ehub-git-flow" name="EHub Git Flow"><mxGraphModel dx="1900" dy="1600" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1900" pageHeight="1600" math="0" shadow="0"><root>${cells.join('')}</root></mxGraphModel></diagram></mxfile>`;
}

const simplifiedGroups = [
  { id: 'development', x: 25, y: 125, w: 665, h: 780, title: 'DEVELOPMENT', stroke: '#0EA5E9' },
  { id: 'release', x: 710, y: 125, w: 675, h: 780, title: 'STAGING & RELEASE', stroke: color.release },
  { id: 'production', x: 1405, y: 125, w: 470, h: 780, title: 'PRODUCTION', stroke: color.main },
];

const simplifiedNodes = [
  { id: 'task', step: 1, x: 125, y: 325, icon: 'branch', color: color.work, title: 'Task Branches', subtitle: 'feature/* · fix/* · docs/* · chore/*' },
  { id: 'gate', step: 2, x: 350, y: 325, icon: 'githubactions', color: brandColor.githubactions, title: 'PR · CI · Review', subtitle: 'Required quality gate' },
  { id: 'develop', step: 3, x: 600, y: 325, icon: 'branch', color: color.develop, title: 'develop', subtitle: 'Protected integration branch' },
  { id: 'staging', step: 4, x: 820, y: 325, icon: 'monitor', color: color.develop, title: 'Staging', subtitle: 'Integration acceptance' },
  { id: 'release', step: 5, x: 1045, y: 325, icon: 'branch', color: color.release, title: 'release/vX.Y.Z', subtitle: 'Frozen release scope' },
  { id: 'uat', step: 6, x: 1270, y: 325, icon: 'review', color: color.release, title: 'Release UAT', subtitle: 'Verify release candidate' },
  { id: 'main', step: 7, x: 1515, y: 325, icon: 'tag', color: color.main, title: 'main + vX.Y.Z', subtitle: 'Approved immutable release' },
  { id: 'production', step: 8, x: 1760, y: 325, icon: 'server', color: color.main, title: 'Production', subtitle: 'Versioned deployment' },
  { id: 'discord', x: 350, y: 545, icon: 'discord', color: '#5865F2', title: 'Discord', subtitle: 'Non-blocking status' },
  { id: 'sync', x: 600, y: 735, icon: 'branch', color: color.develop, title: 'Sync to develop', subtitle: 'Release and production fixes' },
  { id: 'hotfix', x: 1515, y: 735, icon: 'shield', color: color.hotfix, title: 'hotfix/*', subtitle: 'Emergency fix from main' },
  { id: 'patch', x: 1760, y: 735, icon: 'server', color: color.hotfix, title: 'Production Patch', subtitle: 'Patch release, e.g. v1.0.1' },
];

const simplifiedEdges = [
  { id: 's1', points: [[159, 325], [316, 325]], label: 'Open PR', labelX: 237, labelY: 300, color: color.work },
  { id: 's2', points: [[384, 325], [566, 325]], label: 'Checks and review pass', labelX: 475, labelY: 300, color: color.develop },
  { id: 's3', points: [[634, 325], [786, 325]], label: 'Auto deploy', labelX: 710, labelY: 300, dashed: true, color: color.develop },
  { id: 's4', points: [[854, 325], [1011, 325]], label: 'Accepted · cut from develop', labelX: 932, labelY: 300, color: color.release },
  { id: 's5', points: [[1079, 325], [1236, 325]], label: 'Deploy RC', labelX: 1157, labelY: 300, dashed: true, color: color.release },
  { id: 's6', points: [[1304, 325], [1481, 325]], label: 'Pass · approval', labelX: 1392, labelY: 300, color: color.main },
  { id: 's7', points: [[1549, 325], [1726, 325]], label: 'Deploy tag', labelX: 1637, labelY: 300, dashed: true, color: color.main },
  { id: 's8', points: [[1270, 425], [1270, 500], [1045, 500], [1045, 425]], label: 'Fail · fix release · build next RC', labelX: 1157, labelY: 525, color: color.hotfix },
  { id: 's9', points: [[350, 425], [350, 511]], label: 'Status', labelX: 390, labelY: 470, dashed: true, color: color.automation },
  { id: 's10', points: [[1000, 425], [1000, 720], [634, 720]], label: 'Release fixes', labelX: 815, labelY: 704, color: color.develop },
  { id: 's11', points: [[1515, 425], [1515, 701]], label: 'Production incident', labelX: 1600, labelY: 565, color: color.hotfix },
  { id: 's12', points: [[1549, 735], [1726, 735]], label: 'PR · CI · merge main · tag', labelX: 1637, labelY: 710, color: color.hotfix },
  { id: 's13', points: [[1481, 750], [634, 750]], label: 'Production fix', labelX: 1057, labelY: 777, color: color.develop },
];

function simplifiedNodeSvg(node) {
  const size = node.id === 'discord' ? 50 : 58;
  const titleY = node.y + 58;
  const subtitleY = node.y + 79;
  const step = node.step
    ? `<g><circle cx="${node.x}" cy="${node.y - 67}" r="15" fill="${node.color}"/><text x="${node.x}" y="${node.y - 62}" text-anchor="middle" class="step-number">${node.step}</text></g>`
    : '';
  return `<g id="simple-${node.id}">${step}${svgIcon(node.icon, node.x - size / 2, node.y - size / 2, size, node.color)}<text x="${node.x}" y="${titleY}" text-anchor="middle" class="simple-title">${esc(node.title)}</text><text x="${node.x}" y="${subtitleY}" text-anchor="middle" class="simple-subtitle">${esc(node.subtitle)}</text></g>`;
}

function buildSimplifiedSvg() {
  const groupMarkup = simplifiedGroups.map((group) => `<g><rect x="${group.x}" y="${group.y}" width="${group.w}" height="${group.h}" rx="5" fill="none" stroke="${group.stroke}" stroke-width="1.5" stroke-dasharray="8 7" opacity="0.75"/><rect x="${group.x + 24}" y="${group.y - 12}" width="${group.title.length * 8 + 28}" height="25" fill="${color.canvas}"/><text x="${group.x + 35}" y="${group.y + 5}" class="phase-title" fill="${group.stroke}">${esc(group.title)}</text></g>`).join('');
  const policy = 'Protected develop/main  •  PR, CI and review required  •  Immutable SemVer tags  •  Release and production fixes sync to develop  •  Merged branches remain inactive';
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${SIMPLE_WIDTH}" height="${SIMPLE_HEIGHT}" viewBox="0 0 ${SIMPLE_WIDTH} ${SIMPLE_HEIGHT}" role="img" aria-labelledby="title desc"><title id="title">EHub Git Branching and Release Flow</title><desc id="desc">Simplified report view of EHub task branches, integration, staging, release UAT, versioned production deployment and hotfix synchronization.</desc><defs><marker id="arrow" markerWidth="10" markerHeight="10" refX="8" refY="3" orient="auto" markerUnits="strokeWidth"><path d="M0,0 L0,6 L9,3 z" fill="${color.line}"/></marker></defs><style>text{font-family:Inter,Segoe UI,Arial,sans-serif;fill:${color.ink}}.page-title{font-size:30px;font-weight:700}.page-subtitle{font-size:15px;fill:${color.muted}}.phase-title{font-size:13px;font-weight:700;letter-spacing:1.1px}.simple-title{font-size:14px;font-weight:700}.simple-subtitle{font-size:11px;fill:${color.muted}}.edge-label{font-size:11px;text-anchor:middle;fill:${color.muted}}.step-number{font-size:13px;font-weight:700;fill:#fff}.policy{font-size:12px;fill:${color.muted};text-anchor:middle}.notation{font-size:11px;fill:${color.muted};text-anchor:middle}</style><rect width="${SIMPLE_WIDTH}" height="${SIMPLE_HEIGHT}" fill="${color.canvas}"/><text x="45" y="55" class="page-title">EHub Git Branching and Release Flow</text><text x="45" y="82" class="page-subtitle">Simplified delivery lifecycle from task branches to verified, versioned production releases</text><text x="1850" y="80" text-anchor="end" class="page-subtitle">Initial setup: main establishes the develop baseline</text>${groupMarkup}<g id="simple-edges">${simplifiedEdges.map(svgEdge).join('')}</g><g id="simple-nodes">${simplifiedNodes.map(simplifiedNodeSvg).join('')}</g><line x1="45" y1="930" x2="1855" y2="930" stroke="${color.light}"/><text x="950" y="968" class="policy">${esc(policy)}</text><text x="950" y="1002" class="notation">Solid arrows: Git, review and approval flow  ·  Dashed arrows: automated deployment or notification</text></svg>`;
}

function buildSimplifiedDrawio() {
  const cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>'];
  cells.push(drawioText('title', 'EHub Git Branching and Release Flow', 45, 25, 900, 40, `fontSize=24;fontStyle=1;fontColor=${color.ink};align=left;verticalAlign=middle;`));
  cells.push(drawioText('subtitle', 'Simplified delivery lifecycle from task branches to verified, versioned production releases', 45, 67, 1100, 25, `fontSize=13;fontColor=${color.muted};align=left;verticalAlign=middle;`));
  cells.push(drawioText('initial-note', 'Initial setup: main establishes the develop baseline', 1320, 67, 530, 25, `fontSize=12;fontColor=${color.muted};align=right;verticalAlign=middle;`));
  simplifiedGroups.forEach((group) => cells.push(`<mxCell id="simple-group-${group.id}" value="${esc(group.title)}" style="rounded=0;whiteSpace=wrap;html=1;dashed=1;dashPattern=8 7;fillColor=none;strokeColor=${group.stroke};strokeWidth=1.5;fontColor=${group.stroke};fontSize=13;fontStyle=1;align=left;verticalAlign=top;spacingTop=8;spacingLeft=22" vertex="1" parent="1"><mxGeometry x="${group.x}" y="${group.y}" width="${group.w}" height="${group.h}" as="geometry"/></mxCell>`));
  simplifiedEdges.forEach((edge) => {
    cells.push(drawioEdge({ ...edge, label: '' }));
    if (edge.label) {
      const width = Math.max(90, edge.label.length * 6.7 + 18);
      cells.push(drawioText(`simple-label-${edge.id}`, edge.label, edge.labelX - width / 2, edge.labelY - 13, width, 22, `fontSize=11;fontColor=${color.muted};align=center;verticalAlign=middle;`));
    }
  });
  simplifiedNodes.forEach((node) => {
    const size = node.id === 'discord' ? 50 : 58;
    cells.push(`<mxCell id="simple-${node.id}" value="" style="${drawioImageStyle(node.icon, node.color)}" vertex="1" parent="1"><mxGeometry x="${node.x - size / 2}" y="${node.y - size / 2}" width="${size}" height="${size}" as="geometry"/></mxCell>`);
    if (node.step) cells.push(drawioText(`step-${node.id}`, String(node.step), node.x - 15, node.y - 82, 30, 30, `shape=ellipse;fillColor=${node.color};strokeColor=${node.color};fontSize=13;fontStyle=1;fontColor=#FFFFFF;align=center;verticalAlign=middle;`));
    cells.push(drawioText(`simple-label-${node.id}`, `${node.title}\n${node.subtitle}`, node.x - 110, node.y + 45, 220, 46, `fontSize=12;fontStyle=1;fontColor=${color.ink};align=center;verticalAlign=top;`));
  });
  cells.push(`<mxCell id="policy-line" value="" style="edgeStyle=none;html=1;endArrow=none;strokeColor=${color.light};strokeWidth=1;" edge="1" parent="1"><mxGeometry relative="1" as="geometry"><mxPoint x="45" y="930" as="sourcePoint"/><mxPoint x="1855" y="930" as="targetPoint"/></mxGeometry></mxCell>`);
  cells.push(drawioText('policy', 'Protected develop/main  •  PR, CI and review required  •  Immutable SemVer tags  •  Release and production fixes sync to develop  •  Merged branches remain inactive', 150, 950, 1600, 35, `fontSize=12;fontColor=${color.muted};align=center;verticalAlign=middle;`));
  cells.push(drawioText('notation', 'Solid arrows: Git, review and approval flow  ·  Dashed arrows: automated deployment or notification', 350, 988, 1200, 25, `fontSize=11;fontColor=${color.muted};align=center;verticalAlign=middle;`));
  return `<mxfile host="app.diagrams.net" agent="EHub Git Flow Generator" version="26.0.9" type="device" compressed="false"><diagram id="ehub-git-flow-simple" name="EHub Git Flow"><mxGraphModel dx="1900" dy="1050" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1900" pageHeight="1050" math="0" shadow="0"><root>${cells.join('')}</root></mxGraphModel></diagram></mxfile>`;
}

async function main() {
  fs.mkdirSync(SVG_DIR, { recursive: true });
  fs.mkdirSync(PNG_DIR, { recursive: true });
  const simplifiedSvg = buildSimplifiedSvg();
  const detailedSvg = buildDetailedSvg();
  fs.writeFileSync(SVG_PATH, simplifiedSvg, 'utf8');
  fs.writeFileSync(DETAILED_SVG_PATH, detailedSvg, 'utf8');
  fs.writeFileSync(DRAWIO_PATH, buildSimplifiedDrawio(), 'utf8');
  fs.writeFileSync(DETAILED_DRAWIO_PATH, buildDetailedDrawio(), 'utf8');

  let sharp;
  try {
    sharp = require('sharp');
  } catch (error) {
    throw new Error('The sharp package is required to render the PNG output.');
  }
  await sharp(Buffer.from(simplifiedSvg)).png().toFile(PNG_PATH);
  await sharp(Buffer.from(detailedSvg)).png().toFile(DETAILED_PNG_PATH);
  console.log(`Generated EHub Git Flow diagram in ${ROOT}`);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
