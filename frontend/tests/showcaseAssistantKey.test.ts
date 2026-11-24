import assert from 'node:assert/strict';
import test from 'node:test';
import type { DraftStorage } from '../src/utils/rubricDraft.ts';
import {
  ASSISTANT_KEY_MAX_AGE_MS,
  ASSISTANT_KEY_PREFIX,
  assistantKeyStorageName,
  forgetAllAssistantKeys,
  forgetAssistantKey,
  recallAssistantKey,
  rememberAssistantKey,
} from '../src/utils/showcaseAssistantKey.ts';
import { ASSISTANT_CHAT_PREFIX } from '../src/utils/showcaseAssistantChat.ts';

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

const NOW = 1_760_000_000_000;
const KEY = 'AIzaSyKeyStoreTest0123456789abcdefgh';
const OTHER_KEY = 'AIzaSyAnotherKey0123456789abcdefghij';

test('a remembered key comes back for the same account, trimmed', () => {
  const storage = new FakeStorage();

  assert.equal(rememberAssistantKey(storage, 'u1', `  ${KEY}\n`, NOW), true);

  assert.equal(recallAssistantKey(storage, 'u1', NOW + 1_000), KEY);
});

test('the key belongs to one account: another account, or no account, never gets it', () => {
  const storage = new FakeStorage();
  rememberAssistantKey(storage, 'u1', KEY, NOW);
  rememberAssistantKey(storage, 'u2', OTHER_KEY, NOW);

  assert.equal(recallAssistantKey(storage, 'u2', NOW), OTHER_KEY);
  assert.equal(recallAssistantKey(storage, 'u3', NOW), '');
  assert.equal(recallAssistantKey(storage, '', NOW), '');
  assert.equal(recallAssistantKey(null, 'u1', NOW), '');
  assert.equal(rememberAssistantKey(storage, '', KEY, NOW), false, 'no account, nothing is stored');
  assert.equal(rememberAssistantKey(null, 'u1', KEY, NOW), false);
  assert.equal(storage.length, 2);
});

test('only something that looks like a key is ever stored', () => {
  const storage = new FakeStorage();

  for (const bad of ['', '   ', 'short', 'GEMINI_API_KEY=' + KEY, 'has a space inside the key 1234567890', 'https://aistudio.google.com/app/apikey']) {
    assert.equal(rememberAssistantKey(storage, 'u1', bad, NOW), false, bad);
  }
  assert.equal(storage.length, 0);
});

test('a remembered key expires after a day and the expired copy is removed', () => {
  const storage = new FakeStorage();
  rememberAssistantKey(storage, 'u1', KEY, NOW);

  assert.equal(recallAssistantKey(storage, 'u1', NOW + ASSISTANT_KEY_MAX_AGE_MS), KEY);
  assert.equal(recallAssistantKey(storage, 'u1', NOW + ASSISTANT_KEY_MAX_AGE_MS + 1), '');
  assert.equal(storage.getItem(assistantKeyStorageName('u1')), null);
});

test('stored data that was damaged or edited by hand is rejected and removed, never trusted', () => {
  const storage = new FakeStorage();
  const name = assistantKeyStorageName('u1');

  storage.setItem(name, 'not json');
  assert.equal(recallAssistantKey(storage, 'u1', NOW), '');

  for (const bad of [
    { version: 2, savedAt: NOW, key: KEY },
    { version: 1, savedAt: 'yesterday', key: KEY },
    { version: 1, savedAt: NOW + 3_600_000, key: KEY },
    { version: 1, savedAt: NOW, key: 12345 },
    { version: 1, savedAt: NOW, key: 'too short' },
    { version: 1, savedAt: NOW, key: 'not a key because it has spaces inside 12345678' },
    ['array'],
    null,
  ]) {
    storage.setItem(name, JSON.stringify(bad));
    assert.equal(recallAssistantKey(storage, 'u1', NOW), '', JSON.stringify(bad));
    assert.equal(storage.getItem(name), null, 'a copy that cannot be trusted is removed');
  }
});

test('forgetting removes only that account\'s key; signing out removes every key and nothing else', () => {
  const storage = new FakeStorage();
  rememberAssistantKey(storage, 'u1', KEY, NOW);
  rememberAssistantKey(storage, 'u2', OTHER_KEY, NOW);
  storage.setItem(`${ASSISTANT_CHAT_PREFIX}u1`, '{}');
  storage.setItem('unrelated', 'keep');

  forgetAssistantKey(storage, 'u1');
  assert.equal(recallAssistantKey(storage, 'u1', NOW), '');
  assert.equal(recallAssistantKey(storage, 'u2', NOW), OTHER_KEY);

  rememberAssistantKey(storage, 'u1', KEY, NOW);
  forgetAllAssistantKeys(storage);
  assert.equal(storage.length, 2);
  assert.notEqual(storage.getItem('unrelated'), null);
  assert.notEqual(storage.getItem(`${ASSISTANT_CHAT_PREFIX}u1`), null, 'the saved conversation is cleared by its own helper');
  assert.ok(![...Array(storage.length).keys()].some(index => storage.key(index)?.startsWith(ASSISTANT_KEY_PREFIX)));
});

test('the key and the conversation use different storage names, so one can never overwrite the other', () => {
  assert.notEqual(ASSISTANT_KEY_PREFIX, ASSISTANT_CHAT_PREFIX);
  assert.ok(!ASSISTANT_KEY_PREFIX.startsWith(ASSISTANT_CHAT_PREFIX));
  assert.ok(!ASSISTANT_CHAT_PREFIX.startsWith(ASSISTANT_KEY_PREFIX));
});

test('blocked or full browser storage never breaks the chat: the key simply is not remembered', () => {
  assert.equal(rememberAssistantKey(throwingStorage, 'u1', KEY, NOW), false);
  assert.equal(recallAssistantKey(throwingStorage, 'u1', NOW), '');
  assert.doesNotThrow(() => forgetAssistantKey(throwingStorage, 'u1'));
  assert.doesNotThrow(() => forgetAllAssistantKeys(throwingStorage));
});
