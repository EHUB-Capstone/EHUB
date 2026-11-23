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

// A dynamic key such as t(`status.${value}`) cannot be checked in full, but its static prefix must exist
// in one of the file's namespaces; this catches keys filed under a namespace the component never loads.
function hasPrefix(namespace: string, prefix: string): boolean {
  let node: Json | string | undefined = resources[namespace];
  if (!node) return false;
  for (const part of prefix.split('.').filter(Boolean)) {
    if (typeof node !== 'object' || node === null) return false;
    if (part in node) node = node[part];
    else return Object.keys(node).some((key) => key.startsWith(part));
  }
  return true;
}

test('dynamic translation keys start with a prefix that exists in the file namespace', () => {
  const missing: string[] = [];
  for (const file of listSourceFiles(srcDir)) {
    const source = readFileSync(file, 'utf8');
    if (!/useTranslation|i18n/.test(source)) continue;
    const namespaces = declaredNamespaces(source);
    for (const match of source.matchAll(/\b(?:t|tr|i18n\.t)\(\s*`([^`$]*)\$\{/g)) {
      const raw = match[1];
      const colon = raw.indexOf(':');
      const candidates = colon > 0 ? [raw.slice(0, colon)] : namespaces;
      const prefix = colon > 0 ? raw.slice(colon + 1) : raw;
      if (!candidates.some((ns) => hasPrefix(ns, prefix))) {
        missing.push(`${file.slice(srcDir.length).replaceAll('\\', '/')}: ${raw}`);
      }
    }
  }
  assert.deepEqual(missing, [], `Dynamic keys with no matching prefix:\n${missing.join('\n')}`);
});
