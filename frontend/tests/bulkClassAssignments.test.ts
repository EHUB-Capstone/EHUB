import assert from 'node:assert/strict';
import test from 'node:test';
import { parseClassIndices } from '../src/utils/bulkClassAssignments.ts';

test('parses comma-separated class numbers and inclusive ranges', () => {
  assert.deepEqual(parseClassIndices('1-3, 6, 8', 1, 10), {
    classIndices: [1, 2, 3, 6, 8],
    error: null,
  });
});

test('rejects repeated and out-of-batch class numbers', () => {
  assert.match(parseClassIndices('2,2', 1, 5).error || '', /repeated/i);
  assert.match(parseClassIndices('1,6', 1, 5).error || '', /between 1 and 5/i);
  assert.match(parseClassIndices('1-999999999', 1, 5).error || '', /between 1 and 5/i);
});

test('rejects malformed and descending ranges', () => {
  assert.match(parseClassIndices('4-2', 1, 5).error || '', /descending/i);
  assert.match(parseClassIndices('1,a', 1, 5).error || '', /use class numbers/i);
});

test('assigns class 8 using its actual number rather than its batch position', () => {
  assert.deepEqual(parseClassIndices('8', 8, 1), { classIndices: [8], error: null });
  assert.match(parseClassIndices('1', 8, 1).error || '', /between 8 and 8/i);
});

test('parses a batch starting after 1 without shifting the submitted class numbers', () => {
  assert.deepEqual(parseClassIndices('8-10, 12', 8, 5), {
    classIndices: [8, 9, 10, 12],
    error: null,
  });
  assert.match(parseClassIndices('7', 8, 5).error || '', /between 8 and 12/i);
  assert.match(parseClassIndices('8-13', 8, 5).error || '', /between 8 and 12/i);
  assert.match(parseClassIndices('8-10,10', 8, 5).error || '', /repeated/i);
});

test('revalidates existing class numbers when the batch changes', () => {
  assert.equal(parseClassIndices('8-10', 8, 3).error, null);
  assert.match(parseClassIndices('8-10', 9, 3).error || '', /between 9 and 11/i);
  assert.match(parseClassIndices('8-10', 8, 2).error || '', /between 8 and 9/i);
});

test('allows empty assignments and the highest supported class number', () => {
  assert.deepEqual(parseClassIndices('  ', 8, 1), { classIndices: [], error: null });
  assert.deepEqual(parseClassIndices('999', 999, 1), { classIndices: [999], error: null });
});

test('rejects invalid batch bounds before expanding assignment ranges', () => {
  for (const [start, quantity] of [[0, 1], [8.5, 1], [8, 0], [8, 1.5], [8, 101], [999, 2], [NaN, 1]]) {
    assert.match(parseClassIndices('8-999999999', start, quantity).error || '', /valid starting class index/i);
  }
});
