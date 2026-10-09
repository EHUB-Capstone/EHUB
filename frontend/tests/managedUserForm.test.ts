import assert from 'node:assert/strict';
import test from 'node:test';
import { mentorTypeError, toManagedUserPayload } from '../src/utils/managedUserForm.ts';

test('mentorTypeError requires a valid mentor type only for mentors', () => {
  assert.equal(mentorTypeError('STUDENT', ''), null);
  assert.equal(mentorTypeError('LECTURER', ''), null);
  assert.match(mentorTypeError('MENTOR', '')!, /Enterprise mentor or a Lecturer mentor/);
  assert.match(mentorTypeError('MENTOR', 'Unknown')!, /Choose/);
  assert.equal(mentorTypeError('MENTOR', 'Enterprise'), null);
  assert.equal(mentorTypeError('MENTOR', 'Academic'), null);
});

test('toManagedUserPayload sends the mentor-only fields only for mentors', () => {
  const base = { name: 'A', email: 'a@b.vn', role: 'MENTOR', mentorType: 'Academic', expertise: ['AI'], bio: 'Bio', availabilityNote: 'Fridays' };
  assert.deepEqual(toManagedUserPayload(base), base);
  assert.deepEqual(toManagedUserPayload({ ...base, role: 'STUDENT' }), { name: 'A', email: 'a@b.vn', role: 'STUDENT' });
});
