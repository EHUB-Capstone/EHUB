import assert from 'node:assert/strict';
import test from 'node:test';
import type { SubjectCheckpointDraft } from '../src/types/subjects.ts';
import {
  hasSupportedRubricWorkbookSignature,
  mergeRubricImport,
  parseRubricSheetRows,
} from '../src/utils/rubricImport.ts';

const templateRows = [
  ['EXE101 Rubric', '', '', '', '', '', ''],
  ['Checkpoint', 'Standards', 'Levels', '', '', '', 'Weight'],
  ['', '', 'Excellent (8.5–10)', 'Good (7.0–8.4)', 'Fair (5.0–6.9)', 'Poor (<5.0)', ''],
  [1, 'Startup Idea Clarity', 'Excellent idea', 'Clear idea', 'Vague idea', 'Unclear idea', 0.6],
  ['', 'Problem Identification', 'Strong evidence', 'Some evidence', 'Weak evidence', 'No evidence', 0.4],
  ['', '', '', '', '', '', 1],
];

test('parses the attached multi-level rubric layout and normalizes fractional weights', () => {
  const result = parseRubricSheetRows(templateRows);

  assert.deepEqual(result.errors, []);
  assert.equal(result.checkpoints.length, 1);
  assert.equal(result.checkpoints[0].number, 1);
  assert.deepEqual(result.checkpoints[0].criteria.map(item => item.weight), [60, 40]);
  assert.deepEqual(result.checkpoints[0].criteria[0].levels[0], {
    key: 'excellent',
    label: 'Excellent',
    range: '8.5–10',
    description: 'Excellent idea',
  });
});

test('accepts the Stardards typo used by the EXE201 worksheet', () => {
  const rows = templateRows.map(row => [...row]);
  rows[1][1] = 'Stardards';

  assert.deepEqual(parseRubricSheetRows(rows).errors, []);
});

test('reports invalid checkpoint totals before the draft is changed', () => {
  const rows = templateRows.map(row => [...row]);
  rows[4][6] = 0.3;

  const result = parseRubricSheetRows(rows);

  assert.match(result.errors[0], /total 90\.0%, not 100\.0%/);
});

test('merges imported criteria while preserving current checkpoint metadata and stable keys', () => {
  const existing: SubjectCheckpointDraft[] = [{
    number: 1,
    title: 'Problem validation',
    shortDescription: 'Keep this description',
    courseWeight: 85,
    requirements: ['Keep this requirement'],
    rubrics: [{
      key: 'stable-problem-key',
      label: 'Problem Identification',
      description: 'Keep this criterion description',
      weight: 100,
      levels: [],
    }],
  }];
  const parsed = parseRubricSheetRows(templateRows);

  const result = mergeRubricImport(existing, parsed.checkpoints);

  assert.equal(result.checkpoints[0].title, 'Problem validation');
  assert.equal(result.checkpoints[0].courseWeight, 85);
  assert.deepEqual(result.checkpoints[0].requirements, ['Keep this requirement']);
  assert.equal(result.checkpoints[0].rubrics[1].key, 'stable-problem-key');
  assert.equal(result.checkpoints[0].rubrics[1].description, 'Keep this criterion description');
});

test('adds missing checkpoints without guessing their course weight', () => {
  const parsed = parseRubricSheetRows(templateRows);
  parsed.checkpoints[0].number = 2;

  const result = mergeRubricImport([], parsed.checkpoints);

  assert.deepEqual(result.createdCheckpointNumbers, [2]);
  assert.equal(result.checkpoints[0].courseWeight, 0);
  assert.equal(result.checkpoints[0].title, 'Checkpoint 2');
});

test('checks workbook signatures against the selected Excel extension', () => {
  assert.equal(hasSupportedRubricWorkbookSignature(new Uint8Array([0x50, 0x4B, 0x03, 0x04]), '.xlsx'), true);
  assert.equal(hasSupportedRubricWorkbookSignature(new Uint8Array([0x50, 0x4B, 0x03, 0x04]), '.xls'), false);
  assert.equal(hasSupportedRubricWorkbookSignature(
    new Uint8Array([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]),
    '.xls',
  ), true);
});
