import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ClassFeatureDisabledError,
  createClassFeatureFlags,
  runClassFeatureRequest,
} from '../src/config/classFeatureFlags.ts';
import { canAccessClassRoute, classRouteAccess } from '../src/config/classAccessPolicy.ts';

test('enables completed class workflow features while keeping unrelated unfinished features disabled', () => {
  const flags = createClassFeatureFlags({});

  assert.equal(flags.lecturerStudentImport, true);
  assert.equal(flags.majorVerification, true);
  assert.equal(flags.teamManagement, true);
  assert.equal(flags.mentorAssignment, true);
  assert.equal(flags.projectDirection, true);
  assert.equal(flags.studentSelfService, true);
  assert.equal(flags.lifecycle, true);
  assert.equal(flags.chatBackfill, true);
  assert.equal(Object.entries(flags)
    .filter(([name]) => !['lecturerStudentImport', 'majorVerification', 'teamManagement', 'mentorAssignment', 'projectDirection', 'studentSelfService', 'lifecycle', 'chatBackfill'].includes(name))
    .every(([, enabled]) => enabled === false), true);
});

test('shows unavailable class controls in local development without enabling their APIs', () => {
  const flags = createClassFeatureFlags({ DEV: true });

  assert.equal(flags.showDevelopmentControls, true);
  assert.equal(flags.rename, false);
  assert.equal(flags.teamManagement, true);
});

test('allows lecturer import to be disabled explicitly', () => {
  const flags = createClassFeatureFlags({ VITE_FEATURE_CLASS_LECTURER_STUDENT_IMPORT: 'false' });

  assert.equal(flags.lecturerStudentImport, false);
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
