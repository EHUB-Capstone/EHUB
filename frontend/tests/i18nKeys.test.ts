import assert from 'node:assert/strict';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';

// Every literal translation key used in source (`t('ns:section.key')`) must exist in the
// reference (en) resources, so a typo or forgotten key fails here instead of rendering a raw key.
// Keys built dynamically (template literals) are not covered.

const srcDir = decodeURIComponent(new URL('../src/', import.meta.url).pathname).replace(/^\/([A-Za-z]:)/, '$1');
const localesDir = join(srcDir, 'i18n', 'locales', 'en');

type Json = { [key: string]: Json | string };

const resources: Record<string, Json> = {};
for (const file of readdirSync(localesDir).filter((name) => name.endsWith('.json'))) {
  resources[file.replace(/\.json$/, '')] = JSON.parse(readFileSync(join(localesDir, file), 'utf8')) as Json;
}

const PLURAL_SUFFIXES = ['', '_zero', '_one', '_two', '_few', '_many', '_other'];

function hasKey(namespace: string, key: string): boolean {
  const root = resources[namespace];
  if (!root) return false;
  return PLURAL_SUFFIXES.some((suffix) => {
    let node: Json | string | undefined = root;
    for (const part of `${key}${suffix}`.split('.')) {
      if (typeof node !== 'object' || node === null || !(part in node)) return false;
      node = node[part];
    }
    return typeof node === 'string';
  });
}

function listSourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return name === 'locales' ? [] : listSourceFiles(path);
    return /\.(ts|tsx)$/.test(name) ? [path] : [];
  });
}

function declaredNamespaces(source: string): string[] {
  const found = new Set<string>();
  for (const call of source.matchAll(/useTranslation\(\s*(\[[^\]]*\]|'[^']*'|"[^"]*")?\s*\)/g)) {
    const args = call[1] ?? '';
    for (const ns of args.matchAll(/['"]([\w-]+)['"]/g)) found.add(ns[1]);
  }
  return found.size > 0 ? [...found] : ['common'];
}

test('every literal translation key used in source exists in the en resources', () => {
  const missing: string[] = [];

  for (const file of listSourceFiles(srcDir)) {
    const source = readFileSync(file, 'utf8');
    if (!/useTranslation|i18n/.test(source)) continue;
    const namespaces = declaredNamespaces(source);

    for (const match of source.matchAll(/\bt\(\s*(['"])([^'"`$\\]+)\1/g)) {
      const raw = match[2];
      const colon = raw.indexOf(':');
      const candidates = colon > 0 ? [raw.slice(0, colon)] : namespaces;
      const key = colon > 0 ? raw.slice(colon + 1) : raw;
      if (!candidates.some((ns) => hasKey(ns, key))) {
        missing.push(`${file.slice(srcDir.length).replace(/\\/g, '/')}: ${raw}`);
      }
    }
  }

  assert.deepEqual(missing, [], `Translation keys missing from en resources:\n${missing.join('\n')}`);
});
