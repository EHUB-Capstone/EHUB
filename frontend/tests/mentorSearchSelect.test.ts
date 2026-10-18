import assert from 'node:assert/strict';
import test from 'node:test';
import { filterSearchOptions, moveActiveIndex } from '../src/utils/mentorSearchSelect.ts';

const options = [
  { id: '1', label: 'Nguyễn Minh An', detail: 'an@example.com · 2 teams' },
  { id: '2', label: 'Bùi Kính Khôi', detail: 'khoibk@fpt.edu.vn · 0 teams' },
  { id: '3', label: 'Lê Hoàng Mai' },
];

test('typing narrows the list by name or detail, ignoring accents and case', () => {
  assert.deepEqual(filterSearchOptions(options, '').map(item => item.id), ['1', '2', '3']);
  assert.deepEqual(filterSearchOptions(options, 'nguyen').map(item => item.id), ['1']);
  assert.deepEqual(filterSearchOptions(options, 'KHOI').map(item => item.id), ['2']);
  assert.deepEqual(filterSearchOptions(options, 'fpt.edu').map(item => item.id), ['2']);
  assert.deepEqual(filterSearchOptions(options, 'minh an').map(item => item.id), ['1']);
  assert.deepEqual(filterSearchOptions(options, 'zzz'), []);
});

test('the highlight moves with the arrow keys and wraps around', () => {
  assert.equal(moveActiveIndex(-1, 1, 3), 0);
  assert.equal(moveActiveIndex(-1, -1, 3), 2);
  assert.equal(moveActiveIndex(2, 1, 3), 0);
  assert.equal(moveActiveIndex(0, -1, 3), 2);
  assert.equal(moveActiveIndex(1, 1, 3), 2);
  assert.equal(moveActiveIndex(0, 1, 0), -1);
});
