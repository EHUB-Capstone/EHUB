import assert from 'node:assert/strict';
import test from 'node:test';
import { PreviewDocumentCache } from '../src/utils/previewDocumentCache.ts';

function createCache(max = 3) {
  const evicted: string[] = [];
  let time = 1_000;
  const cache = new PreviewDocumentCache<{ name: string }>(max, (key) => evicted.push(key), () => time);
  return { cache, evicted, advance: (ms: number) => { time += ms; } };
}

test('a cached document is returned until it expires, then evicted', () => {
  const { cache, evicted, advance } = createCache();
  const doc = { name: 'a' };
  cache.set('a', doc, 5_000);
  assert.equal(cache.get('a'), doc);
  advance(4_999);
  assert.equal(cache.get('a'), doc);
  advance(1);
  assert.equal(cache.get('a'), undefined);
  assert.deepEqual(evicted, ['a']);
  assert.equal(cache.size, 0);
});

test('the least recently used document is evicted when the limit is exceeded', () => {
  const { cache, evicted } = createCache(3);
  for (const key of ['a', 'b', 'c']) cache.set(key, { name: key });
  cache.get('a'); // a becomes the most recently used
  cache.set('d', { name: 'd' });
  assert.deepEqual(evicted, ['b']);
  assert.equal(cache.get('b'), undefined);
  assert.ok(cache.get('a') && cache.get('c') && cache.get('d'));
  assert.equal(cache.size, 3);
});

test('replacing an entry releases the old document but not the one that is kept', () => {
  const { cache, evicted } = createCache();
  const first = { name: 'first' };
  const second = { name: 'second' };
  cache.set('a', first);
  cache.set('a', first); // same object again: nothing to release
  assert.deepEqual(evicted, []);
  cache.set('a', second);
  assert.deepEqual(evicted, ['a']);
  assert.equal(cache.get('a'), second);
});

test('holds tells the owner whether it may still destroy a document', () => {
  const { cache, advance } = createCache();
  const doc = { name: 'a' };
  assert.equal(cache.holds('a', doc), false);
  cache.set('a', doc, 1_000);
  assert.equal(cache.holds('a', doc), true);
  assert.equal(cache.holds('a', { name: 'other' }), false);
  advance(1_001);
  assert.equal(cache.holds('a', doc), false);
});

test('clear releases every document (logout)', () => {
  const { cache, evicted } = createCache();
  cache.set('a', { name: 'a' });
  cache.set('b', { name: 'b' });
  cache.clear();
  assert.deepEqual(evicted.sort(), ['a', 'b']);
  assert.equal(cache.size, 0);
  assert.equal(cache.get('a'), undefined);
});
