import assert from 'node:assert/strict';
import test from 'node:test';
import {
  decideEvaluationSync,
  editableSnapshot,
  evaluationSyncKey,
  initialRows,
  normalizeCriteria,
  savedOverrides,
  type RubricEvaluationData,
} from '../src/utils/rubricFormState.ts';

const criteria = normalizeCriteria([
  { key: 'idea', label: 'Idea', weight: 60, maxScore: 10 },
  { key: 'market', label: 'Market', weight: 40, maxScore: 10 },
]);

const evaluation = (overrides: Partial<RubricEvaluationData> = {}): RubricEvaluationData => ({
  _id: 'eval-1',
  status: 'DRAFT',
  updatedAt: '2026-10-23T06:00:00Z',
  rubricScores: [{ criterionKey: 'idea', score: 8, comment: 'Clear' }],
  overallFeedback: 'Good start',
  memberScores: [{ studentId: 's1', score: 7, isOverridden: true }, { studentId: 's2', score: 8, isOverridden: false }],
  ...overrides,
});

const snapshotOf = (data: RubricEvaluationData) =>
  editableSnapshot(initialRows(criteria, data), data.overallFeedback, savedOverrides(data));

test('normalizeCriteria accepts workspace and saved criterion shapes with defaults', () => {
  assert.deepEqual(normalizeCriteria([{ criterionKey: 'a', criterionName: 'A' }]), [
    { criterionKey: 'a', criterionName: 'A', description: '', weight: 1, maxScore: 10 },
  ]);
  assert.deepEqual(normalizeCriteria(undefined), []);
});

test('initialRows fills saved scores and leaves unscored criteria empty', () => {
  const rows = initialRows(criteria, evaluation());
  assert.equal(rows[0].score, 8);
  assert.equal(rows[0].comment, 'Clear');
  assert.equal(rows[1].score, '');
  assert.equal(rows[1].comment, '');
});

test('savedOverrides keeps only individually overridden member scores', () => {
  assert.deepEqual(savedOverrides(evaluation()), { s1: '7' });
  assert.deepEqual(savedOverrides(null), {});
});

test('snapshot ignores formatting differences but sees real edits', () => {
  const rows = initialRows(criteria, evaluation());
  const base = editableSnapshot(rows, 'Good start', { s1: '7' });
  assert.equal(editableSnapshot(rows.map(row => row.criterionKey === 'idea' ? { ...row, score: '8.0' } : row), 'Good start', { s1: '7.00' }), base);
  assert.notEqual(editableSnapshot(rows, 'Good start edited', { s1: '7' }), base);
  assert.notEqual(editableSnapshot(rows.map(row => row.criterionKey === 'market' ? { ...row, score: '5' } : row), 'Good start', { s1: '7' }), base);
});

test('sync key is stable for a refetch that returns identical data in new objects', () => {
  const first = evaluationSyncKey(evaluation(), criteria);
  const refetched = evaluationSyncKey(evaluation(), normalizeCriteria([
    { key: 'idea', label: 'Idea', weight: 60, maxScore: 10 },
    { key: 'market', label: 'Market', weight: 40, maxScore: 10 },
  ]));
  assert.equal(refetched, first);
});

test('sync key changes when the evaluation, status, timestamp or rubric changes', () => {
  const base = evaluationSyncKey(evaluation(), criteria);
  assert.notEqual(evaluationSyncKey(evaluation({ updatedAt: '2026-10-23T06:05:00Z' }), criteria), base);
  assert.notEqual(evaluationSyncKey(evaluation({ status: 'SUBMITTED' }), criteria), base);
  assert.notEqual(evaluationSyncKey(evaluation({ _id: 'eval-2' }), criteria), base);
  assert.notEqual(evaluationSyncKey(null, criteria), base);
  assert.notEqual(evaluationSyncKey(evaluation(), normalizeCriteria([{ key: 'idea', label: 'Idea', weight: 100, maxScore: 10 }])), base);
});

test('a background refetch with the same server version never touches unsaved typing', () => {
  const saved = evaluation();
  const key = evaluationSyncKey(saved, criteria);
  const typed = editableSnapshot(
    initialRows(criteria, saved).map(row => row.criterionKey === 'market' ? { ...row, score: '9', comment: 'Typed, not saved' } : row),
    'Typed feedback',
    { s1: '7' },
  );
  assert.equal(decideEvaluationSync({
    baselineKey: key, incomingKey: key, baselineSnapshot: snapshotOf(saved), incomingSnapshot: snapshotOf(saved), currentSnapshot: typed,
  }), 'keep');
});

test('a new server version replaces the form when the lecturer has no unsaved edits', () => {
  const before = evaluation();
  const after = evaluation({ updatedAt: '2026-10-23T06:10:00Z', overallFeedback: 'Changed in another session' });
  assert.equal(decideEvaluationSync({
    baselineKey: evaluationSyncKey(before, criteria),
    incomingKey: evaluationSyncKey(after, criteria),
    baselineSnapshot: snapshotOf(before),
    incomingSnapshot: snapshotOf(after),
    currentSnapshot: snapshotOf(before),
  }), 'apply');
});

test('saving a draft does not trigger a reload prompt: the new version equals what was typed', () => {
  const before = evaluation();
  const typedRows = initialRows(criteria, before).map(row => row.criterionKey === 'market' ? { ...row, score: '9' } : row);
  const typed = editableSnapshot(typedRows, 'Good start', { s1: '7' });
  const afterSave = evaluation({
    updatedAt: '2026-10-23T06:12:00Z',
    rubricScores: [{ criterionKey: 'idea', score: 8, comment: 'Clear' }, { criterionKey: 'market', score: 9, comment: '' }],
  });
  assert.equal(decideEvaluationSync({
    baselineKey: evaluationSyncKey(before, criteria),
    incomingKey: evaluationSyncKey(afterSave, criteria),
    baselineSnapshot: snapshotOf(before),
    incomingSnapshot: snapshotOf(afterSave),
    currentSnapshot: typed,
  }), 'apply');
});

test('a different server version arriving while the lecturer has unsaved edits keeps the edits and notifies', () => {
  const before = evaluation();
  const after = evaluation({ updatedAt: '2026-10-23T06:10:00Z', overallFeedback: 'Changed in another session' });
  const typed = editableSnapshot(
    initialRows(criteria, before).map(row => row.criterionKey === 'market' ? { ...row, score: '9' } : row),
    'Good start',
    { s1: '7' },
  );
  assert.equal(decideEvaluationSync({
    baselineKey: evaluationSyncKey(before, criteria),
    incomingKey: evaluationSyncKey(after, criteria),
    baselineSnapshot: snapshotOf(before),
    incomingSnapshot: snapshotOf(after),
    currentSnapshot: typed,
  }), 'notify');
});
