import assert from 'node:assert/strict';
import test from 'node:test';
import { matchesSearchQuery, normalizeSearchText } from '../src/utils/searchText.ts';

test('normalizeSearchText strips Vietnamese diacritics and đ', () => {
  assert.equal(normalizeSearchText('Bùi Mạnh Thịnh'), 'bui manh thinh');
  assert.equal(normalizeSearchText('Đặng'), 'dang');
});

test('partially typed Telex input still matches the accented name', () => {
  const fields = ['Bùi Mạnh Thịnh', 'DE180185', 'thinh@example.com'];
  for (const query of ['b', 'bu', 'bùi', 'bùi ma', 'bùi mạnh', 'bui manh thinh', 'DE1801']) {
    assert.equal(matchesSearchQuery(query, fields), true, query);
  }
});

test('non-matching or blank queries behave correctly', () => {
  assert.equal(matchesSearchQuery('xyz', ['Bùi Mạnh Thịnh']), false);
  assert.equal(matchesSearchQuery('   ', ['Bùi Mạnh Thịnh']), true);
  assert.equal(matchesSearchQuery('bui', [null, undefined]), false);
});
