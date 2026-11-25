import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  DEFAULT_LANGUAGE,
  LANGUAGE_STORAGE_KEY,
  SUPPORTED_LANGUAGES,
  getLanguageOption,
  normalizeLanguage,
  readStoredLanguage,
  writeStoredLanguage,
} from '../src/i18n/config.ts';
import i18n from '../src/i18n/index.ts';
import { lazyNamespaces, resources } from '../src/i18n/resources.ts';
import {
  formatDate,
  formatDateTime,
  formatNumber,
  formatPercent,
  formatRelativeTime,
} from '../src/i18n/format.ts';

function withStorage<T>(initial: Record<string, string>, run: (store: Map<string, string>) => T): T {
  const store = new Map(Object.entries(initial));
  const previous = Object.getOwnPropertyDescriptor(globalThis, 'localStorage');
  Object.defineProperty(globalThis, 'localStorage', {
    configurable: true,
    value: {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => void store.set(key, value),
    },
  });
  try {
    return run(store);
  } finally {
    if (previous) Object.defineProperty(globalThis, 'localStorage', previous);
    else delete (globalThis as { localStorage?: unknown }).localStorage;
  }
}

test('English is the default and the supported set is en + vi', () => {
  assert.equal(DEFAULT_LANGUAGE, 'en');
  assert.deepEqual(SUPPORTED_LANGUAGES.map((language) => language.code), ['en', 'vi']);
});

test('normalizeLanguage maps region variants and rejects unknown values', () => {
  assert.equal(normalizeLanguage('vi'), 'vi');
  assert.equal(normalizeLanguage('vi-VN'), 'vi');
  assert.equal(normalizeLanguage('EN-us'), 'en');
  assert.equal(normalizeLanguage('fr'), 'en');
  assert.equal(normalizeLanguage(''), 'en');
  assert.equal(normalizeLanguage(null), 'en');
  assert.equal(getLanguageOption('vi').short, 'VI');
});

test('readStoredLanguage restores the saved choice and falls back to en', () => {
  withStorage({}, () => assert.equal(readStoredLanguage(), 'en'));
  withStorage({ [LANGUAGE_STORAGE_KEY]: 'vi' }, () => assert.equal(readStoredLanguage(), 'vi'));
  withStorage({ [LANGUAGE_STORAGE_KEY]: 'klingon' }, () => assert.equal(readStoredLanguage(), 'en'));
});

test('writeStoredLanguage persists the choice and tolerates missing storage', () => {
  withStorage({}, (store) => {
    writeStoredLanguage('vi');
    assert.equal(store.get(LANGUAGE_STORAGE_KEY), 'vi');
  });
  assert.doesNotThrow(() => writeStoredLanguage('vi'));
  assert.equal(readStoredLanguage(), 'en');
});

test('i18n switches language immediately and keeps both bundles in sync', async () => {
  await i18n.changeLanguage('en');
  assert.equal(i18n.t('language.switch'), resources.en.common.language.switch);
  await i18n.changeLanguage('vi');
  assert.equal(i18n.t('language.switch'), resources.vi.common.language.switch);
  assert.notEqual(resources.en.common.language.switch, resources.vi.common.language.switch);
  assert.equal(i18n.t('language.current', { language: 'Tiếng Việt' }), 'Ngôn ngữ hiện tại: Tiếng Việt');
  await i18n.changeLanguage('en');
});

test('formatters follow the active language and fall back for invalid input', async () => {
  const date = new Date(Date.UTC(2026, 2, 5, 12, 0, 0));
  await i18n.changeLanguage('en');
  assert.equal(formatDate(date, { timeZone: 'UTC', day: '2-digit', month: '2-digit', year: 'numeric' }), '05/03/2026');
  assert.equal(formatNumber(1234567.5), '1,234,567.5');
  assert.equal(formatPercent(42.5, 1), '42.5%');
  await i18n.changeLanguage('vi');
  assert.equal(formatDate(date, { timeZone: 'UTC', day: '2-digit', month: '2-digit', year: 'numeric' }), '05/03/2026');
  assert.equal(formatNumber(1234567.5), '1.234.567,5');
  assert.equal(formatPercent(42.5, 1), '42,5%');
  assert.match(formatDateTime(date, { timeZone: 'UTC', hour: '2-digit', minute: '2-digit' }), /12:00/);
  assert.equal(formatDate('not a date', undefined, '—'), '—');
  assert.equal(formatDate(null), '');
  assert.equal(formatNumber(Number.NaN, undefined, '-'), '-');
  assert.ok(formatRelativeTime(new Date(Date.now() - 3 * 24 * 60 * 60 * 1000)).length > 0);
  await i18n.changeLanguage('en');
});

test('feature namespaces can be added on demand for both languages', async () => {
  // The app loads these through dynamic imports (see resources.ts); Node cannot import JSON without an
  // attribute, so the same files are read from disk and registered the way the backend does.
  const read = (language: string) => JSON.parse(readFileSync(new URL(`../src/i18n/locales/${language}/workspace.json`, import.meta.url), 'utf8'));
  i18n.addResourceBundle('vi', 'workspace', read('vi'), true, true);
  i18n.addResourceBundle('en', 'workspace', read('en'), true, true);
  await i18n.changeLanguage('vi');
  assert.equal(i18n.t('workspace:taskStatus.TODO'), 'Cần làm');
  await i18n.changeLanguage('en');
  assert.equal(i18n.t('workspace:taskStatus.TODO'), 'To Do');
});

test('every lazy namespace resolves a loader for each supported language', () => {
  for (const [namespace, loaders] of Object.entries(lazyNamespaces)) {
    for (const { code } of SUPPORTED_LANGUAGES) {
      assert.equal(typeof (loaders as Record<string, unknown>)[code], 'function', `${namespace}/${code}`);
    }
  }
});

test('showcase language errors name every matched group in Vietnamese and English', async () => {
  const { localizeApiMessage } = await import('../src/i18n/messages.ts');
  await i18n.changeLanguage('vi');
  assert.equal(localizeApiMessage('SHOWCASE_LANGUAGE_BLOCKED_PROFANITY', 'server', 'fallback'), 'Không được sử dụng ngôn từ tục tĩu.');
  const mixed = localizeApiMessage('SHOWCASE_LANGUAGE_BLOCKED_PROFANITY_THREAT', 'server', 'fallback');
  assert.match(mixed, /tục tĩu/);
  assert.match(mixed, /đe dọa/);
  assert.doesNotMatch(mixed, /chủng tộc/);
  await i18n.changeLanguage('en');
  assert.equal(localizeApiMessage('SHOWCASE_LANGUAGE_BLOCKED_RACISM', 'server', 'fallback'), 'Racist and racially derogatory language is not allowed.');
  assert.equal(localizeApiMessage('UNRELATED_ERROR', 'specific server message', 'fallback'), 'specific server message');
});
