import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';

// Every language must expose the same namespaces and keys as the reference language (en),
// with non-empty values and the same {{placeholders}}, so a missing translation is caught
// here instead of showing a raw key in the UI.

const localesDir = decodeURIComponent(new URL('../src/i18n/locales/', import.meta.url).pathname)
  .replace(/^\/([A-Za-z]:)/, '$1');
const REFERENCE = 'en';

type Json = { [key: string]: Json | string };

function readNamespace(language: string, file: string): Json {
  return JSON.parse(readFileSync(join(localesDir, language, file), 'utf8')) as Json;
}

function flatten(node: Json, prefix = ''): Map<string, string> {
  const result = new Map<string, string>();
  for (const [key, value] of Object.entries(node)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') result.set(path, value);
    else for (const [childPath, childValue] of flatten(value, path)) result.set(childPath, childValue);
  }
  return result;
}

const placeholders = (value: string): string[] =>
  [...value.matchAll(/\{\{\s*([\w.]+)\s*(?:,[^}]*)?\}\}/g)].map((match) => match[1]).sort();

const languages = readdirSync(localesDir).filter((name) => name !== REFERENCE);
const namespaces = readdirSync(join(localesDir, REFERENCE)).filter((name) => name.endsWith('.json'));

test('at least one non-reference language is present', () => {
  assert.ok(languages.length > 0);
  assert.ok(namespaces.length > 0);
});

for (const language of languages) {
  test(`${language} has the same namespaces as ${REFERENCE}`, () => {
    const files = readdirSync(join(localesDir, language)).filter((name) => name.endsWith('.json')).sort();
    assert.deepEqual(files, [...namespaces].sort());
  });

  for (const file of namespaces) {
    test(`${language}/${file} matches ${REFERENCE} keys, placeholders and has no empty values`, () => {
      const reference = flatten(readNamespace(REFERENCE, file));
      const target = flatten(readNamespace(language, file));

      const missing = [...reference.keys()].filter((key) => !target.has(key));
      const extra = [...target.keys()].filter((key) => !reference.has(key));
      assert.deepEqual(missing, [], `Missing in ${language}/${file}`);
      assert.deepEqual(extra, [], `Unknown in ${language}/${file}`);

      for (const [key, value] of target) {
        assert.notEqual(value.trim(), '', `${language}/${file}: "${key}" is empty`);
        assert.deepEqual(
          placeholders(value),
          placeholders(reference.get(key) ?? ''),
          `${language}/${file}: "${key}" placeholders differ from ${REFERENCE}`,
        );
      }
    });
  }
}

for (const file of namespaces) {
  test(`${REFERENCE}/${file} has no empty values`, () => {
    for (const [key, value] of flatten(readNamespace(REFERENCE, file))) {
      assert.notEqual(value.trim(), '', `${REFERENCE}/${file}: "${key}" is empty`);
    }
  });
}
