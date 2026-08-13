import assert from 'node:assert/strict';
import test from 'node:test';
import { canManageClass, hasClassRole } from '../src/utils/classPermissions.ts';

test('admin can manage every class even when ADMIN is not the first role', () => {
  const user = { id: 'admin-1', role: 'LECTURER', roles: ['LECTURER', 'ADMIN'] };
  assert.equal(hasClassRole(user, 'ADMIN'), true);
  assert.equal(canManageClass(user, { primaryLecturerId: 'lecturer-2' }), true);
});

test('assigned lecturer can manage only the assigned class', () => {
  const user = { _id: 'LECTURER-1', role: 'LECTURER', roles: ['LECTURER'] };
  assert.equal(canManageClass(user, { primaryLecturerId: 'lecturer-1' }), true);
  assert.equal(canManageClass(user, { lectureId: { _id: 'lecturer-2' } }), false);
});

test('student and mentor cannot manage a class', () => {
  const targetClass = { primaryLecturerId: 'lecturer-1' };
  assert.equal(canManageClass({ id: 'student-1', role: 'STUDENT' }, targetClass), false);
  assert.equal(canManageClass({ id: 'mentor-1', role: 'MENTOR' }, targetClass), false);
});
