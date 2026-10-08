import assert from 'node:assert/strict';
import test from 'node:test';
import {
  getSelectionState,
  keepEligibleSelection,
  setVisibleSelection,
  toggleSelection,
} from '../src/utils/mentorBatchAssignment.ts';

test('toggleSelection adds and removes an id without mutating the input', () => {
  const selected = ['a'];
  assert.deepEqual(toggleSelection(selected, 'b'), ['a', 'b']);
  assert.deepEqual(toggleSelection(['a', 'b'], 'a'), ['b']);
  assert.deepEqual(selected, ['a']);
});

test('setVisibleSelection only touches the teams currently shown by the search filter', () => {
  assert.deepEqual(setVisibleSelection(['x'], ['a', 'b'], true), ['x', 'a', 'b']);
  assert.deepEqual(setVisibleSelection(['x', 'a', 'b'], ['a', 'b'], false), ['x']);
});

test('getSelectionState describes none, some and all of the visible teams', () => {
  assert.equal(getSelectionState([], ['a', 'b']), 'none');
  assert.equal(getSelectionState(['a'], ['a', 'b']), 'some');
  assert.equal(getSelectionState(['a', 'b', 'z'], ['a', 'b']), 'all');
  assert.equal(getSelectionState(['a'], []), 'none');
});

test('keepEligibleSelection drops teams that can no longer take the mentor', () => {
  assert.deepEqual(keepEligibleSelection(['a', 'b', 'c'], ['b', 'c', 'd']), ['b', 'c']);
});
