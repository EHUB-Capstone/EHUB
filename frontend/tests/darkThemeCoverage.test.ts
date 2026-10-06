import assert from 'node:assert/strict';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';

// Dark mode is implemented as unlayered overrides in index.css. A light tint
// without an override stays light in dark mode while its text turns light, which
// makes it unreadable. This test fails when such a class is used but not covered.

const srcDir = new URL('../src/', import.meta.url);
const srcPath = decodeURIComponent(srcDir.pathname).replace(/^\/([A-Za-z]:)/, '$1');

const COLORS = 'amber|orange|yellow|green|emerald|red|rose|pink|purple|violet|indigo|blue|sky|cyan|teal|lime|primary|secondary|success|warning|danger';
const STATE_VARIANTS = new Set(['hover', 'focus', 'active', 'disabled', 'group-hover', 'focus-within']);

const lightTintPatterns = [
  new RegExp('^(?:bg|border|divide|ring)-(?:slate|gray)-(?:50|100|200|300)(?:/\\d+)?$'),
  /^bg-white(?:\/(?:[5-9]\d))?$/,
  new RegExp(`^(?:bg|ring)-(?:${COLORS})-(?:50|100|200)(?:/\\d+)?$`),
  new RegExp(`^(?:border|divide)-(?:${COLORS})-(?:50|100|200|300)(?:/\\d+)?$`),
  new RegExp(`^text-(?:${COLORS})-(?:700|800|900)$`),
  new RegExp(`^text-(?:amber|orange|yellow|green|emerald|red|rose|pink|purple|violet|indigo|blue|sky|cyan|teal|lime|secondary)-600$`),
  /^text-(?:slate|gray)-(?:600|700|800|900|950)$/,
  new RegExp(`^(?:from|via|to)-(?:slate|gray|${COLORS})-(?:50|100|200)(?:/\\d+)?$`),
  /^(?:from|via|to)-white(?:\/(?:[5-9]\d))?$/,
  /^border-(?:success|warning|danger)-light$/,
];

function listSourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return listSourceFiles(path);
    return /\.tsx$/.test(name) ? [path] : [];
  });
}

function coveredClasses(css: string): Set<string> {
  const covered = new Set<string>();
  for (const match of css.matchAll(/\.((?:\\.|[A-Za-z0-9_-])+)/g)) {
    covered.add(match[1].replace(/\\(.)/g, '$1'));
  }
  return covered;
}

function isLightTint(token: string): boolean {
  const separator = token.lastIndexOf(':');
  const variants = separator === -1 ? [] : token.slice(0, separator).split(':');
  const base = separator === -1 ? token : token.slice(separator + 1);
  if (!variants.every((variant) => STATE_VARIANTS.has(variant))) return false;
  return lightTintPatterns.some((pattern) => pattern.test(base));
}

test('every light tint class used in components has a dark override', () => {
  const css = readFileSync(join(srcPath, 'index.css'), 'utf8');
  const covered = coveredClasses(css);
  const missing = new Map<string, Set<string>>();

  for (const file of listSourceFiles(srcPath)) {
    const source = readFileSync(file, 'utf8');
    for (const match of source.matchAll(/[A-Za-z0-9:/._-]+/g)) {
      const token = match[0];
      if (!isLightTint(token) || covered.has(token)) continue;
      const files = missing.get(token) ?? new Set<string>();
      files.add(file.slice(srcPath.length));
      missing.set(token, files);
    }
  }

  const report = [...missing.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([token, files]) => `${token}  (${[...files].slice(0, 2).join(', ')}${files.size > 2 ? ', …' : ''})`);
  assert.deepEqual(report, [], `Add a .dark override in src/index.css for:\n${report.join('\n')}`);
});

test('bare borders get a neutral default color for Tailwind v4', () => {
  const css = readFileSync(join(srcPath, 'index.css'), 'utf8');
  assert.match(css, /@layer base\s*\{[^}]*border-color:\s*var\(--color-slate-200\)/s);
});
