import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ClassFeatureDisabledError,
  createClassFeatureFlags,
  runClassFeatureRequest,
} from '../src/config/classFeatureFlags.ts';
import { canAccessClassRoute, classRouteAccess } from '../src/config/classAccessPolicy.ts';

test('keeps unfinished class features disabled by default', () => {
  const flags = createClassFeatureFlags({});

  assert.equal(Object.values(flags).every(enabled => enabled === false), true);
});

test('does not execute a request factory when its feature is disabled', async () => {
  let requestCount = 0;
  const request = async () => {
    requestCount += 1;
    return { success: true };
  };

  await assert.rejects(
    runClassFeatureRequest(false, 'Team management', request),
    ClassFeatureDisabledError,
  );
  assert.equal(requestCount, 0);
});

test('executes the request only when the corresponding feature is enabled', async () => {
  let requestCount = 0;
  const request = async () => {
    requestCount += 1;
    return { success: true };
  };

  const result = await runClassFeatureRequest(true, 'Team management', request);

  assert.deepEqual(result, { success: true });
  assert.equal(requestCount, 1);
});

test('does not allow Mentor into Lecturer class routes', () => {
  assert.equal(canAccessClassRoute('MENTOR', classRouteAccess.lecturerArea), false);
  assert.equal(canAccessClassRoute('MENTOR', classRouteAccess.classDetail), false);
  assert.equal(canAccessClassRoute('LECTURER', classRouteAccess.lecturerArea), true);
});
