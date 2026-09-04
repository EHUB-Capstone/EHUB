import assert from 'node:assert/strict';
import test from 'node:test';
import { parseClassPositions } from '../src/utils/bulkClassAssignments.ts';

test('parses comma-separated positions and inclusive ranges', () => {
  assert.deepEqual(parseClassPositions('1-3, 6, 8', 10), {
    positions: [1, 2, 3, 6, 8],
    error: null,
  });
});

test('rejects repeated and out-of-batch positions', () => {
  assert.match(parseClassPositions('2,2', 5).error || '', /repeated/i);
  assert.match(parseClassPositions('1,6', 5).error || '', /between 1 and 5/i);
  assert.match(parseClassPositions('1-999999999', 5).error || '', /between 1 and 5/i);
});

test('rejects malformed and descending ranges', () => {
  assert.match(parseClassPositions('4-2', 5).error || '', /descending/i);
  assert.match(parseClassPositions('1,a', 5).error || '', /use positions/i);
});
