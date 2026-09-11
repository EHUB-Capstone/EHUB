import assert from 'node:assert/strict';
import test from 'node:test';
import { createReleaseFeatureFlags } from '../src/config/releaseFeatureFlags.ts';

test('keeps optional pilot pages visible by default', () => {
  const flags = createReleaseFeatureFlags({});

  assert.equal(Object.values(flags).every((enabled) => enabled === true), true);
});

test('allows one page to be disabled explicitly without hiding the others', () => {
  const flags = createReleaseFeatureFlags({
    VITE_FEATURE_RANKINGS: 'true',
    VITE_FEATURE_AI: 'false',
  });

  assert.equal(flags.rankings, true);
  assert.equal(flags.ai, false);
  assert.equal(flags.chat, true);
});
