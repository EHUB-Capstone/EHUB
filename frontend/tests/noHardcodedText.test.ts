import assert from 'node:assert/strict';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import ts from 'typescript';

// User-facing text must come from the translation resources (src/i18n/locales), not from literals in
// components. This scans .tsx files for readable text in JSX, text-bearing attributes and toast calls.
// Add an entry to ALLOWED only for text that must not be translated (brand names, symbols), with a reason.

const srcDir = decodeURIComponent(new URL('../src/', import.meta.url).pathname).replace(/^\/([A-Za-z]:)/, '$1');

const TEXT_ATTRIBUTES = new Set(['placeholder', 'title', 'aria-label', 'alt', 'label']);
const ALLOWED = new Set<string>([
  'E', // "E" + "HUB" logo wordmark, split into coloured spans
  'HUB',
  'EHUB',
  'EHub',
  'E-HUB',
  'Hubby', // mascot name
  'Google',
  'Gemini',
  'Zalo',
  'LinkedIn',
  'GitHub',
  'PDF',
  'Excel',
  'Kanban',
  'ProtectedRoute', // unused placeholder component with no real UI
]);

function listSourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return name === 'i18n' || name === 'mocks' ? [] : listSourceFiles(path);
    return /\.tsx$/.test(name) ? [path] : [];
  });
}

const hasWords = (text: string) => /\p{L}{2,}/u.test(text);
const isAllowed = (text: string) => ALLOWED.has(text.trim());
const decodeEntities = (text: string) => text.replace(/&(ldquo|rdquo|lsquo|rsquo|amp|nbsp|middot|mdash|ndash|hellip|times|bull);/g, ' ');

function findLiterals(file: string): string[] {
  const source = readFileSync(file, 'utf8');
  const sf = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const found: string[] = [];
  const where = (node: ts.Node) => `${file.slice(srcDir.length).replace(/\\/g, '/')}:${sf.getLineAndCharacterOfPosition(node.getStart()).line + 1}`;

  const visit = (node: ts.Node): void => {
    if (ts.isJsxText(node)) {
      const text = decodeEntities(node.text).replace(/\s+/g, ' ').trim();
      if (hasWords(text) && !isAllowed(text)) found.push(`${where(node)} text: ${text}`);
    }
    if (ts.isJsxAttribute(node) && ts.isIdentifier(node.name) && TEXT_ATTRIBUTES.has(node.name.text) && node.initializer && ts.isStringLiteral(node.initializer)) {
      const text = node.initializer.text.trim();
      if (hasWords(text) && !isAllowed(text) && !/^[\w./:#@-]+$/.test(text)) found.push(`${where(node)} ${node.name.text}="${text}"`);
    }
    if (ts.isCallExpression(node)) {
      const callee = node.expression;
      const isToast = ts.isPropertyAccessExpression(callee) && ts.isIdentifier(callee.expression) && callee.expression.text === 'toast'
        && ['success', 'error', 'loading'].includes(callee.name.text);
      const first = node.arguments[0];
      if (isToast && first && (ts.isStringLiteral(first) || ts.isNoSubstitutionTemplateLiteral(first)) && hasWords(first.text)) {
        found.push(`${where(node)} toast: ${first.text}`);
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(sf);
  return found;
}

test('components render translated text instead of hardcoded literals', () => {
  const offenders = listSourceFiles(srcDir).flatMap(findLiterals);
  assert.deepEqual(
    offenders,
    [],
    `Move this text into src/i18n/locales/{en,vi} and read it with t('key'):\n${offenders.join('\n')}`,
  );
});

test('the detector flags literal UI text and ignores translated calls', () => {
  const flagged = ts.createSourceFile('x.tsx', `const a = <button title="Save changes">Save</button>;`, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const clean = ts.createSourceFile('y.tsx', `const a = <button title={t('save')}>{t('save')}</button>;`, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const count = (sf: ts.SourceFile): number => {
    let n = 0;
    const visit = (node: ts.Node): void => {
      if (ts.isJsxText(node) && hasWords(node.text)) n++;
      if (ts.isJsxAttribute(node) && node.initializer && ts.isStringLiteral(node.initializer) && hasWords(node.initializer.text)) n++;
      ts.forEachChild(node, visit);
    };
    visit(sf);
    return n;
  };
  assert.equal(count(flagged), 2);
  assert.equal(count(clean), 0);
});
