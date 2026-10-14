import assert from 'node:assert/strict';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import { cn } from '../src/utils/cn.ts';

const srcDir = new URL('../src/', import.meta.url);
const srcPath = decodeURIComponent(srcDir.pathname).replace(/^\/([A-Za-z]:)/, '$1');
const css = readFileSync(join(srcPath, 'index.css'), 'utf8');

function listSourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return listSourceFiles(path);
    return /\.tsx$/.test(name) ? [path] : [];
  });
}

function themeValue(name: string): string {
  const match = css.match(new RegExp(`${name}:\\s*(#[0-9A-Fa-f]{6})`));
  assert.ok(match, `${name} is not defined in index.css`);
  return match[1];
}

function luminance(hex: string): number {
  const channels = [1, 3, 5].map((i) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
}

function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

test('every custom text-size token used in components is defined', () => {
  const custom = ['display', 'heading', 'subheading', 'body-lg', 'body', 'body-sm', 'caption', 'overline'];
  const used = new Set<string>();
  for (const file of listSourceFiles(srcPath)) {
    const source = readFileSync(file, 'utf8');
    for (const match of source.matchAll(/(?<![\w-])text-(display|heading|subheading|body-lg|body-sm|body|caption|overline)(?![\w-])/g)) {
      used.add(match[1]);
    }
  }
  const missing = [...used].filter((name) => !new RegExp(`--text-${name}:`).test(css));
  assert.deepEqual(missing, [], `Define --text-<name> in index.css for: ${missing.join(', ')}`);
  assert.ok(custom.every((name) => new RegExp(`--text-${name}:`).test(css)));
});

test('text-bearing brand and status colors meet WCAG AA (4.5:1)', () => {
  const pairs: Array<[string, string, string]> = [
    ['--color-primary-700', '#FFFFFF', 'white on pressed/dark orange'],
    ['--color-primary-800', '#FFF1E8', 'brand badge text'],
    ['--color-secondary', '#FFFFFF', 'white on navy'],
    ['--color-success-dark', '#DCFCE7', 'success badge'],
    ['--color-warning-dark', '#FEF3C7', 'warning badge'],
    ['--color-danger-dark', '#FEE2E2', 'danger badge'],
    ['--color-info-dark', '#DBEAFE', 'info badge'],
  ];
  for (const [token, other, label] of pairs) {
    const ratio = contrast(themeValue(token), other);
    assert.ok(ratio >= 4.5, `${label}: ${ratio.toFixed(2)}:1 is below 4.5:1`);
  }
});

test('cn keeps a custom text size next to a text color', () => {
  assert.equal(cn('text-display', 'text-white'), 'text-display text-white');
  assert.equal(cn('text-overline uppercase', 'text-slate-600'), 'text-overline uppercase text-slate-600');
  assert.equal(cn('text-body', 'text-caption'), 'text-caption');
});
