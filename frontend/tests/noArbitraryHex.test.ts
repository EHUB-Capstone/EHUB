import assert from 'node:assert/strict';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';

// Colors come from the design tokens in src/index.css (see AGENTS.md, section 6.1).
// A hex value inside a Tailwind arbitrary value, such as `text-[#034EA2]` or
// `dark:bg-[#0F172A]`, bypasses the tokens and lets the palette drift apart again.
// This check only reads class-like tokens; SVG attributes (fill="#...") and hex
// strings in code are not covered.

const srcDir = new URL('../src/', import.meta.url);
const srcPath = decodeURIComponent(srcDir.pathname).replace(/^\/([A-Za-z]:)/, '$1');

// Add a file here only with a reason a token cannot express.
const ALLOWED_FILES = new Set<string>([]);

const ARBITRARY_HEX = /[A-Za-z0-9:/._-]*-\[[^\]"'`\s]*#[0-9A-Fa-f]{3,8}[^\]"'`\s]*\]/g;

function listSourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return listSourceFiles(path);
    return /\.tsx$/.test(name) ? [path] : [];
  });
}

test('class names use design tokens instead of arbitrary hex colors', () => {
  const offenders = new Map<string, Set<string>>();

  for (const file of listSourceFiles(srcPath)) {
    const relative = file.slice(srcPath.length).replace(/\\/g, '/');
    if (ALLOWED_FILES.has(relative)) continue;
    const source = readFileSync(file, 'utf8');
    for (const match of source.matchAll(ARBITRARY_HEX)) {
      const files = offenders.get(match[0]) ?? new Set<string>();
      files.add(relative);
      offenders.set(match[0], files);
    }
  }

  const report = [...offenders.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([token, files]) => `${token}  (${[...files].slice(0, 2).join(', ')}${files.size > 2 ? ', …' : ''})`);

  assert.deepEqual(
    report,
    [],
    `Use a design token instead of a hex value (primary, secondary, slate, success, warning, danger, info, dark-page, dark-surface).\n` +
      `See AGENTS.md section 6.1. Offending classes:\n${report.join('\n')}`,
  );
});

test('the detector flags arbitrary hex classes and ignores token classes', () => {
  const flagged = ['text-[#034EA2]', 'dark:bg-[#0F172A]', 'border-[#E5E7EB]/80', 'bg-[linear-gradient(135deg,#EA6A12,#D97706)]'];
  const clean = ['text-secondary', 'bg-dark-page', 'border-slate-200/80', 'w-[320px]', 'rounded-[14px]'];
  for (const token of flagged) assert.match(token, new RegExp(ARBITRARY_HEX.source), token);
  for (const token of clean) assert.doesNotMatch(token, new RegExp(ARBITRARY_HEX.source), token);
});
