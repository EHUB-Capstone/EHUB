import assert from 'node:assert/strict';
import test from 'node:test';
import { canCreateClasses, canManageClass, hasClassRole } from '../src/utils/classPermissions.ts';

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

test('only an administrator can create classes', () => {
  assert.equal(canCreateClasses({ id: 'admin-1', role: 'ADMIN' }), true);
  assert.equal(canCreateClasses({ id: 'multi-role', role: 'LECTURER', roles: ['LECTURER', 'ADMIN'] }), true);
  assert.equal(canCreateClasses({ id: 'lecturer-1', role: 'LECTURER' }), false);
  assert.equal(canCreateClasses({ id: 'student-1', role: 'STUDENT' }), false);
});
