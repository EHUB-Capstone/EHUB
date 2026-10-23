import assert from 'node:assert/strict';
import test from 'node:test';
import {
  RUBRIC_DRAFT_PREFIX,
  applyDraftToRows,
  clearAllRubricDrafts,
  clearRubricDraft,
  decideDraftRestore,
  loadRubricDraft,
  rubricDraftKey,
  saveRubricDraft,
  type DraftStorage,
  type RubricDraft,
} from '../src/utils/rubricDraft.ts';
import { initialRows, normalizeCriteria } from '../src/utils/rubricFormState.ts';

class FakeStorage implements DraftStorage {
  private readonly items = new Map<string, string>();

  get length() { return this.items.size; }
  key(index: number) { return [...this.items.keys()][index] ?? null; }
  getItem(key: string) { return this.items.get(key) ?? null; }
  setItem(key: string, value: string) { this.items.set(key, value); }
  removeItem(key: string) { this.items.delete(key); }
}

const throwingStorage: DraftStorage = {
  length: 1,
  key() { throw new Error('blocked'); },
  getItem() { throw new Error('blocked'); },
  setItem() { throw new Error('quota'); },
  removeItem() { throw new Error('blocked'); },
};

const draft = (overrides: Partial<RubricDraft> = {}): RubricDraft => ({
  version: 1,
  baselineKey: 'baseline-1',
  savedAt: 1_760_000_000_000,
  rows: [{ criterionKey: 'idea', score: '8.5', comment: 'Typed, not saved' }],
  feedback: 'Typed feedback',
  overrides: { s1: '7' },
  ...overrides,
});

test('draft keys are scoped by user, team, checkpoint and evaluation', () => {
  const base = rubricDraftKey('u1', 't1', 2, 'e1');
  assert.ok(base.startsWith(RUBRIC_DRAFT_PREFIX));
  assert.notEqual(rubricDraftKey('u2', 't1', 2, 'e1'), base);
  assert.notEqual(rubricDraftKey('u1', 't2', 2, 'e1'), base);
  assert.notEqual(rubricDraftKey('u1', 't1', 3, 'e1'), base);
  assert.notEqual(rubricDraftKey('u1', 't1', 2, 'e2'), base);
  assert.equal(rubricDraftKey('u1', 't1', 2, undefined), rubricDraftKey('u1', 't1', 2, null));
  assert.ok(rubricDraftKey('u1', 't1', 2).endsWith(':new'));
});

test('a saved draft round-trips and another user cannot read it', () => {
  const storage = new FakeStorage();
  const key = rubricDraftKey('u1', 't1', 1, 'e1');
  saveRubricDraft(storage, key, draft());
  assert.deepEqual(loadRubricDraft(storage, key), draft());
  assert.equal(loadRubricDraft(storage, rubricDraftKey('u2', 't1', 1, 'e1')), null);
});

test('corrupt, outdated or incomplete drafts are ignored instead of throwing', () => {
  const storage = new FakeStorage();
  const key = rubricDraftKey('u1', 't1', 1, 'e1');
  storage.setItem(key, '{not json');
  assert.equal(loadRubricDraft(storage, key), null);
  storage.setItem(key, JSON.stringify({ ...draft(), version: 99 }));
  assert.equal(loadRubricDraft(storage, key), null);
  storage.setItem(key, JSON.stringify({ ...draft(), rows: 'nope' }));
  assert.equal(loadRubricDraft(storage, key), null);
  storage.setItem(key, JSON.stringify({ ...draft(), rows: [{ criterionKey: 'idea', score: {}, comment: '' }] }));
  assert.equal(loadRubricDraft(storage, key), null);
  storage.setItem(key, JSON.stringify({ ...draft(), overrides: { s1: 7 } }));
  assert.equal(loadRubricDraft(storage, key), null);
  storage.setItem(key, JSON.stringify([draft()]));
  assert.equal(loadRubricDraft(storage, key), null);
});

test('missing or blocked storage never breaks grading', () => {
  const key = rubricDraftKey('u1', 't1', 1, 'e1');
  assert.doesNotThrow(() => saveRubricDraft(null, key, draft()));
  assert.equal(loadRubricDraft(null, key), null);
  assert.doesNotThrow(() => clearRubricDraft(null, key));
  assert.doesNotThrow(() => clearAllRubricDrafts(null));
  assert.doesNotThrow(() => saveRubricDraft(throwingStorage, key, draft()));
  assert.equal(loadRubricDraft(throwingStorage, key), null);
  assert.doesNotThrow(() => clearRubricDraft(throwingStorage, key));
  assert.doesNotThrow(() => clearAllRubricDrafts(throwingStorage));
});

test('clearing one draft keeps the others', () => {
  const storage = new FakeStorage();
  const first = rubricDraftKey('u1', 't1', 1, 'e1');
  const second = rubricDraftKey('u1', 't1', 2, 'e2');
  saveRubricDraft(storage, first, draft());
  saveRubricDraft(storage, second, draft());
  clearRubricDraft(storage, first);
  assert.equal(loadRubricDraft(storage, first), null);
  assert.deepEqual(loadRubricDraft(storage, second), draft());
});

test('logout cleanup removes every rubric draft and nothing else', () => {
  const storage = new FakeStorage();
  saveRubricDraft(storage, rubricDraftKey('u1', 't1', 1, 'e1'), draft());
  saveRubricDraft(storage, rubricDraftKey('u1', 't2', 3, null), draft());
  storage.setItem('ehub:pending-otp', 'keep-me');
  storage.setItem('unrelated', 'keep-me-too');
  clearAllRubricDrafts(storage);
  assert.equal(storage.length, 2);
  assert.equal(storage.getItem('ehub:pending-otp'), 'keep-me');
  assert.equal(storage.getItem('unrelated'), 'keep-me-too');
});

test('draft values are matched to criteria by key, so a changed rubric cannot misalign rows', () => {
  const rows = initialRows(normalizeCriteria([
    { key: 'market', label: 'Market', weight: 40, maxScore: 10 },
    { key: 'idea', label: 'Idea', weight: 60, maxScore: 10 },
  ]), null);
  const applied = applyDraftToRows(rows, draft({
    rows: [
      { criterionKey: 'idea', score: '8.5', comment: 'Typed, not saved' },
      { criterionKey: 'removed-criterion', score: '3', comment: 'gone' },
    ],
  }));
  assert.equal(applied[0].criterionKey, 'market');
  assert.equal(applied[0].score, '');
  assert.equal(applied[1].criterionKey, 'idea');
  assert.equal(applied[1].score, '8.5');
  assert.equal(applied[1].comment, 'Typed, not saved');
});

test('restore decision: nothing to restore, safe restore, or let the lecturer choose', () => {
  assert.equal(decideDraftRestore({
    draftBaselineKey: 'a', currentSyncKey: 'a', draftSnapshot: 'same', serverSnapshot: 'same',
  }), 'discard');
  assert.equal(decideDraftRestore({
    draftBaselineKey: 'a', currentSyncKey: 'b', draftSnapshot: 'same', serverSnapshot: 'same',
  }), 'discard');
  assert.equal(decideDraftRestore({
    draftBaselineKey: 'a', currentSyncKey: 'a', draftSnapshot: 'typed', serverSnapshot: 'saved',
  }), 'restore');
  assert.equal(decideDraftRestore({
    draftBaselineKey: 'a', currentSyncKey: 'b', draftSnapshot: 'typed', serverSnapshot: 'saved',
  }), 'offer');
});
